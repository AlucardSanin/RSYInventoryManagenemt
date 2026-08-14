using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class RecycleInvoiceService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordService _passwords;
    private readonly AuditService _audit;

    public RecycleInvoiceService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        PasswordService passwords,
        AuditService audit)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _passwords = passwords;
        _audit = audit;
    }

    public static DateOnly GetMondayOfWeek(DateOnly anyDayInWeek)
    {
        var diff = ((int)anyDayInWeek.DayOfWeek + 6) % 7; // Monday=0
        return anyDayInWeek.AddDays(-diff);
    }

    public static (DateOnly Monday, DateOnly Friday) GetWorkWeek(DateOnly anyDayInWeek)
    {
        var monday = GetMondayOfWeek(anyDayInWeek);
        return (monday, monday.AddDays(4));
    }

    public static (DateOnly Monday, DateOnly Friday) GetLastCompletedWorkWeek()
    {
        var today = YardTimeZone.TodayEastern();
        var thisMonday = GetMondayOfWeek(today);
        var lastMonday = thisMonday.AddDays(-7);
        return (lastMonday, lastMonday.AddDays(4));
    }

    public async Task<List<RecycleBillToCompany>> GetBillToCompaniesAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var q = db.RecycleBillToCompanies.AsNoTracking().AsQueryable();
        if (activeOnly)
            q = q.Where(c => c.IsActive);
        return await q.OrderBy(c => c.Alias).ToListAsync(ct);
    }

    public async Task<RecycleBillToCompany> UpsertBillToAsync(
        int? id,
        string alias,
        string companyName,
        string addressLine,
        string? contactLine,
        bool isActive,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        alias = (alias ?? "").Trim();
        companyName = (companyName ?? "").Trim();
        addressLine = (addressLine ?? "").Trim();
        contactLine = string.IsNullOrWhiteSpace(contactLine) ? null : contactLine.Trim();

        if (alias.Length == 0 || companyName.Length == 0 || addressLine.Length == 0)
            throw new InvalidOperationException("Alias, nombre y dirección son obligatorios.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        RecycleBillToCompany entity;
        if (id is > 0)
        {
            entity = await db.RecycleBillToCompanies.FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw new InvalidOperationException("Compañía no encontrada.");
            entity.Alias = alias;
            entity.CompanyName = companyName;
            entity.AddressLine = addressLine;
            entity.ContactLine = contactLine;
            entity.IsActive = isActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            entity = new RecycleBillToCompany
            {
                Alias = alias,
                CompanyName = companyName,
                AddressLine = addressLine,
                ContactLine = contactLine,
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.RecycleBillToCompanies.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<RecycleWeeklyInvoice>> ListInvoicesAsync(CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RecycleWeeklyInvoices
            .AsNoTracking()
            .Include(i => i.BillToCompany)
            .Include(i => i.GeneratedByUser)
            .OrderByDescending(i => i.WeekStartDate)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<RecycleWeeklyInvoice?> GetInvoiceForWeekAsync(
        DateOnly weekStartMonday,
        int billToCompanyId,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RecycleWeeklyInvoices
            .AsNoTracking()
            .Include(i => i.BillToCompany)
            .FirstOrDefaultAsync(
                i => i.WeekStartDate == weekStartMonday && i.BillToCompanyId == billToCompanyId,
                ct);
    }

    public async Task<List<RecycleLoad>> GetLoadsForWeekAsync(
        DateOnly monday,
        DateOnly friday,
        int billToCompanyId,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RecycleLoads
            .AsNoTracking()
            .Where(l => l.BillToCompanyId == billToCompanyId
                        && l.LoadDate >= monday
                        && l.LoadDate <= friday)
            .OrderBy(l => l.LoadDate)
            .ThenBy(l => l.LoadExternalId)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Allocates/reuses invoice number, builds PDF via callback, then persists the weekly invoice.
    /// Only includes loads registered for the selected BILL TO company that week.
    /// Replacing an existing week+company invoice requires the current user's password.
    /// </summary>
    public async Task<RecycleWeeklyInvoice> CreateOrReplaceWeekAsync(
        DateOnly monday,
        DateOnly friday,
        int billToCompanyId,
        DateOnly invoiceDate,
        string? confirmPasswordForReplace,
        Func<int, RecycleBillToCompany, List<RecycleLoad>, Task<(string PdfPath, decimal Total, int Count)>> writePdf,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        if (friday != monday.AddDays(4))
            throw new InvalidOperationException("La semana de trabajo debe ser lunes a viernes.");
        ArgumentNullException.ThrowIfNull(writePdf);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var billTo = await db.RecycleBillToCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == billToCompanyId && c.IsActive, ct)
            ?? throw new InvalidOperationException("Selecciona una compañía BILL TO activa.");

        var loads = await db.RecycleLoads
            .AsNoTracking()
            .Where(l => l.BillToCompanyId == billToCompanyId
                        && l.LoadDate >= monday
                        && l.LoadDate <= friday)
            .OrderBy(l => l.LoadDate)
            .ThenBy(l => l.LoadExternalId)
            .ToListAsync(ct);

        if (loads.Count == 0)
            throw new InvalidOperationException(
                $"No hay cargas de {billTo.Alias} en esa semana (lun–vie).");

        var existing = await db.RecycleWeeklyInvoices
            .FirstOrDefaultAsync(
                i => i.WeekStartDate == monday && i.BillToCompanyId == billToCompanyId,
                ct);

        int invoiceNumber;
        if (existing is not null)
        {
            await EnsurePasswordForReplaceAsync(db, confirmPasswordForReplace, ct);
            invoiceNumber = existing.InvoiceNumber;
        }
        else
        {
            invoiceNumber = await AllocateInvoiceNumberAsync(db, ct);
        }

        var (pdfPath, total, count) = await writePdf(invoiceNumber, billTo, loads);

        if (existing is not null)
        {
            existing.BillToCompanyId = billTo.Id;
            existing.PdfRelativePath = pdfPath;
            existing.TotalAmountUsd = total;
            existing.LoadCount = count;
            existing.GeneratedAtUtc = DateTime.UtcNow;
            existing.GeneratedByUserId = _currentUser.UserId;
            existing.InvoiceDate = invoiceDate;
            existing.WeekEndDate = friday;
            await db.SaveChangesAsync(ct);

            await _audit.WriteAsync(
                "RecycleWeeklyInvoiceReplaced",
                "RecycleWeeklyInvoice",
                existing.Id,
                $"Invoice #{existing.InvoiceNumber} reemplazado · {billTo.Alias} · semana {monday:yyyy-MM-dd}",
                null,
                ct);

            return existing;
        }

        var entity = new RecycleWeeklyInvoice
        {
            InvoiceNumber = invoiceNumber,
            WeekStartDate = monday,
            WeekEndDate = friday,
            BillToCompanyId = billTo.Id,
            PdfRelativePath = pdfPath,
            TotalAmountUsd = total,
            LoadCount = count,
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByUserId = _currentUser.UserId,
            InvoiceDate = invoiceDate
        };
        db.RecycleWeeklyInvoices.Add(entity);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "RecycleWeeklyInvoiceCreated",
            "RecycleWeeklyInvoice",
            entity.Id,
            $"Invoice #{entity.InvoiceNumber} · {billTo.Alias} · semana {monday:yyyy-MM-dd}",
            null,
            ct);

        return entity;
    }

    /// <summary>
    /// Persists a generated weekly invoice. Replacing an existing week+company requires the current user's password.
    /// Prefer <see cref="CreateOrReplaceWeekAsync"/> when the PDF needs the final invoice number.
    /// </summary>
    public async Task<RecycleWeeklyInvoice> SaveWeeklyInvoiceAsync(
        DateOnly monday,
        DateOnly friday,
        int billToCompanyId,
        DateOnly invoiceDate,
        string pdfRelativePath,
        decimal totalAmount,
        int loadCount,
        string? confirmPasswordForReplace,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        if (friday != monday.AddDays(4))
            throw new InvalidOperationException("La semana de trabajo debe ser lunes a viernes.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var billTo = await db.RecycleBillToCompanies.FirstOrDefaultAsync(c => c.Id == billToCompanyId && c.IsActive, ct)
            ?? throw new InvalidOperationException("Selecciona una compañía BILL TO activa.");

        var existing = await db.RecycleWeeklyInvoices
            .FirstOrDefaultAsync(
                i => i.WeekStartDate == monday && i.BillToCompanyId == billToCompanyId,
                ct);

        if (existing is not null)
        {
            await EnsurePasswordForReplaceAsync(db, confirmPasswordForReplace, ct);

            existing.BillToCompanyId = billTo.Id;
            existing.PdfRelativePath = pdfRelativePath;
            existing.TotalAmountUsd = totalAmount;
            existing.LoadCount = loadCount;
            existing.GeneratedAtUtc = DateTime.UtcNow;
            existing.GeneratedByUserId = _currentUser.UserId;
            existing.InvoiceDate = invoiceDate;
            existing.WeekEndDate = friday;
            await db.SaveChangesAsync(ct);

            await _audit.WriteAsync(
                "RecycleWeeklyInvoiceReplaced",
                "RecycleWeeklyInvoice",
                existing.Id,
                $"Invoice #{existing.InvoiceNumber} reemplazado · {billTo.Alias} · semana {monday:yyyy-MM-dd}",
                null,
                ct);

            return existing;
        }

        var invoiceNumber = await AllocateInvoiceNumberAsync(db, ct);
        var entity = new RecycleWeeklyInvoice
        {
            InvoiceNumber = invoiceNumber,
            WeekStartDate = monday,
            WeekEndDate = friday,
            BillToCompanyId = billTo.Id,
            PdfRelativePath = pdfRelativePath,
            TotalAmountUsd = totalAmount,
            LoadCount = loadCount,
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedByUserId = _currentUser.UserId,
            InvoiceDate = invoiceDate
        };
        db.RecycleWeeklyInvoices.Add(entity);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "RecycleWeeklyInvoiceCreated",
            "RecycleWeeklyInvoice",
            entity.Id,
            $"Invoice #{entity.InvoiceNumber} · {billTo.Alias} · semana {monday:yyyy-MM-dd}",
            null,
            ct);

        return entity;
    }

    /// <summary>
    /// Deletes a saved weekly invoice and rewinds the sequence when appropriate
    /// so the next generate can reuse the freed number (floor 20).
    /// Returns the PDF relative path for best-effort file cleanup.
    /// </summary>
    public async Task<string> DeleteInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.RecycleWeeklyInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct)
            ?? throw new InvalidOperationException("Invoice no encontrado.");

        var number = entity.InvoiceNumber;
        var path = entity.PdfRelativePath;
        var week = entity.WeekStartDate;

        db.RecycleWeeklyInvoices.Remove(entity);
        await db.SaveChangesAsync(ct);

        var maxRemaining = await db.RecycleWeeklyInvoices
            .Select(i => (int?)i.InvoiceNumber)
            .MaxAsync(ct);
        var desiredNext = Math.Max(20, (maxRemaining ?? 19) + 1);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.RecycleInvoiceSequence SET NextNumber = {desiredNext} WHERE Id = 1",
            ct);

        await _audit.WriteAsync(
            "RecycleWeeklyInvoiceDeleted",
            "RecycleWeeklyInvoice",
            invoiceId,
            $"Invoice #{number} eliminado · semana {week:yyyy-MM-dd}",
            $"NextNumber → {desiredNext}",
            ct);

        return path;
    }

    private async Task EnsurePasswordForReplaceAsync(
        YardInventoryDbContext db,
        string? password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Ya existe un invoice para esa semana y compañía. Ingresa tu contraseña para reemplazarlo.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct)
            ?? throw new UnauthorizedAccessException("Usuario no encontrado.");

        if (!_passwords.Verify(user, password))
            throw new UnauthorizedAccessException("Contraseña incorrecta.");
    }

    private static async Task<int> AllocateInvoiceNumberAsync(YardInventoryDbContext db, CancellationToken ct)
    {
        var assigned = await db.Database.SqlQueryRaw<int>(
                """
                UPDATE dbo.RecycleInvoiceSequence WITH (UPDLOCK, ROWLOCK)
                SET NextNumber = NextNumber + 1
                OUTPUT deleted.NextNumber AS [Value]
                WHERE Id = 1;
                """)
            .ToListAsync(ct);

        if (assigned.Count == 0)
            throw new InvalidOperationException(
                "No se pudo asignar número de invoice. ¿Ejecutaste 019_RecycleLoadInvoice.sql?");

        return assigned[0];
    }

    private void EnsureCanManage()
    {
        if (!_currentUser.CanManageScrapLogistics)
            throw new UnauthorizedAccessException("No tiene permiso para invoices de reciclaje.");
    }
}
