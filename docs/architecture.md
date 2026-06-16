---
title: Architecture Decisions — L2Titan Launcher
version: 1.0
date: 2026-06-03
status: as-built (post-audit 2026-06-03)
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - docs/prd.md
---

# Architecture Decisions — L2Titan Launcher

Registro de decisiones reales del sistema tal como está construido y auditado (auditoría multi-agente 2026-06-03: 43 fixes aplicados y verificados en vivo). Cada decisión incluye su porqué para que cualquier agente IA o humano implemente de forma consistente.

## D1 — Stack y forma de la aplicación

**Decisión:** WPF sobre .NET 10 (`net10.0-windows`), MVVM, ventana única sin chrome nativo (`WindowStyle=None` + `WindowChrome` con `CaptionHeight=0`, `ResizeBorderThickness=6`), publicación single-file self-contained win-x64 sin compresión.

**Por qué:** UI custom "epic" imposible con chrome nativo; `WindowChrome` da resize/maximize correctos sin `AllowsTransparency` (incompatible con WebView2). Sin compresión single-file porque los AV marcan binarios empaquetados como malware.

- Assembly: `L2TitanLauncher` (¡el .csproj se llama `Lineage2Launcher.csproj` — no confundir!).
- Entry point: generado por WPF desde `App.xaml` (no hay `Program.cs`).
- **Capa de servicios** (`Services/`, sin dependencias de WPF, testeable): `ConfigService` (config.json), `UpdateService` (verificación+descarga; reporta a la UI vía `IUpdateHost`), `PathSafety` (anti path-traversal), `FileIntegrity` (SHA-256), `ManifestSecurity` (firma RSA), `LauncherUpdater` (auto-update del exe), `ErrorClassifier`, modelos en `LauncherModels`. `MainViewModel` quedó como **coordinador de UI** (estado del botón, brushes, comandos) e implementa `IUpdateHost`. (Refactor 2026-06-16 desde el god-object original.)
- **Tests** (`Tests/`, xUnit, `net10.0-windows`, `InternalsVisibleTo`): 29 casos sobre la lógica crítica (ResolveSafePath, hashing, verificación de firma, resolución de config). Correr: `dotnet test Tests/L2TitanLauncher.Tests.csproj`.

## D2 — Cadena de integridad de archivos (CRÍTICA)

**Decisión:** SHA-256 hex minúscula, comparación `OrdinalIgnoreCase`, idéntico en los DOS lados:
- Servidor: `generate_manifest.py::calculate_sha256` → `hexdigest().lower()`
- Cliente: `Services/FileIntegrity.ComputeSha256` → `SHA256.Create()` + `ToLowerInvariant()`

**Por qué:** MD5 roto + el hash llega del mismo canal que el archivo. Ambos lados DEBEN cambiar juntos; cambiar uno solo rompe el 100 % de las descargas. (El servidor ya sirve el manifest SHA-256 en producción desde 2026-06-16.)

**Regla para agentes:** nunca tocar el algoritmo/formato de hash en un solo lado.

## D3 — Canal de updates: HTTPS forzado

**Decisión:** `Services/ConfigService.EnforceHttps` (invocado por `Resolve`) rechaza cualquier `ServerUrl`/`ManifestUrl` que no empiece por `https://` y restaura el default `https://downloads.l2-titan.com`.

**Por qué:** un launcher que descarga ejecutables sobre HTTP = RCE por MITM. Consecuencia aceptada: no se puede testear contra servidor HTTP local; usar endpoint HTTPS real o túnel.

