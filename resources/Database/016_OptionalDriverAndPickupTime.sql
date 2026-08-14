/*
  016_OptionalDriverAndPickupTime.sql
  - AssignedDriverUserId becomes optional
  - ScheduledPickupWindow: free-text time window with seller (e.g. "9 - 12pm")
  Safe if a previous draft added ScheduledPickupTime as TIME.
*/
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE [RSYYardInventory];
GO

/* Drop mistaken TIME column from an earlier draft of this script (if present). */
IF COL_LENGTH(N'dbo.ScheduledVehiclePickups', N'ScheduledPickupTime') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.ScheduledVehiclePickups')
          AND c.name = N'ScheduledPickupTime'
          AND t.name = N'time')
BEGIN
    ALTER TABLE dbo.ScheduledVehiclePickups DROP COLUMN ScheduledPickupTime;
END
GO

IF COL_LENGTH(N'dbo.ScheduledVehiclePickups', N'ScheduledPickupWindow') IS NULL
BEGIN
    ALTER TABLE dbo.ScheduledVehiclePickups
        ADD ScheduledPickupWindow NVARCHAR(80) NULL;
END
GO

IF COL_LENGTH(N'dbo.ScheduledVehiclePickups', N'PickupAddress') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ScheduledVehiclePickups
        ALTER COLUMN PickupAddress NVARCHAR(500) NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ScheduledVehiclePickups')
      AND name = N'AssignedDriverUserId'
      AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.ScheduledVehiclePickups
        ALTER COLUMN AssignedDriverUserId INT NULL;
END
GO

PRINT N'016_OptionalDriverAndPickupTime applied.';
GO
