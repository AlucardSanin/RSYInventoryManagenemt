# RSY Yard Inventory

Aplicación híbrida para inventario de motores, transmisiones y vehículos entrantes en una yarda.

| Capa | Proyecto | Tecnología |
|------|----------|------------|
| Web | `src/RSYInventory.Web` | Blazor Web App (.NET 9, Interactive Server) |
| Móvil | `src/RSYInventory.Mobile` | .NET MAUI Blazor Hybrid (Android / iOS) |
| Datos | `src/RSYInventory.Data` | Entity Framework Core + SQL Server |
| SQL | `resources/Database` | Scripts SSMS para recrear la arquitectura |

Solución: `RSYInventory.slnx`

## Primeros pasos

### 1. Base de datos (SSMS)

1. Crea la base `RSYYardInventory` en SQL Server.
2. Ejecuta `resources/Database/001_InitialSchema.sql`.
3. Ajusta la connection string en `src/RSYInventory.Web/appsettings.json`.

### 2. Web (rápido para empezar a usar)

```bash
dotnet restore RSYInventory.slnx
dotnet run --project src/RSYInventory.Web
```

### 3. Móvil (Android / iOS)

```bash
# Android (Linux/macOS/Windows con workload maui-android)
dotnet build src/RSYInventory.Mobile -f net9.0-android

# iOS requiere macOS + workload maui-ios
dotnet build src/RSYInventory.Mobile -f net9.0-ios
```

### 4. Entity Framework (migraciones opcionales)

Los modelos están en `RSYInventory.Data`. El esquema canónico para recrear la BD es el SQL en `resources/Database`. Si usas migraciones EF:

```bash
dotnet ef migrations add InitialCreate --project src/RSYInventory.Data --startup-project src/RSYInventory.Web
```

## Modelo de negocio (resumen)

- **Roles**: Viewer (solo ver), Inventory Editor (CRUD inventario + ubicar vehículos), Zone Manager (crear/editar zonas), Vehicle Acquirer (registrar compras).
- **Layout**: Zona → Fila → Paleta. Filas y paletas son flexibles al crear/editar una zona.
- **Paleta (partes)**: máximo 1 motor, máximo 2 transmisiones; motor + transmisión permitido.
- **Vehículos**: VIN, año, marca, modelo, tipo de transmisión + drivetrain (2 combobox), km, observaciones, fuente (Wheelzy, Pebble, Facebook…), fecha de adquisición. La ubicación se asigna después.
- **Zonas por defecto (vehículos)**: Zona A y Zona B.
- **Reglas**: no eliminar zona/fila/paleta con inventario; al cambiar ubicación preguntar Vendido vs Reubicado; historial en `InventoryMovements`.

## Estructura

```
RSYInventory.slnx
src/
  RSYInventory.Data/       # Entidades, DbContext, reglas de capacidad
  RSYInventory.Web/        # Blazor web
  RSYInventory.Mobile/     # MAUI Blazor Hybrid
resources/
  Database/
    001_InitialSchema.sql  # Arquitectura SQL (actualizar al evolucionar)
```
