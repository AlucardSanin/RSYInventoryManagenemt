/*
================================================================================
 RSY Yard Inventory — Database Architecture Script
================================================================================
 Purpose:
   Living SQL script that recreates the full database schema (tables, indexes,
   constraints, seed data). Update this file whenever the database architecture
   changes so the yard inventory can be rebuilt from source control.

 Target:
   Microsoft SQL Server (SQL Server Management Studio / SSMS)

 How to use:
   1. Create an empty database (e.g. RSYYardInventory) in SSMS, or uncomment
      the CREATE DATABASE section below.
   2. Run this script against that database.
   3. Point Entity Framework (RSYInventory.Data) at the same connection string.

 Notes on business rules enforced in application code (not only SQL):
   - Pallet capacity (parts): max 1 engine, max 2 transmissions; engine+transmission OK.
   - Cannot delete a Zone / Row / Pallet that still holds inventory or vehicles.
   - Location changes must record Sold vs Relocated in InventoryMovements.
================================================================================
*/

-- USE [master];
-- GO
-- IF DB_ID(N'RSYYardInventory') IS NULL
--     CREATE DATABASE [RSYYardInventory];
-- GO
-- USE [RSYYardInventory];
-- GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/*------------------------------------------------------------------------------
  Roles & Users
------------------------------------------------------------------------------*/
IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        Id          INT            NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
        Code        INT            NOT NULL, -- AppRole enum
        Name        NVARCHAR(100)  NOT NULL,
        Description NVARCHAR(500)  NULL,
        CONSTRAINT UQ_Roles_Code UNIQUE (Code)
    );
END
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id           INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
        UserName     NVARCHAR(100)  NOT NULL,
        DisplayName  NVARCHAR(200)  NOT NULL,
        Email        NVARCHAR(256)  NULL,
        PasswordHash NVARCHAR(500)  NULL,
        IsActive     BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(7)   NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_Users_UserName UNIQUE (UserName)
    );
END
GO

IF OBJECT_ID(N'dbo.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserRoles
    (
        UserId INT NOT NULL,
        RoleId INT NOT NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id)
    );
END
GO

