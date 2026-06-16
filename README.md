# L2Titan Launcher

Launcher de escritorio para el servidor **L2Titan** (Lineage 2): verifica la instalación local del cliente contra un manifiesto del servidor, descarga/repara los archivos que falten o estén corruptos y lanza el juego.

> ⚠️ **Fuente de verdad:** la arquitectura real y as-built está en [`docs/architecture.md`](docs/architecture.md) y los requisitos en [`docs/prd.md`](docs/prd.md). Si algo aquí contradice esos documentos o el código, manda el código y esos docs. (Este README fue reescrito el 2026-06-16 porque la versión anterior describía un stack obsoleto — WinForms/.NET 8/MD5 — que ya no corresponde.)

## Stack real

- **WPF** sobre **.NET 10** (`net10.0-windows`), patrón **MVVM**.
- Ventana única sin chrome nativo (`WindowStyle=None` + `WindowChrome`).
- Panel web embebido con **WebView2** (apunta a `https://l2-titan.com/`).
- Publicación **single-file self-contained** `win-x64`, **sin compresión** (para evitar falsos positivos de antivirus). El `.exe` resultante se llama **`L2TitanLauncher.exe`** (el `.csproj` se llama `Lineage2Launcher.csproj` — no confundir).

## Cómo funciona (cadena de actualización)

1. El cliente descarga `manifest.json` desde el servidor por **HTTPS forzado** (rechaza `http://`).
2. Para cada archivo del manifiesto calcula su **SHA-256** y lo compara (case-insensitive) con el del manifiesto.
3. Lo que falta o no coincide se descarga a un archivo temporal `.part`, se verifica el hash y solo entonces se hace `File.Move` atómico al destino final.
4. Reintentos con backoff para errores de red; los errores de hash (archivo corrupto en el servidor) **no** se reintentan.
5. `system\L2.exe` se lanza al pulsar PLAY.

> El **hash es SHA-256 en ambos lados** y DEBE mantenerse sincronizado: cliente (`Services/FileIntegrity.ComputeSha256`) ↔ servidor (`generate_manifest.py::calculate_sha256`). Cambiar el algoritmo en un solo lado rompe el 100 % de las verificaciones.

## Compilar

```powershell
# Requiere el SDK de .NET 10
dotnet build -c Debug                 # desarrollo
dotnet publish -c Release             # single-file self-contained -> bin/Release/net10.0-windows/win-x64/publish/L2TitanLauncher.exe
```

## Configuración (`config.json`)

Orden de búsqueda: (1) carpeta del exe, (2) `%APPDATA%\Lineage2Launcher\`, (3) directorio actual.

```json
{
  "GamePath": "C:\\Juegos\\Lineage2",
  "ServerUrl": "https://downloads.l2-titan.com",
  "ManifestUrl": "https://downloads.l2-titan.com/manifest.json",
  "GameExecutable": "system\\L2.exe",
  "GameParameters": ""
}
```

`ServerUrl`/`ManifestUrl` deben ser `https://`. `GameExecutable` debe quedar dentro de `GamePath` (se valida contra path traversal).

## Servidor y despliegue

El servidor (nginx) sirve los archivos del cliente + `manifest.json` sobre HTTPS en `https://downloads.l2-titan.com`. Ver [`docs/architecture.md`](docs/architecture.md) (D9) y los scripts:

- `generate_manifest.py` — genera el manifiesto SHA-256 desde una carpeta del cliente.
- `deploy-simple.sh` — despliegue (requiere `zip` y Python locales).
- `setup-server-security.sh` — aprovisiona el VPS (usuario `deploy`, SSH por llave, sudo acotado, directorios web).

## Deuda técnica conocida

Ver la tabla en [`docs/architecture.md`](docs/architecture.md).

- ✅ **Firma del manifiesto** (clave pública embebida + verificación RSA) — implementado 2026-06-16 (`Services/ManifestSecurity.cs` + `sign-manifest.sh`).
- ✅ **Auto-actualización del launcher** — implementado 2026-06-16 (`Services/LauncherUpdater.cs` + `launcher.json` firmado + `publish-launcher.sh`).
- ✅ **Deploy unificado** con firma + verificación + rollback — `deploy.sh` (reemplaza a `deploy-simple.sh`).
