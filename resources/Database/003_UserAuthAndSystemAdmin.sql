/*
================================================================================
 003 — Auth (PasswordHash) + System Admin (god) role for demo
================================================================================
 Apply after 001 / 002. Safe to re-run.
 Demo login: user `demo` / password `demo`
================================================================================
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.Users', N'PasswordHash') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD PasswordHash NVARCHAR(500) NULL;
END
GO

MERGE dbo.Roles AS target
USING (VALUES
    (5, 5, N'System Admin', N'God user: create users and assign access levels / functions.')
) AS source (Id, Code, Name, Description)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, Description)
    VALUES (source.Id, source.Code, source.Name, source.Description)
WHEN MATCHED THEN
    UPDATE SET Code = source.Code, Name = source.Name, Description = source.Description;
GO

/* ASP.NET Identity PasswordHasher hash for password: demo */
UPDATE dbo.Users
SET PasswordHash = N'AQAAAAIAAYagAAAAENl2oP93QO5LljTb8s+67VWhHBhLl8ccGd7afHpPe3rwgJ/65cbyUNj5lGFaxmS3ew=='
WHERE UserName = N'demo'
  AND (PasswordHash IS NULL OR PasswordHash = N'');
GO

MERGE dbo.UserRoles AS target
USING (VALUES
    (1, 1), (1, 2), (1, 3), (1, 4), (1, 5)
) AS source (UserId, RoleId)
ON target.UserId = source.UserId AND target.RoleId = source.RoleId
WHEN NOT MATCHED THEN
    INSERT (UserId, RoleId) VALUES (source.UserId, source.RoleId);
GO

PRINT N'003_UserAuthAndSystemAdmin applied. Login: demo / demo';
GO
