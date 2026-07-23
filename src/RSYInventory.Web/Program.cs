using Microsoft.AspNetCore.DataProtection;
using RSYInventory.Data;
using RSYInventory.Data.Services;
using RSYInventory.Web.Components;
using RSYInventory.Web.Services;
using RSYInventory.Web.Services.Invoice;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment()
        || builder.Configuration.GetValue<bool>("DetailedErrors");
});

// Persist keys so antiforgery / protected session survive restarts.
// Ephemeral keys break InputFile uploads after deploy while Blazor circuits still look "logged in".
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("RSYInventory");

// ConnectionStrings:YardInventory
// - Development → instancia local (.\MSSQLSERVER01 / RSYYardInventory)
// - Production  → servidor remoto (appsettings.Production.json)
// El esquema NO se crea desde la app: usa resources/Database/*.sql en SSMS.
var connectionString = builder.Configuration.GetConnectionString("YardInventory")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:YardInventory. Configúrala en appsettings / User Secrets.");

builder.Services.AddYardInventoryData(connectionString);
builder.Services.AddScoped<IUserSessionStore, ProtectedUserSessionStore>();
builder.Services.AddScoped<UiBusyService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddSingleton<MediaStorageService>();

builder.Services.Configure<CompanyInvoiceOptions>(
    builder.Configuration.GetSection(CompanyInvoiceOptions.SectionName));
builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));

builder.Services.AddSingleton<PurchaseInvoicePdfService>();
builder.Services.AddScoped<InvoiceEmailService>();
builder.Services.AddScoped<VehicleInvoiceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
