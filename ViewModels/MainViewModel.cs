using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using L2TitanLauncher.Services;

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

    // Coordinador de UI. La lógica de dominio vive en Services/ (ConfigService,
    // UpdateService, PathSafety, FileIntegrity, ManifestSecurity). El VM implementa
    // IUpdateHost para que UpdateService reporte progreso/estado a la UI sin conocer WPF.
    public class MainViewModel : INotifyPropertyChanged, IUpdateHost
    {
        private int _progress;
        private string _statusText = "Ready to verify";
        private ButtonState _currentButtonState = ButtonState.Disabled;
        // volatile: lo escribe el hilo de UI (pausa/reanuda) y lo leen bucles de espera
        // en hilos de fondo (UpdateService); evita lectura rancia.
        private volatile bool _isDownloadPaused = false;
        private string _gamePath = string.Empty;
        private string _serverUrl = string.Empty;
        private string _manifestUrl = string.Empty;
        private readonly HttpClient _httpClient = new();
        private readonly CancellationTokenSource _cts = new();

        private readonly ConfigService _configService = new();
        private readonly UpdateService _updateService;

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

                    // Update brushes when state changes - do this on UI thread.
                    // All change notifications are raised here so they run on the UI thread.
                    var app = Application.Current;
                    if (app == null)
                        return;

                    // Un único marshalling al hilo de UI: actualizar brushes primero y
                    // notificar cada propiedad una sola vez. Seguro porque los brushes están
                    // Freeze()-ados (sin riesgo de threading) y los setters de brush ya filtran
                    // cambios; el doble Invoke con prioridad Loaded era código defensivo redundante.
                    app.Dispatcher.Invoke(() =>
                    {
                        UpdateButtonBrushes();

                        OnPropertyChanged(nameof(CurrentButtonState));
                        OnPropertyChanged(nameof(ButtonText));
                        OnPropertyChanged(nameof(IsPlayEnabled));
                        OnPropertyChanged(nameof(IsPaused));
                        OnPropertyChanged(nameof(IsReady));
                        OnPropertyChanged(nameof(ButtonBackgroundBrush));
                        OnPropertyChanged(nameof(ButtonBorderBrush));
                        OnPropertyChanged(nameof(ButtonForegroundBrush));
                    }, System.Windows.Threading.DispatcherPriority.Render);
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

        // Botón habilitado cuando está Ready, Downloading, Paused o Disabled (NO en Checking).
        public bool IsPlayEnabled => CurrentButtonState == ButtonState.Ready ||
                                     CurrentButtonState == ButtonState.Downloading ||
                                     CurrentButtonState == ButtonState.Paused ||
                                     CurrentButtonState == ButtonState.Disabled;

        public bool IsPaused => CurrentButtonState == ButtonState.Paused;
        public bool IsReady => CurrentButtonState == ButtonState.Ready;

        public Brush ButtonBackgroundBrush
        {
            get => _buttonBackgroundBrush;
            private set { if (_buttonBackgroundBrush != value) { _buttonBackgroundBrush = value; OnPropertyChanged(); } }
        }

        public Brush ButtonBorderBrush
        {
            get => _buttonBorderBrush;
            private set { if (_buttonBorderBrush != value) { _buttonBorderBrush = value; OnPropertyChanged(); } }
        }

        public Brush ButtonForegroundBrush
        {
            get => _buttonForegroundBrush;
            private set { if (_buttonForegroundBrush != value) { _buttonForegroundBrush = value; OnPropertyChanged(); } }
        }

        private void UpdateButtonBrushes()
        {
            _buttonBackgroundBrush = GetBackgroundBrushForState(CurrentButtonState);
            _buttonBorderBrush = GetBorderBrushForState(CurrentButtonState);
            _buttonForegroundBrush = GetForegroundBrushForState(CurrentButtonState);
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
                    var readyBg = (Brush)Application.Current.FindResource("PlayButtonGradientBrush");
                    if (readyBg is LinearGradientBrush lgb) { var c = lgb.Clone(); c.Freeze(); return c; }
                    return readyBg;
                case ButtonState.Downloading:
                    return RedActive;
                case ButtonState.Paused:
                    return RedDark;
                case ButtonState.Checking:
                    return RedBright;
                default:
                    return RedBright;
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
            _updateService = new UpdateService(_httpClient);

            // Resolver configuración (find/create config.json, heurística de GamePath, https forzado)
            var resolved = _configService.Resolve(AddLog);
            _gamePath = resolved.GamePath;
            _serverUrl = resolved.ServerUrl;
            _manifestUrl = resolved.ManifestUrl;

            UpdateButtonBrushes();

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
                    var kind = ClassifyError(ex);
                    InvokeOnUi(() =>
                    {
                        CurrentButtonState = ButtonState.Disabled;
                        StatusText = kind == UpdateErrorKind.Connection
                            ? StatusConnectionError
                            : "Auto-verification failed. Click RETRY to try again.";
                    });
                }
            });
        }

        // Mensajes de estado reutilizados en los catch de verificación/descarga.
        private const string StatusConnectionError = "Could not connect to server. Please try again later.";
        private const string StatusPermissionError = "No write permission in game folder. Move game outside Program Files.";

        private enum UpdateErrorKind { Connection, Permission, Generic }

        // Única copia de la heurística de clasificación de errores (antes triplicada en
        // los catch del constructor, HandlePlayAction y StartAutoVerification).
        private static UpdateErrorKind ClassifyError(Exception ex)
        {
            var m = ex.Message.ToLowerInvariant();
            if (m.Contains("could not connect") || m.Contains("connection"))
                return UpdateErrorKind.Connection;
            if (m.Contains("access to the path") || m.Contains("access is denied") || m.Contains("no write permission"))
                return UpdateErrorKind.Permission;
            return UpdateErrorKind.Generic;
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
                    await StartGame();
                }
                else if (CurrentButtonState == ButtonState.Downloading)
                {
                    AddLog("Pausing download...");
                    _isDownloadPaused = true;
                    CurrentButtonState = ButtonState.Paused;
                    StatusText = "Download paused";
                    AddLog("Download paused by user.");
                }
                else if (CurrentButtonState == ButtonState.Paused)
                {
                    AddLog("Resuming download...");
                    _isDownloadPaused = false;
                    CurrentButtonState = ButtonState.Downloading;
                    StatusText = "Downloading...";
                    AddLog("Download resumed by user.");
                }
                else if (CurrentButtonState == ButtonState.Disabled)
                {
                    AddLog("Starting verification and download process...");

                    Progress = 0;
                    CurrentButtonState = ButtonState.Checking;
                    StatusText = "Connecting to server...";

                    try
                    {
                        // Ejecutar verificación/hashing en un hilo de fondo: este comando corre
                        // en el hilo de UI (clic), así que sin Task.Run el hashing SHA-256
                        // síncrono congelaría la ventana al verificar una instalación grande.
                        await Task.Run(() => _updateService.RunAsync(_gamePath, _serverUrl, _manifestUrl, this));
                        InvokeOnUi(() =>
                        {
                            CurrentButtonState = ButtonState.Ready;
                            StatusText = "Everything ready to play!";
                        });
                        AddLog("✓ Verification completed successfully!");
                    }
                    catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (LauncherError le)
                    {
                        AddLog($"✗ Error during verification: {le.Message}");
                        MessageBox.Show(le.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CurrentButtonState = ButtonState.Disabled;
                        StatusText = le.Message;
                    }
                    catch (Exception ex)
                    {
                        AddLog($"✗ Error during verification: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Verification error: {ex.Message}");

                        if (ex.InnerException != null)
                            AddLog($"  Inner exception: {ex.InnerException.Message}");

                        CurrentButtonState = ButtonState.Disabled;
                        switch (ClassifyError(ex))
                        {
                            case UpdateErrorKind.Connection:
                                MessageBox.Show("Could not connect to server.\n\nPlease check your internet connection and try again later.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                StatusText = StatusConnectionError;
                                break;
                            case UpdateErrorKind.Permission:
                                MessageBox.Show(
                                    "The launcher cannot write files to your game folder.\n\n" +
                                    "Recommended: Move the game to a non-protected folder (for example: C:\\Games\\L2).\n" +
                                    "Alternative: Run the launcher as Administrator.\n\n" +
                                    $"Current path: {_gamePath}",
                                    "Permission Required",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                StatusText = StatusPermissionError;
                                break;
                            default:
                                MessageBox.Show($"Error during verification: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                StatusText = "Error occurred. Click RETRY to try again.";
                                break;
                        }
                    }
                }
                else
                {
                    AddLog($"Unknown button state: {CurrentButtonState}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"✗ Unexpected error in HandlePlayAction: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Unexpected error in HandlePlayAction: {ex.Message}");

                if (ex.InnerException != null)
                    AddLog($"  Inner exception: {ex.InnerException.Message}");

                MessageBox.Show($"Unexpected error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentButtonState = ButtonState.Disabled;
                StatusText = "Error occurred. Click PLAY to retry.";
            }
        }

        private async Task StartAutoVerification()
        {
            AddLog("Starting auto-verification...");
            Progress = 0;
            CurrentButtonState = ButtonState.Checking;
            StatusText = "Connecting to server...";
            try
            {
                await _updateService.RunAsync(_gamePath, _serverUrl, _manifestUrl, this);
                InvokeOnUi(() =>
                {
                    CurrentButtonState = ButtonState.Ready;
                    StatusText = "Everything ready to play!";
                });
                AddLog("✓ Auto-verification completed successfully!");
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (LauncherError le)
            {
                AddLog($"✗ Auto-verification error: {le.Message}");
                CurrentButtonState = ButtonState.Disabled;
                StatusText = le.Message;
            }
            catch (Exception ex)
            {
                AddLog($"✗ Auto-verification error: {ex.Message}");
                if (ex.InnerException != null)
                    AddLog($"  Inner exception: {ex.InnerException.Message}");

                CurrentButtonState = ButtonState.Disabled;
                switch (ClassifyError(ex))
                {
                    case UpdateErrorKind.Connection:
                        StatusText = StatusConnectionError;
                        AddLog("Connection error detected. User can retry manually.");
                        break;
                    case UpdateErrorKind.Permission:
                        StatusText = StatusPermissionError;
                        AddLog("Permission error detected. Game path likely requires admin rights.");
                        break;
                    default:
                        StatusText = "Auto-verification failed. Click RETRY to try again.";
                        break;
                }
            }
        }

        private async Task StartGame()
        {
            try
            {
                string gameParameters = "";
                string gameExecutable = "system\\L2.exe";
                var config = _configService.LoadRaw();
                if (config != null)
                {
                    gameParameters = config.GameParameters ?? "";
                    if (!string.IsNullOrWhiteSpace(config.GameExecutable))
                        gameExecutable = config.GameExecutable;
                }

                // Validar que el ejecutable configurado quede DENTRO de la carpeta del juego:
                // un config.json plantado no debe poder lanzar una ruta absoluta/traversal como admin.
                string exePath;
                try
                {
                    exePath = PathSafety.ResolveSafePath(_gamePath, gameExecutable);
                }
                catch
                {
                    MessageBox.Show($"The configured game executable path is invalid: {gameExecutable}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog($"✗ Invalid game executable path (escapes game folder): {gameExecutable}");
                    return;
                }

                AddLog($"Looking for game executable: {exePath}");

                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Game executable not found: {exePath}\n\nMake sure the game files are downloaded and the executable exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    AddLog($"✗ Game executable not found: {exePath}");
                    return;
                }

                // Evitar lanzar una segunda instancia del cliente.
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
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
#endif
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
                fullMessage += $"\n  Stack Trace: {exception.StackTrace}";
            AddLog(fullMessage);
        }

        // === IUpdateHost: puente UpdateService -> UI ===
        void IUpdateHost.Log(string message) => AddLog(message);
        void IUpdateHost.SetProgress(int percent) => Progress = percent;
        void IUpdateHost.SetStatus(string status) => StatusText = status;
        void IUpdateHost.OnDownloadingStarted()
        {
            CurrentButtonState = ButtonState.Downloading;
            _isDownloadPaused = false;
        }
        bool IUpdateHost.IsPaused => _isDownloadPaused;
        CancellationToken IUpdateHost.Token => _cts.Token;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Ejecuta una acción en el hilo de UI, con guard ante teardown (Application.Current null).
        private static void InvokeOnUi(Action action)
        {
            var app = Application.Current;
            if (app == null)
                return;
            app.Dispatcher.Invoke(action, System.Windows.Threading.DispatcherPriority.Render);
        }

        // Marshal de notificaciones al hilo de UI para no lanzar PropertyChanged cross-thread.
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

        // Cancela loops/descargas y libera recursos al cerrar la ventana.
        public void Shutdown()
        {
            // Solo cancelar - no disponer el CTS mientras tareas de fondo aún leen su token.
            try { _cts.Cancel(); } catch { }
            try { _httpClient.Dispose(); } catch { }
        }
    }
}
