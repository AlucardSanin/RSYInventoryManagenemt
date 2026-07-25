/*
  014_SaulMotorsSourceAndTemplate.sql
  - Vehicle source "Sauls Motor Co"
  - Invoice template "Sauls Motor Co" (shared invoice number sequence)
  - Optional DocuSealTemplateId per branding template
  - Logo path for Sauls Motor Co / RSY

  Safe to re-run.
*/
USE [RSYYardInventory];
GO

/* ---- DocuSeal template id on invoice branding ---- */
IF COL_LENGTH(N'dbo.InvoiceTemplates', N'DocuSealTemplateId') IS NULL
    ALTER TABLE dbo.InvoiceTemplates ADD DocuSealTemplateId INT NULL;
GO

IF COL_LENGTH(N'dbo.InvoiceTemplates', N'MatchedSourceName') IS NULL
    ALTER TABLE dbo.InvoiceTemplates ADD MatchedSourceName NVARCHAR(100) NULL;
GO

/* ---- Vehicle source (rename legacy "Saul Motors" if present) ---- */
IF EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'Saul Motors')
    AND NOT EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'Sauls Motor Co')
BEGIN
    UPDATE dbo.VehicleSources
    SET Name = N'Sauls Motor Co'
    WHERE Name = N'Saul Motors';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'Sauls Motor Co')
BEGIN
    INSERT INTO dbo.VehicleSources (Name, IsActive)
    VALUES (N'Sauls Motor Co', 1);
END
ELSE
BEGIN
    UPDATE dbo.VehicleSources
    SET IsActive = 1
    WHERE Name = N'Sauls Motor Co';
END
GO

/* ---- Ensure RSY logo path + matched source ---- */
UPDATE dbo.InvoiceTemplates
SET LogoRelativePath = COALESCE(LogoRelativePath, N'/invoice/rsy-invoice-logo.png'),
    MatchedSourceName = COALESCE(MatchedSourceName, NULL),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE Name = N'Rodriguez Salvage Yard';
GO

/* ---- Rename legacy template name if needed ---- */
IF EXISTS (SELECT 1 FROM dbo.InvoiceTemplates WHERE Name = N'Saul Motors')
    AND NOT EXISTS (SELECT 1 FROM dbo.InvoiceTemplates WHERE Name = N'Sauls Motor Co')
BEGIN
    UPDATE dbo.InvoiceTemplates
    SET Name = N'Sauls Motor Co'
    WHERE Name = N'Saul Motors';
END
GO

/* ---- Sauls Motor Co invoice template ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceTemplates WHERE Name = N'Sauls Motor Co')
BEGIN
    INSERT INTO dbo.InvoiceTemplates
    (
        Name, BuyerCompanyName, BuyerAuthorizedName, AddressLine1, CityStateZip, Email, Phone,
        TemplateKind, LogoRelativePath, MatchedSourceName, IsDefault, IsActive
    )
    VALUES
    (
        N'Sauls Motor Co',
        N'Sauls Motor Co',
        N'SAULS MOTOR CO',
        N'304 Fareway Dr',
        N'Smithfield, NC, 27577',
        N'sautomotive93@gmail.com',
        NULL,
        0,
        N'/invoice/saul-motors-logo.png',
        N'Sauls Motor Co',
        0,
        1
    );
END
ELSE
BEGIN
    UPDATE dbo.InvoiceTemplates
    SET BuyerCompanyName = N'Sauls Motor Co',
        BuyerAuthorizedName = N'SAULS MOTOR CO',
        AddressLine1 = N'304 Fareway Dr',
        CityStateZip = N'Smithfield, NC, 27577',
        Email = N'sautomotive93@gmail.com',
        LogoRelativePath = N'/invoice/saul-motors-logo.png',
        MatchedSourceName = N'Sauls Motor Co',
        IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Name = N'Sauls Motor Co';
END
GO

PRINT N'014_SaulMotorsSourceAndTemplate applied.';
GO
