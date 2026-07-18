using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RSYInventory.Data.Data;

/// <summary>
/// Design-time factory for EF Core tools (migrations / scaffolding).
/// Update the connection string to match your local SQL Server instance.
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
