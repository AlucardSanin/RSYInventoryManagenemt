using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSYInventory.Data.Data;
using RSYInventory.Data.Services;

namespace RSYInventory.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core against an existing SQL Server database.
    /// Schema and seed data are managed with resources/Database/*.sql (SSMS), not EnsureCreated.
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
}