**Implementado (D2+D3, 2026-06-16):** firma RSA detached del manifest (`manifest.json.sig`, PKCS#1 v1.5 / SHA-256). Clave pública embebida en `Services/ManifestSecurity.cs`; la privada vive solo local (`keys/`, fuera de git) y se firma con `sign-manifest.sh` tras regenerar el manifest. El cliente verifica la firma ANTES de confiar en el manifest — elimina la confianza exclusiva en TLS/hosting. Regla: si regeneras el manifest, vuelve a firmarlo o el launcher lo rechazará.

## D4 — Descarga: staging atómico + clasificación de fallos

**Decisión:**
- Todo archivo baja a `<destino>.part`; solo tras verificar hash se hace `File.Move(temp, final, overwrite: true)`. El `.part` se borra en TODO camino de fallo/cancelación.
- Errores transitorios (red): hasta 3 reintentos con backoff.
- `HashMismatchException` (bytes corruptos en servidor): NO se reintenta — mismos bytes fallarán igual; se informa como problema del servidor.
- Timeout de inactividad deslizante de 60 s por `ReadAsync` (CTS enlazado a `_cts`), colocado tras el loop de pausa para que pausar >60 s no dispare timeout falso.
- Path de destino SIEMPRE vía `ResolveSafePath` (canonicaliza y rechaza rutas con `..`/absolutas que escapen del directorio del juego → bloquea path-traversal del manifest).

**Por qué:** crash/pausa/cierre jamás deja un archivo final truncado; clasificar fallos evita loops de retry inútiles y da mensajes honestos.

## D5 — Threading y ciclo de vida

**Decisión:**
- Trabajo pesado en `Task.Run`; toda notificación a UI vía `RaiseOnUi` (CheckAccess + BeginInvoke) o `Dispatcher.Invoke` con null-guard de `Application.Current`.
- Un único `CancellationTokenSource _cts` de instancia gobierna loops de noticias, polling de estado y descargas. `Shutdown()` (llamado al cerrar la ventana) hace `_cts.Cancel()` + `_httpClient.Dispose()` pero **NO** `_cts.Dispose()` (los loops aún leen el token; ODE evitada deliberadamente).
- `OperationCanceledException` con `_cts.IsCancellationRequested` = cierre normal, nunca estado de error.
- `HttpClient` único compartido, todas las llamadas con token.

**Por qué:** WPF lanza `InvalidOperationException` ante mutación cross-thread; el patrón único evita el zoo de soluciones ad-hoc. La no-disposición del CTS es intencional — no "arreglarla".

## D6 — Modelo de errores de cara al usuario

**Decisión:** excepción privada `LauncherError` marca mensajes redactados para el usuario; los `catch` de `StartAutoVerification`/`HandlePlayAction` la capturan ANTES que `Exception` y muestran `Message` literal en la barra de estado. Todo lo demás cae al genérico "Auto-verification failed. Click RETRY".

**Por qué:** verificado en vivo — sin el tipo marcador, el mensaje bueno ("Server file corrupted (X)…") se perdía en el catch genérico. Regla: nuevo error accionable ⇒ `throw new LauncherError("…")`.

## D7 — Configuración

**Decisión:** orden de búsqueda de `config.json`: (1) carpeta del exe, (2) `%APPDATA%\Lineage2Launcher\`, (3) cwd. Lectura y escritura usan la carpeta legacy `Lineage2Launcher` — **no renombrar** a `L2TitanLauncher` (rompería configs de usuarios existentes).
GamePath con heurística `LooksLikeLineageClient` (existe `system\L2.exe`): exe-dir si es cliente válido > GamePath configurado si es cliente válido > default `C:\Juegos\Lineage2`.

## D8 — UI / recursos

- Claves que el ViewModel resuelve con `FindResource` y DEBEN existir: `PlayButtonGradientBrush`, `GoldBorderGradientBrush`. (`BrightGoldBrush` se consume como `StaticResource` desde `MainWindow.xaml`, no vía el VM.)
- Pack URIs usan el assembly real: `pack://application:,,,/L2TitanLauncher;component/...`.
- WebView2: `Source` se asigna en code-behind tras `EnsureCoreWebView2Async` (asignarlo en XAML carrea la init); `_firstNavigationHandled` hace que solo el fallo de navegación inicial muestre el fallback y que el éxito restaure el WebView; `Dispose()` en `Closed`.
- Solo se embebe `Assets/images/background.png` + fuentes Cinzel; assets no referenciados se eliminaron (estaban inflando el exe ~10 MB).

## D9 — Operación

- `deploy-simple.sh`: heredocs remotos con `set -e`, `rc=$?` capturado inmediatamente tras cada ssh, `unzip … || [ $? -eq 1 ]` (exit 1 = warning no fatal), permisos finales 644 archivos / 755 dirs. El manifest se REGENERA en cada deploy (paso 1) — el `manifest.json` del repo es solo un artefacto de referencia y está obsoleto hasta el próximo deploy.
- **CI (GitHub Actions; gratis al ser el repo público):** `ci.yml` (build + tests en push/PR) y `build-and-sign.yml` (tags `v*`: build+test+publish + firma con **SignPath Foundation**, requiere alta + secrets). El despliegue soportado es `./deploy.sh` (firma + verifica); `deploy-simple.sh` quedó obsoleto.
- `StartGame`: `Verb="runas"` + handler `Win32Exception 1223` (UAC cancelado) — restaurado tras regresión; no eliminar.

## Deuda técnica registrada

| # | Ítem | Prioridad |
|---|------|-----------|
| 1 | ✅ Redeploy del servidor (manifest SHA-256) — HECHO 2026-06-16 (nginx+TLS en downloads.l2-titan.com) | — |
| 2 | ✅ Firma del manifest (RSA, clave embebida + verificación) — HECHO 2026-06-16 | — |
| 3 | Auto-update del launcher | Media |
| 4 | `manifest.json` del repo obsoleto (MD5) — regenerar o quitar del repo | Baja |
| 5 | Doble click en header no maximiza (decisión: solo botón) | Baja |
