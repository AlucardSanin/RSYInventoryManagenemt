using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RSYInventory.Data.Data;

/// <summary>
/// Design-time factory for EF Core tools. Points at the local RSYYardInventory database.
/// Runtime uses ConnectionStrings:YardInventory from appsettings (Dev local / Prod server).
/// </summary>
public class YardInventoryDbContextFactory : IDesignTimeDbContextFactory<YardInventoryDbContext>
{
    public YardInventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<YardInventoryDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;");

        return new YardInventoryDbContext(optionsBuilder.Options);
    }
}
