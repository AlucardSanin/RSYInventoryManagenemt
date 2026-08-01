/*
  012_InvoiceTemplates.sql
  Receipt / purchase-acknowledgement templates (buyer company branding).
  Seed: Rodriguez Salvage Yard (default).

  Safe to re-run.
*/
USE [RSYYardInventory];
GO

IF OBJECT_ID(N'dbo.InvoiceTemplates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceTemplates
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InvoiceTemplates PRIMARY KEY,
        Name NVARCHAR(120) NOT NULL,
        BuyerCompanyName NVARCHAR(200) NOT NULL,
        BuyerAuthorizedName NVARCHAR(200) NOT NULL,
        AddressLine1 NVARCHAR(200) NOT NULL,
        CityStateZip NVARCHAR(120) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Phone NVARCHAR(40) NULL,
        -- 0 = built-in QuestPDF layout with branding; 1 = uploaded PDF (future)
        TemplateKind INT NOT NULL CONSTRAINT DF_InvoiceTemplates_TemplateKind DEFAULT (0),
        PdfRelativePath NVARCHAR(400) NULL,
        LogoRelativePath NVARCHAR(400) NULL,
        IsDefault BIT NOT NULL CONSTRAINT DF_InvoiceTemplates_IsDefault DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_InvoiceTemplates_IsActive DEFAULT (1),
        CreatedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_InvoiceTemplates_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(7) NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceTemplates WHERE Name = N'Rodriguez Salvage Yard')
BEGIN
    INSERT INTO dbo.InvoiceTemplates
    (
        Name, BuyerCompanyName, BuyerAuthorizedName, AddressLine1, CityStateZip, Email, Phone,
        TemplateKind, IsDefault, IsActive
    )
    VALUES
    (
        N'Rodriguez Salvage Yard',
        N'Rodriguez Salvage Yard, CORP',
        N'RODRIGUEZ SALVAGE YARD',
        N'4417 US-70 BUS',
        N'Clayton, NC, 27520',
        N'rodriguezyardclayton@gmail.com',
        NULL,
        0,
        1,
        1
    );
END
GO

PRINT N'012_InvoiceTemplates applied.';
GO
