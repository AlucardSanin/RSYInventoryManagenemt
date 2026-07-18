# RSY Yard Inventory

Aplicación híbrida para inventario de motores, transmisiones y vehículos entrantes en una yarda.

| Capa | Proyecto | Tecnología |
|------|----------|------------|
| Web | `src/RSYInventory.Web` | Blazor Web App (.NET 9, Interactive Server) |
| Móvil | `src/RSYInventory.Mobile` | .NET MAUI Blazor Hybrid (Android / iOS) |
| Datos | `src/RSYInventory.Data` | Entity Framework Core + SQL Server |
| SQL | `resources/Database` | Scripts SSMS para recrear la arquitectura |

Solución: `RSYInventory.slnx`

## Depurar en Visual Studio (tu PC)

El agente Cloud empuja cambios a GitHub; en tu máquina solo tienes que actualizar la rama:

```powershell
git fetch origin
git checkout cursor/yard-inventory-foundation-a4a6
git pull origin cursor/yard-inventory-foundation-a4a6
```

1. Abre `RSYInventory.slnx` en Visual Studio 2022.
2. En SSMS, crea la BD `RSYYardInventory` y ejecuta en orden:
   - `resources/Database/001_InitialSchema.sql`
   - `resources/Database/002_DemoUserAndPartsZone.sql`
3. Ajusta la connection string si no usas LocalDB:
   - Dev: `src/RSYInventory.Web/appsettings.Development.json` → `(localdb)\MSSQLLocalDB`
   - O tu instancia SQL: `Server=localhost;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;`
4. Establece `RSYInventory.Web` como proyecto de inicio y pulsa F5.

Repo: https://github.com/AlucardSanin/RSYInventoryManagenemt  
PR: https://github.com/AlucardSanin/RSYInventoryManagenemt/pull/1

## Pantallas web (ya disponibles)

- `/inventory` — listar / añadir motores y transmisiones
- `/inventory/{id}/move` — preguntar Vendido vs Reubicado + historial
- `/vehicles` — listado; `/vehicles/acquire` — alta con VIN (auto-relleno básico)
- `/vehicles/{id}/assign` — ubicar en Zona A / B (u otras zonas de vehículos)
- `/zones` — crear / redimensionar / eliminar zonas (bloquea si hay inventario)
- `/pallets/{id}/movements` — últimos movimientos por paleta y usuario

## Primeros pasos (CLI)

```bash
dotnet restore RSYInventory.slnx
dotnet run --project src/RSYInventory.Web
```

## Modelo de negocio (resumen)

- **Roles**: Viewer, Inventory Editor, Zone Manager, Vehicle Acquirer (usuario demo tiene todos).
- **Layout**: Zona → Fila → Paleta.
- **Paleta (partes)**: máximo 1 motor, máximo 2 transmisiones; motor + transmisión OK.
- **Vehículos**: VIN, año, marca/modelo, transmisión + drivetrain, km, observaciones, fuente, fecha; ubicación después.
- **Reglas**: no eliminar zona/fila/paleta con inventario; al mover pieza → Vendido o Reubicado; log en `InventoryMovements`.
