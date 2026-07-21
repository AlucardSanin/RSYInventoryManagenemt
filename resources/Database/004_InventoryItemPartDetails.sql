/*
================================================================================
 004 — Inventory item details: donor VIN, engine displacement, transmission attrs
================================================================================
 Apply after 001–003. Safe to re-run.
================================================================================
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'SourceVin') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD SourceVin NVARCHAR(17) NULL;
END
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'DisplacementLiters') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD DisplacementLiters DECIMAL(4, 2) NULL;
END
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'TransmissionType') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD TransmissionType INT NULL; -- 1 Automatic, 2 Manual
END
GO

IF COL_LENGTH(N'dbo.InventoryItems', N'DriveType') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems ADD DriveType INT NULL; -- FWD/RWD/AWD/4x4/4x2
END
GO

PRINT N'004_InventoryItemPartDetails applied.';
GO
