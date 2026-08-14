/*
  015_DriverPickupSchedule.sql
  - Role: Driver (chofer)
  - Users: PreferredLanguage, DriverAccessToken
  - ScheduledVehiclePickups + images
  - Seed driver users from distinct Vehicles.PickupDriver values
*/
/*
  Roles.Id is NOT identity (see 001 / 003). Insert Id explicitly via MERGE.
  For production, change USE to [RSYYardInventory_prod] before running.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [RSYYardInventory];
GO

MERGE dbo.Roles AS target
USING (VALUES
    (6, 6, N'Driver', N'Chofer: agenda de recolección por enlace, sin login de yarda.')
) AS source (Id, Code, Name, Description)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, Description)
    VALUES (source.Id, source.Code, source.Name, source.Description)
WHEN MATCHED THEN
    UPDATE SET Code = source.Code, Name = source.Name, Description = source.Description;
GO

IF COL_LENGTH('dbo.Users', 'PreferredLanguage') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD PreferredLanguage NVARCHAR(5) NOT NULL
            CONSTRAINT DF_Users_PreferredLanguage DEFAULT (N'es');
END
GO

IF COL_LENGTH('dbo.Users', 'DriverAccessToken') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD DriverAccessToken UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_Users_DriverAccessToken' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE UNIQUE INDEX UQ_Users_DriverAccessToken
        ON dbo.Users (DriverAccessToken)
        WHERE DriverAccessToken IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.ScheduledVehiclePickups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledVehiclePickups
    (
        Id                   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduledVehiclePickups PRIMARY KEY,
        Vin                  NVARCHAR(17)      NOT NULL,
        [Year]               INT               NULL,
        Make                 NVARCHAR(100)     NULL,
        Model                NVARCHAR(100)     NULL,
        TransmissionType     INT               NULL,
        DriveType            INT               NULL,
        Mileage              INT               NULL,
        PurchasePrice        DECIMAL(12,2)     NULL,
        Observations         NVARCHAR(2000)    NULL,
        VehicleSourceId      INT               NOT NULL,
        ScheduledPickupDate  DATE              NOT NULL,
        ScheduledPickupWindow NVARCHAR(80)     NULL, -- e.g. "9 - 12pm"
        PickupAddress        NVARCHAR(500)     NULL,
        SellerName           NVARCHAR(150)     NULL,
        SellerPhone          NVARCHAR(40)      NULL,
        SellerEmail          NVARCHAR(256)     NULL,
        PaymentMethod        NVARCHAR(80)      NULL,
        AssignedDriverUserId INT               NULL,
        -- 0 = Pending, 1 = PickedUp (awaiting noon promotion), 2 = Promoted to Vehicles
        Status               TINYINT           NOT NULL
            CONSTRAINT DF_ScheduledVehiclePickups_Status DEFAULT (0),
        PickedUpAtUtc        DATETIME2         NULL,
        PromotedVehicleId    INT               NULL,
        PromotedAtUtc        DATETIME2         NULL,
        CreatedByUserId      INT               NOT NULL,
        ImageRelativePath    NVARCHAR(400)     NULL,
        CreatedAtUtc         DATETIME2         NOT NULL
            CONSTRAINT DF_ScheduledVehiclePickups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc         DATETIME2         NULL,
        CONSTRAINT FK_ScheduledVehiclePickups_VehicleSources
            FOREIGN KEY (VehicleSourceId) REFERENCES dbo.VehicleSources (Id),
        CONSTRAINT FK_ScheduledVehiclePickups_AssignedDriver
            FOREIGN KEY (AssignedDriverUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_ScheduledVehiclePickups_CreatedBy
            FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_ScheduledVehiclePickups_PromotedVehicle
            FOREIGN KEY (PromotedVehicleId) REFERENCES dbo.Vehicles (Id),
        CONSTRAINT CK_ScheduledVehiclePickups_Status
            CHECK (Status IN (0, 1, 2))
    );

    CREATE INDEX IX_ScheduledVehiclePickups_Driver_Status_Date
        ON dbo.ScheduledVehiclePickups (AssignedDriverUserId, Status, ScheduledPickupDate DESC);

    CREATE INDEX IX_ScheduledVehiclePickups_Status_PickedUp
        ON dbo.ScheduledVehiclePickups (Status, PickedUpAtUtc)
        WHERE Status = 1;
END
GO

IF OBJECT_ID(N'dbo.ScheduledVehiclePickupImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScheduledVehiclePickupImages
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScheduledVehiclePickupImages PRIMARY KEY,
        ScheduledId   INT               NOT NULL,
        RelativePath  NVARCHAR(400)     NOT NULL,
        SortOrder     INT               NOT NULL
            CONSTRAINT DF_ScheduledVehiclePickupImages_SortOrder DEFAULT (0),
        CreatedAtUtc  DATETIME2         NOT NULL
            CONSTRAINT DF_ScheduledVehiclePickupImages_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ScheduledVehiclePickupImages_Schedule
            FOREIGN KEY (ScheduledId) REFERENCES dbo.ScheduledVehiclePickups (Id)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_ScheduledVehiclePickupImages_ScheduledId_SortOrder
        ON dbo.ScheduledVehiclePickupImages (ScheduledId, SortOrder, Id);
END
GO

/* Seed driver users from existing free-text PickupDriver values (idempotent). */
DECLARE @DriverRoleId INT = (SELECT Id FROM dbo.Roles WHERE Code = 6);

IF @DriverRoleId IS NULL
    THROW 50001, N'Role Driver (Code=6) was not created. Re-run the Roles MERGE at the top of this script.', 1;

;WITH DistinctDrivers AS (
    SELECT DISTINCT LTRIM(RTRIM(v.PickupDriver)) AS DisplayName
    FROM dbo.Vehicles v
    WHERE v.PickupDriver IS NOT NULL
      AND LTRIM(RTRIM(v.PickupDriver)) <> N''
)
INSERT INTO dbo.Users (UserName, DisplayName, Email, PasswordHash, IsActive, PreferredLanguage, DriverAccessToken, CreatedAtUtc)
SELECT
    N'driver.' + LOWER(REPLACE(REPLACE(REPLACE(d.DisplayName, N' ', N'.'), N'''', N''), N'"', N'')),
    d.DisplayName,
    NULL,
    NULL,
    1,
    N'es',
    NEWID(),
    SYSUTCDATETIME()
FROM DistinctDrivers d
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Users u
    WHERE u.DisplayName = d.DisplayName
       OR u.UserName = N'driver.' + LOWER(REPLACE(REPLACE(REPLACE(d.DisplayName, N' ', N'.'), N'''', N''), N'"', N''))
);

INSERT INTO dbo.UserRoles (UserId, RoleId)
SELECT u.Id, @DriverRoleId
FROM dbo.Users u
WHERE u.UserName LIKE N'driver.%'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.UserRoles ur
      WHERE ur.UserId = u.Id AND ur.RoleId = @DriverRoleId
  );

UPDATE dbo.Users
SET DriverAccessToken = NEWID()
WHERE UserName LIKE N'driver.%'
  AND DriverAccessToken IS NULL;

PRINT N'015_DriverPickupSchedule applied.';
GO
