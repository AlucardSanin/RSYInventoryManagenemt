using RSYInventory.Data;
using RSYInventory.Data.Services;
using RSYInventory.Web.Components;
using RSYInventory.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
builder.Services.AddSingleton<MediaStorageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
