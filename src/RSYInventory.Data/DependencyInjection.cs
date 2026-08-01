using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RSYInventory.Data.Data;
using RSYInventory.Data.Services;

namespace RSYInventory.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core against an existing SQL Server database.
    /// Uses <see cref="IDbContextFactory{TContext}"/> so Blazor Server never shares one context across concurrent UI work.
    /// Host must also register <see cref="IUserSessionStore"/>.
    /// </summary>
    public static IServiceCollection AddYardInventoryData(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<YardInventoryDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(maxRetryCount: 3);
                sql.MigrationsAssembly(typeof(YardInventoryDbContext).Assembly.FullName);
            }));

        services.AddScoped<CurrentUserState>();
        services.AddScoped<ICurrentUserService, DatabaseCurrentUserService>();
        services.AddSingleton<PasswordService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserAdminService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<PickupScheduleService>();
        services.AddScoped<InvoiceTemplateService>();
        services.AddScoped<AuditService>();
        services.AddScoped<ActivityLogService>();
        services.AddScoped<YardLayoutService>();

        services.AddHttpClient<VinLookupService>(client =>
        {
            client.BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
