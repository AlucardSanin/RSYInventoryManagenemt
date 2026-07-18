using RSYInventory.Data;
using RSYInventory.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Connection string name: YardInventory
// - Development: appsettings.Development.json → local RSYYardInventory
// - Production:  appsettings.Production.json → server remoto (cámbialo cuando toque)
var connectionString = builder.Configuration.GetConnectionString("YardInventory")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:YardInventory. Configúrala en appsettings / User Secrets.");

builder.Services.AddYardInventoryData(connectionString);

var app = builder.Build();

// Crea tablas si la BD está vacía y asegura roles/usuario/zonas/fuentes EN SQL Server.
await app.Services.InitializeYardInventoryDatabaseAsync();

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
