using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Data;

public class YardInventoryDbContext : DbContext
{
    public YardInventoryDbContext(DbContextOptions<YardInventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Row> Rows => Set<Row>();
    public DbSet<Pallet> Pallets => Set<Pallet>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<VehicleSource> VehicleSources => Set<VehicleSource>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.HasIndex(e => e.UserName).IsUnique();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);
            // Fixed role ids (1..4) live in SQL; not an IDENTITY column.
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Code).HasConversion<int>();
            entity.HasIndex(e => e.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(e => new { e.UserId, e.RoleId });
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Zone>(entity =>
        {
            entity.ToTable("Zones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Purpose).HasConversion<int>();
            entity.HasIndex(e => new { e.Name, e.Purpose }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint("CK_Zones_RowCount", "[RowCount] >= 0"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Zones_PalletsPerRow", "[PalletsPerRow] >= 0"));
        });

        modelBuilder.Entity<Row>(entity =>
        {
            entity.ToTable("Rows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(50);
            entity.HasIndex(e => new { e.ZoneId, e.RowNumber }).IsUnique();
            entity.HasOne(e => e.Zone)
                .WithMany(z => z.Rows)
                .HasForeignKey(e => e.ZoneId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pallet>(entity =>
        {
            entity.ToTable("Pallets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(50);
            entity.HasIndex(e => new { e.RowId, e.PalletNumber }).IsUnique();
            entity.HasOne(e => e.Row)
                .WithMany(r => r.Pallets)
                .HasForeignKey(e => e.RowId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemType).HasConversion<int>();
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PartNumber).HasMaxLength(100);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.HasOne(e => e.Pallet)
                .WithMany(p => p.InventoryItems)
                .HasForeignKey(e => e.PalletId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.PalletId);
            entity.HasIndex(e => e.ItemType);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MovementType).HasConversion<int>();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.InventoryItem)
                .WithMany(i => i.Movements)
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Vehicle)
                .WithMany(v => v.Movements)
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FromPallet)
                .WithMany(p => p.MovementsFrom)
                .HasForeignKey(e => e.FromPalletId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToPallet)
                .WithMany(p => p.MovementsTo)
                .HasForeignKey(e => e.ToPalletId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                .WithMany(u => u.Movements)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.MovedAtUtc);
            entity.HasIndex(e => e.FromPalletId);
            entity.HasIndex(e => e.ToPalletId);
            entity.HasIndex(e => e.UserId);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_InventoryMovements_Subject",
                "([InventoryItemId] IS NOT NULL AND [VehicleId] IS NULL) OR ([InventoryItemId] IS NULL AND [VehicleId] IS NOT NULL)"));
        });

        modelBuilder.Entity<VehicleSource>(entity =>
        {
            entity.ToTable("VehicleSources");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.ToTable("Vehicles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Vin).HasMaxLength(17).IsRequired();
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Observations).HasMaxLength(2000);
            entity.Property(e => e.TransmissionType).HasConversion<int?>();
            entity.Property(e => e.DriveType).HasConversion<int?>();
            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasOne(e => e.VehicleSource)
                .WithMany(s => s.Vehicles)
                .HasForeignKey(e => e.VehicleSourceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AcquiredByUser)
                .WithMany(u => u.AcquiredVehicles)
                .HasForeignKey(e => e.AcquiredByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Pallet)
                .WithMany(p => p.Vehicles)
                .HasForeignKey(e => e.PalletId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Reference/demo rows are stored in SQL Server via ReferenceDataSeeder
        // and resources/Database/*.sql — not as in-memory HasData.
    }
}
