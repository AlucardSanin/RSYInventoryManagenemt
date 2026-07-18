# Database architecture scripts

This folder holds the SQL Server scripts that define and evolve the yard inventory database.

| File | Description |
|------|-------------|
| `001_InitialSchema.sql` | Creates tables, indexes, constraints, and seed data (roles, vehicle sources, Zona A / Zona B). |
| `002_DemoUserAndPartsZone.sql` | Demo user (all roles) + Zona Partes for engines/transmissions. |

## Workflow

1. Create the database yourself in SSMS (the app never calls `EnsureCreated`).
2. Apply scripts in order against `RSYYardInventory`.
3. When the schema changes, add a new numbered script (`003_...sql`, etc.) **and** update EF models in `src/RSYInventory.Data`.
4. This folder is the source of truth for recreating / evolving the architecture.

## Connection string (local example)

```
Server=.\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;
```
