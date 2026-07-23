using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Data;

public partial class YardInventoryDbContext : DbContext
{
    public YardInventoryDbContext(DbContextOptions<YardInventoryDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditEvent> AuditEvents { get; set; }

    public virtual DbSet<InventoryItem> InventoryItems { get; set; }

    public virtual DbSet<InventoryMovement> InventoryMovements { get; set; }

    public virtual DbSet<Pallet> Pallets { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Row> Rows { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Vehicle> Vehicles { get; set; }

    public virtual DbSet<VehicleImage> VehicleImages { get; set; }

    public virtual DbSet<VehicleSource> VehicleSources { get; set; }

    public virtual DbSet<Zone> Zones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.HasIndex(e => e.CreatedAtUtc, "IX_AuditEvents_CreatedAtUtc").IsDescending();
            entity.HasIndex(e => e.UserId, "IX_AuditEvents_UserId");
            entity.HasIndex(e => e.EventType, "IX_AuditEvents_EventType");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EventType).HasMaxLength(80);
            entity.Property(e => e.EntityType).HasMaxLength(80);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasOne(d => d.User).WithMany(p => p.AuditEvents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditEvents_Users");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasIndex(e => e.ItemType, "IX_InventoryItems_ItemType");

            entity.HasIndex(e => e.PalletId, "IX_InventoryItems_PalletId");

            entity.HasIndex(e => e.Status, "IX_InventoryItems_Status");

            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplacementLiters).HasPrecision(4, 2);
            entity.Property(e => e.ImageRelativePath).HasMaxLength(400);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.PartNumber).HasMaxLength(100);
            entity.Property(e => e.SourceVin).HasMaxLength(17);
            entity.Property(e => e.Status).HasDefaultValue(1);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.CreatedInventoryItems)
                .HasForeignKey(d => d.CreatedByUserId)
                .HasConstraintName("FK_InventoryItems_CreatedByUser");

            entity.HasOne(d => d.Pallet).WithMany(p => p.InventoryItems)
                .HasForeignKey(d => d.PalletId)
                .HasConstraintName("FK_InventoryItems_Pallets");
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasIndex(e => e.FromPalletId, "IX_InventoryMovements_FromPalletId");

            entity.HasIndex(e => e.MovedAtUtc, "IX_InventoryMovements_MovedAtUtc").IsDescending();

            entity.HasIndex(e => e.ToPalletId, "IX_InventoryMovements_ToPalletId");

            entity.HasIndex(e => e.UserId, "IX_InventoryMovements_UserId");

            entity.Property(e => e.MovedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(d => d.FromPallet).WithMany(p => p.InventoryMovementFromPallets)
                .HasForeignKey(d => d.FromPalletId)
                .HasConstraintName("FK_InventoryMovements_FromPallet");

            entity.HasOne(d => d.InventoryItem).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.InventoryItemId)
                .HasConstraintName("FK_InventoryMovements_InventoryItems");

            entity.HasOne(d => d.ToPallet).WithMany(p => p.InventoryMovementToPallets)
                .HasForeignKey(d => d.ToPalletId)
                .HasConstraintName("FK_InventoryMovements_ToPallet");

            entity.HasOne(d => d.User).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_InventoryMovements_Users");

            entity.HasOne(d => d.Vehicle).WithMany(p => p.InventoryMovements)
                .HasForeignKey(d => d.VehicleId)
                .HasConstraintName("FK_InventoryMovements_Vehicles");
        });

        modelBuilder.Entity<Pallet>(entity =>
        {
            entity.HasIndex(e => new { e.RowId, e.PalletNumber }, "UQ_Pallets_Row_PalletNumber").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Label).HasMaxLength(50);

            entity.HasOne(d => d.Row).WithMany(p => p.Pallets)
                .HasForeignKey(d => d.RowId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Pallets_Rows");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Code, "UQ_Roles_Code").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Row>(entity =>
        {
            entity.HasIndex(e => new { e.ZoneId, e.RowNumber }, "UQ_Rows_Zone_RowNumber").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Label).HasMaxLength(50);

            entity.HasOne(d => d.Zone).WithMany(p => p.Rows)
                .HasForeignKey(d => d.ZoneId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Rows_Zones");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.UserName, "UQ_Users_UserName").IsUnique();

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.UserName).HasMaxLength(100);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserRole",
                    r => r.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Roles"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserRoles_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("UserRoles");
                    });
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(e => e.Vin, "UQ_Vehicles_Vin").IsUnique();

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.ImageRelativePath).HasMaxLength(400);
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Observations).HasMaxLength(2000);
            entity.Property(e => e.PurchasePrice).HasPrecision(12, 2);
            entity.Property(e => e.AcquisitionLocation).HasMaxLength(200);
            entity.Property(e => e.SellerName).HasMaxLength(150);
            entity.Property(e => e.SellerPhone).HasMaxLength(40);
            entity.Property(e => e.SellerEmail).HasMaxLength(256);
            entity.Property(e => e.PickupDriver).HasMaxLength(150);
            entity.Property(e => e.Vin).HasMaxLength(17);

            entity.HasOne(d => d.AcquiredByUser).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.AcquiredByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicles_Users");

            entity.HasOne(d => d.Pallet).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.PalletId)
                .HasConstraintName("FK_Vehicles_Pallets");

            entity.HasOne(d => d.VehicleSource).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.VehicleSourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicles_VehicleSources");
        });

        modelBuilder.Entity<VehicleImage>(entity =>
        {
            entity.HasIndex(e => new { e.VehicleId, e.SortOrder, e.Id }, "IX_VehicleImages_VehicleId_SortOrder");

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RelativePath).HasMaxLength(400);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);

            entity.HasOne(d => d.Vehicle).WithMany(p => p.Images)
                .HasForeignKey(d => d.VehicleId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_VehicleImages_Vehicles");
        });

        modelBuilder.Entity<VehicleSource>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_VehicleSources_Name").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasIndex(e => new { e.Name, e.Purpose }, "UQ_Zones_Name_Purpose").IsUnique();

            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
