# CLAUDE.md — L2Titan Launcher

Launcher de escritorio (WPF / .NET 10, MVVM) que verifica la instalación de Lineage 2 contra un manifiesto firmado del servidor, descarga/repara archivos y lanza el juego. Sirve desde `https://downloads.l2-titan.com`.

> Guía completa en **`docs/best-practices.md`**. Decisiones de arquitectura en **`docs/architecture.md`**. Requisitos en **`docs/prd.md`**. El `.csproj` se llama `Lineage2Launcher.csproj` pero el binario es **`L2TitanLauncher.exe`**.

## Mapa de capas (dónde va cada cosa)

- **`Services/`** — lógica de dominio SIN dependencias de WPF, testeable: `ConfigService`, `UpdateService` (descarga/verificación), `PathSafety`, `FileIntegrity`, `ManifestSecurity`, `LauncherModels`. Nada de `Dispatcher`/`MessageBox` aquí.
- **`ViewModels/`** — UI/MVVM. `MainViewModel` coordina el estado del botón/progreso e implementa `IUpdateHost` (el puente por el que `UpdateService` reporta a la UI sin conocer WPF).
- **`Tests/`** — xUnit (`net10.0-windows`, `InternalsVisibleTo`). Testea la lógica crítica de `Services/`.
- **`Themes/`** — recursos XAML (solo las claves usadas; no acumular estilos muertos).
- Scripts de servidor: `generate_manifest.py`, `deploy-simple.sh`, `setup-server-security.sh`, `sign-manifest.sh`.

## Reglas NO NEGOCIABLES (seguridad / correctitud)

1. **SHA-256 idéntico cliente↔servidor**, hex minúscula, comparación `OrdinalIgnoreCase`. Cliente `FileIntegrity.ComputeSha256`, servidor `generate_manifest.py`. Nunca cambies el algoritmo en un solo lado.
2. **HTTPS forzado**: `ConfigService.EnforceHttps` rechaza cualquier URL no-`https://`.
3. **Anti path-traversal en AMBOS lados**: cliente `PathSafety.ResolveSafePath` (rechaza `..`/absolutas/UNC), servidor filtra en `generate_manifest.py`. También validar `GameExecutable`.
4. **Firma del manifiesto**: el cliente verifica `manifest.json.sig` (RSA, clave pública en `ManifestSecurity.cs`) ANTES de confiar. La clave PRIVADA vive solo en `keys/` (fuera de git). **Tras regenerar el manifiesto, re-firmar con `./sign-manifest.sh`** o el launcher lo rechaza.
5. **Staging atómico**: descargar a `.part`, verificar hash, luego `File.Move`. Borrar el `.part` en todo camino de fallo.
6. **Hilos**: trabajo pesado en `Task.Run`; tocar UI solo vía `RaiseOnUi`/`InvokeOnUi` (con null-guard de `Application.Current`). `Shutdown()` hace `_cts.Cancel()` pero **NO** `_cts.Dispose()` (loops aún leen el token).
7. **Errores de usuario**: lanzar `LauncherError` con mensaje accionable (se muestra tal cual). `HashMismatchException` = no reintentar.

## Comandos

```bash
dotnet build -c Debug                         # compilar
dotnet test Tests/L2TitanLauncher.Tests.csproj # 29 tests
dotnet publish -c Release                      # single-file self-contained, sin compresión (AV)
```

## Al cambiar código

- ¿Lógica sin WPF? → `Services/` + test en `Tests/`. ¿Estado/comando de UI? → `MainViewModel`.
- Si añades una clave XAML, úsala; si quitas una feature, borra sus recursos (no dejar estilos muertos).
- Verifica `dotnet build` + `dotnet test` verdes antes de commitear. No commitees `keys/` ni binarios.
