using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSYInventory.Data.Data;
using RSYInventory.Data.Services;

namespace RSYInventory.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core against the YardInventory connection string and domain services.
    /// Change the connection string in appsettings to point at local SQL or the future server.
    /// </summary>
    public static IServiceCollection AddYardInventoryData(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<YardInventoryDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3);
                sql.MigrationsAssembly(typeof(YardInventoryDbContext).Assembly.FullName);
            }));

        services.AddScoped<ICurrentUserService, DatabaseCurrentUserService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<YardLayoutService>();

        return services;
    }

    /// <summary>
    /// Ensures the configured database is reachable and reference/demo rows exist in SQL.
    /// </summary>
    public static async Task InitializeYardInventoryDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<YardInventoryDbContext>();

        // Does not create the database file/server — you create RSYYardInventory in SSMS.
        // Applies missing tables if the DB is empty, then seeds reference data into SQL.
        await db.Database.EnsureCreatedAsync(ct);
        await ReferenceDataSeeder.EnsureSeededAsync(db, ct);
    }
}
