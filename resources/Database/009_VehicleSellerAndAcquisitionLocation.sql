/*
  009_VehicleSellerAndAcquisitionLocation.sql
  - Rename source "Pebble" → "Peddle"
  - Acquisition location (where the vehicle was obtained)
  - Seller contact: name, phone, email
*/
USE [RSYYardInventory];
GO

UPDATE dbo.VehicleSources
SET Name = N'Peddle'
WHERE Name = N'Pebble';
GO

IF COL_LENGTH(N'dbo.Vehicles', N'AcquisitionLocation') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD AcquisitionLocation NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SellerName') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD SellerName NVARCHAR(150) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SellerPhone') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD SellerPhone NVARCHAR(40) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SellerEmail') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD SellerEmail NVARCHAR(256) NULL;
END
GO

PRINT '009_VehicleSellerAndAcquisitionLocation applied.';
GO
