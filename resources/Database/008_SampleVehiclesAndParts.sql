/*
================================================================================
 008_SampleVehiclesAndParts.sql
================================================================================
 Datos de prueba: vehículos adquiridos + motores/transmisiones en inventario.

 NO crea usuarios. Usa el primer usuario activo de la BD para AcquiredByUserId /
 CreatedByUserId (debe existir al menos uno).

 Requisitos previos:
   - Schema completo (001 … 007)
   - Al menos 1 usuario activo
   - Fuentes de vehículo (Wheelzy / Pebble / …)
   - Zona de partes con paletas (p. ej. 002_DemoUserAndPartsZone)
   - Zonas de vehículos con paletas (Zona A / B del 001) — opcionales; si no hay,
     los vehículos quedan sin ubicar

 Seguro de re-ejecutar: salta VINs / piezas ya insertadas (prefijo TESTVEH / SAMPLE-).
================================================================================
*/
USE [RSYYardInventory];
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @UserId INT =
(
    SELECT TOP (1) Id
    FROM dbo.Users
    WHERE IsActive = 1
    ORDER BY Id
);

IF @UserId IS NULL
BEGIN
    RAISERROR(N'No hay usuarios activos. Crea al menos un usuario antes de cargar datos de prueba.', 16, 1);
    RETURN;
END;

DECLARE @SourceWheelzy INT = COALESCE(
    (SELECT TOP 1 Id FROM dbo.VehicleSources WHERE Name = N'Wheelzy' AND IsActive = 1),
    (SELECT TOP 1 Id FROM dbo.VehicleSources WHERE IsActive = 1 ORDER BY Id));
DECLARE @SourcePebble INT = COALESCE(
    (SELECT TOP 1 Id FROM dbo.VehicleSources WHERE Name = N'Pebble' AND IsActive = 1),
    @SourceWheelzy);
DECLARE @SourceFb INT = COALESCE(
    (SELECT TOP 1 Id FROM dbo.VehicleSources WHERE Name = N'Facebook' AND IsActive = 1),
    @SourceWheelzy);
DECLARE @SourceOther INT = COALESCE(
    (SELECT TOP 1 Id FROM dbo.VehicleSources WHERE Name = N'Other' AND IsActive = 1),
    @SourceWheelzy);

IF @SourceWheelzy IS NULL
BEGIN
    RAISERROR(N'No hay fuentes de vehículo (VehicleSources). Aplica 001_InitialSchema.sql.', 16, 1);
    RETURN;
END;

/* Paletas de vehículos (Purpose = 2) */
DECLARE @VehPallets TABLE (Ord INT IDENTITY(1,1) PRIMARY KEY, PalletId INT NOT NULL);
INSERT INTO @VehPallets (PalletId)
SELECT p.Id
FROM dbo.Pallets p
INNER JOIN dbo.Rows r ON r.Id = p.RowId
INNER JOIN dbo.Zones z ON z.Id = r.ZoneId
WHERE p.IsActive = 1 AND r.IsActive = 1 AND z.IsActive = 1 AND z.Purpose = 2
ORDER BY z.Name, r.RowNumber, p.PalletNumber;

/* Paletas de partes (Purpose = 1) */
DECLARE @PartPallets TABLE (Ord INT IDENTITY(1,1) PRIMARY KEY, PalletId INT NOT NULL);
INSERT INTO @PartPallets (PalletId)
SELECT p.Id
FROM dbo.Pallets p
INNER JOIN dbo.Rows r ON r.Id = p.RowId
INNER JOIN dbo.Zones z ON z.Id = r.ZoneId
WHERE p.IsActive = 1 AND r.IsActive = 1 AND z.IsActive = 1 AND z.Purpose = 1
ORDER BY z.Name, r.RowNumber, p.PalletNumber;

IF NOT EXISTS (SELECT 1 FROM @PartPallets)
BEGIN
    RAISERROR(N'No hay paletas en zonas de partes. Aplica 002_DemoUserAndPartsZone.sql (o crea una zona de partes).', 16, 1);
    RETURN;
END;

DECLARE @Vp1 INT = (SELECT PalletId FROM @VehPallets WHERE Ord = 1);
DECLARE @Vp2 INT = (SELECT PalletId FROM @VehPallets WHERE Ord = 2);
DECLARE @Vp3 INT = (SELECT PalletId FROM @VehPallets WHERE Ord = 3);

