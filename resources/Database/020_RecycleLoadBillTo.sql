/*
  020_RecycleLoadBillTo.sql
  - RecycleLoads.BillToCompanyId: compañía a la que se vendió la carga (al registrar)
  - Weekly invoices: unique por (semana, compañía) — un PDF por compañía/semana
*/
SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'BillToCompanyId') IS NULL
BEGIN
    ALTER TABLE dbo.RecycleLoads ADD BillToCompanyId INT NULL;
END
GO

/* Backfill existing loads to the first BILL TO company (typically RMR seed). */
UPDATE l
SET l.BillToCompanyId = c.Id
FROM dbo.RecycleLoads l
CROSS APPLY (
    SELECT TOP (1) Id
    FROM dbo.RecycleBillToCompanies
    ORDER BY Id
) c
WHERE l.BillToCompanyId IS NULL;
GO

IF EXISTS (
    SELECT 1
    FROM dbo.RecycleLoads
    WHERE BillToCompanyId IS NULL
)
BEGIN
    RAISERROR(N'020: hay cargas sin BillToCompanyId y no existen compañías BILL TO. Crea una compañía (019) y vuelve a ejecutar.', 16, 1);
    RETURN;
END
GO

IF COL_LENGTH(N'dbo.RecycleLoads', N'BillToCompanyId') IS NOT NULL
   AND EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.RecycleLoads')
          AND name = N'BillToCompanyId'
          AND is_nullable = 1
   )
BEGIN
    ALTER TABLE dbo.RecycleLoads ALTER COLUMN BillToCompanyId INT NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RecycleLoads_BillTo'
)
BEGIN
    ALTER TABLE dbo.RecycleLoads WITH CHECK
    ADD CONSTRAINT FK_RecycleLoads_BillTo
        FOREIGN KEY (BillToCompanyId) REFERENCES dbo.RecycleBillToCompanies (Id);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_RecycleLoads_BillToCompanyId_LoadDate'
      AND object_id = OBJECT_ID(N'dbo.RecycleLoads')
)
BEGIN
    CREATE INDEX IX_RecycleLoads_BillToCompanyId_LoadDate
        ON dbo.RecycleLoads (BillToCompanyId, LoadDate DESC);
END
GO

/* One invoice per work week + BILL TO company (not one invoice for the whole week). */
IF EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE name = N'UQ_RecycleWeeklyInvoices_Week'
      AND parent_object_id = OBJECT_ID(N'dbo.RecycleWeeklyInvoices')
)
BEGIN
    ALTER TABLE dbo.RecycleWeeklyInvoices DROP CONSTRAINT UQ_RecycleWeeklyInvoices_Week;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_RecycleWeeklyInvoices_Week'
      AND object_id = OBJECT_ID(N'dbo.RecycleWeeklyInvoices')
)
BEGIN
    DROP INDEX UQ_RecycleWeeklyInvoices_Week ON dbo.RecycleWeeklyInvoices;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_RecycleWeeklyInvoices_Week_BillTo'
      AND object_id = OBJECT_ID(N'dbo.RecycleWeeklyInvoices')
)
BEGIN
    CREATE UNIQUE INDEX UQ_RecycleWeeklyInvoices_Week_BillTo
        ON dbo.RecycleWeeklyInvoices (WeekStartDate, BillToCompanyId);
END
GO

PRINT N'020_RecycleLoadBillTo applied.';
GO
