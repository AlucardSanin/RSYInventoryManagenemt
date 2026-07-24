using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Services;

public sealed class InvoiceTemplateService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;

    public InvoiceTemplateService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InvoiceTemplate>> ListActiveAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InvoiceTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InvoiceTemplate>> ListAllAsync(CancellationToken ct = default)
    {
        EnsureAdmin();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InvoiceTemplates.AsNoTracking()
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<InvoiceTemplate?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InvoiceTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
    }

    public async Task<InvoiceTemplate?> GetByIdAnyAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InvoiceTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<InvoiceTemplate> GetDefaultAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var template = await db.InvoiceTemplates.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return template
            ?? throw new InvalidOperationException(
                "No hay plantillas de recibo. Ejecuta resources/Database/012_InvoiceTemplates.sql.");
    }

    public async Task<InvoiceTemplate> CreateAsync(
        string name,
        string buyerCompanyName,
        string buyerAuthorizedName,
        string addressLine1,
        string cityStateZip,
        string email,
        string? phone,
        bool makeDefault,
        CancellationToken ct = default)
    {
        EnsureAdmin();

        name = Require(name, "Nombre de plantilla");
        buyerCompanyName = Require(buyerCompanyName, "Nombre de la empresa compradora");
        buyerAuthorizedName = Require(buyerAuthorizedName, "Nombre autorizado (firma)");
        addressLine1 = Require(addressLine1, "Dirección");
        cityStateZip = Require(cityStateZip, "Ciudad / estado / ZIP");
        email = Require(email, "Correo");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.InvoiceTemplates.AnyAsync(t => t.Name == name, ct))
            throw new InvalidOperationException("Ya existe una plantilla con ese nombre.");

        if (makeDefault)
        {
            foreach (var existing in await db.InvoiceTemplates.Where(t => t.IsDefault).ToListAsync(ct))
                existing.IsDefault = false;
        }

        var row = new InvoiceTemplate
        {
            Name = name,
            BuyerCompanyName = buyerCompanyName,
            BuyerAuthorizedName = buyerAuthorizedName.ToUpperInvariant(),
            AddressLine1 = addressLine1,
            CityStateZip = cityStateZip,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            TemplateKind = 0,
            IsDefault = makeDefault || !await db.InvoiceTemplates.AnyAsync(ct),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.InvoiceTemplates.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task SetDefaultAsync(int id, CancellationToken ct = default)
    {
        EnsureAdmin();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var target = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct)
            ?? throw new InvalidOperationException("Plantilla no encontrada.");

        foreach (var t in await db.InvoiceTemplates.Where(x => x.IsDefault).ToListAsync(ct))
            t.IsDefault = false;

        target.IsDefault = true;
        target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int id, bool isActive, CancellationToken ct = default)
    {
        EnsureAdmin();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var target = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Plantilla no encontrada.");

        if (!isActive && target.IsDefault)
            throw new InvalidOperationException("No puede desactivar la plantilla predeterminada.");

        target.IsActive = isActive;
        target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Stores an uploaded PDF path for future custom layouts / DocuSeal mapping.</summary>
    public async Task AttachPdfAsync(int id, string relativePath, CancellationToken ct = default)
    {
        EnsureAdmin();
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Ruta de PDF inválida.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var target = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Plantilla no encontrada.");

        target.PdfRelativePath = relativePath.Trim();
        target.TemplateKind = 1;
        target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetDocuSealTemplateIdAsync(int id, int? docuSealTemplateId, CancellationToken ct = default)
    {
        EnsureAdmin();
        if (docuSealTemplateId is <= 0)
            docuSealTemplateId = null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var target = await db.InvoiceTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Plantilla no encontrada.");

        target.DocuSealTemplateId = docuSealTemplateId;
        target.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Picks the branding template that matches the vehicle source (e.g. Saul Motors),
    /// otherwise the default / first active template.
    /// </summary>
    public async Task<InvoiceTemplate?> SuggestForVehicleSourceAsync(
        string? vehicleSourceName,
        CancellationToken ct = default)
    {
        var templates = await ListActiveAsync(ct);
        if (templates.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(vehicleSourceName))
        {
            var source = vehicleSourceName.Trim();
            var matched = templates.FirstOrDefault(t =>
                (!string.IsNullOrWhiteSpace(t.MatchedSourceName)
                 && t.MatchedSourceName.Equals(source, StringComparison.OrdinalIgnoreCase))
                || t.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            if (matched is not null)
                return matched;
        }

        return templates.FirstOrDefault(t => t.IsDefault) ?? templates[0];
    }

    private void EnsureAdmin()
    {
        if (!_currentUser.CanManageUsers)
            throw new UnauthorizedAccessException("Solo el administrador puede gestionar plantillas de recibo.");
    }

    private static string Require(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{label} es obligatorio.");
        return value.Trim();
    }
}