DECLARE @Pp1 INT = (SELECT PalletId FROM @PartPallets WHERE Ord = 1);
DECLARE @Pp2 INT = COALESCE((SELECT PalletId FROM @PartPallets WHERE Ord = 2), @Pp1);
DECLARE @Pp3 INT = COALESCE((SELECT PalletId FROM @PartPallets WHERE Ord = 3), @Pp1);
DECLARE @Pp4 INT = COALESCE((SELECT PalletId FROM @PartPallets WHERE Ord = 4), @Pp2);

DECLARE @PartPalletCount INT = (SELECT COUNT(*) FROM @PartPallets);
DECLARE @VehPalletCount INT = (SELECT COUNT(*) FROM @VehPallets);

PRINT N'Usuario seed Id=' + CAST(@UserId AS NVARCHAR(20))
    + N' | Paletas partes=' + CAST(@PartPalletCount AS NVARCHAR(10))
    + N' | Paletas vehículos=' + CAST(@VehPalletCount AS NVARCHAR(10));

/*------------------------------------------------------------------------------
  Vehículos (VIN = TESTVEH…)
------------------------------------------------------------------------------*/
;WITH SeedVehicles AS
(
    SELECT *
    FROM (VALUES
        (N'TESTVEH0000000001', 2018, N'Toyota',     N'Camry',     1, 1,  82000,  1850.00, N'Seed · sin daño mayor',           @SourceWheelzy, CAST(N'2026-03-12' AS DATE), @Vp1),
        (N'TESTVEH0000000002', 2015, N'Honda',      N'Civic',     1, 1, 112000,  1200.00, N'Seed · motor ok',                 @SourcePebble,  CAST(N'2026-04-01' AS DATE), @Vp1),
        (N'TESTVEH0000000003', 2012, N'Ford',       N'F-150',     1, 4, 145000,  2400.00, N'Seed · 4x4',                      @SourceFb,      CAST(N'2026-02-20' AS DATE), @Vp2),
        (N'TESTVEH0000000004', 2020, N'Chevrolet',  N'Silverado', 1, 5,  67000,  3100.00, N'Seed · cabina doble',             @SourceWheelzy, CAST(N'2026-05-08' AS DATE), @Vp2),
        (N'TESTVEH0000000005', 2008, N'Nissan',     N'Altima',    1, 1, 168000,   650.00, N'Seed · para partes',              @SourceOther,   CAST(N'2026-01-15' AS DATE), NULL),
        (N'TESTVEH0000000006', 2016, N'BMW',        N'328i',      1, 2,  94000,  2200.00, N'Seed · RWD',                      @SourcePebble,  CAST(N'2026-06-02' AS DATE), @Vp3),
        (N'TESTVEH0000000007', 2019, N'Jeep',       N'Wrangler',  2, 4,  51000,  4500.00, N'Seed · manual',                   @SourceFb,      CAST(N'2026-06-18' AS DATE), NULL),
        (N'TESTVEH0000000008', 2014, N'Subaru',     N'Outback',   1, 3, 128000,  1500.00, N'Seed · AWD',                      @SourceWheelzy, CAST(N'2026-03-28' AS DATE), @Vp3),
        (N'TESTVEH0000000009', 2011, N'Hyundai',    N'Sonata',    1, 1, 155000,   700.00, N'Seed · pendiente ubicar',         @SourceOther,   CAST(N'2026-07-01' AS DATE), NULL),
        (N'TESTVEH0000000010', 2017, N'GMC',        N'Sierra',    1, 4,  88000,  2800.00, N'Seed · buen estado cosmético',    @SourcePebble,  CAST(N'2026-07-10' AS DATE), @Vp1)
    ) AS v (Vin, [Year], Make, Model, TransmissionType, DriveType, Mileage, PurchasePrice, Observations, VehicleSourceId, AcquiredAt, PalletId)
)
INSERT INTO dbo.Vehicles
(
    Vin, [Year], Make, Model, TransmissionType, DriveType, Mileage, PurchasePrice,
    Observations, VehicleSourceId, AcquiredAt, AcquiredByUserId, PalletId, CreatedAtUtc
)
SELECT
    s.Vin, s.[Year], s.Make, s.Model, s.TransmissionType, s.DriveType, s.Mileage, s.PurchasePrice,
    s.Observations, s.VehicleSourceId, s.AcquiredAt, @UserId, s.PalletId, SYSUTCDATETIME()
FROM SeedVehicles s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Vehicles x WHERE x.Vin = s.Vin);

