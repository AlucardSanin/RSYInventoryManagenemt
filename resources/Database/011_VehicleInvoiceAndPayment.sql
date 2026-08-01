/*
  011_VehicleInvoiceAndPayment.sql
  - Optional payment method on acquisition
  - Invoice number per vehicle (assigned on first generate)
  - Sequence counter starting at 1000

  Safe to re-run. ALTER and CREATE INDEX are in separate batches
  (SQL Server cannot reference a newly added column in the same batch).
*/
USE [RSYYardInventory];
GO

IF COL_LENGTH(N'dbo.Vehicles', N'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD PaymentMethod NVARCHAR(80) NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'InvoiceNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD InvoiceNumber INT NULL;
END
GO

IF COL_LENGTH(N'dbo.Vehicles', N'InvoiceIssuedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.Vehicles ADD InvoiceIssuedAtUtc DATETIME2(7) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UQ_Vehicles_InvoiceNumber'
      AND object_id = OBJECT_ID(N'dbo.Vehicles')
)
BEGIN
    CREATE UNIQUE INDEX UQ_Vehicles_InvoiceNumber
        ON dbo.Vehicles (InvoiceNumber)
        WHERE InvoiceNumber IS NOT NULL;
END
GO

IF OBJECT_ID(N'dbo.InvoiceSequence', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InvoiceSequence
    (
        Id         INT NOT NULL CONSTRAINT PK_InvoiceSequence PRIMARY KEY,
        NextNumber INT NOT NULL
    );

    INSERT INTO dbo.InvoiceSequence (Id, NextNumber) VALUES (1, 1000);
END
GO

PRINT '011_VehicleInvoiceAndPayment applied.';
GO
