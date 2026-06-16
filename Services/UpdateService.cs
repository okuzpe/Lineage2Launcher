using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace L2TitanLauncher.Services
{
    // Puente hacia la UI: el servicio no conoce WPF; reporta a través de estos callbacks
    // que el ViewModel mapea a sus propiedades (Progress/StatusText/estado del botón).
    internal interface IUpdateHost
    {
        void Log(string message);
        void SetProgress(int percent);
        void SetStatus(string status);
        void OnDownloadingStarted();        // VM: CurrentButtonState = Downloading; _isDownloadPaused = false
        bool IsPaused { get; }
        CancellationToken Token { get; }
    }

    // Verificación + descarga del cliente. Sin dependencias de WPF (testeable/aislado).
    // El comportamiento es el mismo que tenía MainViewModel.CheckAndDownloadFiles/DownloadFile.
    internal sealed class UpdateService
    {
        private readonly HttpClient _httpClient;

        public UpdateService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task RunAsync(string gamePath, string serverUrl, string manifestUrl, IUpdateHost host)
        {
            host.Log("=== Starting file check and download ===");
            host.Log($"Manifest URL: {manifestUrl}");
            host.Log($"Server URL: {serverUrl}");
            host.Log($"Game Path: {gamePath}");
            host.Log("Connecting to server...");
            host.SetStatus("Connecting to server...");

            string manifestJson = string.Empty;
            const int manifestRetries = 3;
            for (int i = 0; i < manifestRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        host.Log($"Retrying manifest download ({i}/{manifestRetries - 1})...");
                        await Task.Delay(2000 * i);
                    }

                    host.Log($"Downloading manifest from: {manifestUrl}");
                    // Descargar bytes crudos + firma RSA y VERIFICAR autenticidad antes de
                    // confiar en una sola entrada (defensa contra servidor comprometido / MITM).
                    var manifestBytes = await _httpClient.GetByteArrayAsync(manifestUrl, host.Token);
                    var signatureB64 = await _httpClient.GetStringAsync(manifestUrl + ".sig", host.Token);
                    if (!ManifestSecurity.Verify(manifestBytes, signatureB64))
                        throw new LauncherError("La firma del manifiesto no es válida. Actualización cancelada por seguridad (el servidor o la conexión no son de confianza). Contacta con soporte.");
                    manifestJson = System.Text.Encoding.UTF8.GetString(manifestBytes);
                    host.Log("✓ Firma del manifiesto verificada.");
                    break;
                }
                catch (Exception ex)
                {
                    // Firma inválida / manifiesto vacío (LauncherError) NO se reintenta.
                    if (ex is LauncherError) throw;
                    System.Diagnostics.Debug.WriteLine($"Manifest download attempt {i + 1} failed: {ex.Message}");
                    if (i == manifestRetries - 1)
                    {
                        string errorMessage = ex.Message.ToLower();
                        if (errorMessage.Contains("connection") || errorMessage.Contains("timeout") ||
                            errorMessage.Contains("network") || errorMessage.Contains("resolve") ||
                            errorMessage.Contains("refused") || errorMessage.Contains("unreachable"))
                        {
                            throw new LauncherError("Could not connect to server. Please check your internet connection and try again later.");
                        }
                        throw new LauncherError($"Could not download manifest after {manifestRetries} attempts: {ex.Message}");
                    }
                    host.Log($"Error downloading manifest: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(manifestJson))
                throw new LauncherError("Could not download manifest.");

            List<FileManifest> fileManifest;
            try
            {
                fileManifest = JsonConvert.DeserializeObject<List<FileManifest>>(manifestJson) ?? new List<FileManifest>();
            }
            catch (JsonException)
            {
                throw new LauncherError("The update manifest from the server is corrupted or invalid. Please try again later or contact support.");
            }

            if (fileManifest == null || fileManifest.Count == 0)
                throw new LauncherError("The manifest is empty or invalid.");

            host.Log($"✓ Manifest downloaded successfully.");
            host.Log($"Found {fileManifest.Count} files in manifest.");
            host.Log($"Game path: {gamePath}");

            if (!Directory.Exists(gamePath))
            {
                Directory.CreateDirectory(gamePath);
                host.Log($"Game directory created: {gamePath}");
            }

            int totalFiles = fileManifest.Count;
            int processedFiles = 0;
            int filesToDownload = 0;
            int filesOk = 0;
            long totalSizeToDownload = 0;

            // Recordar qué entradas hay que descargar para no re-hashear todo el cliente
            // una segunda vez en el segundo pase (I/O enorme en una instalación real).
            var entriesToDownload = new HashSet<FileManifest>();

            host.Log("\nVerifying local files...");
            host.SetStatus("Verifying files...");
            int verifiedFiles = 0;
            foreach (var fileInfo in fileManifest)
            {
                verifiedFiles++;
                var filePath = PathSafety.ResolveSafePath(gamePath, fileInfo.Path);

                var verifyProgress = (int)((verifiedFiles * 100) / totalFiles);
                host.SetProgress(verifyProgress);
                host.SetStatus($"Verifying files... {verifyProgress}% ({verifiedFiles}/{totalFiles})");

                if (!File.Exists(filePath))
                {
                    filesToDownload++;
                    totalSizeToDownload += fileInfo.Size;
                    entriesToDownload.Add(fileInfo);
                }
                else
                {
                    var localHash = FileIntegrity.ComputeSha256(filePath);
                    if (!string.Equals(localHash, fileInfo.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        filesToDownload++;
                        totalSizeToDownload += fileInfo.Size;
                        entriesToDownload.Add(fileInfo);
                    }
                    else
                    {
                        filesOk++;
                    }
                }
            }

            host.Log($"\nSummary:");
            host.Log($"  Files OK: {filesOk}");
            host.Log($"  Files to download: {filesToDownload}");
            if (totalSizeToDownload > 0)
                host.Log($"  Total size to download: {totalSizeToDownload / 1024 / 1024:F2} MB");

            if (filesToDownload == 0)
            {
                host.Log("\n✓ All files are up to date!");
                host.SetProgress(100);
                host.SetStatus("Everything ready to play!");
                return;
            }

            host.Log("\nStarting file download...");
            host.OnDownloadingStarted();

            // Conducir el progreso por bytes transferidos (no por conteo de archivos) para
            // que un único archivo grande no salte la barra a ~100% mientras aún baja.
            long downloadedSoFar = 0;

            foreach (var fileInfo in fileManifest)
            {
                while (host.IsPaused)
                {
                    await Task.Delay(100);
                }

                host.Token.ThrowIfCancellationRequested();
                processedFiles++;

                // Reusar el resultado de la verificación del primer pase en vez de re-hashear.
                if (!entriesToDownload.Contains(fileInfo))
                {
                    if (filesOk <= 10 || processedFiles % 10 == 0)
                    {
                        host.Log($"✓ {fileInfo.Path} - OK");
                    }
                    continue;
                }

                var filePath = PathSafety.ResolveSafePath(gamePath, fileInfo.Path);
                var fileUrl = $"{serverUrl}/{fileInfo.Path.Replace('\\', '/')}";

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await DownloadFile(fileUrl, filePath, fileInfo.Hash, downloadedSoFar, totalSizeToDownload, host);
                downloadedSoFar += fileInfo.Size;
            }

            host.Log("\n✓ Verification and download completed!");
            host.SetProgress(100);
            host.SetStatus("Everything ready to play!");
        }

        private async Task DownloadFile(string url, string filePath, string expectedHash, long bytesBeforeThisFile, long totalDownloadBytes, IUpdateHost host)
        {
            const int maxRetries = 3;
            const int idleTimeoutSeconds = 60; // cortar una conexión estancada en vez de colgar para siempre
            int retryCount = 0;
            Exception? lastException = null;

            // Stage en un .part para no truncar la copia buena existente y que una descarga
            // pausada/caída no deje un archivo final a medias.
            var tempPath = filePath + ".part";

            while (retryCount < maxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        host.Log($"Retry {retryCount}/{maxRetries - 1} for: {Path.GetFileName(filePath)}");
                        await Task.Delay(1000 * retryCount, host.Token);
                    }

                    host.Log($"Downloading: {Path.GetFileName(filePath)}");

                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, host.Token))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var downloadedBytes = 0L;
                        var startTime = DateTime.Now;
                        var fileName = Path.GetFileName(filePath);
                        int overallProgress = 0;

                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var httpStream = await response.Content.ReadAsStreamAsync(host.Token))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;
                            var lastUpdateTime = DateTime.Now;

                            while (true)
                            {
                                // Honrar la pausa a mitad de archivo (respetando cancelación).
                                while (host.IsPaused && !host.Token.IsCancellationRequested)
                                {
                                    await Task.Delay(100, host.Token);
                                }

                                // Timeout de inactividad deslizante: cancelar una lectura sin bytes.
                                using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(host.Token))
                                {
                                    readCts.CancelAfter(TimeSpan.FromSeconds(idleTimeoutSeconds));
                                    try
                                    {
                                        bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);
                                    }
                                    catch (OperationCanceledException) when (!host.Token.IsCancellationRequested)
                                    {
                                        throw new Exception("The connection stalled while downloading. Please try again.");
                                    }
                                }

                                if (bytesRead <= 0)
                                    break;

                                await fileStream.WriteAsync(buffer, 0, bytesRead, host.Token);
                                downloadedBytes += bytesRead;

                                // Progreso global por bytes transferidos en todos los archivos.
                                if (totalDownloadBytes > 0)
                                {
                                    var overallBytes = bytesBeforeThisFile + downloadedBytes;
                                    overallProgress = (int)Math.Min(100, (overallBytes * 100) / totalDownloadBytes);
                                    host.SetProgress(overallProgress);
                                }

                                if (totalBytes > 0)
                                {
                                    var fileProgress = (int)((downloadedBytes * 100) / totalBytes);

                                    var timeSinceUpdate = (DateTime.Now - lastUpdateTime).TotalMilliseconds;
                                    if (timeSinceUpdate >= 100 || downloadedBytes % 1048576 < 8192)
                                    {
                                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                        if (elapsed > 0)
                                        {
                                            var speed = downloadedBytes / elapsed / 1024;
                                            var overallBytes = bytesBeforeThisFile + downloadedBytes;
                                            var overallSpeed = overallBytes / elapsed;
                                            var etaText = "";
                                            // Clamp: un manifiesto obsoleto puede hacer que los bytes reales
                                            // superen el estimado y dar un ETA negativo (engañoso).
                                            var remainingBytes = Math.Max(0, totalDownloadBytes - overallBytes);
                                            if (remainingBytes > 0 && overallSpeed > 0)
                                            {
                                                var etaSeconds = remainingBytes / overallSpeed;
                                                etaText = $" - ETA {TimeSpan.FromSeconds(etaSeconds):mm\\:ss}";
                                            }
                                            host.SetStatus($"Downloading... {overallProgress}% - {fileName} ({fileProgress}%) - {speed:F0} KB/s{etaText}");
                                        }
                                        else
                                        {
                                            host.SetStatus($"Downloading... {overallProgress}% - {fileName} ({fileProgress}%)");
                                        }
                                        lastUpdateTime = DateTime.Now;
                                    }
                                }
                            }
                        }
                    }

                    host.Log($"Verifying integrity: {Path.GetFileName(filePath)}");
                    var downloadedHash = FileIntegrity.ComputeSha256(tempPath);
                    if (!string.Equals(downloadedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                        // Los mismos bytes fallarán igual al reintentar: problema del servidor.
                        throw new HashMismatchException();
                    }

                    // Mover atómicamente el .part verificado a su ubicación final.
                    File.Move(tempPath, filePath, true);

                    host.Log($"✓ Downloaded and verified: {Path.GetFileName(filePath)}");
                    return;
                }
                catch (OperationCanceledException) when (host.Token.IsCancellationRequested)
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    throw;
                }
                catch (HashMismatchException)
                {
                    host.Log($"✗ Hash mismatch (server file appears corrupted): {Path.GetFileName(filePath)}");
                    throw new LauncherError(
                        $"Server file corrupted ({Path.GetFileName(filePath)}). Retrying won't help - please contact support.");
                }
                catch (Exception ex)
                {
                    if (ex is UnauthorizedAccessException ||
                        ex.Message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new LauncherError(
                            "No write permission in game folder. Move the game outside Program Files " +
                            "(for example C:\\Games\\L2) or run the launcher as Administrator.");
                    }

                    if (ex is IOException && (ex.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase) ||
                                              ex.Message.Contains("enough space on the disk", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                        throw new LauncherError("Not enough disk space to download updates. Free up space and try again.");
                    }

                    if (ex is IOException && ex.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                        throw new LauncherError("Please close the game before updating, then click RETRY.");
                    }

                    lastException = ex;
                    retryCount++;

                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }

                    if (retryCount >= maxRetries)
                    {
                        host.Log($"✗ Error after {maxRetries} attempts: {Path.GetFileName(filePath)}");
                        throw new LauncherError($"Error downloading {Path.GetFileName(filePath)} after {maxRetries} attempts: {ex.Message}");
                    }
                }
            }

            throw lastException ?? new Exception($"Unknown error downloading {Path.GetFileName(filePath)}");
        }
    }
}
