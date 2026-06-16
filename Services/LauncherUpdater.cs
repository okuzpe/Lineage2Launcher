using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace L2TitanLauncher.Services
{
    // Metadata de versión del launcher (launcher.json, FIRMADO con la misma clave RSA que el manifest).
    internal sealed class LauncherInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;     // URL https del .exe nuevo (mismo host que launcher.json)
        public string Sha256 { get; set; } = string.Empty;  // hash esperado del .exe
        public long Size { get; set; }
    }

    // Recuerda la versión más alta del launcher vista, para impedir downgrade a una versión
    // antigua (aunque esté firmada legítimamente). Best-effort en %LOCALAPPDATA%.
    internal static class VersionStore
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "L2TitanLauncher", "launcher-version.txt");

        public static Version? ReadHighest()
        {
            try { return Version.TryParse(File.ReadAllText(FilePath).Trim(), out var v) ? v : null; }
            catch { return null; }
        }

        public static void Persist(string version)
        {
            try
            {
                if (!Version.TryParse(version, out var v)) return;
                var cur = ReadHighest();
                if (cur != null && cur >= v) return;
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, v.ToString());
            }
            catch { }
        }
    }

    // Auto-actualización del PROPIO launcher.
    //
    // Cadena de confianza (misma que el manifest): clave pública embebida -> verifica
    // launcher.json.sig -> confía en launcher.json -> confía en el Sha256 del exe -> verifica
    // el exe descargado contra ese hash. Auto-ejecutar un exe descargado SIN esta verificación
    // sería RCE; por eso la firma es obligatoria. Además: el exe debe venir del mismo host https
    // que el launcher.json (anti-SSRF) y no se permite downgrade (anti-rollback vía VersionStore).
    //
    // Best-effort: cualquier fallo (sin red, 404, firma inválida, host distinto, hash que no
    // cuadra) NO actualiza y deja seguir con la versión actual.
    internal sealed class LauncherUpdater
    {
        private readonly HttpClient _http;

        public LauncherUpdater(HttpClient http) { _http = http; }

        // Decisión pura testeable: ¿la versión del servidor es estrictamente mayor que la actual?
        public static bool IsNewer(string? serverVersion, Version current)
        {
            return Version.TryParse(serverVersion, out var sv) && sv > current;
        }

        // El exe debe servirse por https desde el MISMO host que el launcher.json firmado.
        public static bool IsSameHttpsHost(string? url, string referenceUrl)
        {
            try
            {
                var u = new Uri(url!);
                var r = new Uri(referenceUrl);
                return u.Scheme == Uri.UriSchemeHttps
                    && string.Equals(u.Host, r.Host, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // Devuelve true si se lanzó un reemplazo (el llamador DEBE cerrar la app para que proceda).
        public async Task<bool> CheckAndUpdateAsync(string launcherInfoUrl, Version currentVersion, Action<string> status, CancellationToken token)
        {
            LauncherInfo? info;
            try
            {
                var bytes = await _http.GetByteArrayAsync(launcherInfoUrl, token);
                var sig = await _http.GetStringAsync(launcherInfoUrl + ".sig", token);
                if (!ManifestSecurity.Verify(bytes, sig))
                    return false; // metadata no confiable -> no tocar nada
                info = JsonConvert.DeserializeObject<LauncherInfo>(Encoding.UTF8.GetString(bytes));
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }

            if (info == null || !IsNewer(info.Version, currentVersion))
                return false;
            if (string.IsNullOrWhiteSpace(info.Sha256))
                return false;
            if (!IsSameHttpsHost(info.Url, launcherInfoUrl))
                return false; // el exe debe venir del mismo host https firmado (anti-SSRF)

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
                return false;
            var newExe = currentExe + ".new";

            try
            {
                status($"Descargando actualización del launcher (v{info.Version})...");
                using (var resp = await _http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    resp.EnsureSuccessStatusCode();
                    using var fs = new FileStream(newExe, FileMode.Create, FileAccess.Write, FileShare.None);
                    using var s = await resp.Content.ReadAsStreamAsync(token);
                    await s.CopyToAsync(fs, token);
                }

                // El exe debe coincidir con el hash que vouchó el launcher.json firmado.
                if (!string.Equals(FileIntegrity.ComputeSha256(newExe), info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(newExe);
                    return false;
                }
            }
            catch (OperationCanceledException) { TryDelete(newExe); throw; }
            catch { TryDelete(newExe); return false; }

            VersionStore.Persist(info.Version); // anti-rollback: no volver a aceptar versiones menores
            SpawnReplaceAndRelaunch(currentExe, newExe);
            return true;
        }

        // Un .exe en uso no puede sobrescribirse a sí mismo: lanzamos un script PowerShell que
        // espera (por PID exacto, con timeout) a que este proceso muera, hace backup, reemplaza
        // el exe y relanza. PowerShell con literales entre comillas simples maneja rutas con
        // %, &, espacios sin inyección. Vive en %LOCALAPPDATA% (per-usuario), no en %TEMP%.
        private static void SpawnReplaceAndRelaunch(string currentExe, string newExe)
        {
            int pid = Environment.ProcessId;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "L2TitanLauncher");
            Directory.CreateDirectory(dir);
            var ps1 = Path.Combine(dir, "update.ps1");

            static string Q(string s) => "'" + s.Replace("'", "''") + "'"; // literal PowerShell seguro

            var script =
                "$ErrorActionPreference = 'SilentlyContinue'\r\n" +
                $"$cur = {Q(currentExe)}\r\n" +
                $"$new = {Q(newExe)}\r\n" +
                "$log = Join-Path $env:LOCALAPPDATA 'L2TitanLauncher\\update.log'\r\n" +
                $"try {{ Wait-Process -Id {pid} -Timeout 60 }} catch {{}}\r\n" +
                "try {\r\n" +
                "    Copy-Item -LiteralPath $cur -Destination ($cur + '.bak') -Force\r\n" +
                "    Move-Item -LiteralPath $new -Destination $cur -Force\r\n" +
                "    Start-Process -FilePath $cur\r\n" +
                "} catch {\r\n" +
                "    Add-Content -LiteralPath $log -Value ('[' + (Get-Date) + '] update failed: ' + $_.Exception.Message)\r\n" +
                "    if (Test-Path -LiteralPath ($cur + '.bak')) { Copy-Item -LiteralPath ($cur + '.bak') -Destination $cur -Force }\r\n" +
                "    Start-Process -FilePath $cur\r\n" +
                "} finally {\r\n" +
                "    Remove-Item -LiteralPath $new -Force\r\n" +
                "    Remove-Item -LiteralPath ($cur + '.bak') -Force\r\n" +
                "}\r\n";

            File.WriteAllText(ps1, script);
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
