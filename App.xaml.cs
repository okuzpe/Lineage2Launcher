using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using L2TitanLauncher.ViewModels;

namespace L2TitanLauncher
{
    public partial class App : Application
    {
#if DEBUG
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool AllocConsole();
#endif

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

#if DEBUG
            AllocConsole();
            Console.WriteLine("=== L2TitanLauncher Debug Mode ===");
            Console.WriteLine($"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
            Console.WriteLine($"Startup Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
#endif

            // Las fuentes Cinzel se resuelven por pack URI desde Themes/Colors.xaml
            // (EpicTitleFont). No hace falta cargarlas a mano en código.
            SetupExceptionHandlers();

#if DEBUG
            Console.WriteLine("Application started successfully.");
            Console.WriteLine();
#endif
        }

        // Persiste las excepciones no manejadas a un archivo para poder diagnosticar
        // crashes en Release (los handlers antes solo escribían a Console/Debug, que se
        // pierden sin consola). Nunca debe lanzar.
        private static void WriteCrashLog(string kind, Exception? ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "L2TitanLauncher");
                Directory.CreateDirectory(dir);
                var entry =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex?.Message}\n{ex?.StackTrace}\n" +
                    (ex?.InnerException != null
                        ? $"  Inner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n"
                        : string.Empty) +
                    new string('-', 60) + "\n";
                File.AppendAllText(Path.Combine(dir, "crash.log"), entry);
            }
            catch { /* el logging de crash jamás debe propagar */ }
        }

        private void SetupExceptionHandlers()
        {
            // Excepciones no manejadas en el hilo de UI: log + aviso, sin cerrar la app.
            DispatcherUnhandledException += (sender, args) =>
            {
                WriteCrashLog("Unhandled UI Thread Exception", args.Exception);
                Console.WriteLine($"\n!!! Unhandled UI Thread Exception: {args.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"!!! {args.Exception}");

                try
                {
                    if (MainWindow?.DataContext is MainViewModel vm)
                        vm.AddLog($"!!! CRITICAL ERROR: {args.Exception.Message}", args.Exception);
                }
                catch { }

                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe application will continue, but some features may not work correctly.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                args.Handled = true;
            };

            // Excepciones no manejadas en hilos de fondo: persistir para diagnóstico.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                WriteCrashLog("Unhandled Background Thread Exception", ex);
                Console.WriteLine($"\n!!! Unhandled Background Thread Exception: {ex?.Message}");
                System.Diagnostics.Debug.WriteLine($"!!! {ex}");

                try
                {
                    if (MainWindow?.DataContext is MainViewModel vm && ex != null)
                        Application.Current.Dispatcher.Invoke(() =>
                            vm.AddLog($"!!! CRITICAL BACKGROUND ERROR: {ex.Message}", ex));
                }
                catch { }
            };
        }
    }
}
