# Buenas prácticas — L2Titan Launcher

Guía de **cómo implementar correctamente** en este repo. Es ortogonal a:
- `docs/architecture.md` — *qué se decidió y por qué* (decisiones D1–D9 + deuda técnica).
- `docs/prd.md` — *requisitos de producto*.
- `CLAUDE.md` — versión condensada (reglas no-negociables + comandos), auto-cargada por Claude Code.

Si algo aquí contradice al código, gana el código (y corrige este doc).

---

## 1. Estructura de capas — dónde va cada cosa

| Capa | Carpeta | Contiene | Regla |
|---|---|---|---|
| Dominio | `Services/` | Config, descarga/verificación, hashing, path-safety, firma, modelos | **Sin** dependencias de WPF (`Dispatcher`, `MessageBox`, `Application`). Métodos puros donde se pueda → testeables sin mocking. |
| Presentación | `ViewModels/` | `MainViewModel` (estado de UI, comandos), `RelayCommand` | Coordina la UI. Implementa `IUpdateHost` para recibir progreso de `UpdateService`. |
| Tests | `Tests/` | xUnit, `net10.0-windows`, `InternalsVisibleTo` | Cubre la lógica crítica de `Services/`. |
| Recursos | `Themes/`, `Assets/`, `Fonts/` | XAML, imágenes y fuentes embebidas | Solo lo referenciado; no acumular estilos/brushes muertos. |
| Operación | raíz / scripts | `*.py`, `*.sh` | Generación de manifiesto, deploy, provisioning, firma. |

**Decisión rápida al añadir código:** ¿no toca WPF? → `Services/` (+ test). ¿es estado/acción de UI? → `ViewModels/`. ¿es seguridad? → `Services/` + test + revisión.

---

## 2. Modelo de hilos y marshalling a UI

El trabajo pesado (hashing, descarga) corre en `Task.Run`. WPF lanza `InvalidOperationException` si mutas algo bindeado desde otro hilo, así que **toda** notificación a la UI pasa por uno de estos:

- **`RaiseOnUi(prop)`** — `CheckAccess()` + `BeginInvoke` (no bloqueante). Para `PropertyChanged` de propiedades como `Progress`/`StatusText`.
- **`InvokeOnUi(action)`** — `Dispatcher.Invoke` con **null-guard de `Application.Current`** (clave durante el cierre). Para bloques que tocan varias propiedades.
- **`AddLog(msg)`** — también marshaliza y tiene null-guard.

**Puente servicio→UI:** `UpdateService` no conoce WPF. Reporta vía la interfaz `IUpdateHost` (`Log`, `SetProgress`, `SetStatus`, `OnDownloadingStarted`, `IsPaused`, `Token`) que `MainViewModel` implementa. Si añades un servicio que deba reportar a la UI, hazlo con un callback/interfaz igual — no metas WPF en `Services/`.

**Ciclo de vida:** un único `CancellationTokenSource _cts`. `Shutdown()` (en `OnClosing`) hace `_cts.Cancel()` + `_httpClient.Dispose()` pero **NO** `_cts.Dispose()` (los loops aún leen el token → evitar `ObjectDisposedException`). `OperationCanceledException` con `_cts.IsCancellationRequested` = cierre normal, no error.

**Flags compartidos cross-thread** (`_isDownloadPaused`) → `volatile`.

---

## 3. Modelo de errores

- **`LauncherError`** — su `Message` está redactado para mostrarse tal cual en la barra de estado. Lánzalo cuando tengas un mensaje accionable para el usuario ("Server file corrupted…", "No write permission…"). Los `catch` lo capturan ANTES que `Exception`.
- **`HashMismatchException`** — marca corrupción de contenido: **no reintentar** (los mismos bytes fallarán igual).
- **`Exception` genérica** — fallo técnico (red/timeout); se puede reintentar con backoff.
- Clasificación de errores transitorios: hay un helper `MainViewModel.ClassifyError` (única copia de la heurística). Si añades un nuevo tipo accionable, prefiere lanzar `LauncherError` desde el servicio en vez de adivinar por substring del mensaje.

---

## 4. Reglas de seguridad (críticas — no negociables)

| # | Regla | Por qué | Dónde |
|---|---|---|---|
| 1 | SHA-256 hex minúscula en **ambos** lados; comparar `OrdinalIgnoreCase` | Integridad extremo a extremo | `FileIntegrity.ComputeSha256` ↔ `generate_manifest.py` |
| 2 | **HTTPS forzado** (rechazar `http://`) | El launcher descarga ejecutables → MITM = RCE | `ConfigService.EnforceHttps` |
| 3 | **Anti path-traversal** en cliente y servidor | Un manifiesto malicioso no debe escribir fuera del juego | `PathSafety.ResolveSafePath` + filtro en `generate_manifest.py` |
| 4 | **Firma RSA del manifiesto** (clave pública embebida, privada fuera de git) | Sin firma, la integridad depende solo de TLS/hosting → RCE si se comprometen | `ManifestSecurity.Verify` + `sign-manifest.sh`; clave en `keys/` |
| 5 | **Staging `.part` + move atómico**; borrar `.part` en todo fallo | Un crash jamás deja un archivo final truncado | `UpdateService.DownloadFile` |
| 6 | Validar `GameExecutable` con `ResolveSafePath` antes de lanzar | Un `config.json` plantado no debe lanzar rutas arbitrarias como admin | `MainViewModel.StartGame` |

