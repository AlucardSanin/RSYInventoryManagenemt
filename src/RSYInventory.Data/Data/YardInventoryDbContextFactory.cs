using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RSYInventory.Data.Data;

/// <summary>
/// Design-time factory for EF Core tools. Matches local Development connection.
/// Runtime uses ConnectionStrings:YardInventory from appsettings.
/// Schema changes: resources/Database/*.sql — do not use EnsureCreated.
/// </summary>
public class YardInventoryDbContextFactory : IDesignTimeDbContextFactory<YardInventoryDbContext>
{
    public YardInventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<YardInventoryDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=.\\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;");

        return new YardInventoryDbContext(optionsBuilder.Options);
    }
}
