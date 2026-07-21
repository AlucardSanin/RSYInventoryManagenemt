/*
  006_VehiclePurchasePrice.sql
  Optional purchase price on vehicles.
*/
USE [RSYYardInventory];
GO

IF COL_LENGTH(N'dbo.Vehicles', N'PurchasePrice') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD PurchasePrice DECIMAL(12, 2) NULL;
END
GO

PRINT '006_VehiclePurchasePrice applied.';
GO