/*------------------------------------------------------------------------------
  Yard layout: Zone -> Row -> Pallet
  Purpose: 1 = Parts (engines/transmissions), 2 = Vehicles
------------------------------------------------------------------------------*/
IF OBJECT_ID(N'dbo.Zones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Zones
    (
        Id            INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_Zones PRIMARY KEY,
        Name          NVARCHAR(100)  NOT NULL,
        Purpose       INT            NOT NULL, -- ZonePurpose enum
        RowAmount      INT            NOT NULL,
        PalletsPerRow INT            NOT NULL,
        IsActive      BIT            NOT NULL CONSTRAINT DF_Zones_IsActive DEFAULT (1),
        CreatedAtUtc  DATETIME2(7)   NOT NULL CONSTRAINT DF_Zones_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc  DATETIME2(7)   NULL,
        CONSTRAINT UQ_Zones_Name_Purpose UNIQUE (Name, Purpose),
        CONSTRAINT CK_Zones_RowAmount CHECK (RowAmount >= 0),
        CONSTRAINT CK_Zones_PalletsPerRow CHECK (PalletsPerRow >= 0)
    );
END
GO

IF OBJECT_ID(N'dbo.Rows', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rows
    (
        Id        INT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_Rows PRIMARY KEY,
        ZoneId    INT           NOT NULL,
        RowNumber INT           NOT NULL,
        Label     NVARCHAR(50)  NULL,
        IsActive  BIT           NOT NULL CONSTRAINT DF_Rows_IsActive DEFAULT (1),
        CONSTRAINT UQ_Rows_Zone_RowNumber UNIQUE (ZoneId, RowNumber),
        CONSTRAINT FK_Rows_Zones FOREIGN KEY (ZoneId) REFERENCES dbo.Zones (Id)
    );
END
GO

IF OBJECT_ID(N'dbo.Pallets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Pallets
    (
        Id           INT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_Pallets PRIMARY KEY,
        RowId        INT           NOT NULL,
        PalletNumber INT           NOT NULL,
        Label        NVARCHAR(50)  NULL,
        IsActive     BIT           NOT NULL CONSTRAINT DF_Pallets_IsActive DEFAULT (1),
        CONSTRAINT UQ_Pallets_Row_PalletNumber UNIQUE (RowId, PalletNumber),
        CONSTRAINT FK_Pallets_Rows FOREIGN KEY (RowId) REFERENCES dbo.Rows (Id)
    );
END
GO

/*------------------------------------------------------------------------------
  Parts inventory (engines & transmissions)
------------------------------------------------------------------------------*/
IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryItems
    (
        Id          INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_InventoryItems PRIMARY KEY,
        ItemType    INT            NOT NULL, -- 1 Engine, 2 Transmission
        Status      INT            NOT NULL CONSTRAINT DF_InventoryItems_Status DEFAULT (1), -- 1 Available, 2 Sold
        PartNumber  NVARCHAR(100)  NULL,
        Brand       NVARCHAR(100)  NULL,
        Model       NVARCHAR(100)  NULL,
        Year        INT            NULL,
        Description NVARCHAR(500)  NULL,
        Notes       NVARCHAR(2000) NULL,
        SourceVin           NVARCHAR(17)   NULL, -- donor vehicle VIN (optional decode)
        DisplacementLiters  DECIMAL(4, 2)  NULL, -- engines
        TransmissionType    INT            NULL, -- transmissions: 1 Automatic, 2 Manual
        DriveType           INT            NULL, -- transmissions: FWD/RWD/AWD/4x4/4x2
        PalletId    INT            NULL, -- NULL when sold / not placed
        CreatedAtUtc DATETIME2(7)  NOT NULL CONSTRAINT DF_InventoryItems_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(7)  NULL,
        CONSTRAINT FK_InventoryItems_Pallets FOREIGN KEY (PalletId) REFERENCES dbo.Pallets (Id)
    );

    CREATE INDEX IX_InventoryItems_PalletId ON dbo.InventoryItems (PalletId);
    CREATE INDEX IX_InventoryItems_ItemType ON dbo.InventoryItems (ItemType);
    CREATE INDEX IX_InventoryItems_Status ON dbo.InventoryItems (Status);
END
GO

/*------------------------------------------------------------------------------
  Vehicle acquisition sources & vehicles
------------------------------------------------------------------------------*/
IF OBJECT_ID(N'dbo.VehicleSources', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleSources
    (
        Id       INT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_VehicleSources PRIMARY KEY,
        Name     NVARCHAR(100) NOT NULL,
        IsActive BIT           NOT NULL CONSTRAINT DF_VehicleSources_IsActive DEFAULT (1),
        CONSTRAINT UQ_VehicleSources_Name UNIQUE (Name)
    );
END
GO

IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicles
    (
        Id                 INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_Vehicles PRIMARY KEY,
        Vin                NVARCHAR(17)   NOT NULL,
        Year               INT            NULL,
        Make               NVARCHAR(100)  NULL,
        Model              NVARCHAR(100)  NULL,
        TransmissionType   INT            NULL, -- Automatic / Manual
        DriveType          INT            NULL, -- FWD / RWD / AWD / 4x4 / 4x2
        Mileage            INT            NULL,
        Observations       NVARCHAR(2000) NULL,
        VehicleSourceId    INT            NOT NULL,
        AcquiredAt         DATETIME2(7)   NOT NULL,
        AcquiredByUserId   INT            NOT NULL,
        PalletId           INT            NULL, -- assigned later by inventory staff
        CreatedAtUtc       DATETIME2(7)   NOT NULL CONSTRAINT DF_Vehicles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc       DATETIME2(7)   NULL,
        CONSTRAINT UQ_Vehicles_Vin UNIQUE (Vin),
        CONSTRAINT FK_Vehicles_VehicleSources FOREIGN KEY (VehicleSourceId) REFERENCES dbo.VehicleSources (Id),
        CONSTRAINT FK_Vehicles_Users FOREIGN KEY (AcquiredByUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_Vehicles_Pallets FOREIGN KEY (PalletId) REFERENCES dbo.Pallets (Id)
    );
END
GO

/*------------------------------------------------------------------------------
  Movement history / audit log
  MovementType: 1 Relocated, 2 Sold, 3 Assigned, 4 Acquired
  Exactly one of InventoryItemId or VehicleId must be set.
------------------------------------------------------------------------------*/
IF OBJECT_ID(N'dbo.InventoryMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryMovements
    (
        Id               BIGINT         NOT NULL IDENTITY(1,1) CONSTRAINT PK_InventoryMovements PRIMARY KEY,
        MovementType     INT            NOT NULL,
        InventoryItemId  INT            NULL,
        VehicleId        INT            NULL,
        FromPalletId     INT            NULL,
        ToPalletId       INT            NULL,
        UserId           INT            NOT NULL,
        Notes            NVARCHAR(1000) NULL,
        MovedAtUtc       DATETIME2(7)   NOT NULL CONSTRAINT DF_InventoryMovements_MovedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_InventoryMovements_InventoryItems FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems (Id),
        CONSTRAINT FK_InventoryMovements_Vehicles FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
        CONSTRAINT FK_InventoryMovements_FromPallet FOREIGN KEY (FromPalletId) REFERENCES dbo.Pallets (Id),
        CONSTRAINT FK_InventoryMovements_ToPallet FOREIGN KEY (ToPalletId) REFERENCES dbo.Pallets (Id),
        CONSTRAINT FK_InventoryMovements_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id),
        CONSTRAINT CK_InventoryMovements_Subject CHECK (
            (InventoryItemId IS NOT NULL AND VehicleId IS NULL)
            OR (InventoryItemId IS NULL AND VehicleId IS NOT NULL)
        )
    );

    CREATE INDEX IX_InventoryMovements_MovedAtUtc ON dbo.InventoryMovements (MovedAtUtc DESC);
    CREATE INDEX IX_InventoryMovements_FromPalletId ON dbo.InventoryMovements (FromPalletId);
    CREATE INDEX IX_InventoryMovements_ToPalletId ON dbo.InventoryMovements (ToPalletId);
    CREATE INDEX IX_InventoryMovements_UserId ON dbo.InventoryMovements (UserId);
END
GO

/*------------------------------------------------------------------------------
  Seed data — roles, sources, default vehicle zones (Zona A / Zona B)
------------------------------------------------------------------------------*/
MERGE dbo.Roles AS target
USING (VALUES
    (1, 1, N'Inventory Viewer',  N'View inventory and locations only.'),
    (2, 2, N'Inventory Editor',  N'Add, edit, remove inventory; assign vehicle locations.'),
    (3, 3, N'Zone Manager',      N'Create and edit zones, rows, and pallets for parts and vehicles.'),
    (4, 4, N'Vehicle Acquirer',  N'Register newly acquired vehicles without assigning yard location.'),
    (5, 5, N'System Admin',      N'God user: create users and assign access levels / functions.')
) AS source (Id, Code, Name, Description)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, Description)
    VALUES (source.Id, source.Code, source.Name, source.Description);
GO

SET IDENTITY_INSERT dbo.VehicleSources ON;
MERGE dbo.VehicleSources AS target
USING (VALUES
    (1, N'Wheelzy',  1),
    (2, N'Peddle',   1),
    (3, N'Facebook', 1),
    (4, N'Other',    1)
) AS source (Id, Name, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, IsActive)
    VALUES (source.Id, source.Name, source.IsActive);
SET IDENTITY_INSERT dbo.VehicleSources OFF;
GO

SET IDENTITY_INSERT dbo.Zones ON;
MERGE dbo.Zones AS target
USING (VALUES
    (1, N'Zona A', 2, 2, 2, 1, CAST(N'2026-01-01T00:00:00' AS DATETIME2)),
    (2, N'Zona B', 2, 2, 2, 1, CAST(N'2026-01-01T00:00:00' AS DATETIME2))
) AS source (Id, Name, Purpose, RowAmount, PalletsPerRow, IsActive, CreatedAtUtc)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Purpose, RowAmount, PalletsPerRow, IsActive, CreatedAtUtc)
    VALUES (source.Id, source.Name, source.Purpose, source.RowAmount, source.PalletsPerRow, source.IsActive, source.CreatedAtUtc);
SET IDENTITY_INSERT dbo.Zones OFF;
GO

SET IDENTITY_INSERT dbo.Rows ON;
MERGE dbo.Rows AS target
USING (VALUES
    (1, 1, 1, N'A-R1', 1),
    (2, 1, 2, N'A-R2', 1),
    (3, 2, 1, N'B-R1', 1),
    (4, 2, 2, N'B-R2', 1)
) AS source (Id, ZoneId, RowNumber, Label, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, ZoneId, RowNumber, Label, IsActive)
    VALUES (source.Id, source.ZoneId, source.RowNumber, source.Label, source.IsActive);
SET IDENTITY_INSERT dbo.Rows OFF;
GO

SET IDENTITY_INSERT dbo.Pallets ON;
MERGE dbo.Pallets AS target
USING (VALUES
    (1, 1, 1, N'A-R1-P1', 1),
    (2, 1, 2, N'A-R1-P2', 1),
    (3, 2, 1, N'A-R2-P1', 1),
    (4, 2, 2, N'A-R2-P2', 1),
    (5, 3, 1, N'B-R1-P1', 1),
    (6, 3, 2, N'B-R1-P2', 1),
    (7, 4, 1, N'B-R2-P1', 1),
    (8, 4, 2, N'B-R2-P2', 1)
) AS source (Id, RowId, PalletNumber, Label, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, RowId, PalletNumber, Label, IsActive)
    VALUES (source.Id, source.RowId, source.PalletNumber, source.Label, source.IsActive);
SET IDENTITY_INSERT dbo.Pallets OFF;
GO

PRINT N'RSY Yard Inventory schema applied successfully.';
GO
