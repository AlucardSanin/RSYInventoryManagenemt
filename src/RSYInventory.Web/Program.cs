using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RSYInventory.Data;
using RSYInventory.Data.Data;
using RSYInventory.Data.Services;
using RSYInventory.Web.Components;
using RSYInventory.Web.Services;
using RSYInventory.Web.Services.Invoice;

var builder = WebApplication.CreateBuilder(args);
var loggerFactory = LoggerFactory.Create(b =>
{
    b.AddConfiguration(builder.Configuration.GetSection("Logging"));
    b.AddConsole();
});
var startupLog = loggerFactory.CreateLogger("Startup");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment()
        || builder.Configuration.GetValue<bool>("DetailedErrors");
});

// Persist keys so antiforgery / protected session survive restarts.
// Prefer App_Data (writable on IIS) over ContentRoot when possible.
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "dataprotection-keys");
try
{
    Directory.CreateDirectory(dataProtectionKeysPath);
}
catch (Exception ex)
{
    startupLog.LogWarning(ex,
        "No se pudo crear {Path}. Se usará ContentRoot/dataprotection-keys. " +
        "En IIS, da permiso de escritura a App_Data para la identidad del Application Pool.",
        dataProtectionKeysPath);
    dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");
    Directory.CreateDirectory(dataProtectionKeysPath);
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("RSYInventory");

// ConnectionStrings:YardInventory
// - Development → .\MSSQLSERVER01 / RSYYardInventory (Encrypt=False evita fallos de certificado SQL local)
// - Production  → appsettings.Production.json o variables de entorno / User Secrets
var connectionString = builder.Configuration.GetConnectionString("YardInventory")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:YardInventory. Configúrala en appsettings / User Secrets / variables de entorno.");

LogConnectionTarget(startupLog, connectionString, builder.Environment.EnvironmentName);

builder.Services.AddYardInventoryData(connectionString);
builder.Services.AddScoped<IUserSessionStore, ProtectedUserSessionStore>();
builder.Services.AddScoped<UiBusyService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<MediaStorageService>();

builder.Services.Configure<CompanyInvoiceOptions>(
    builder.Configuration.GetSection(CompanyInvoiceOptions.SectionName));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<DocuSealOptions>(
    builder.Configuration.GetSection(DocuSealOptions.SectionName));

builder.Services.AddSingleton<PurchaseInvoicePdfService>();
builder.Services.AddSingleton<DriverReportPdfService>();
builder.Services.AddSingleton<RecycleWeeklyInvoicePdfService>();
builder.Services.AddSingleton<DocuSealMasterTemplateService>();
builder.Services.AddScoped<InvoiceEmailService>();
builder.Services.AddHttpClient<DocuSealClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DocuSealOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
        ? "http://166.1.85.41:8080"
        : opts.BaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<VehicleInvoiceService>();

var app = builder.Build();

await VerifyDatabaseAsync(app.Services, startupLog);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Behind IIS / reverse proxy without HTTPS binding, redirection can break the site.
var enableHttpsRedirect = app.Configuration.GetValue("EnableHttpsRedirection", app.Environment.IsDevelopment());
if (enableHttpsRedirect)
    app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static void LogConnectionTarget(ILogger log, string connectionString, string environmentName)
{
    try
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
        log.LogInformation(
            "Entorno={Environment}. SQL Server={Server}; Database={Database}; Encrypt={Encrypt}; TrustServerCertificate={Trust}",
            environmentName,
            builder.DataSource,
            builder.InitialCatalog,
            builder.Encrypt,
            builder.TrustServerCertificate);
    }
    catch (Exception ex)
    {
        log.LogError(ex, "ConnectionStrings:YardInventory no se pudo interpretar.");
    }
}

static async Task VerifyDatabaseAsync(IServiceProvider services, ILogger log)
{
    try
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<YardInventoryDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
        {
            log.LogError(
                "No se pudo conectar a SQL Server. Revisa ConnectionStrings:YardInventory " +
                "(instancia, BD RSYYardInventory, Trusted_Connection / usuario, Encrypt/TrustServerCertificate).");
            return;
        }

        log.LogInformation("Conexión a RSYYardInventory OK.");
    }
    catch (Exception ex)
    {
        log.LogError(ex,
            "Error al verificar la base de datos. " +
            "Si el mensaje menciona certificado/SSL, usa TrustServerCertificate=True y/o Encrypt=False en local " +
            "(ver appsettings.Development.json). En el servidor, configura un certificado válido o TrustServerCertificate=True.");
    }
}
