/*
================================================================================
 002 — Demo user + Zona Partes (engines/transmissions)
================================================================================
 Apply after 001_InitialSchema.sql. Safe to re-run (MERGE / IDENTITY_INSERT).
================================================================================
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* Demo user (Id = 1) used by DevCurrentUserService for local VS debugging */
SET IDENTITY_INSERT dbo.Users ON;
MERGE dbo.Users AS target
USING (VALUES
    (1, N'demo', N'Demo Admin', N'demo@rsy.local', 1, CAST(N'2026-01-01T00:00:00' AS DATETIME2))
) AS source (Id, UserName, DisplayName, Email, IsActive, CreatedAtUtc)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, UserName, DisplayName, Email, IsActive, CreatedAtUtc)
    VALUES (source.Id, source.UserName, source.DisplayName, source.Email, source.IsActive, source.CreatedAtUtc);
SET IDENTITY_INSERT dbo.Users OFF;
GO

MERGE dbo.UserRoles AS target
USING (VALUES
    (1, 1), (1, 2), (1, 3), (1, 4)
) AS source (UserId, RoleId)
ON target.UserId = source.UserId AND target.RoleId = source.RoleId
WHEN NOT MATCHED THEN
    INSERT (UserId, RoleId) VALUES (source.UserId, source.RoleId);
GO

/* Parts zone for motors / transmissions */
SET IDENTITY_INSERT dbo.Zones ON;
MERGE dbo.Zones AS target
USING (VALUES
    (3, N'Zona Partes', 1, 2, 2, 1, CAST(N'2026-01-01T00:00:00' AS DATETIME2))
) AS source (Id, Name, Purpose, RowAmount, PalletsPerRow, IsActive, CreatedAtUtc)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Purpose, RowAmount, PalletsPerRow, IsActive, CreatedAtUtc)
    VALUES (source.Id, source.Name, source.Purpose, source.RowAmount, source.PalletsPerRow, source.IsActive, source.CreatedAtUtc);
SET IDENTITY_INSERT dbo.Zones OFF;
GO

SET IDENTITY_INSERT dbo.Rows ON;
MERGE dbo.Rows AS target
USING (VALUES
    (5, 3, 1, N'P-R1', 1),
    (6, 3, 2, N'P-R2', 1)
) AS source (Id, ZoneId, RowNumber, Label, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, ZoneId, RowNumber, Label, IsActive)
    VALUES (source.Id, source.ZoneId, source.RowNumber, source.Label, source.IsActive);
SET IDENTITY_INSERT dbo.Rows OFF;
GO

SET IDENTITY_INSERT dbo.Pallets ON;
MERGE dbo.Pallets AS target
USING (VALUES
    (9,  5, 1, N'P-R1-P1', 1),
    (10, 5, 2, N'P-R1-P2', 1),
    (11, 6, 1, N'P-R2-P1', 1),
    (12, 6, 2, N'P-R2-P2', 1)
) AS source (Id, RowId, PalletNumber, Label, IsActive)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, RowId, PalletNumber, Label, IsActive)
    VALUES (source.Id, source.RowId, source.PalletNumber, source.Label, source.IsActive);
SET IDENTITY_INSERT dbo.Pallets OFF;
GO

PRINT N'002_DemoUserAndPartsZone applied.';
GO