DECLARE @VehicleSeedCount INT = (SELECT COUNT(*) FROM dbo.Vehicles WHERE Vin LIKE N'TESTVEH%');
PRINT N'Vehículos TESTVEH* en BD: ' + CAST(@VehicleSeedCount AS NVARCHAR(10));

/*------------------------------------------------------------------------------
  Partes (PartNumber = SAMPLE-…)
  Capacidad: máx. 1 motor y 2 transmisiones por paleta.
------------------------------------------------------------------------------*/
;WITH SeedParts AS
(
    SELECT *
    FROM (VALUES
        (N'SAMPLE-ENG-001', 1, N'Toyota',    N'Camry',     2018, N'Motor completo',      N'Seed motor', N'TESTVEH0000000001', 2.5,  NULL, NULL, @Pp1),
        (N'SAMPLE-TRN-001', 2, N'Toyota',    N'Camry',     2018, N'Caja automática',     N'Seed caja',  N'TESTVEH0000000001', NULL, 1,    1,    @Pp1),
        (N'SAMPLE-TRN-002', 2, N'Honda',     N'Civic',     2015, N'Caja automática',     N'Seed caja',  N'TESTVEH0000000002', NULL, 1,    1,    @Pp1),
        (N'SAMPLE-ENG-002', 1, N'Ford',      N'F-150',     2012, N'Motor 5.0',           N'Seed motor', N'TESTVEH0000000003', 5.0,  NULL, NULL, @Pp2),
        (N'SAMPLE-TRN-003', 2, N'Ford',      N'F-150',     2012, N'Transmisión 4x4',     N'Seed caja',  N'TESTVEH0000000003', NULL, 1,    4,    @Pp2),
        (N'SAMPLE-ENG-003', 1, N'Chevrolet', N'Silverado', 2020, N'Motor 5.3',           N'Seed motor', N'TESTVEH0000000004', 5.3,  NULL, NULL, @Pp3),
        (N'SAMPLE-TRN-004', 2, N'BMW',       N'328i',      2016, N'Caja automática RWD', N'Seed caja',  N'TESTVEH0000000006', NULL, 1,    2,    @Pp3),
        (N'SAMPLE-TRN-005', 2, N'Subaru',    N'Outback',   2014, N'Caja CVT AWD',        N'Seed caja',  N'TESTVEH0000000008', NULL, 1,    3,    @Pp3),
        (N'SAMPLE-ENG-004', 1, N'Nissan',    N'Altima',    2008, N'Motor 2.5',           N'Seed motor', N'TESTVEH0000000005', 2.5,  NULL, NULL, @Pp4),
        (N'SAMPLE-TRN-006', 2, N'Jeep',      N'Wrangler',  2019, N'Caja manual 4x4',     N'Seed caja',  N'TESTVEH0000000007', NULL, 2,    4,    @Pp4),
        (N'SAMPLE-TRN-007', 2, N'GMC',       N'Sierra',    2017, N'Caja automática 4x4', N'Seed caja',  N'TESTVEH0000000010', NULL, 1,    4,    @Pp4),
        (N'SAMPLE-TRN-008', 2, N'Hyundai',   N'Sonata',    2011, N'Caja automática',     N'Seed caja',  NULL,                 NULL, 1,    1,    @Pp2)
    ) AS p (PartNumber, ItemType, Brand, Model, [Year], [Description], Notes, SourceVin, DisplacementLiters, TransmissionType, DriveType, PalletId)
)
INSERT INTO dbo.InventoryItems
(
    ItemType, Status, PartNumber, Brand, Model, [Year], [Description], Notes,
    SourceVin, DisplacementLiters, TransmissionType, DriveType,
    PalletId, CreatedByUserId, CreatedAtUtc
)
SELECT
    s.ItemType,
    1, -- Available
    s.PartNumber,
    s.Brand,
    s.Model,
    s.[Year],
    s.[Description],
    s.Notes,
    s.SourceVin,
    s.DisplacementLiters,
    s.TransmissionType,
    s.DriveType,
    s.PalletId,
    @UserId,
    SYSUTCDATETIME()
FROM SeedParts s
WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryItems x WHERE x.PartNumber = s.PartNumber);

DECLARE @PartSeedCount INT = (SELECT COUNT(*) FROM dbo.InventoryItems WHERE PartNumber LIKE N'SAMPLE-%');
PRINT N'Partes SAMPLE-* en BD: ' + CAST(@PartSeedCount AS NVARCHAR(10));

PRINT N'008_SampleVehiclesAndParts applied.';
GO
