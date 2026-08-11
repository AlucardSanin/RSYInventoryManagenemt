/*
  018_SpecialDrivers.sql
  - Roles: RecycleDriver (7), ConstructorDriver (8), ScrapLogisticsAdmin (9)
  - RecycleLoads + RecycleLoadDocuments (BOL / NUCOR / SCALE)
  - ConstructorActivityLogs (single photo activity)
*/
SET NOCOUNT ON;
GO

MERGE dbo.Roles AS target
USING (VALUES
    (7, 7, N'RecycleDriver', N'Chofer de reciclaje: registra cargas de chatarra con fotos BOL/NUCOR/SCALE por enlace.'),
    (8, 8, N'ConstructorDriver', N'Chofer de constructora: registra actividad con una foto por enlace.'),
    (9, 9, N'ScrapLogisticsAdmin', N'Administra agenda de reciclaje y actividad de constructora.')
) AS source (Id, Code, Name, Description)
ON target.Id = source.Id
WHEN NOT MATCHED THEN
    INSERT (Id, Code, Name, Description)
    VALUES (source.Id, source.Code, source.Name, source.Description)
WHEN MATCHED THEN
    UPDATE SET
        Code = source.Code,
        Name = source.Name,
        Description = source.Description;
GO

IF OBJECT_ID(N'dbo.RecycleLoads', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleLoads
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecycleLoads PRIMARY KEY,
        LoadExternalId    NVARCHAR(80)      NOT NULL,
        DriverUserId      INT               NOT NULL,
        RecordedAtUtc     DATETIME2(0)      NOT NULL CONSTRAINT DF_RecycleLoads_RecordedAtUtc DEFAULT (SYSUTCDATETIME()),
        RecordedByUserId  INT               NOT NULL,
        IsVerified        BIT               NOT NULL CONSTRAINT DF_RecycleLoads_IsVerified DEFAULT (0),
        VerifiedAtUtc     DATETIME2(0)      NULL,
        VerifiedByUserId  INT               NULL,
        Notes             NVARCHAR(1000)    NULL,
        CreatedAtUtc      DATETIME2(0)      NOT NULL CONSTRAINT DF_RecycleLoads_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc      DATETIME2(0)      NULL,
        CONSTRAINT FK_RecycleLoads_Driver
            FOREIGN KEY (DriverUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_RecycleLoads_RecordedBy
            FOREIGN KEY (RecordedByUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_RecycleLoads_VerifiedBy
            FOREIGN KEY (VerifiedByUserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_RecycleLoads_DriverUserId_RecordedAtUtc
        ON dbo.RecycleLoads (DriverUserId, RecordedAtUtc DESC);

    CREATE INDEX IX_RecycleLoads_LoadExternalId
        ON dbo.RecycleLoads (LoadExternalId);

    CREATE INDEX IX_RecycleLoads_IsVerified_RecordedAtUtc
        ON dbo.RecycleLoads (IsVerified, RecordedAtUtc DESC);
END
GO

IF OBJECT_ID(N'dbo.RecycleLoadDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleLoadDocuments
    (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecycleLoadDocuments PRIMARY KEY,
        RecycleLoadId  INT               NOT NULL,
        DocumentType   TINYINT           NOT NULL, -- 1=BOL, 2=NUCOR, 3=SCALE
        RelativePath   NVARCHAR(400)     NOT NULL,
        CreatedAtUtc   DATETIME2(0)      NOT NULL CONSTRAINT DF_RecycleLoadDocuments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_RecycleLoadDocuments_Load
            FOREIGN KEY (RecycleLoadId) REFERENCES dbo.RecycleLoads (Id) ON DELETE CASCADE,
        CONSTRAINT UQ_RecycleLoadDocuments_Load_Type
            UNIQUE (RecycleLoadId, DocumentType)
    );

    CREATE INDEX IX_RecycleLoadDocuments_RecycleLoadId
        ON dbo.RecycleLoadDocuments (RecycleLoadId);
END
GO

IF OBJECT_ID(N'dbo.ConstructorActivityLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConstructorActivityLogs
    (
        Id               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ConstructorActivityLogs PRIMARY KEY,
        DriverUserId     INT               NOT NULL,
        RelativePath     NVARCHAR(400)     NOT NULL,
        RecordedAtUtc    DATETIME2(0)      NOT NULL CONSTRAINT DF_ConstructorActivityLogs_RecordedAtUtc DEFAULT (SYSUTCDATETIME()),
        RecordedByUserId INT               NOT NULL,
        Notes            NVARCHAR(1000)    NULL,
        CreatedAtUtc     DATETIME2(0)      NOT NULL CONSTRAINT DF_ConstructorActivityLogs_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ConstructorActivityLogs_Driver
            FOREIGN KEY (DriverUserId) REFERENCES dbo.Users (Id),
        CONSTRAINT FK_ConstructorActivityLogs_RecordedBy
            FOREIGN KEY (RecordedByUserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_ConstructorActivityLogs_DriverUserId_RecordedAtUtc
        ON dbo.ConstructorActivityLogs (DriverUserId, RecordedAtUtc DESC);
END
GO

PRINT N'018_SpecialDrivers applied.';
GO
