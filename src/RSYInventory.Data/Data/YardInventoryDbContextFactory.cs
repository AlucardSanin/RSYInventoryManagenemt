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
            "Server=np:\\\\.\\pipe\\MSSQL$MSSQLSERVER01\\sql\\query;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30;");

        return new YardInventoryDbContext(optionsBuilder.Options);
    }
}
