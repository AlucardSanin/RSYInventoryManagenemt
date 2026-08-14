/*
  021_RecycleInvoiceSequenceFrom21.sql  (corregido)
  El siguiente invoice de reciclaje debe ser el 20 (no 21).

  - Borra invoices semanales ya generados (para poder regenerar desde el #20)
  - Deja NextNumber = 20

  Ejecutar una vez en SSMS si ya generaste un invoice con número incorrecto.
*/
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.RecycleWeeklyInvoices', N'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.RecycleWeeklyInvoices;
END
GO

IF OBJECT_ID(N'dbo.RecycleInvoiceSequence', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.RecycleInvoiceSequence
    SET NextNumber = 20
    WHERE Id = 1;
END
ELSE
BEGIN
    CREATE TABLE dbo.RecycleInvoiceSequence
    (
        Id         INT NOT NULL CONSTRAINT PK_RecycleInvoiceSequence PRIMARY KEY,
        NextNumber INT NOT NULL
    );
    INSERT INTO dbo.RecycleInvoiceSequence (Id, NextNumber) VALUES (1, 20);
END
GO

PRINT N'021: RecycleWeeklyInvoices cleared; next invoice number = 20.';
GO
