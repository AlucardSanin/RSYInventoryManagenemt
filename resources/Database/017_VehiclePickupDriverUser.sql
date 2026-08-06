/*
  017_VehiclePickupDriverUser.sql
  Link Vehicles.PickupDriver (free text) to registered driver Users.
  - Adds PickupDriverUserId FK
  - Backfills by exact name, known aliases, and unique first-name match

  NOTE: ADD COLUMN and any statement that references the new column must be
  in separate batches (GO). SQL Server compiles each batch before running it.
*/
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'PickupDriverUserId') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD PickupDriverUserId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_Vehicles_PickupDriverUser'
      AND parent_object_id = OBJECT_ID(N'dbo.Vehicles')
)
BEGIN
    ALTER TABLE dbo.Vehicles WITH CHECK
    ADD CONSTRAINT FK_Vehicles_PickupDriverUser
        FOREIGN KEY (PickupDriverUserId) REFERENCES dbo.Users (Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Vehicles_PickupDriverUserId'
      AND object_id = OBJECT_ID(N'dbo.Vehicles')
)
BEGIN
    CREATE INDEX IX_Vehicles_PickupDriverUserId
        ON dbo.Vehicles (PickupDriverUserId)
        WHERE PickupDriverUserId IS NOT NULL;
END
GO

/* 1) Exact display name */
;WITH Drivers AS (
    SELECT u.Id, u.DisplayName,
           LOWER(LTRIM(RTRIM(u.DisplayName))) AS NormName
    FROM dbo.Users u
    INNER JOIN dbo.UserRoles ur ON ur.UserId = u.Id
    INNER JOIN dbo.Roles r ON r.Id = ur.RoleId AND r.Code = 6
    WHERE u.IsActive = 1
),
VehicleText AS (
    SELECT
        v.Id AS VehicleId,
        LTRIM(RTRIM(v.PickupDriver)) AS RawName,
        LOWER(LTRIM(RTRIM(v.PickupDriver))) AS NormName
    FROM dbo.Vehicles v
    WHERE v.PickupDriverUserId IS NULL
      AND v.PickupDriver IS NOT NULL
      AND LTRIM(RTRIM(v.PickupDriver)) <> N''
      AND v.DeletedAtUtc IS NULL
)
UPDATE v
SET PickupDriverUserId = d.Id,
    PickupDriver = d.DisplayName
FROM dbo.Vehicles v
INNER JOIN VehicleText t ON t.VehicleId = v.Id
INNER JOIN Drivers d ON d.NormName = t.NormName
WHERE v.PickupDriverUserId IS NULL;
GO

/* 2) Known aliases */
;WITH Drivers AS (
    SELECT u.Id, u.DisplayName,
           LOWER(LTRIM(RTRIM(u.DisplayName))) AS NormName
    FROM dbo.Users u
    INNER JOIN dbo.UserRoles ur ON ur.UserId = u.Id
    INNER JOIN dbo.Roles r ON r.Id = ur.RoleId AND r.Code = 6
    WHERE u.IsActive = 1
),
Aliases AS (
    SELECT * FROM (VALUES
        (N'ulyces', N'ulysses'),
        (N'ulyses', N'ulysses'),
        (N'ulysse', N'ulysses'),
        (N'david', N'david johnson'),
        (N'bryant', N'brayant'),
        (N'bryan', N'brayant')
    ) a(AliasNorm, TargetNorm)
)
UPDATE v
SET PickupDriverUserId = d.Id,
    PickupDriver = d.DisplayName
FROM dbo.Vehicles v
INNER JOIN (
    SELECT
        v2.Id AS VehicleId,
        LOWER(LTRIM(RTRIM(v2.PickupDriver))) AS NormName
    FROM dbo.Vehicles v2
    WHERE v2.PickupDriverUserId IS NULL
      AND v2.PickupDriver IS NOT NULL
      AND LTRIM(RTRIM(v2.PickupDriver)) <> N''
) t ON t.VehicleId = v.Id
INNER JOIN Aliases a ON a.AliasNorm = t.NormName
                      OR a.AliasNorm = LEFT(t.NormName, CHARINDEX(' ', t.NormName + ' ') - 1)
INNER JOIN Drivers d ON d.NormName = a.TargetNorm
                     OR d.NormName LIKE a.TargetNorm + N'%'
WHERE v.PickupDriverUserId IS NULL
  AND (
      SELECT COUNT(*) FROM Drivers d2
      WHERE d2.NormName = a.TargetNorm OR d2.NormName LIKE a.TargetNorm + N'%'
  ) = 1;
GO

/* 3) Unique first-name match (e.g. "Miguel" → only one driver named Miguel…) */
;WITH Drivers AS (
    SELECT u.Id, u.DisplayName,
           LOWER(LTRIM(RTRIM(u.DisplayName))) AS NormName,
           LOWER(LEFT(LTRIM(RTRIM(u.DisplayName)),
                 CHARINDEX(' ', LTRIM(RTRIM(u.DisplayName)) + ' ') - 1)) AS FirstName
    FROM dbo.Users u
    INNER JOIN dbo.UserRoles ur ON ur.UserId = u.Id
    INNER JOIN dbo.Roles r ON r.Id = ur.RoleId AND r.Code = 6
    WHERE u.IsActive = 1
),
UniqueFirst AS (
    SELECT FirstName
    FROM Drivers
    GROUP BY FirstName
    HAVING COUNT(*) = 1 AND LEN(FirstName) >= 3
),
Targets AS (
    SELECT d.Id, d.DisplayName, d.FirstName
    FROM Drivers d
    INNER JOIN UniqueFirst u ON u.FirstName = d.FirstName
)
UPDATE v
SET PickupDriverUserId = t.Id,
    PickupDriver = t.DisplayName
FROM dbo.Vehicles v
INNER JOIN Targets t ON t.FirstName = LOWER(LEFT(LTRIM(RTRIM(v.PickupDriver)),
    CHARINDEX(' ', LTRIM(RTRIM(v.PickupDriver)) + ' ') - 1))
WHERE v.PickupDriverUserId IS NULL
  AND v.PickupDriver IS NOT NULL
  AND LTRIM(RTRIM(v.PickupDriver)) <> N'';
GO

PRINT N'017_VehiclePickupDriverUser applied.';
GO
