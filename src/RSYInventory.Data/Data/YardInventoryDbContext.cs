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

    public virtual DbSet<InvoiceTemplate> InvoiceTemplates { get; set; }

    public virtual DbSet<ScheduledVehiclePickup> ScheduledVehiclePickups { get; set; }

    public virtual DbSet<ScheduledVehiclePickupImage> ScheduledVehiclePickupImages { get; set; }

    public virtual DbSet<RecycleLoad> RecycleLoads { get; set; }

    public virtual DbSet<RecycleLoadDocument> RecycleLoadDocuments { get; set; }

    public virtual DbSet<RecycleBillToCompany> RecycleBillToCompanies { get; set; }

    public virtual DbSet<RecycleWeeklyInvoice> RecycleWeeklyInvoices { get; set; }

    public virtual DbSet<ConstructorActivityLog> ConstructorActivityLogs { get; set; }

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
            entity.Property(e => e.PreferredLanguage).HasMaxLength(5).HasDefaultValue("es");
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.HasIndex(e => e.DriverAccessToken, "UQ_Users_DriverAccessToken")
                .IsUnique()
                .HasFilter("[DriverAccessToken] IS NOT NULL");

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
            entity.Property(e => e.PaymentMethod).HasMaxLength(80);
            entity.Property(e => e.InvoiceNumber);
            entity.Property(e => e.SellerSigningUrl).HasMaxLength(500);
            entity.Property(e => e.UnsignedPdfRelativePath).HasMaxLength(400);
            entity.Property(e => e.SignedPdfRelativePath).HasMaxLength(400);
            entity.Property(e => e.Vin).HasMaxLength(17);

            entity.HasIndex(e => e.PickupDriverUserId, "IX_Vehicles_PickupDriverUserId");

            entity.HasOne(d => d.AcquiredByUser).WithMany(p => p.Vehicles)
                .HasForeignKey(d => d.AcquiredByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Vehicles_Users");

            entity.HasOne(d => d.PickupDriverUser).WithMany(p => p.PickedUpVehicles)
                .HasForeignKey(d => d.PickupDriverUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Vehicles_PickupDriverUser");

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

        modelBuilder.Entity<InvoiceTemplate>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(120);
            entity.Property(e => e.BuyerCompanyName).HasMaxLength(200);
            entity.Property(e => e.BuyerAuthorizedName).HasMaxLength(200);
            entity.Property(e => e.AddressLine1).HasMaxLength(200);
            entity.Property(e => e.CityStateZip).HasMaxLength(120);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(40);
            entity.Property(e => e.PdfRelativePath).HasMaxLength(400);
            entity.Property(e => e.LogoRelativePath).HasMaxLength(400);
            entity.Property(e => e.MatchedSourceName).HasMaxLength(100);
            entity.Property(e => e.TemplateKind).HasDefaultValue(0);
            entity.Property(e => e.IsDefault).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<ScheduledVehiclePickup>(entity =>
        {
            entity.HasIndex(e => new { e.AssignedDriverUserId, e.Status, e.ScheduledPickupDate },
                "IX_ScheduledVehiclePickups_Driver_Status_Date");

            entity.Property(e => e.Vin).HasMaxLength(17);
            entity.Property(e => e.Make).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.Observations).HasMaxLength(2000);
            entity.Property(e => e.PurchasePrice).HasPrecision(12, 2);
            entity.Property(e => e.PickupAddress).HasMaxLength(500);
            entity.Property(e => e.SellerName).HasMaxLength(150);
            entity.Property(e => e.SellerPhone).HasMaxLength(40);
            entity.Property(e => e.SellerEmail).HasMaxLength(256);
            entity.Property(e => e.PaymentMethod).HasMaxLength(80);
            entity.Property(e => e.ImageRelativePath).HasMaxLength(400);
            entity.Property(e => e.ScheduledPickupWindow).HasMaxLength(80);
            entity.Property(e => e.Status).HasDefaultValue((byte)0);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.AssignedDriver).WithMany(p => p.AssignedPickups)
                .HasForeignKey(d => d.AssignedDriverUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduledVehiclePickups_AssignedDriver");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.CreatedPickups)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduledVehiclePickups_CreatedBy");

            entity.HasOne(d => d.PromotedVehicle).WithMany()
                .HasForeignKey(d => d.PromotedVehicleId)
                .HasConstraintName("FK_ScheduledVehiclePickups_PromotedVehicle");

            entity.HasOne(d => d.VehicleSource).WithMany()
                .HasForeignKey(d => d.VehicleSourceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ScheduledVehiclePickups_VehicleSources");
        });

        modelBuilder.Entity<ScheduledVehiclePickupImage>(entity =>
        {
            entity.HasIndex(e => new { e.ScheduledId, e.SortOrder, e.Id },
                "IX_ScheduledVehiclePickupImages_ScheduledId_SortOrder");

            entity.Property(e => e.RelativePath).HasMaxLength(400);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Scheduled).WithMany(p => p.Images)
                .HasForeignKey(d => d.ScheduledId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ScheduledVehiclePickupImages_Schedule");
        });

        modelBuilder.Entity<RecycleLoad>(entity =>
        {
            entity.HasIndex(e => new { e.DriverUserId, e.RecordedAtUtc }, "IX_RecycleLoads_DriverUserId_RecordedAtUtc")
                .IsDescending(false, true);
            entity.HasIndex(e => e.LoadExternalId, "IX_RecycleLoads_LoadExternalId");
            entity.HasIndex(e => new { e.IsVerified, e.RecordedAtUtc }, "IX_RecycleLoads_IsVerified_RecordedAtUtc")
                .IsDescending(false, true);

            entity.Property(e => e.LoadExternalId).HasMaxLength(80);
            entity.Property(e => e.TruckNumber).HasMaxLength(40);
            entity.Property(e => e.RateUsd).HasPrecision(12, 2).HasDefaultValue(575m);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.IsVerified).HasDefaultValue(false);
            entity.Property(e => e.RecordedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasIndex(e => e.LoadDate, "IX_RecycleLoads_LoadDate").IsDescending();
            entity.HasIndex(e => new { e.BillToCompanyId, e.LoadDate }, "IX_RecycleLoads_BillToCompanyId_LoadDate")
                .IsDescending(false, true);

            entity.HasOne(d => d.Driver).WithMany(p => p.RecycleLoadsAsDriver)
                .HasForeignKey(d => d.DriverUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleLoads_Driver");

            entity.HasOne(d => d.BillToCompany).WithMany()
                .HasForeignKey(d => d.BillToCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleLoads_BillTo");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.RecycleLoadsRecorded)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleLoads_RecordedBy");

            entity.HasOne(d => d.VerifiedByUser).WithMany()
                .HasForeignKey(d => d.VerifiedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleLoads_VerifiedBy");
        });

        modelBuilder.Entity<RecycleBillToCompany>(entity =>
        {
            entity.Property(e => e.Alias).HasMaxLength(80);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.AddressLine).HasMaxLength(300);
            entity.Property(e => e.ContactLine).HasMaxLength(300);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<RecycleWeeklyInvoice>(entity =>
        {
            entity.HasIndex(e => e.InvoiceNumber, "UQ_RecycleWeeklyInvoices_InvoiceNumber").IsUnique();
            entity.HasIndex(e => new { e.WeekStartDate, e.BillToCompanyId }, "UQ_RecycleWeeklyInvoices_Week_BillTo").IsUnique();
            entity.HasIndex(e => e.WeekStartDate, "IX_RecycleWeeklyInvoices_WeekStartDate").IsDescending();

            entity.Property(e => e.PdfRelativePath).HasMaxLength(400);
            entity.Property(e => e.TotalAmountUsd).HasPrecision(12, 2);
            entity.Property(e => e.GeneratedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.BillToCompany).WithMany()
                .HasForeignKey(d => d.BillToCompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleWeeklyInvoices_BillTo");

            entity.HasOne(d => d.GeneratedByUser).WithMany()
                .HasForeignKey(d => d.GeneratedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_RecycleWeeklyInvoices_GeneratedBy");
        });

        modelBuilder.Entity<RecycleLoadDocument>(entity =>
        {
            entity.HasIndex(e => new { e.RecycleLoadId, e.DocumentType }, "UQ_RecycleLoadDocuments_Load_Type")
                .IsUnique();

            entity.Property(e => e.RelativePath).HasMaxLength(400);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.RecycleLoad).WithMany(p => p.Documents)
                .HasForeignKey(d => d.RecycleLoadId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RecycleLoadDocuments_Load");
        });

        modelBuilder.Entity<ConstructorActivityLog>(entity =>
        {
            entity.HasIndex(e => new { e.DriverUserId, e.RecordedAtUtc },
                    "IX_ConstructorActivityLogs_DriverUserId_RecordedAtUtc")
                .IsDescending(false, true);

            entity.Property(e => e.RelativePath).HasMaxLength(400);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RecordedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Driver).WithMany(p => p.ConstructorActivitiesAsDriver)
                .HasForeignKey(d => d.DriverUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConstructorActivityLogs_Driver");

            entity.HasOne(d => d.RecordedByUser).WithMany(p => p.ConstructorActivitiesRecorded)
                .HasForeignKey(d => d.RecordedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConstructorActivityLogs_RecordedBy");
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
