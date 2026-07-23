/*
  013_VehicleInvoiceSigning.sql
  Track DocuSeal signing state and stored PDF paths per vehicle receipt.

  Safe to re-run.
*/
USE [RSYYardInventory];
GO

IF COL_LENGTH(N'dbo.Vehicles', N'InvoiceTemplateId') IS NULL
    ALTER TABLE dbo.Vehicles ADD InvoiceTemplateId INT NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'DocuSealSubmissionId') IS NULL
    ALTER TABLE dbo.Vehicles ADD DocuSealSubmissionId INT NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SellerSigningUrl') IS NULL
    ALTER TABLE dbo.Vehicles ADD SellerSigningUrl NVARCHAR(500) NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'UnsignedPdfRelativePath') IS NULL
    ALTER TABLE dbo.Vehicles ADD UnsignedPdfRelativePath NVARCHAR(400) NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SignedPdfRelativePath') IS NULL
    ALTER TABLE dbo.Vehicles ADD SignedPdfRelativePath NVARCHAR(400) NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SignatureSentAtUtc') IS NULL
    ALTER TABLE dbo.Vehicles ADD SignatureSentAtUtc DATETIME2(7) NULL;
GO

IF COL_LENGTH(N'dbo.Vehicles', N'SignedAtUtc') IS NULL
    ALTER TABLE dbo.Vehicles ADD SignedAtUtc DATETIME2(7) NULL;
GO

PRINT N'013_VehicleInvoiceSigning applied.';
GO
