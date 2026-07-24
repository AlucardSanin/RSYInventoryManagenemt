/*
  014_SaulMotorsSourceAndTemplate.sql
  - Vehicle source "Saul Motors"
  - Invoice template "Saul Motors" (shared invoice number sequence)
  - Optional DocuSealTemplateId per branding template
  - Logo path for Saul Motors / RSY

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

/* ---- Vehicle source ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.VehicleSources WHERE Name = N'Saul Motors')
BEGIN
    INSERT INTO dbo.VehicleSources (Name, IsActive)
    VALUES (N'Saul Motors', 1);
END
GO

/* ---- Ensure RSY logo path + matched source ---- */
UPDATE dbo.InvoiceTemplates
SET LogoRelativePath = COALESCE(LogoRelativePath, N'/invoice/rsy-invoice-logo.png'),
    MatchedSourceName = COALESCE(MatchedSourceName, NULL),
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE Name = N'Rodriguez Salvage Yard';
GO

/* ---- Saul Motors invoice template ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.InvoiceTemplates WHERE Name = N'Saul Motors')
BEGIN
    INSERT INTO dbo.InvoiceTemplates
    (
        Name, BuyerCompanyName, BuyerAuthorizedName, AddressLine1, CityStateZip, Email, Phone,
        TemplateKind, LogoRelativePath, MatchedSourceName, IsDefault, IsActive
    )
    VALUES
    (
        N'Saul Motors',
        N'Saul Motors',
        N'SAUL MOTORS',
        N'4417 US-70 BUS',
        N'Clayton, NC, 27520',
        N'rodriguezyardclayton@gmail.com',
        NULL,
        0,
        N'/invoice/saul-motors-logo.png',
        N'Saul Motors',
        0,
        1
    );
END
ELSE
BEGIN
    UPDATE dbo.InvoiceTemplates
    SET BuyerCompanyName = N'Saul Motors',
        BuyerAuthorizedName = N'SAUL MOTORS',
        LogoRelativePath = N'/invoice/saul-motors-logo.png',
        MatchedSourceName = N'Saul Motors',
        IsActive = 1,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE Name = N'Saul Motors';
END
GO

PRINT N'014_SaulMotorsSourceAndTemplate applied.';
GO
