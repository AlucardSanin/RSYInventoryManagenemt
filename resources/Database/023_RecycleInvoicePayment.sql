/*
  023_RecycleInvoicePayment.sql
  Payment (check) registration for weekly recycle invoices.
*/
USE [RSYYardInventory];
GO

IF OBJECT_ID(N'dbo.RecycleInvoicePayments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecycleInvoicePayments
    (
        Id                      INT             NOT NULL IDENTITY(1,1)
            CONSTRAINT PK_RecycleInvoicePayments PRIMARY KEY,
        RecycleWeeklyInvoiceId  INT             NOT NULL,
        CheckImageRelativePath  NVARCHAR(400)   NOT NULL,
        CheckDate               DATE            NOT NULL,
        AmountUsd               DECIMAL(12, 2)  NOT NULL,
        Notes                   NVARCHAR(1000)  NULL,
        RecordedAtUtc           DATETIME2(7)    NOT NULL
            CONSTRAINT DF_RecycleInvoicePayments_RecordedAtUtc DEFAULT (SYSUTCDATETIME()),
        RecordedByUserId        INT             NOT NULL,
        UpdatedAtUtc            DATETIME2(7)    NULL,
        CONSTRAINT UQ_RecycleInvoicePayments_Invoice UNIQUE (RecycleWeeklyInvoiceId),
        CONSTRAINT CK_RecycleInvoicePayments_Amount CHECK (AmountUsd > 0),
        CONSTRAINT FK_RecycleInvoicePayments_Invoice
            FOREIGN KEY (RecycleWeeklyInvoiceId) REFERENCES dbo.RecycleWeeklyInvoices (Id) ON DELETE CASCADE,
        CONSTRAINT FK_RecycleInvoicePayments_User
            FOREIGN KEY (RecordedByUserId) REFERENCES dbo.Users (Id)
    );

    CREATE INDEX IX_RecycleInvoicePayments_RecordedAtUtc
        ON dbo.RecycleInvoicePayments (RecordedAtUtc DESC);
END
GO

PRINT N'023_RecycleInvoicePayment applied.';
GO
