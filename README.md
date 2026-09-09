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

```powershell
git fetch origin
git checkout cursor/vehicle-price-history-a4a6
git pull origin cursor/vehicle-price-history-a4a6
```

1. Abre `RSYInventory.slnx` en Visual Studio 2022.
2. BD local: **`RSYYardInventory`** en `.\MSSQLSERVER01`.
3. Connection string (Development ya apunta a esa instancia):
   ```
   Server=.\MSSQLSERVER01;Database=RSYYardInventory;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30;
   ```
   `Encrypt=False` + `TrustServerCertificate=True` evita el error de certificado SSL de SQL Client en local.
4. Si F5 usa el perfil **https** y falla el certificado del sitio: `dotnet dev-certs https --trust`
5. En el servidor (Production): edita `appsettings.Production.json` o variables de entorno (`ConnectionStrings__YardInventory`). No dejes `YOUR_PRODUCTION_SERVER`.
6. Esquema: scripts en `resources/Database/*.sql` (SSMS). La app no crea la BD.
7. Proyecto de inicio: `RSYInventory.Web` → F5. Al arrancar el log debe decir `Conexión a RSYYardInventory OK.`

## Si no carga la BD ni los certificados

| Síntoma | Causa habitual | Qué hacer |
|---------|----------------|-----------|
| Error SSL / certificate chain al conectar SQL | SqlClient cifra por defecto | Usa `Encrypt=False` (local) o `TrustServerCertificate=True` |
| Login/páginas vacías, sin datos | Connection string apunta a otra instancia (p. ej. LocalDB) | Confirma `.\MSSQLSERVER01` en Development |
| Sitio HTTPS no abre en VS | Certificado de desarrollo no confiable | `dotnet dev-certs https --trust` o usa perfil **http** |
| Tras publicar en IIS, sesión/uploads rotos | DataProtection sin escritura | Permiso de escritura en `App_Data/dataprotection-keys` para el App Pool |

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
