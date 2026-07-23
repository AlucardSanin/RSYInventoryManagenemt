/*
  010_VehiclePickupDriver.sql
  - Driver who collected / picked up the acquired vehicle
*/
USE [RSYYardInventory];
GO

IF COL_LENGTH(N'dbo.Vehicles', N'PickupDriver') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD PickupDriver NVARCHAR(150) NULL;
END
GO

PRINT '010_VehiclePickupDriver applied.';
GO
