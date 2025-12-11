using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Lineage2Launcher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Cargar fuentes embebidas si están disponibles
            LoadEmbeddedFonts();
        }
        
        private void LoadEmbeddedFonts()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string fontsDir = Path.Combine(baseDir, "Fonts");
                
                if (!Directory.Exists(fontsDir))
                {
                    System.Diagnostics.Debug.WriteLine($"Fonts directory not found: {fontsDir}");
                    return;
                }
                
                // Lista de archivos de fuente Cinzel a cargar
                var fontFiles = new List<string>
                {
                    Path.Combine(fontsDir, "Cinzel-Regular.ttf"),
                    Path.Combine(fontsDir, "Cinzel-Bold.ttf"),
                    Path.Combine(fontsDir, "Cinzel-SemiBold.ttf"),
                    Path.Combine(fontsDir, "Cinzel-Medium.ttf"),
                    Path.Combine(fontsDir, "Cinzel-ExtraBold.ttf"),
                    Path.Combine(fontsDir, "Cinzel-Black.ttf")
                };
                
                int loadedCount = 0;
                foreach (var fontFile in fontFiles)
                {
                    if (File.Exists(fontFile))
                    {
                        try
                        {
                            // Crear URI para la fuente usando file://
                            var fontUri = new Uri(fontFile, UriKind.Absolute);
                            
                            // Crear FontFamily usando el URI directo
                            var fontFamily = new FontFamily(fontUri.ToString());
                            
                            // Verificar que la fuente se puede cargar creando un Typeface
                            var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                            if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
                            {
                                loadedCount++;
                                System.Diagnostics.Debug.WriteLine($"✓ Loaded font: {Path.GetFileName(fontFile)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"✗ Error loading font {Path.GetFileName(fontFile)}: {ex.Message}");
                        }
                    }
                }
                
                if (loadedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded {loadedCount} Cinzel font(s)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No Cinzel fonts were loaded. Using system fonts as fallback.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading fonts: {ex.Message}");
            }
        }
    }
}


