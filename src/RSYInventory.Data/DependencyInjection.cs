using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSYInventory.Data.Data;
using RSYInventory.Data.Services;

namespace RSYInventory.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddYardInventoryData(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<YardInventoryDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ICurrentUserService, DevCurrentUserService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<YardLayoutService>();

        return services;
    }
}
