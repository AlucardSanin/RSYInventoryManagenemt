/*
  005_ImagesAuditAndCreatedBy.sql
  - ImageRelativePath on parts & vehicles (path under wwwroot/uploads, OS-agnostic)
  - CreatedByUserId on inventory items (who checked the part in)
  - DeletedAtUtc for future soft-delete (Sold still uses Status today)
  - AuditEvents for admin activity log (zones + other ops)
*/
USE [RSYYardInventory];
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'ImageRelativePath') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD ImageRelativePath NVARCHAR(400) NULL;
END
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD CreatedByUserId INT NULL;
    ALTER TABLE dbo.InventoryItems WITH CHECK
        ADD CONSTRAINT FK_InventoryItems_CreatedByUser
        FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id);
    CREATE INDEX IX_InventoryItems_CreatedByUserId ON dbo.InventoryItems (CreatedByUserId);
END
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'DeletedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD DeletedAtUtc DATETIME2(7) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'ImageRelativePath') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD ImageRelativePath NVARCHAR(400) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'DeletedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD DeletedAtUtc DATETIME2(7) NULL;
END
GO

IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEvents
    (
        Id            BIGINT         NOT NULL IDENTITY(1,1) CONSTRAINT PK_AuditEvents PRIMARY KEY,
        EventType     NVARCHAR(80)   NOT NULL,
        EntityType    NVARCHAR(80)   NOT NULL,
        EntityId      INT            NULL,
        UserId        INT            NOT NULL,
        Summary       NVARCHAR(500)  NOT NULL,
        Details       NVARCHAR(2000) NULL,
        CreatedAtUtc  DATETIME2(7)   NOT NULL CONSTRAINT DF_AuditEvents_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_AuditEvents_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_AuditEvents_CreatedAtUtc ON dbo.AuditEvents (CreatedAtUtc DESC);
    CREATE INDEX IX_AuditEvents_UserId ON dbo.AuditEvents (UserId);
    CREATE INDEX IX_AuditEvents_EventType ON dbo.AuditEvents (EventType);
    CREATE INDEX IX_AuditEvents_Entity ON dbo.AuditEvents (EntityType, EntityId);
END
GO

PRINT '005_ImagesAuditAndCreatedBy applied.';
GO
