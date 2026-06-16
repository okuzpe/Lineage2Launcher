using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace L2TitanLauncher.Services
{
    // Metadata de versión del launcher (launcher.json, FIRMADO con la misma clave RSA que el manifest).
    internal sealed class LauncherInfo
    {
        public string Version { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;     // URL https del .exe nuevo
        public string Sha256 { get; set; } = string.Empty;  // hash esperado del .exe
        public long Size { get; set; }
    }

    // Auto-actualización del PROPIO launcher.
    //
    // Cadena de confianza (misma que el manifest): clave pública embebida -> verifica
    // launcher.json.sig -> confía en launcher.json -> confía en el Sha256 del exe -> verifica
    // el exe descargado contra ese hash. Auto-ejecutar un exe descargado SIN esta verificación
    // sería RCE; por eso la firma es obligatoria.
    //
    // Es best-effort: cualquier fallo (sin red, 404, firma inválida, hash que no cuadra)
    // simplemente NO actualiza y deja seguir con la versión actual.
    internal sealed class LauncherUpdater
    {
        private readonly HttpClient _http;

        public LauncherUpdater(HttpClient http) { _http = http; }

        // Decisión pura testeable: ¿la versión del servidor es estrictamente mayor que la actual?
        public static bool IsNewer(string? serverVersion, Version current)
        {
            return Version.TryParse(serverVersion, out var sv) && sv > current;
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
                info = JsonConvert.DeserializeObject<LauncherInfo>(System.Text.Encoding.UTF8.GetString(bytes));
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }

            if (info == null || !IsNewer(info.Version, currentVersion))
                return false;
            if (string.IsNullOrWhiteSpace(info.Url) || !info.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(info.Sha256))
                return false;

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

            SpawnReplaceAndRelaunch(currentExe, newExe);
            return true;
        }

        // Un .exe en uso no puede sobrescribirse a sí mismo: lanzamos un .bat que espera a que
        // este proceso muera, reemplaza el exe y relanza el launcher.
        private static void SpawnReplaceAndRelaunch(string currentExe, string newExe)
        {
            int pid = Environment.ProcessId;
            var bat = Path.Combine(Path.GetTempPath(), "l2_update_" + Guid.NewGuid().ToString("N") + ".bat");
            var script =
                "@echo off\r\n" +
                ":wait\r\n" +
                $"tasklist /FI \"PID eq {pid}\" 2>nul | find \"{pid}\" >nul && (timeout /t 1 /nobreak >nul & goto wait)\r\n" +
                $"move /Y \"{newExe}\" \"{currentExe}\" >nul\r\n" +
                $"start \"\" \"{currentExe}\"\r\n" +
                "del \"%~f0\"\r\n";
            File.WriteAllText(bat, script);
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{bat}\"",
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
