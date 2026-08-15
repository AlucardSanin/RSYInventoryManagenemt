/*
  022_VehiclePriceHistory.sql
  Historial de cambios de precio de compra para vehículos adquiridos
  y para agendas de recolección (ScheduledVehiclePickups).
*/
USE [RSYYardInventory];
GO

IF OBJECT_ID(N'dbo.VehiclePriceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VehiclePriceHistory
    (
        Id                         BIGINT         NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_VehiclePriceHistory PRIMARY KEY,
        VehicleId                  INT            NULL,
        ScheduledVehiclePickupId   INT            NULL,
        OldPrice                   DECIMAL(12, 2) NULL,
        NewPrice                   DECIMAL(12, 2) NULL,
        ChangedByUserId            INT            NOT NULL,
        Notes                      NVARCHAR(500)  NULL,
        ChangedAtUtc               DATETIME2(7)   NOT NULL
            CONSTRAINT DF_VehiclePriceHistory_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_VehiclePriceHistory_Subject CHECK (
            (VehicleId IS NOT NULL AND ScheduledVehiclePickupId IS NULL)
            OR (VehicleId IS NULL AND ScheduledVehiclePickupId IS NOT NULL)
        ),
        CONSTRAINT FK_VehiclePriceHistory_Vehicles
            FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicles (Id),
        CONSTRAINT FK_VehiclePriceHistory_ScheduledVehiclePickups
            FOREIGN KEY (ScheduledVehiclePickupId) REFERENCES dbo.ScheduledVehiclePickups (Id),
        CONSTRAINT FK_VehiclePriceHistory_Users
            FOREIGN KEY (ChangedByUserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_VehiclePriceHistory_VehicleId_ChangedAt
        ON dbo.VehiclePriceHistory (VehicleId, ChangedAtUtc DESC)
        WHERE VehicleId IS NOT NULL;

    CREATE INDEX IX_VehiclePriceHistory_ScheduleId_ChangedAt
        ON dbo.VehiclePriceHistory (ScheduledVehiclePickupId, ChangedAtUtc DESC)
        WHERE ScheduledVehiclePickupId IS NOT NULL;

    CREATE INDEX IX_VehiclePriceHistory_ChangedByUserId
        ON dbo.VehiclePriceHistory (ChangedByUserId);
END
GO

PRINT N'022_VehiclePriceHistory applied.';
GO
