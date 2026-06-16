using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json;

namespace L2TitanLauncher.ViewModels
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
        private string _statusText = "Ready to verify";
        private ButtonState _currentButtonState = ButtonState.Disabled;
        private bool _isDownloadPaused = false;
        private string _gamePath = string.Empty;
        private string _serverUrl = string.Empty;
        private string _manifestUrl = string.Empty;
        private List<FileManifest> _fileManifest = new();
        private HttpClient _httpClient = new();
        private readonly System.Threading.CancellationTokenSource _cts = new();

        // Cached brushes - updated when state changes
        private Brush _buttonBackgroundBrush = Brushes.Transparent;
        private Brush _buttonBorderBrush = Brushes.Transparent;
        private Brush _buttonForegroundBrush = Brushes.Transparent;

        public string VersionText
        {
            get
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"LAUNCHER v.{version.Major}.{version.Minor}.{version.Build}" : "LAUNCHER";
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    RaiseOnUi(nameof(Progress));
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
                    RaiseOnUi(nameof(StatusText));
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

                    // Update brushes when state changes - do this on UI thread
                    // IMPORTANT: Update brushes first, then notify property changes
                    // All change notifications are raised here so they run on the UI thread
                    var app = Application.Current;
                    if (app == null)
                        return;

                    app.Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(CurrentButtonState));
                        OnPropertyChanged(nameof(ButtonText));
                        OnPropertyChanged(nameof(IsPlayEnabled));
                        OnPropertyChanged(nameof(IsPaused));
                        OnPropertyChanged(nameof(IsReady));

                        // Store old brushes to force change detection
                        var oldBg = _buttonBackgroundBrush;
                        var oldBorder = _buttonBorderBrush;
                        var oldFg = _buttonForegroundBrush;

                        // Update brushes
                        UpdateButtonBrushes();

                        // Force property change notifications explicitly
                        // This ensures WPF re-evaluates the bindings
                        OnPropertyChanged(nameof(ButtonBackgroundBrush));
                        OnPropertyChanged(nameof(ButtonBorderBrush));
                        OnPropertyChanged(nameof(ButtonForegroundBrush));

                        // Also notify dependent properties
                        OnPropertyChanged(nameof(IsReady));
                        OnPropertyChanged(nameof(IsPaused));
                    }, System.Windows.Threading.DispatcherPriority.Render);

                    // Force UI update when state changes to Ready - do it again with higher priority
                    if (value == ButtonState.Ready)
                    {
                        app.Dispatcher.Invoke(() =>
                        {
                            // Force another update to ensure the UI reflects the Ready state
                            UpdateButtonBrushes();
                            OnPropertyChanged(nameof(ButtonBackgroundBrush));
                            OnPropertyChanged(nameof(ButtonBorderBrush));
                            OnPropertyChanged(nameof(ButtonForegroundBrush));
                            OnPropertyChanged(nameof(IsReady));
                            OnPropertyChanged(nameof(IsPaused));
                        }, System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
            }
        }

        public string ButtonText
        {
            get
            {
                return CurrentButtonState switch
                {
                    ButtonState.Checking => "⏳ CHECKING...",
                    ButtonState.Downloading => "⏸ PAUSE",
                    ButtonState.Paused => "▶ RESUME",
                    ButtonState.Ready => "▶ PLAY",
                    _ => "▶ RETRY"
                };
            }
        }

        // Botón habilitado cuando está Ready, Downloading, Paused o Disabled (NO cuando está Checking)
        // Permite reintentar cuando falla la auto-verificación
        public bool IsPlayEnabled => CurrentButtonState == ButtonState.Ready ||
                                     CurrentButtonState == ButtonState.Downloading ||
                                     CurrentButtonState == ButtonState.Paused ||
                                     CurrentButtonState == ButtonState.Disabled;

        // Propiedad para detectar si está pausado (para aplicar estilo gris)
        public bool IsPaused => CurrentButtonState == ButtonState.Paused;

        // Propiedad para detectar si está listo (para aplicar estilo dorado)
        public bool IsReady => CurrentButtonState == ButtonState.Ready;

        // Brush properties for button styling - return appropriate brushes based on state
        public Brush ButtonBackgroundBrush
        {
            get => _buttonBackgroundBrush;
            private set
            {
                if (_buttonBackgroundBrush != value)
                {
                    _buttonBackgroundBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public Brush ButtonBorderBrush
        {
            get => _buttonBorderBrush;
            private set
            {
                if (_buttonBorderBrush != value)
                {
                    _buttonBorderBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        public Brush ButtonForegroundBrush
        {
            get => _buttonForegroundBrush;
            private set
            {
                if (_buttonForegroundBrush != value)
                {
                    _buttonForegroundBrush = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateButtonBrushes()
        {
            // Get new brushes
            var newBg = GetBackgroundBrushForState(CurrentButtonState);
            var newBorder = GetBorderBrushForState(CurrentButtonState);
            var newFg = GetForegroundBrushForState(CurrentButtonState);

            // Always update, even if the brush looks the same, to force WPF to re-evaluate
            // This is important because WPF may cache brush references
            _buttonBackgroundBrush = newBg;
            _buttonBorderBrush = newBorder;
            _buttonForegroundBrush = newFg;

            // The OnPropertyChanged will be called explicitly after this method
        }

        private static LinearGradientBrush MakeVerticalGradient(Color top, Color bottom)
        {
            var b = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(top, 0),
                new GradientStop(bottom, 1)
            }, new Point(0, 0), new Point(0, 1));
            b.Freeze();
            return b;
        }

        // Red shades: bright for idle/error, slightly darker for active, darkest for paused
        private static readonly LinearGradientBrush RedBright   = MakeVerticalGradient(Color.FromRgb(0xEF, 0x3B, 0x36), Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly LinearGradientBrush RedActive   = MakeVerticalGradient(Color.FromRgb(0xD3, 0x2F, 0x2F), Color.FromRgb(0xB7, 0x1C, 0x1C));
        private static readonly LinearGradientBrush RedDark     = MakeVerticalGradient(Color.FromRgb(0xB7, 0x1C, 0x1C), Color.FromRgb(0x8E, 0x12, 0x12));

        private static readonly LinearGradientBrush RedBrightBorder = MakeVerticalGradient(Color.FromRgb(0xFF, 0x80, 0x72), Color.FromRgb(0xEF, 0x3B, 0x36));
        private static readonly LinearGradientBrush RedActiveBorder = MakeVerticalGradient(Color.FromRgb(0xEF, 0x5B, 0x50), Color.FromRgb(0xC6, 0x28, 0x28));
        private static readonly LinearGradientBrush RedDarkBorder   = MakeVerticalGradient(Color.FromRgb(0xD3, 0x2F, 0x2F), Color.FromRgb(0x8E, 0x12, 0x12));

        private Brush GetBackgroundBrushForState(ButtonState state)
        {
            switch (state)
            {
                case ButtonState.Ready:
                    // Apple green — "GO, ready to play"
                    var readyBg = (Brush)Application.Current.FindResource("PlayButtonGradientBrush");
                    if (readyBg is LinearGradientBrush lgb) { var c = lgb.Clone(); c.Freeze(); return c; }
                    return readyBg;
                case ButtonState.Downloading:
                    return RedActive;   // darker red — downloading (shows PAUSE)
                case ButtonState.Paused:
                    return RedDark;     // darkest red — paused (shows RESUME)
                case ButtonState.Checking:
                    return RedBright;   // bright red — verifying (disabled, shows CHECKING)
                default:
                    return RedBright;   // bright red — idle/error (shows RETRY)
            }
        }

        private Brush GetBorderBrushForState(ButtonState state)
        {
            switch (state)
            {
                case ButtonState.Ready:
                    var readyBorder = (Brush)Application.Current.FindResource("GoldBorderGradientBrush");
                    if (readyBorder is LinearGradientBrush lgb) { var c = lgb.Clone(); c.Freeze(); return c; }
                    return readyBorder;
                case ButtonState.Downloading:
                    return RedActiveBorder;
                case ButtonState.Paused:
                    return RedDarkBorder;
                case ButtonState.Checking:
                    return RedBrightBorder;
                default:
                    return RedBrightBorder;
            }
        }

        private static Brush GetForegroundBrushForState(ButtonState state)
        {
            var fg = new SolidColorBrush(Colors.White);
            fg.Freeze();
            return fg;
        }

        public ICommand PlayCommand { get; }
        public ICommand OpenSupportCommand { get; }
        public ICommand OpenDiscordCommand { get; }
        public ICommand OpenTikTokCommand { get; }

        public MainViewModel()
        {
            _httpClient.Timeout = TimeSpan.FromMinutes(30);
            LoadConfiguration();

            // Initialize button brushes with default state
            UpdateButtonBrushes();

            // Usar AsyncRelayCommand para manejar correctamente métodos async
            PlayCommand = new AsyncRelayCommand(async () => await HandlePlayAction(), () => IsPlayEnabled);
            OpenSupportCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://l2-titan.com/", UseShellExecute = true }));
            OpenDiscordCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://discord.gg/xH76F9vsGf", UseShellExecute = true }));
            OpenTikTokCommand = new RelayCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://www.tiktok.com/@omar781002", UseShellExecute = true }));

            // Auto-start verification - no bloquear si falla
            _ = Task.Run(async () =>
            {
                try
                {
                    await StartAutoVerification();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AddLog($"Auto-verification failed: {ex.Message}");
                    string errorMessage = ex.Message.ToLower();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (errorMessage.Contains("could not connect") || errorMessage.Contains("connection"))
                        {
                            CurrentButtonState = ButtonState.Disabled;
                            StatusText = "Could not connect to server. Please try again later.";
                        }
                        else
                        {
                            CurrentButtonState = ButtonState.Disabled;
                            StatusText = "Auto-verification failed. Click RETRY to try again.";
                        }
                    });
                }
            });
        }

        private async Task HandlePlayAction()
        {
            AddLog("=== Play button clicked ===");
            System.Diagnostics.Debug.WriteLine("Play button clicked - HandlePlayAction started");

            try
            {
                if (CurrentButtonState == ButtonState.Ready)
                {
                    AddLog("Starting game...");
                    System.Diagnostics.Debug.WriteLine("Current state: Ready - Starting game");
                    await StartGame();
                }
                else if (CurrentButtonState == ButtonState.Downloading)
                {
                    AddLog("Pausing download...");
                    System.Diagnostics.Debug.WriteLine("Current state: Downloading - Pausing");
                    _isDownloadPaused = true;
                    CurrentButtonState = ButtonState.Paused;
                    StatusText = "Download paused";
                    AddLog("Download paused by user.");
                }
                else if (CurrentButtonState == ButtonState.Paused)
                {
                    AddLog("Resuming download...");
                    System.Diagnostics.Debug.WriteLine("Current state: Paused - Resuming");
                    _isDownloadPaused = false;
                    CurrentButtonState = ButtonState.Downloading;
                    StatusText = "Downloading...";
                    AddLog("Download resumed by user.");
                }
                else if (CurrentButtonState == ButtonState.Disabled)
                {
                    AddLog("Starting verification and download process...");
                    System.Diagnostics.Debug.WriteLine($"Current state: {CurrentButtonState} - Starting verification");
                    System.Diagnostics.Debug.WriteLine($"Manifest URL: {_manifestUrl}");
                    System.Diagnostics.Debug.WriteLine($"Game Path: {_gamePath}");

                    Progress = 0;
                    CurrentButtonState = ButtonState.Checking;
                    StatusText = "Connecting to server...";

                    try
                    {
                        // Run verification/hashing on a background thread. This command
                        // executes on the UI thread (button click), so without Task.Run the
                        // synchronous per-file SHA-256 hashing after the manifest await would
                        // resume on the UI thread and freeze the window while verifying a
                        // large Lineage 2 install. The auto-verification path is already wrapped
                        // in Task.Run; this matches that behavior for the manual RETRY/PLAY path.
                        await Task.Run(() => CheckAndDownloadFiles());
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            CurrentButtonState = ButtonState.Ready;
                            StatusText = "Everything ready to play!";
                        }, System.Windows.Threading.DispatcherPriority.Render);
                        AddLog("✓ Verification completed successfully!");
                        System.Diagnostics.Debug.WriteLine("Verification completed successfully");
                    }
                    catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                    {
                        // Normal cancellation (e.g. window closing) - not a failure
                        return;
                    }
                    catch (LauncherError le)
                    {
                        // Crafted user-facing message: show it verbatim instead of the generic text
                        AddLog($"✗ Error during verification: {le.Message}");
                        MessageBox.Show(le.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CurrentButtonState = ButtonState.Disabled;
                        StatusText = le.Message;
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Error during verification: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Verification error: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                        if (ex.InnerException != null)
                        {
                            AddLog($"  Inner exception: {ex.InnerException.Message}");
                            System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                        }

                        // Detectar si es un error de conexión
                        string errorMessage = ex.Message.ToLower();
                        if (errorMessage.Contains("could not connect") || errorMessage.Contains("connection"))
                        {
                            MessageBox.Show("Could not connect to server.\n\nPlease check your internet connection and try again later.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            CurrentButtonState = ButtonState.Disabled;
                            StatusText = "Could not connect to server. Please try again later.";
                        }
                        else if (errorMessage.Contains("access to the path") || errorMessage.Contains("access is denied") || errorMessage.Contains("no write permission"))
                        {
                            MessageBox.Show(
                                "The launcher cannot write files to your game folder.\n\n" +
                                "Recommended: Move the game to a non-protected folder (for example: C:\\Games\\L2).\n" +
                                "Alternative: Run the launcher as Administrator.\n\n" +
                                $"Current path: {_gamePath}",
                                "Permission Required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            CurrentButtonState = ButtonState.Disabled;
                            StatusText = "No write permission in game folder. Move game outside Program Files.";
                        }
                        else
                        {
                            MessageBox.Show($"Error during verification: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            CurrentButtonState = ButtonState.Disabled;
                            StatusText = "Error occurred. Click RETRY to try again.";
                        }
                    }
                }
                else
                {
                    AddLog($"Unknown button state: {CurrentButtonState}");
                    System.Diagnostics.Debug.WriteLine($"Unknown button state: {CurrentButtonState}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Unexpected error in HandlePlayAction: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Unexpected error in HandlePlayAction: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    AddLog($"  Inner exception: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentButtonState = ButtonState.Disabled;
                StatusText = "Error occurred. Click PLAY to retry.";
            }

            System.Diagnostics.Debug.WriteLine("HandlePlayAction completed");
        }

        private async Task StartAutoVerification()
        {
            AddLog("Starting auto-verification...");
            Progress = 0;
            CurrentButtonState = ButtonState.Checking;
            StatusText = "Connecting to server...";
            try
            {
                await CheckAndDownloadFiles();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentButtonState = ButtonState.Ready;
                    StatusText = "Everything ready to play!";
                }, System.Windows.Threading.DispatcherPriority.Render);
                AddLog("✓ Auto-verification completed successfully!");
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // Normal cancellation (e.g. window closing) - not a failure
                return;
            }
            catch (LauncherError le)
            {
                // Crafted user-facing message: show it verbatim instead of the generic text
                AddLog($"✗ Auto-verification error: {le.Message}");
                CurrentButtonState = ButtonState.Disabled;
                StatusText = le.Message;
            }
            catch (Exception ex)
            {
                AddLog($"✗ Auto-verification error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    AddLog($"  Inner exception: {ex.InnerException.Message}");
                }

                // Detectar si es un error de conexión
                string errorMessage = ex.Message.ToLower();
                if (errorMessage.Contains("could not connect") || errorMessage.Contains("connection"))
                {
                    CurrentButtonState = ButtonState.Disabled;
                    StatusText = "Could not connect to server. Please try again later.";
                    AddLog("Connection error detected. User can retry manually.");
                }
                else if (errorMessage.Contains("access to the path") || errorMessage.Contains("access is denied") || errorMessage.Contains("no write permission"))
                {
                    CurrentButtonState = ButtonState.Disabled;
                    StatusText = "No write permission in game folder. Move game outside Program Files.";
                    AddLog("Permission error detected. Game path likely requires admin rights.");
                }
                else
                {
                    CurrentButtonState = ButtonState.Disabled;
                    StatusText = "Auto-verification failed. Click RETRY to try again.";
                }
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                // Use AppContext.BaseDirectory for single-file apps compatibility
                string exeDir = AppContext.BaseDirectory ?? Environment.CurrentDirectory;
                string defaultInstallPath = @"C:\Juegos\Lineage2";

                string? configPath = FindConfigFile();

                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    var configuredGamePath = config?.GamePath;
                    var hasConfiguredPath = !string.IsNullOrWhiteSpace(configuredGamePath);
                    var configuredLooksLikeClient = hasConfiguredPath && LooksLikeLineageClient(configuredGamePath!);
                    var exeDirLooksLikeClient = LooksLikeLineageClient(exeDir);

                    if (exeDirLooksLikeClient)
                    {
                        // Priority 1: if launcher is already inside a valid client, use that folder.
                        _gamePath = exeDir;
                        if (hasConfiguredPath && !string.Equals(configuredGamePath, _gamePath, StringComparison.OrdinalIgnoreCase))
                        {
                            AddLog($"Launcher folder detected as valid client. Using: {_gamePath}");
                        }
                    }
                    else if (configuredLooksLikeClient)
                    {
                        _gamePath = configuredGamePath!;
                    }
                    else
                    {
                        // Priority 3: clean install path when no valid existing client is detected.
                        _gamePath = defaultInstallPath;
                        if (hasConfiguredPath)
                        {
                            AddLog($"Configured GamePath does not look like a Lineage client: {configuredGamePath}");
                            AddLog($"Using default install path: {_gamePath}");
                        }
                    }
                    _serverUrl = config?.ServerUrl ?? "https://downloads.l2-titan.com";
                    _manifestUrl = config?.ManifestUrl ?? $"{_serverUrl}/manifest.json";
                    // Reject plaintext schemes even if a user/AppData config or a MITM-served
                    // config supplies an http:// URL - force the secure default channel.
                    if (!_serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog($"Insecure ServerUrl scheme rejected (must be https): {_serverUrl}");
                        _serverUrl = "https://downloads.l2-titan.com";
                    }
                    if (!_manifestUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog($"Insecure ManifestUrl scheme rejected (must be https): {_manifestUrl}");
                        _manifestUrl = $"{_serverUrl}/manifest.json";
                    }
                    AddLog($"Configuration loaded from: {configPath}");
                    AddLog($"  Server URL: {_serverUrl}");
                    AddLog($"  Manifest URL: {_manifestUrl}");
                    AddLog($"  Game Path: {_gamePath}");
                    if (IsProtectedSystemPath(_gamePath))
                    {
                        AddLog("Warning: Game path is inside Program Files/Windows. Downloads may fail without admin rights.");
                    }
                }
                else
                {
                    _gamePath = LooksLikeLineageClient(exeDir) ? exeDir : defaultInstallPath;
                    _serverUrl = "https://downloads.l2-titan.com";
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

        private static bool IsProtectedSystemPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return false;
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            return fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.StartsWith(windowsDir, StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeLineageClient(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string systemDir = Path.Combine(fullPath, "system");
                string l2Exe = Path.Combine(systemDir, "L2.exe");
                return Directory.Exists(systemDir) && File.Exists(l2Exe);
            }
            catch
            {
                return false;
            }
        }

        // Resolve a manifest-supplied relative path to an absolute path inside _gamePath.
        // Rejects rooted/absolute paths and ".." traversal so a malicious manifest cannot
        // write outside the install directory (path traversal -> arbitrary file write).
        private string ResolveSafePath(string relativePath)
        {
            var baseDir = Path.GetFullPath(_gamePath);
            if (!baseDir.EndsWith(Path.DirectorySeparatorChar))
                baseDir += Path.DirectorySeparatorChar;

            var full = Path.GetFullPath(Path.Combine(baseDir, relativePath));
            if (Path.IsPathRooted(relativePath) ||
                !full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Manifest path escapes install directory: {relativePath}");
            }
            return full;
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
            AddLog("=== Starting file check and download ===");
            AddLog($"Manifest URL: {_manifestUrl}");
            AddLog($"Server URL: {_serverUrl}");
            AddLog($"Game Path: {_gamePath}");
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
                    System.Diagnostics.Debug.WriteLine($"Attempting to download manifest from: {_manifestUrl}");
                    manifestJson = await _httpClient.GetStringAsync(_manifestUrl, _cts.Token);
                    System.Diagnostics.Debug.WriteLine($"Manifest downloaded successfully, length: {manifestJson?.Length ?? 0} characters");
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Manifest download attempt {i + 1} failed: {ex.Message}");
                    if (i == manifestRetries - 1)
                    {
                        // Detectar si es un error de conexión
                        string errorMessage = ex.Message.ToLower();
                        if (errorMessage.Contains("connection") || errorMessage.Contains("timeout") ||
                            errorMessage.Contains("network") || errorMessage.Contains("resolve") ||
                            errorMessage.Contains("refused") || errorMessage.Contains("unreachable"))
                        {
                            throw new LauncherError("Could not connect to server. Please check your internet connection and try again later.");
                        }
                        throw new LauncherError($"Could not download manifest after {manifestRetries} attempts: {ex.Message}");
                    }
                    AddLog($"Error downloading manifest: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(manifestJson))
                throw new LauncherError("Could not download manifest.");

            try
            {
                _fileManifest = JsonConvert.DeserializeObject<List<FileManifest>>(manifestJson) ?? new List<FileManifest>();
            }
            catch (JsonException)
            {
                throw new LauncherError("The update manifest from the server is corrupted or invalid. Please try again later or contact support.");
            }

            if (_fileManifest == null || _fileManifest.Count == 0)
                throw new LauncherError("The manifest is empty or invalid.");

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

            // Remember which entries actually need downloading so the second pass does
            // not re-hash the entire client a second time (huge I/O on a real L2 install)
            var entriesToDownload = new HashSet<FileManifest>();

            AddLog("\nVerifying local files...");
            StatusText = "Verifying files...";
            int verifiedFiles = 0;
            foreach (var fileInfo in _fileManifest)
            {
                verifiedFiles++;
                var filePath = ResolveSafePath(fileInfo.Path);

                // Actualizar progreso durante la verificación
                var verifyProgress = (int)((verifiedFiles * 100) / totalFiles);
                Progress = verifyProgress;
                StatusText = $"Verifying files... {verifyProgress}% ({verifiedFiles}/{totalFiles})";

                if (!File.Exists(filePath))
                {
                    filesToDownload++;
                    totalSizeToDownload += fileInfo.Size;
                    entriesToDownload.Add(fileInfo);
                }
                else
                {
                    var localHash = CalculateFileHash(filePath);
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

            AddLog($"\nSummary:");
            AddLog($"  Files OK: {filesOk}");
            AddLog($"  Files to download: {filesToDownload}");
            if (totalSizeToDownload > 0)
                AddLog($"  Total size to download: {totalSizeToDownload / 1024 / 1024:F2} MB");

            if (filesToDownload == 0)
            {
                AddLog("\n✓ All files are up to date!");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Progress = 100;
                    CurrentButtonState = ButtonState.Ready;
                    StatusText = "Everything ready to play!";
                }, System.Windows.Threading.DispatcherPriority.Render);
                return;
            }

            AddLog("\nStarting file download...");
            CurrentButtonState = ButtonState.Downloading;
            _isDownloadPaused = false;

            // Drive progress by bytes transferred (not file count) so a single large file
            // does not jump the bar to ~100% while it is still downloading.
            long downloadedSoFar = 0;

            foreach (var fileInfo in _fileManifest)
            {
                while (_isDownloadPaused)
                {
                    await Task.Delay(100);
                }

                _cts.Token.ThrowIfCancellationRequested();
                processedFiles++;

                // Reuse the verification result from the first pass instead of re-hashing
                if (!entriesToDownload.Contains(fileInfo))
                {
                    if (filesOk <= 10 || processedFiles % 10 == 0)
                    {
                        AddLog($"✓ {fileInfo.Path} - OK");
                    }
                    continue;
                }

                var filePath = ResolveSafePath(fileInfo.Path);
                var fileUrl = $"{_serverUrl}/{fileInfo.Path.Replace('\\', '/')}";

                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await DownloadFile(fileUrl, filePath, fileInfo.Hash, downloadedSoFar, totalSizeToDownload);
                downloadedSoFar += fileInfo.Size;
            }

            AddLog("\n✓ Verification and download completed!");
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = 100;
                CurrentButtonState = ButtonState.Ready;
                StatusText = "Everything ready to play!";
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private async Task DownloadFile(string url, string filePath, string expectedHash, long bytesBeforeThisFile, long totalDownloadBytes)
        {
            const int maxRetries = 3;
            const int idleTimeoutSeconds = 60; // fail a stalled connection instead of hanging forever
            int retryCount = 0;
            Exception? lastException = null;

            // Stage into a temp file so the existing good copy is never truncated and a
            // paused/crashed download cannot leave a half-written final file.
            var tempPath = filePath + ".part";

            while (retryCount < maxRetries)
            {
                try
                {
                    if (retryCount > 0)
                    {
                        AddLog($"Retry {retryCount}/{maxRetries - 1} for: {Path.GetFileName(filePath)}");
                        await Task.Delay(1000 * retryCount, _cts.Token);
                    }

                    AddLog($"Downloading: {Path.GetFileName(filePath)}");

                    using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cts.Token))
                    {
                        response.EnsureSuccessStatusCode();

                        var totalBytes = response.Content.Headers.ContentLength ?? 0;
                        var downloadedBytes = 0L;
                        var startTime = DateTime.Now;
                        var fileName = Path.GetFileName(filePath);

                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var httpStream = await response.Content.ReadAsStreamAsync(_cts.Token))
                        {
                            var buffer = new byte[8192];
                            int bytesRead;
                            var lastUpdateTime = DateTime.Now;

                            while (true)
                            {
                                // Honor pause mid-file so a large download stops promptly,
                                // not just between files (respecting cancellation)
                                while (_isDownloadPaused && !_cts.IsCancellationRequested)
                                {
                                    await Task.Delay(100, _cts.Token);
                                }

                                // Sliding inactivity timeout: cancel a read that delivers no bytes
                                using (var readCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                                {
                                    readCts.CancelAfter(TimeSpan.FromSeconds(idleTimeoutSeconds));
                                    try
                                    {
                                        bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token);
                                    }
                                    catch (OperationCanceledException) when (!_cts.IsCancellationRequested)
                                    {
                                        throw new Exception("The connection stalled while downloading. Please try again.");
                                    }
                                }

                                if (bytesRead <= 0)
                                    break;

                                await fileStream.WriteAsync(buffer, 0, bytesRead, _cts.Token);
                                downloadedBytes += bytesRead;

                                // Drive overall progress by total bytes transferred across all files
                                if (totalDownloadBytes > 0)
                                {
                                    var overallBytes = bytesBeforeThisFile + downloadedBytes;
                                    Progress = (int)Math.Min(100, (overallBytes * 100) / totalDownloadBytes);
                                }

                                if (totalBytes > 0)
                                {
                                    var fileProgress = (int)((downloadedBytes * 100) / totalBytes);

                                    // Actualizar cada 100ms o cada 1MB
                                    var timeSinceUpdate = (DateTime.Now - lastUpdateTime).TotalMilliseconds;
                                    if (timeSinceUpdate >= 100 || downloadedBytes % 1048576 < 8192)
                                    {
                                        var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                        if (elapsed > 0)
                                        {
                                            var speed = downloadedBytes / elapsed / 1024;
                                            // ETA from the overall rolling byte rate
                                            var overallBytes = bytesBeforeThisFile + downloadedBytes;
                                            var overallSpeed = overallBytes / elapsed;
                                            var etaText = "";
                                            // Clamp remaining bytes: a stale manifest can make real bytes
                                            // exceed the estimate, yielding a negative (misleading) ETA
                                            var remainingBytes = Math.Max(0, totalDownloadBytes - overallBytes);
                                            if (remainingBytes > 0 && overallSpeed > 0)
                                            {
                                                var etaSeconds = remainingBytes / overallSpeed;
                                                etaText = $" - ETA {TimeSpan.FromSeconds(etaSeconds):mm\\:ss}";
                                            }
                                            StatusText = $"Downloading... {Progress}% - {fileName} ({fileProgress}%) - {speed:F0} KB/s{etaText}";
                                        }
                                        else
                                        {
                                            StatusText = $"Downloading... {Progress}% - {fileName} ({fileProgress}%)";
                                        }
                                        lastUpdateTime = DateTime.Now;
                                    }
                                }
                            }
                        }
                    }

                    AddLog($"Verifying integrity: {Path.GetFileName(filePath)}");
                    var downloadedHash = CalculateFileHash(tempPath);
                    if (!string.Equals(downloadedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(tempPath))
                        {
                            try { File.Delete(tempPath); } catch { }
                        }
                        // Same bytes will fail the same way on retry - flag as a server problem
                        throw new HashMismatchException();
                    }

                    // Atomically move the verified temp file into its final location
                    File.Move(tempPath, filePath, true);

                    AddLog($"✓ Downloaded and verified: {Path.GetFileName(filePath)}");
                    return;
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    if (File.Exists(tempPath))
                    {
                        try { File.Delete(tempPath); } catch { }
                    }
                    throw;
                }
                catch (HashMismatchException)
                {
                    // Do not retry: the server file is corrupt or its manifest hash is wrong,
                    // so re-downloading the identical bytes cannot succeed.
                    AddLog($"✗ Hash mismatch (server file appears corrupted): {Path.GetFileName(filePath)}");
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
                        AddLog($"✗ Error after {maxRetries} attempts: {Path.GetFileName(filePath)}");
                        throw new LauncherError($"Error downloading {Path.GetFileName(filePath)} after {maxRetries} attempts: {ex.Message}");
                    }
                }
            }

            throw lastException ?? new Exception($"Unknown error downloading {Path.GetFileName(filePath)}");
        }

        // Distinguishes a content/hash-verification failure from a transient network error
        private class HashMismatchException : Exception { }

        // User-facing error: its Message is crafted to be shown directly in the status bar,
        // instead of being collapsed into the generic "Auto-verification failed" text.
        private class LauncherError : Exception
        {
            public LauncherError(string message) : base(message) { }
        }

        private static string CalculateFileHash(string filePath)
        {
            // SHA-256 must match generate_manifest.py (calculate_sha256). The manifest
            // ships SHA-256 hashes; using MD5 here would fail verification on every file.
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private async Task StartGame()
        {
            try
            {
                string gameParameters = "";
                string gameExecutable = "system\\L2.exe";
                string? configPath = FindConfigFile();
                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    gameParameters = config?.GameParameters ?? "";
                    if (!string.IsNullOrWhiteSpace(config?.GameExecutable))
                        gameExecutable = config!.GameExecutable;
                }

                var exePath = Path.Combine(_gamePath, gameExecutable);

                AddLog($"Looking for game executable: {exePath}");

                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Game executable not found: {exePath}\n\nMake sure the game files are downloaded and the executable exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog($"✗ Game executable not found: {exePath}");
                    return;
                }

                // Avoid launching a second client instance
                var exeName = Path.GetFileNameWithoutExtension(exePath);
                if (Process.GetProcessesByName(exeName).Length > 0)
                {
                    MessageBox.Show("The game is already running.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
                    AddLog("Game launch skipped - already running.");
                    return;
                }

                AddLog($"Starting game: {exePath}");
                StatusText = "Starting game...";

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
                catch (System.ComponentModel.Win32Exception winEx) when (winEx.NativeErrorCode == 1223)
                {
                    AddLog("Game launch cancelled by user.");
                    MessageBox.Show("Game launch was cancelled. The game requires administrator privileges to run.", "Launch Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
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

        public void AddLog(string message)
        {
#if DEBUG
            // Mirror to the debug console so failures are diagnosable without UI access
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
            // Guard against shutdown: Application.Current is null during teardown
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(() =>
            {
                _logMessages.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                if (_logMessages.Count > 100)
                    _logMessages.RemoveAt(0);
                OnPropertyChanged(nameof(LogText));
            });
        }

        public void AddLog(string message, Exception exception)
        {
            string fullMessage = $"{message}\n  Exception: {exception.Message}";
            if (exception.StackTrace != null)
            {
                fullMessage += $"\n  Stack Trace: {exception.StackTrace}";
            }
            AddLog(fullMessage);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Marshal change notifications onto the UI thread so background verify/download
        // tasks do not raise PropertyChanged cross-thread (would throw on bound controls)
        private void RaiseOnUi(string propertyName)
        {
            var app = Application.Current;
            if (app == null)
                return;

            if (app.Dispatcher.CheckAccess())
                OnPropertyChanged(propertyName);
            else
                app.Dispatcher.BeginInvoke(() => OnPropertyChanged(propertyName));
        }

        // Cancel background loops/downloads and release resources on window close
        public void Shutdown()
        {
            // Cancel only - don't dispose the CTS while background loops may still
            // read _cts.Token (Task.Delay), which would throw ObjectDisposedException.
            // Disposing a cancelled CTS is optional.
            try { _cts.Cancel(); } catch { }
            try { _httpClient.Dispose(); } catch { }
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
