# Database architecture scripts

This folder holds the SQL Server scripts that define and evolve the yard inventory database.

| File | Description |
|------|-------------|
| `001_InitialSchema.sql` | Creates tables, indexes, constraints, and seed data (roles, vehicle sources, Zona A / Zona B). |
| `002_DemoUserAndPartsZone.sql` | Demo user (all roles) + Zona Partes for engines/transmissions. |
| `008_SampleVehiclesAndParts.sql` | Sample vehicles + engines/transmissions (no users). Safe to re-run. |
| `009_VehicleSellerAndAcquisitionLocation.sql` | Peddle rename + seller/location fields on Vehicles. |
| `010_VehiclePickupDriver.sql` | Driver who collected the vehicle (`PickupDriver`). |
| `011_VehicleInvoiceAndPayment.sql` | Payment method, invoice number (from 1000), `InvoiceSequence`. |
| `015_DriverPickupSchedule.sql` | Driver role, language + access token, scheduled pickups + images; seeds drivers from `PickupDriver`. |
| `016_OptionalDriverAndPickupTime.sql` | Optional assigned driver + `ScheduledPickupWindow` text (e.g. "9 - 12pm"). |

## Workflow

1. Create the database yourself in SSMS (the app never calls `EnsureCreated`).
2. Apply scripts in order against `RSYYardInventory`.
3. When the schema changes, add a new numbered script (`003_...sql`, etc.) **and** update EF models in `src/RSYInventory.Data`.
4. This folder is the source of truth for recreating / evolving the architecture.

## Connection string (local example)

```
Server=.\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;
```

## Scaffold (importar modelos desde la BD)

Comando en `scaffold-command.txt`.

Ejecuta desde la **raíz del repo** (donde está `RSYInventory.slnx`), no desde `src\RSYInventory.Data`:

```powershell
cd <ruta-del-repo>
dotnet tool install --global dotnet-ef

dotnet ef dbcontext scaffold "Server=.\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --project src/RSYInventory.Data/RSYInventory.Data.csproj --startup-project src/RSYInventory.Web/RSYInventory.Web.csproj --context YardInventoryDbContext --context-dir Data --output-dir Entities --namespace RSYInventory.Data.Entities --context-namespace RSYInventory.Data.Data --no-onconfiguring --force
```

Si ya estás en `src\RSYInventory.Data`, usa:

```powershell
dotnet ef dbcontext scaffold "Server=.\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --project .\RSYInventory.Data.csproj --startup-project ..\RSYInventory.Web\RSYInventory.Web.csproj --context YardInventoryDbContext --context-dir Data --output-dir Entities --namespace RSYInventory.Data.Entities --context-namespace RSYInventory.Data.Data --no-onconfiguring --force
```

`--force` sobrescribe `Entities/` y `Data/YardInventoryDbContext.cs`.
