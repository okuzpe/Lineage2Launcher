using System;
using System.IO;
using Newtonsoft.Json;

namespace L2TitanLauncher.Services
{
    // Carga y resolución de config.json. La lógica de decisión (esquema https,
    // elección de GamePath) está en métodos estáticos puros para poder testearse;
    // la orquestación con disco vive en Resolve()/LoadRaw().
    internal sealed class ConfigService
    {
        public const string DefaultServerUrl = "https://downloads.l2-titan.com";
        public const string DefaultInstallPath = @"C:\Juegos\Lineage2";

        // PURO: fuerza https. Devuelve defaultUrl si el esquema no es https://.
        public static string EnforceHttps(string? url, string defaultUrl)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return defaultUrl;
            return url;
        }

        // PURO (toca FS solo para detectar cliente): elige GamePath por prioridad:
        // 1) carpeta del exe si es un cliente válido, 2) GamePath configurado si es válido,
        // 3) ruta de instalación por defecto.
        public static string ResolveGamePath(string? configuredGamePath, string exeDir)
        {
            if (PathSafety.LooksLikeLineageClient(exeDir))
                return exeDir;
            if (!string.IsNullOrWhiteSpace(configuredGamePath) && PathSafety.LooksLikeLineageClient(configuredGamePath!))
                return configuredGamePath!;
            return DefaultInstallPath;
        }

        // Orden de búsqueda: (1) carpeta del exe, (2) %APPDATA%\Lineage2Launcher\, (3) cwd.
        public static string? FindConfigFile()
        {
            string exeDir = AppContext.BaseDirectory ?? string.Empty;
            string configInExeDir = Path.Combine(exeDir, "config.json");
            if (File.Exists(configInExeDir))
                return configInExeDir;

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lineage2Launcher",
                "config.json"
            );
            if (File.Exists(appDataPath))
                return appDataPath;

            if (File.Exists("config.json"))
                return Path.GetFullPath("config.json");

            return null;
        }

        // Devuelve el LauncherConfig crudo del archivo encontrado (o null).
        public LauncherConfig? LoadRaw()
        {
            var configPath = FindConfigFile();
            if (configPath != null && File.Exists(configPath))
            {
                try { return JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath)); }
                catch { return null; }
            }
            return null;
        }

        // Resuelve la configuración efectiva (GamePath/ServerUrl/ManifestUrl), creando un
        // config.json por defecto si no existe. log() recibe los mensajes (los muestra el VM).
        public ResolvedConfig Resolve(Action<string> log)
        {
            var result = new ResolvedConfig
            {
                GamePath = DefaultInstallPath,
                ServerUrl = DefaultServerUrl,
                ManifestUrl = $"{DefaultServerUrl}/manifest.json"
            };

            try
            {
                string exeDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                string? configPath = FindConfigFile();

                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    var configuredGamePath = config?.GamePath;
                    var hasConfiguredPath = !string.IsNullOrWhiteSpace(configuredGamePath);

                    var resolvedGamePath = ResolveGamePath(configuredGamePath, exeDir);
                    result.GamePath = resolvedGamePath;

                    if (PathSafety.LooksLikeLineageClient(exeDir))
                    {
                        if (hasConfiguredPath && !string.Equals(configuredGamePath, resolvedGamePath, StringComparison.OrdinalIgnoreCase))
                            log($"Launcher folder detected as valid client. Using: {resolvedGamePath}");
                    }
                    else if (resolvedGamePath == DefaultInstallPath && hasConfiguredPath)
                    {
                        log($"Configured GamePath does not look like a Lineage client: {configuredGamePath}");
                        log($"Using default install path: {resolvedGamePath}");
                    }

                    var server = config?.ServerUrl ?? DefaultServerUrl;
                    var manifest = config?.ManifestUrl ?? $"{server}/manifest.json";

                    var secureServer = EnforceHttps(server, DefaultServerUrl);
                    if (secureServer != server)
                        log($"Insecure ServerUrl scheme rejected (must be https): {server}");
                    server = secureServer;

                    var secureManifest = EnforceHttps(manifest, $"{server}/manifest.json");
                    if (secureManifest != manifest)
                        log($"Insecure ManifestUrl scheme rejected (must be https): {manifest}");
                    manifest = secureManifest;

                    result.ServerUrl = server;
                    result.ManifestUrl = manifest;

                    log($"Configuration loaded from: {configPath}");
                    log($"  Server URL: {server}");
                    log($"  Manifest URL: {manifest}");
                    log($"  Game Path: {result.GamePath}");
                    if (PathSafety.IsProtectedSystemPath(result.GamePath))
                        log("Warning: Game path is inside Program Files/Windows. Downloads may fail without admin rights.");
                }
                else
                {
                    result.GamePath = PathSafety.LooksLikeLineageClient(exeDir) ? exeDir : DefaultInstallPath;

                    string appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Lineage2Launcher"
                    );
                    Directory.CreateDirectory(appDataPath);
                    string defaultConfigPath = Path.Combine(appDataPath, "config.json");

                    var defaultConfig = new LauncherConfig
                    {
                        GamePath = result.GamePath,
                        ServerUrl = result.ServerUrl,
                        ManifestUrl = result.ManifestUrl,
                        GameExecutable = "system\\L2.exe",
                        GameParameters = ""
                    };
                    File.WriteAllText(defaultConfigPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    log($"config.json file created at: {defaultConfigPath}");
                }
            }
            catch (Exception ex)
            {
                log($"Error loading configuration: {ex.Message}");
            }

            return result;
        }
    }
}
