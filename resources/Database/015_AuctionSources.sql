/*
  015_AuctionSources.sql
  - Copart - Subastas
  - IAAI - Subastas

  Auction purchases have no private seller and do not use purchase receipts.
  Safe to re-run.
*/
USE [RSYYardInventory];
GO

IF NOT EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'Copart - Subastas')
BEGIN
    INSERT INTO dbo.VehicleSources (Name, IsActive)
    VALUES (N'Copart - Subastas', 1);
END
ELSE
BEGIN
    UPDATE dbo.VehicleSources SET IsActive = 1 WHERE Name = N'Copart - Subastas';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'IAAI - Subastas')
BEGIN
    INSERT INTO dbo.VehicleSources (Name, IsActive)
    VALUES (N'IAAI - Subastas', 1);
END
ELSE
BEGIN
    UPDATE dbo.VehicleSources SET IsActive = 1 WHERE Name = N'IAAI - Subastas';
END
GO

PRINT N'015_AuctionSources applied.';
GO
