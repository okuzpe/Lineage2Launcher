using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
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
            
            // Configurar manejadores globales de excepciones
            SetupExceptionHandlers();
            
            // Cargar fuentes embebidas si están disponibles
            LoadEmbeddedFonts();
            
#if DEBUG
            Console.WriteLine("Application started successfully.");
            Console.WriteLine();
#endif
        }

        private void SetupExceptionHandlers()
        {
            // Manejador para excepciones no manejadas en el hilo UI
            DispatcherUnhandledException += (sender, args) =>
            {
                string errorMessage = $"Unhandled UI Thread Exception: {args.Exception.Message}";
                string fullError = $"{errorMessage}\n\nStack Trace:\n{args.Exception.StackTrace}";
                
                if (args.Exception.InnerException != null)
                {
                    fullError += $"\n\nInner Exception: {args.Exception.InnerException.Message}\n{args.Exception.InnerException.StackTrace}";
                }
                
                Console.WriteLine($"\n!!! {errorMessage}");
                Console.WriteLine(fullError);
                System.Diagnostics.Debug.WriteLine($"!!! {errorMessage}");
                System.Diagnostics.Debug.WriteLine(fullError);
                
                // Intentar loggear en el ViewModel si está disponible
                try
                {
                    if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
                    {
                        vm.AddLog($"!!! CRITICAL ERROR: {args.Exception.Message}", args.Exception);
                    }
                }
                catch { }
                
                // Mostrar mensaje al usuario
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe application will continue, but some features may not work correctly.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                
                // Marcar como manejado para que la app no se cierre
                args.Handled = true;
            };
            
            // Manejador para excepciones no manejadas en otros hilos
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception? ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    string errorMessage = $"Unhandled Background Thread Exception: {ex.Message}";
                    string fullError = $"{errorMessage}\n\nStack Trace:\n{ex.StackTrace}";
                    
                    if (ex.InnerException != null)
                    {
                        fullError += $"\n\nInner Exception: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}";
                    }
                    
                    Console.WriteLine($"\n!!! {errorMessage}");
                    Console.WriteLine(fullError);
                    System.Diagnostics.Debug.WriteLine($"!!! {errorMessage}");
                    System.Diagnostics.Debug.WriteLine(fullError);
                    
                    // Intentar loggear en el ViewModel si está disponible
                    try
                    {
                        if (MainWindow?.DataContext is ViewModels.MainViewModel vm)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                vm.AddLog($"!!! CRITICAL BACKGROUND ERROR: {ex.Message}", ex);
                            });
                        }
                    }
                    catch { }
                }
            };
        }
        
        private void LoadEmbeddedFonts()
        {
            try
            {
                // Cargar fuentes desde recursos embebidos usando pack:// URIs con assembly name
                var fontResources = new List<(string resourcePath, string fontName)>
                {
                    ("Fonts/Cinzel-Regular.ttf", "Cinzel"),
                    ("Fonts/Cinzel-Bold.ttf", "Cinzel"),
                    ("Fonts/Cinzel-SemiBold.ttf", "Cinzel"),
                    ("Fonts/Cinzel-Medium.ttf", "Cinzel"),
                    ("Fonts/Cinzel-ExtraBold.ttf", "Cinzel"),
                    ("Fonts/Cinzel-Black.ttf", "Cinzel"),
                    ("Fonts/Cinzel-VariableFont_wght.ttf", "Cinzel")
                };
                
                int loadedCount = 0;
                foreach (var (resourcePath, fontName) in fontResources)
                {
                    try
                    {
                        // Formato: pack://application:,,,/AssemblyName;component/ResourcePath#FontName
                        var fontUri = new Uri($"pack://application:,,,/L2TitanLauncher;component/{resourcePath}#{fontName}", UriKind.Absolute);
                        
                        // Crear FontFamily usando el URI de pack
                        var fontFamily = new FontFamily(fontUri.ToString());
                        
                        // Verificar que la fuente se puede cargar creando un Typeface
                        var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
                        {
                            // Get actual font name from metadata
                            string actualFontName = glyphTypeface.Win32FamilyNames.Values.FirstOrDefault() ?? fontName;
                            if (actualFontName != fontName)
                            {
                                // Try with actual font name
                                fontUri = new Uri($"pack://application:,,,/L2TitanLauncher;component/{resourcePath}#{actualFontName}", UriKind.Absolute);
                                fontFamily = new FontFamily(fontUri.ToString());
                                System.Diagnostics.Debug.WriteLine($"  Detected font name: {actualFontName} (expected: {fontName})");
                            }
                            
                            loadedCount++;
                            System.Diagnostics.Debug.WriteLine($"✓ Loaded embedded font: {Path.GetFileName(resourcePath)} as {actualFontName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // No es crítico si alguna fuente falla, continuar con las demás
                        System.Diagnostics.Debug.WriteLine($"⚠ Could not load embedded font {Path.GetFileName(resourcePath)}: {ex.Message}");
                    }
                }
                
                if (loadedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded {loadedCount} embedded Cinzel font(s)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No embedded Cinzel fonts were loaded. Using system fonts as fallback.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading embedded fonts: {ex.Message}");
            }
        }
    }
}


