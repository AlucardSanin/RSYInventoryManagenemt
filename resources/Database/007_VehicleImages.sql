/*
  007_VehicleImages.sql
  - Multiple photos per acquired vehicle (VehicleImages)
  - Backfills existing Vehicles.ImageRelativePath into VehicleImages
*/
USE [RSYYardInventory];
GO

IF OBJECT_ID(N'dbo.VehicleImages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehicleImages
    (
        Id            INT            NOT NULL IDENTITY(1,1) CONSTRAINT PK_VehicleImages PRIMARY KEY,
        VehicleId     INT            NOT NULL,
        RelativePath  NVARCHAR(400)  NOT NULL,
        SortOrder     INT            NOT NULL CONSTRAINT DF_VehicleImages_SortOrder DEFAULT (0),
        CreatedAtUtc  DATETIME2(7)   NOT NULL CONSTRAINT DF_VehicleImages_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_VehicleImages_Vehicles
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_VehicleImages_VehicleId_SortOrder
        ON dbo.VehicleImages (VehicleId, SortOrder, Id);
END
GO

-- Preserve legacy single-path photos as the first gallery image.
IF OBJECT_ID(N'dbo.VehicleImages', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Vehicles', N'ImageRelativePath') IS NOT NULL
BEGIN
    INSERT INTO dbo.VehicleImages (VehicleId, RelativePath, SortOrder, CreatedAtUtc)
    SELECT v.Id, v.ImageRelativePath, 0, SYSUTCDATETIME()
    FROM dbo.Vehicles v
    WHERE v.ImageRelativePath IS NOT NULL
      AND LTRIM(RTRIM(v.ImageRelativePath)) <> N''
      AND NOT EXISTS (
          SELECT 1
          FROM dbo.VehicleImages vi
          WHERE vi.VehicleId = v.Id
            AND vi.RelativePath = v.ImageRelativePath
      );
END
GO

PRINT '007_VehicleImages applied.';
GO
