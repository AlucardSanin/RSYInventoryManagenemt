/*
  019_RecycleLoadInvoice.sql
  - RecycleLoads: RateUsd, TruckNumber, LoadDate
  - Bill-to companies for weekly recycle invoices
  - Weekly recycle invoices + sequence starting at 20
*/
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'RateUsd') IS NULL
BEGIN
    ALTER TABLE dbo.RecycleLoads ADD RateUsd DECIMAL(12,2) NOT NULL
        CONSTRAINT DF_RecycleLoads_RateUsd DEFAULT (575.00);
END
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'TruckNumber') IS NULL
BEGIN
    ALTER TABLE dbo.RecycleLoads ADD TruckNumber NVARCHAR(40) NULL;
END
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'LoadDate') IS NULL
BEGIN
    ALTER TABLE dbo.RecycleLoads ADD LoadDate DATE NULL;
END
GO

/* Backfill LoadDate from RecordedAtUtc (Eastern calendar day approx via UTC date; app will store explicitly going forward). */
UPDATE dbo.RecycleLoads
SET LoadDate = CAST(RecordedAtUtc AS DATE)
WHERE LoadDate IS NULL;
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'LoadDate') IS NOT NULL
BEGIN
    ALTER TABLE dbo.RecycleLoads ALTER COLUMN LoadDate DATE NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RecycleLoads_LoadDate'
      AND object_id = OBJECT_ID(N'dbo.RecycleLoads')
)
BEGIN
    CREATE INDEX IX_RecycleLoads_LoadDate ON dbo.RecycleLoads (LoadDate DESC);
END
GO

IF OBJECT_ID(N'dbo.RecycleBillToCompanies', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleBillToCompanies
    (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecycleBillToCompanies PRIMARY KEY,
        Alias        NVARCHAR(80)      NOT NULL,
        CompanyName  NVARCHAR(200)     NOT NULL,
        AddressLine  NVARCHAR(300)     NOT NULL,
        ContactLine  NVARCHAR(300)     NULL,
        IsActive     BIT               NOT NULL CONSTRAINT DF_RecycleBillToCompanies_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(0)      NOT NULL CONSTRAINT DF_RecycleBillToCompanies_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(0)      NULL
    );

    CREATE UNIQUE INDEX UQ_RecycleBillToCompanies_Alias
        ON dbo.RecycleBillToCompanies (Alias)
        WHERE IsActive = 1;
END
GO

IF OBJECT_ID(N'dbo.RecycleInvoiceSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleInvoiceSequence
    (
        Id         INT NOT NULL CONSTRAINT PK_RecycleInvoiceSequence PRIMARY KEY,
        NextNumber INT NOT NULL
    );
    INSERT INTO dbo.RecycleInvoiceSequence (Id, NextNumber) VALUES (1, 20);
END
GO

IF OBJECT_ID(N'dbo.RecycleWeeklyInvoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleWeeklyInvoices
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecycleWeeklyInvoices PRIMARY KEY,
        InvoiceNumber     INT               NOT NULL,
        WeekStartDate     DATE              NOT NULL, -- Monday
        WeekEndDate       DATE              NOT NULL, -- Friday
        BillToCompanyId   INT               NOT NULL,
        PdfRelativePath   NVARCHAR(400)     NOT NULL,
        TotalAmountUsd    DECIMAL(12,2)     NOT NULL,
        LoadCount         INT               NOT NULL,
        GeneratedAtUtc    DATETIME2(0)      NOT NULL CONSTRAINT DF_RecycleWeeklyInvoices_GeneratedAtUtc DEFAULT (SYSUTCDATETIME()),
        GeneratedByUserId INT               NOT NULL,
        InvoiceDate       DATE              NOT NULL,
        CONSTRAINT UQ_RecycleWeeklyInvoices_InvoiceNumber UNIQUE (InvoiceNumber),
        CONSTRAINT UQ_RecycleWeeklyInvoices_Week UNIQUE (WeekStartDate),
        CONSTRAINT FK_RecycleWeeklyInvoices_BillTo
            FOREIGN KEY (BillToCompanyId) REFERENCES dbo.RecycleBillToCompanies (Id),
        CONSTRAINT FK_RecycleWeeklyInvoices_GeneratedBy
            FOREIGN KEY (GeneratedByUserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_RecycleWeeklyInvoices_WeekStartDate
        ON dbo.RecycleWeeklyInvoices (WeekStartDate DESC);
END
GO

/* Seed sample BILL TO if empty (RMR from legacy invoice). */
IF NOT EXISTS (SELECT 1 FROM dbo.RecycleBillToCompanies)
BEGIN
    INSERT INTO dbo.RecycleBillToCompanies (Alias, CompanyName, AddressLine, ContactLine, IsActive, CreatedAtUtc)
    VALUES (
        N'RMR',
        N'RMR',
        N'816 Halifax Rd, Rocky Mount, NC 27803',
        N'tcarroll@rmrnc.com - 252-443-1521',
        1,
        SYSUTCDATETIME()
    );
END
GO

PRINT N'019_RecycleLoadInvoice applied.';
GO