**Regla operativa:** si regeneras el manifiesto en el servidor, **vuelve a firmarlo** (`./sign-manifest.sh`) o el launcher rechazará la actualización. Respaldar `keys/manifest_private.pem` (no es recuperable; perderla obliga a re-publicar el launcher con una pública nueva).

Cada una de estas reglas tiene un test en `Tests/` (hashing, https, traversal, firma). Si tocas la lógica, mantén/añade el test.

---

## 5. Build, publish y tests

```bash
dotnet build -c Debug                          # desarrollo (0 warnings esperado)
dotnet test Tests/L2TitanLauncher.Tests.csproj  # suite xUnit
dotnet publish -c Release                       # single-file self-contained win-x64
```

- **Single-file, self-contained, SIN compresión** (`EnableCompressionInSingleFile=false`): un binario comprimido parece malware empaquetado para los antivirus.
- Solo se embeben `Assets/images/background.png` + fuentes Cinzel. No añadir assets sin usar (inflan el `.exe`).
- Tests: para acceder a tipos `internal` de `Services/` se usa `<InternalsVisibleTo Include="L2TitanLauncher.Tests" />` en el `.csproj` principal.
- **Tests:** correr localmente con `dotnet test Tests/L2TitanLauncher.Tests.csproj`. **No hay CI en GitHub Actions** (decisión del proyecto: no se usa). Verifica `dotnet build` + `dotnet test` en verde antes de commitear.

---

## 6. Configuración (`config.json`)

Orden de búsqueda (en `ConfigService.FindConfigFile`): (1) carpeta del exe, (2) `%APPDATA%\Lineage2Launcher\`, (3) cwd. **No renombrar** la carpeta legacy `Lineage2Launcher` (rompería configs existentes).

`ConfigService.ResolveGamePath` elige: cliente válido en la carpeta del exe > `GamePath` configurado si es cliente válido > default `C:\Juegos\Lineage2`. "Cliente válido" = existe `system\L2.exe` (`PathSafety.LooksLikeLineageClient`).

---

## 7. UI / recursos XAML

- El `MainViewModel` resuelve por `FindResource`: `PlayButtonGradientBrush`, `GoldBorderGradientBrush`. Deben existir en `Themes/Colors.xaml`.
- WebView2 se inicializa en code-behind tras `EnsureCoreWebView2Async` (no asignar `Source` en XAML); `Dispose` + desuscripción de `NavigationCompleted` en `Closed`.
- Pack URIs con el assembly real: `pack://application:,,,/L2TitanLauncher;component/...`.
- **No dejar estilos/brushes muertos**: si una clave no se referencia en ningún `StaticResource`/`DynamicResource`/`FindResource`, bórrala. Verificación: que toda clave referenciada tenga definición (si no, crash en runtime, no en build).

---

## 8. Deploy y operación

Estado actual: servidor nginx en `downloads.l2-titan.com` (TLS Let's Encrypt), usuario `deploy` por SSH:2222 con sudo acotado.

- **Deploy del juego:** usa **`./deploy.sh`** (tar+ssh → genera el manifest en el server → firma → publica `manifest`+`.sig` juntos → verifica HTTPS → backup/rollback). Reemplaza a `deploy-simple.sh` (obsoleto: no firmaba). Por dentro el manifest se genera en el server con `generate_manifest.py`.
- **Auto-update del launcher:** `Services/LauncherUpdater.cs` comprueba al arrancar `launcher.json` (firmado), y si hay versión mayor descarga el exe, verifica SHA-256 + firma RSA y se auto-reemplaza. **Seguridad (revisada adversarialmente):** firma RSA obligatoria antes de usar nada; el exe debe venir del **mismo host https** que el `launcher.json` (anti-SSRF); **anti-rollback** (no baja de la versión más alta vista, persistida en `%LOCALAPPDATA%`); el reemplazo es un script **PowerShell** en `%LOCALAPPDATA%` (maneja rutas con `%`/`&`/espacios, espera el PID con timeout, backup+rollback+log). Para publicar una versión nueva: sube `<Version>` en el csproj → `dotnet publish -c Release` → `./publish-launcher.sh <version>`.
- **Después de generar el manifiesto: `./sign-manifest.sh`** (firma local + sube `manifest.json.sig`).
- `setup-server-security.sh` aprovisiona un VPS desde cero (usuario `deploy`, llave SSH, sudo acotado, ufw, directorios web).

---

## 9. Checklist para una PR / cambio

- [ ] ¿La lógica nueva quedó en la capa correcta (`Services/` si no es WPF)?
- [ ] ¿`dotnet build` y `dotnet test` en verde?
- [ ] ¿Tocaste alguna regla de seguridad (§4)? → mantén/añade su test.
- [ ] ¿Añadiste/quitaste recursos XAML? → sin claves colgantes ni estilos muertos.
- [ ] ¿Cambió el manifiesto del servidor? → re-firmar.
- [ ] No commitear `keys/`, binarios, ni `config.json`/`manifest.json` locales (ya en `.gitignore`).
