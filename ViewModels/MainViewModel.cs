using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Lineage2Launcher.ViewModels
{
    public enum ButtonState
    {
        Checking,
        Downloading,
        Paused,
        Ready,
        Disabled
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private int _progress;
        private string _statusText = "Ready to verify...";
        private ButtonState _currentButtonState = ButtonState.Disabled;
        private bool _isDownloadPaused = false;
        private string _gamePath = string.Empty;
        private string _serverUrl = string.Empty;
        private string _manifestUrl = string.Empty;
        private List<FileManifest> _fileManifest = new();
        private HttpClient _httpClient = new();

        public ObservableCollection<ServerViewModel> Servers { get; } = new();
        public ObservableCollection<NewsItem> News { get; } = new();
        public ServerRatesViewModel ServerRates { get; } = new();

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ButtonState CurrentButtonState
        {
            get => _currentButtonState;
            set
            {
                if (_currentButtonState != value)
                {
                    _currentButtonState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ButtonText));
                    OnPropertyChanged(nameof(IsPlayEnabled));
                }
            }
        }

        public string ButtonText
        {
            get
            {
                return CurrentButtonState switch
                {
                    ButtonState.Checking => "🔍 COMPROBANDO...",
                    ButtonState.Downloading => "⏸ PAUSAR",
                    ButtonState.Paused => "▶ REANUDAR",
                    ButtonState.Ready => "▶ JUGAR",
                    _ => "⏸ ESPERANDO..."
                };
            }
        }

        public bool IsPlayEnabled => CurrentButtonState == ButtonState.Ready || 
                                     CurrentButtonState == ButtonState.Downloading || 
                                     CurrentButtonState == ButtonState.Paused;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand ResumeCommand { get; }
        public ICommand OpenSupportCommand { get; }
        public ICommand OpenForumCommand { get; }
        public ICommand OpenDiscordCommand { get; }

        public MainViewModel()
        {
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            LoadConfiguration();
            InitializeMockData();
            
            PlayCommand = new RelayCommand(async () => await HandlePlayAction(), () => IsPlayEnabled);
            PauseCommand = new RelayCommand(() => { _isDownloadPaused = true; CurrentButtonState = ButtonState.Paused; AddLog("Download paused by user."); });
            ResumeCommand = new RelayCommand(() => { _isDownloadPaused = false; CurrentButtonState = ButtonState.Downloading; AddLog("Download resumed by user."); });
            OpenSupportCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://support.example.com", UseShellExecute = true }));
            OpenForumCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://forum.example.com", UseShellExecute = true }));
            OpenDiscordCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://discord.gg/example", UseShellExecute = true }));
            
            // Auto-start verification
            _ = StartAutoVerification();
        }

        private void InitializeMockData()
        {
            // Mock Servers
            Servers.Add(new ServerViewModel { Name = "L2Titan - Main Server", Status = ServerStatus.Online });
            Servers.Add(new ServerViewModel { Name = "L2Titan - PvP Server", Status = ServerStatus.Online });
            Servers.Add(new ServerViewModel { Name = "L2Titan - Test Server", Status = ServerStatus.Maintenance });
            Servers.Add(new ServerViewModel { Name = "L2Titan - Classic", Status = ServerStatus.Online });
            Servers.Add(new ServerViewModel { Name = "L2Titan - Retro", Status = ServerStatus.Offline });

            // Mock News
            News.Add(new NewsItem 
            { 
                Date = DateTime.Now.AddDays(-2), 
                Title = "New Update Available!", 
                Summary = "Check out the latest features and improvements in version 2.0",
                ImageUrl = ""
            });
            News.Add(new NewsItem 
            { 
                Date = DateTime.Now.AddDays(-5), 
                Title = "Server Maintenance", 
                Summary = "Scheduled maintenance completed successfully. All servers are now online.",
                ImageUrl = ""
            });
            News.Add(new NewsItem 
            { 
                Date = DateTime.Now.AddDays(-10), 
                Title = "Welcome to L2Titan", 
                Summary = "Join thousands of players in the epic world of Lineage 2",
                ImageUrl = ""
            });
        }

        private async Task HandlePlayAction()
        {
            if (CurrentButtonState == ButtonState.Ready)
            {
                await StartGame();
            }
            else if (CurrentButtonState == ButtonState.Downloading)
            {
                PauseCommand.Execute(null);
            }
            else if (CurrentButtonState == ButtonState.Paused)
            {
                ResumeCommand.Execute(null);
            }
            else
            {
                Progress = 0;
                CurrentButtonState = ButtonState.Checking;
                try
                {
                    await CheckAndDownloadFiles();
                    CurrentButtonState = ButtonState.Ready;
                    StatusText = "Everything ready to play!";
                }
                catch (Exception ex)
                {
                    AddLog($"Error: {ex.Message}");
                    MessageBox.Show($"Error during verification: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    CurrentButtonState = ButtonState.Disabled;
                }
            }
        }

        private async Task StartAutoVerification()
        {
            Progress = 0;
            CurrentButtonState = ButtonState.Checking;
            try
            {
                await CheckAndDownloadFiles();
                CurrentButtonState = ButtonState.Ready;
                StatusText = "Everything ready to play!";
            }
            catch (Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                CurrentButtonState = ButtonState.Disabled;
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                // Use AppContext.BaseDirectory for single-file apps compatibility
                string exeDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                
                string? configPath = FindConfigFile();
                
                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    _gamePath = exeDir;
                    _serverUrl = config?.ServerUrl ?? "http://tu-servidor.com/lineage2";
                    _manifestUrl = config?.ManifestUrl ?? $"{_serverUrl}/manifest.json";
                }
                else
                {
                    _gamePath = exeDir;
                    _serverUrl = "http://tu-servidor.com/lineage2";
                    _manifestUrl = $"{_serverUrl}/manifest.json";
                    
                    string appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Lineage2Launcher"
                    );
                    Directory.CreateDirectory(appDataPath);
                    string defaultConfigPath = Path.Combine(appDataPath, "config.json");
                    
                    var defaultConfig = new LauncherConfig
                    {
                        GamePath = _gamePath,
                        ServerUrl = _serverUrl,
                        ManifestUrl = _manifestUrl,
                        GameExecutable = "system\\L2.exe",
                        GameParameters = ""
                    };
                    File.WriteAllText(defaultConfigPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    AddLog($"config.json file created at: {defaultConfigPath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error loading configuration: {ex.Message}");
            }
        }

        private string? FindConfigFile()
        {
            // Use AppContext.BaseDirectory for single-file apps compatibility
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

        private async Task CheckAndDownloadFiles()
        {
            AddLog("Connecting to server...");
            StatusText = "Connecting to server...";

            string manifestJson = string.Empty;
            const int manifestRetries = 3;
            for (int i = 0; i < manifestRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        AddLog($"Retrying manifest download ({i}/{manifestRetries - 1})...");
                        await Task.Delay(2000 * i);
                    }

                    AddLog($"Downloading manifest from: {_manifestUrl}");
                    manifestJson = await _httpClient.GetStringAsync(_manifestUrl);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == manifestRetries - 1)
                        throw new Exception($"Could not download manifest after {manifestRetries} attempts: {ex.Message}");
                    AddLog($"Error downloading manifest: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(manifestJson))
                throw new Exception("Could not download manifest.");

            _fileManifest = JsonConvert.DeserializeObject<List<FileManifest>>(manifestJson) ?? new List<FileManifest>();

            if (_fileManifest == null || _fileManifest.Count == 0)
                throw new Exception("The manifest is empty or invalid.");

            AddLog($"✓ Manifest downloaded successfully.");
            AddLog($"Found {_fileManifest.Count} files in manifest.");
            AddLog($"Game path: {_gamePath}");

            if (!Directory.Exists(_gamePath))
            {
                Directory.CreateDirectory(_gamePath);
                AddLog($"Game directory created: {_gamePath}");
            }

            int totalFiles = _fileManifest.Count;
            int processedFiles = 0;
            int filesToDownload = 0;
            int filesOk = 0;
            long totalSizeToDownload = 0;

            AddLog("\nVerifying local files...");
            foreach (var fileInfo in _fileManifest)
            {
                var filePath = Path.Combine(_gamePath, fileInfo.Path);
                if (!File.Exists(filePath))
                {
                    filesToDownload++;
                    totalSizeToDownload += fileInfo.Size;
                }
                else
                {
                    var localHash = CalculateFileHash(filePath);
                    if (localHash != fileInfo.Hash)
                    {
                        filesToDownload++;
                        totalSizeToDownload += fileInfo.Size;
                    }
                    else
                    {
                        filesOk++;
                    }
                }
            }

            AddLog($"\nSummary:");
            AddLog($"  Files OK: {filesOk}");
            AddLog($"  Files to download: {filesToDownload}");
            if (totalSizeToDownload > 0)
                AddLog($"  Total size to download: {totalSizeToDownload / 1024 / 1024:F2} MB");

            if (filesToDownload == 0)
            {
                AddLog("\n✓ All files are up to date!");
                return;
            }

            AddLog("\nStarting file download...");
            CurrentButtonState = ButtonState.Downloading;
            _isDownloadPaused = false;

            foreach (var fileInfo in _fileManifest)
            {
                while (_isDownloadPaused)
                {
                    await Task.Delay(100);
                }
                
                processedFiles++;
                var filePath = Path.Combine(_gamePath, fileInfo.Path);
                var fileUrl = $"{_serverUrl}/{fileInfo.Path.Replace('\\', '/')}";

                StatusText = $"Verifying: {fileInfo.Path} ({processedFiles}/{totalFiles})";
                Progress = (int)((processedFiles * 100) / totalFiles);

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool needsDownload = false;

                if (!File.Exists(filePath))
                {
                    needsDownload = true;
                }
                else
                {
                    var localHash = CalculateFileHash(filePath);
                    if (localHash != fileInfo.Hash)
                    {
                        needsDownload = true;
                    }
                    else
                    {
                        if (filesOk <= 10 || processedFiles % 10 == 0)
                        {
                            AddLog($"✓ {fileInfo.Path} - OK");
                        }
                    }
                }

                if (needsDownload)
                {
                    await DownloadFile(fileUrl, filePath, fileInfo.Hash);
                }
            }

            AddLog("\n✓ Verification and download completed!");
        }

        private async Task DownloadFile(string url, string filePath, string expectedHash)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        AddLog($"Retry {retryCount}/{maxRetries - 1} for: {Path.GetFileName(filePath)}");
                        await Task.Delay(1000 * retryCount);
                    }

                    AddLog($"Downloading: {Path.GetFileName(filePath)}");
                    StatusText = $"Downloading: {Path.GetFileName(filePath)}";

                    var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    var downloadedBytes = 0L;
                    var startTime = DateTime.Now;

                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var httpStream = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[8192];
                        int bytesRead;

                        while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);
                            downloadedBytes += bytesRead;

                            if (totalBytes > 0)
                            {
                                var progress = (int)((downloadedBytes * 100) / totalBytes);
                                Progress = Math.Min(progress, 100);

                                if (downloadedBytes % 1048576 < 8192)
                                {
                                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                    if (elapsed > 0)
                                    {
                                        var speed = downloadedBytes / elapsed / 1024;
                                        StatusText = $"Downloading: {Path.GetFileName(filePath)} ({speed:F0} KB/s)";
                                    }
                                }
                            }
                        }
                    }

                    AddLog($"Verifying integrity: {Path.GetFileName(filePath)}");
                    var downloadedHash = CalculateFileHash(filePath);
                    if (downloadedHash != expectedHash)
                    {
                        File.Delete(filePath);
                        throw new Exception($"Downloaded file does not match expected hash.");
                    }

                    AddLog($"✓ Downloaded and verified: {Path.GetFileName(filePath)} ({(totalBytes > 0 ? $"{totalBytes / 1024 / 1024:F2} MB" : "unknown size")})");
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    retryCount++;

                    if (File.Exists(filePath))
                    {
                        try { File.Delete(filePath); } catch { }
                    }

                    if (retryCount >= maxRetries)
                    {
                        AddLog($"✗ Error after {maxRetries} attempts: {Path.GetFileName(filePath)}");
                        throw new Exception($"Error downloading {Path.GetFileName(filePath)} after {maxRetries} attempts: {ex.Message}");
                    }
                }
            }

            throw lastException ?? new Exception($"Unknown error downloading {Path.GetFileName(filePath)}");
        }

        private static string CalculateFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private async Task StartGame()
        {
            try
            {
                var exePath = Path.Combine(_gamePath, "system", "L2.exe");
                
                AddLog($"Looking for game executable: {exePath}");

                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Game executable not found: {exePath}\n\nMake sure the game files are downloaded and the executable exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog($"✗ Game executable not found: {exePath}");
                    return;
                }

                AddLog($"Starting game: {exePath}");
                StatusText = "Starting game...";

                string gameParameters = "";
                string? configPath = FindConfigFile();
                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    gameParameters = config?.GameParameters ?? "";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _gamePath,
                    Arguments = gameParameters,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try
                {
                    Process.Start(startInfo);
                    AddLog("Game started!");
                }
                catch (System.ComponentModel.Win32Exception winEx)
                {
                    if (winEx.NativeErrorCode == 1223)
                    {
                        AddLog("Game launch cancelled by user.");
                        MessageBox.Show("Game launch was cancelled. The game requires administrator privileges to run.", "Launch Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += $"\n\nInner exception: {ex.InnerException.Message}";
                
                MessageBox.Show($"Error starting game: {errorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"Error: {errorMessage}");
            }
        }

        private readonly List<string> _logMessages = new();
        public string LogText => string.Join("\n", _logMessages);

        private void AddLog(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                if (_logMessages.Count > 100)
                    _logMessages.RemoveAt(0);
                OnPropertyChanged(nameof(LogText));
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LauncherConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public string ManifestUrl { get; set; } = string.Empty;
        public string GameExecutable { get; set; } = string.Empty;
        public string GameParameters { get; set; } = string.Empty;
    }

    public class FileManifest
    {
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
    }
}

