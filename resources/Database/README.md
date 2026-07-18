# Database architecture scripts

This folder holds the SQL Server scripts that define and evolve the yard inventory database.

| File | Description |
|------|-------------|
| `001_InitialSchema.sql` | Creates tables, indexes, constraints, and seed data (roles, vehicle sources, Zona A / Zona B). |
| `002_DemoUserAndPartsZone.sql` | Demo user (all roles) + Zona Partes for engines/transmissions. |

## Workflow

1. Apply scripts in order with SQL Server Management Studio (SSMS) against database `RSYYardInventory` (or your chosen name).
2. When you change the schema (new column, table, constraint), update the corresponding script **and** the EF models in `src/RSYInventory.Data`.
3. Prefer additive numbered scripts (`002_...sql`, `003_...sql`) for later changes so the full history can rebuild an empty database.

## Connection string (example)

```
Server=localhost;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;
```

For SQL authentication:

```
Server=localhost;Database=RSYYardInventory;User Id=sa;Password=YourPassword;TrustServerCertificate=True;
```
