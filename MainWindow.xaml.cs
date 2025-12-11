using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Lineage2Launcher.ViewModels;

namespace Lineage2Launcher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

            private void MainWindow_Loaded(object sender, RoutedEventArgs e)
            {
                // Cargar imágenes de fondo después del renderizado completo usando Dispatcher
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadBackgroundImages();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // Aplicar fuentes después de que el árbol visual esté completamente renderizado
                // Usar Dispatcher para asegurar que se ejecute después del renderizado
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadFonts();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            
            private void LoadFonts()
            {
                try
                {
                    // Cargar fuentes desde recursos embebidos
                    FontFamily? cinzelFamily = null;
                    FontFamily? cinzelBoldFamily = null;
                    FontFamily? cinzelSemiBoldFamily = null;
                    FontFamily? cinzelMediumFamily = null;
                    
                    // Cargar Cinzel Regular desde recursos embebidos
                    // Formato: pack://application:,,,/AssemblyName;component/Path/To/Font.ttf#FontName
                    try
                    {
                        var regularUri = new Uri("pack://application:,,,/Fonts/Cinzel-Regular.ttf#Cinzel", UriKind.Absolute);
                        cinzelFamily = new FontFamily(regularUri.ToString());
                        System.Diagnostics.Debug.WriteLine($"✓ Cinzel Regular loaded from embedded resource");
                    }
                    catch
                    {
                        // Fallback a VariableFont
                        try
                        {
                            var variableUri = new Uri("pack://application:,,,/Fonts/Cinzel-VariableFont_wght.ttf#Cinzel", UriKind.Absolute);
                            cinzelFamily = new FontFamily(variableUri.ToString());
                            System.Diagnostics.Debug.WriteLine($"✓ Cinzel VariableFont loaded as Regular from embedded resource");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Could not load Cinzel Regular: {ex.Message}");
                        }
                    }
                    
                    // Cargar Cinzel Bold desde recursos embebidos
                    try
                    {
                        var boldUri = new Uri("pack://application:,,,/Fonts/Cinzel-Bold.ttf#Cinzel", UriKind.Absolute);
                        cinzelBoldFamily = new FontFamily(boldUri.ToString());
                        System.Diagnostics.Debug.WriteLine($"✓ Cinzel Bold loaded from embedded resource");
                    }
                    catch
                    {
                        // Fallback a VariableFont
                        try
                        {
                            var variableUri = new Uri("pack://application:,,,/Fonts/Cinzel-VariableFont_wght.ttf#Cinzel", UriKind.Absolute);
                            cinzelBoldFamily = new FontFamily(variableUri.ToString());
                            System.Diagnostics.Debug.WriteLine($"✓ Cinzel VariableFont loaded as Bold from embedded resource");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠ Could not load Cinzel Bold: {ex.Message}");
                        }
                    }
                    
                    // Cargar Cinzel SemiBold desde recursos embebidos (opcional)
                    try
                    {
                        var semiBoldUri = new Uri("pack://application:,,,/Fonts/Cinzel-SemiBold.ttf#Cinzel", UriKind.Absolute);
                        cinzelSemiBoldFamily = new FontFamily(semiBoldUri.ToString());
                        System.Diagnostics.Debug.WriteLine($"✓ Cinzel SemiBold loaded from embedded resource");
                    }
                    catch
                    {
                        // Usar Bold como fallback
                        cinzelSemiBoldFamily = cinzelBoldFamily;
                    }
                    
                    // Cargar Cinzel Medium desde recursos embebidos (opcional)
                    try
                    {
                        var mediumUri = new Uri("pack://application:,,,/Fonts/Cinzel-Medium.ttf#Cinzel", UriKind.Absolute);
                        cinzelMediumFamily = new FontFamily(mediumUri.ToString());
                        System.Diagnostics.Debug.WriteLine($"✓ Cinzel Medium loaded from embedded resource");
                    }
                    catch
                    {
                        // Usar Regular como fallback
                        cinzelMediumFamily = cinzelFamily;
                    }
                    
                    // Aplicar fuentes a los elementos principales
                    if (cinzelBoldFamily != null)
                    {
                        // Título principal L2TITAN
                        var titleText = this.FindName("TitleText") as System.Windows.Controls.TextBlock;
                        if (titleText != null)
                        {
                            titleText.FontFamily = cinzelBoldFamily;
                            System.Diagnostics.Debug.WriteLine("✓ Applied Cinzel Bold to L2TITAN title");
                        }
                        
                        // Título RATES
                        ApplyFontToChildren(this, "RATES", cinzelBoldFamily);
                        
                        // Botón PLAY
                        var playButton = FindVisualChild<System.Windows.Controls.Button>(this, btn => 
                            btn.Content?.ToString() == "PLAY" || 
                            (btn.Content is string content && content.Contains("PLAY")) ||
                            btn.Command != null);
                        if (playButton != null)
                        {
                            playButton.FontFamily = cinzelBoldFamily;
                            System.Diagnostics.Debug.WriteLine("✓ Applied Cinzel Bold to PLAY button");
                        }
                    }
                    
                    // Aplicar fuente Regular o Medium a subtítulos
                    FontFamily? subtitleFont = cinzelMediumFamily ?? cinzelSemiBoldFamily ?? cinzelFamily;
                    if (subtitleFont != null)
                    {
                        // Subtítulo LAUNCHER v.1.0.0
                        ApplyFontToChildren(this, "LAUNCHER", subtitleFont);
                        System.Diagnostics.Debug.WriteLine($"✓ Applied Cinzel {(cinzelMediumFamily != null ? "Medium" : cinzelSemiBoldFamily != null ? "SemiBold" : "Regular")} to subtitle");
                    }
                    
                    // Aplicar fuentes a todos los elementos que usan EpicTitleFont o EpicSubtitleFont
                    ApplyFontsToAllElements(cinzelBoldFamily, subtitleFont);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading fonts in MainWindow: {ex.Message}");
                }
            }
            
            private FontFamily? LoadFontFromFile(string fontPath, string fontName)
            {
                try
                {
                    // Método 1: Intentar con formato file:///path#FontName
                    var fontUri = new Uri(fontPath, UriKind.Absolute);
                    string fontUriString = fontUri.ToString() + "#" + fontName;
                    
                    try
                    {
                        var fontFamily = new FontFamily(fontUriString);
                        // Verificar que la fuente se puede cargar
                        var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
                        {
                            // Obtener el nombre real de la fuente desde el archivo
                            string actualFontName = glyphTypeface.Win32FamilyNames.Values.FirstOrDefault() ?? fontName;
                            
                            // Si el nombre es diferente, crear FontFamily con el nombre correcto
                            if (actualFontName != fontName && !string.IsNullOrEmpty(actualFontName))
                            {
                                fontUriString = fontUri.ToString() + "#" + actualFontName;
                                fontFamily = new FontFamily(fontUriString);
                                System.Diagnostics.Debug.WriteLine($"  Font name in file: {actualFontName}");
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully loaded font: {fontPath} as {actualFontName ?? fontName}");
                            return fontFamily;
                        }
                    }
                    catch (Exception ex1)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Method 1 failed: {ex1.Message}");
                    }
                    
                    // Método 2: Intentar sin especificar nombre (WPF puede detectarlo automáticamente)
                    try
                    {
                        var fontFamily2 = new FontFamily(fontUri.ToString());
                        var testTypeface = new Typeface(fontFamily2, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        if (testTypeface.TryGetGlyphTypeface(out var testGlyph))
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully loaded font (method 2): {fontPath}");
                            return fontFamily2;
                        }
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Method 2 failed: {ex2.Message}");
                    }
                    
                    // Método 3: Intentar con formato relativo ./Fonts/#FontName
                    try
                    {
                        string relativePath = "./Fonts/#" + fontName;
                        var fontFamily3 = new FontFamily(relativePath);
                        var testTypeface3 = new Typeface(fontFamily3, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        if (testTypeface3.TryGetGlyphTypeface(out var testGlyph3))
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Successfully loaded font (method 3 - relative): {fontPath}");
                            return fontFamily3;
                        }
                    }
                    catch (Exception ex3)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Method 3 failed: {ex3.Message}");
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✗ All methods failed for: {fontPath}");
                    return null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error loading font from {fontPath}: {ex.Message}");
                    return null;
                }
            }
            
            private void ApplyFontToChildren(DependencyObject? parent, string searchText, FontFamily? fontFamily)
            {
                if (parent == null || fontFamily == null) return;
                
                try
                {
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                        
                        if (child is System.Windows.Controls.TextBlock textBlock && 
                            textBlock.Text != null && 
                            textBlock.Text.Contains(searchText))
                        {
                            textBlock.FontFamily = fontFamily;
                        }
                        
                        ApplyFontToChildren(child, searchText, fontFamily);
                    }
                }
                catch
                {
                    // Ignore errors when traversing visual tree
                }
            }
            
            private void ApplyFontsToAllElements(FontFamily? boldFont, FontFamily? regularFont)
            {
                if (boldFont == null && regularFont == null) return;
                
                try
                {
                    // Buscar todos los TextBlocks y Buttons y aplicar fuentes según su estilo
                    ApplyFontToVisualTree(this, boldFont, regularFont);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error applying fonts to all elements: {ex.Message}");
                }
            }
            
            private void ApplyFontToVisualTree(DependencyObject? parent, FontFamily? boldFont, FontFamily? regularFont)
            {
                if (parent == null) return;
                
                try
                {
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                        
                        // Aplicar a TextBlocks
                        if (child is System.Windows.Controls.TextBlock textBlock)
                        {
                            // Si usa EpicTitleFont o tiene FontWeight Bold, usar Bold
                            if (textBlock.FontWeight == FontWeights.Bold || 
                                textBlock.FontWeight == FontWeights.SemiBold ||
                                textBlock.FontWeight == FontWeights.ExtraBold ||
                                textBlock.FontWeight == FontWeights.Black)
                            {
                                if (boldFont != null)
                                {
                                    textBlock.FontFamily = boldFont;
                                }
                            }
                            // Si usa fuente épica pero no es bold, usar Regular/Medium
                            else if (textBlock.FontFamily != null && 
                                     (textBlock.FontFamily.Source.Contains("Cinzel") || 
                                      textBlock.FontFamily.Source.Contains("Trajan")))
                            {
                                if (regularFont != null)
                                {
                                    textBlock.FontFamily = regularFont;
                                }
                            }
                        }
                        // Aplicar a Buttons
                        else if (child is System.Windows.Controls.Button button)
                        {
                            if (button.FontWeight == FontWeights.Bold && boldFont != null)
                            {
                                button.FontFamily = boldFont;
                            }
                            else if (regularFont != null && 
                                     (button.FontFamily?.Source.Contains("Cinzel") == true || 
                                      button.FontFamily?.Source.Contains("Trajan") == true))
                            {
                                button.FontFamily = regularFont;
                            }
                        }
                        
                        // Recursivo
                        ApplyFontToVisualTree(child, boldFont, regularFont);
                    }
                }
                catch
                {
                    // Ignore errors when traversing visual tree
                }
            }
            
            private T? FindVisualChild<T>(DependencyObject parent, Func<T, bool>? predicate = null) where T : DependencyObject
            {
                if (parent == null) return null;
                
                try
                {
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                        
                        if (child is T result && (predicate == null || predicate(result)))
                        {
                            return result;
                        }
                        
                        var childOfChild = FindVisualChild<T>(child, predicate);
                        if (childOfChild != null)
                        {
                            return childOfChild;
                        }
                    }
                }
                catch
                {
                    // Ignore errors when traversing visual tree
                }
                
                return null;
            }

        private void LoadBackgroundImages()
        {
            // Cargar imágenes desde recursos embebidos (pack://application:,,,)
            // Esto permite que todo esté en un solo EXE
            
            try
            {
                // Cargar textura de fondo desde recursos embebidos
                Uri? backgroundUri = null;
                
                // Intentar background.png primero
                try
                {
                    backgroundUri = new Uri("pack://application:,,,/Assets/images/background.png", UriKind.Absolute);
                    var testStream = Application.GetResourceStream(backgroundUri);
                    if (testStream == null) throw new Exception("Resource not found");
                    testStream.Stream.Close();
                }
                catch
                {
                    // Fallback a placeholder
                    try
                    {
                        backgroundUri = new Uri("pack://application:,,,/Assets/images/background_texture_placeholder.jpg", UriKind.Absolute);
                        var testStream = Application.GetResourceStream(backgroundUri);
                        if (testStream == null) throw new Exception("Resource not found");
                        testStream.Stream.Close();
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Background texture resource not found");
                    }
                }
                
                if (backgroundUri != null)
                {
                    var textureBrush = new ImageBrush(new BitmapImage(backgroundUri))
                    {
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.75,
                        TileMode = TileMode.Tile
                    };
                    
                    // Aplicar al Border principal
                    var mainBorder = this.FindName("MainBorder") as Border;
                    if (mainBorder != null)
                    {
                        mainBorder.Background = textureBrush;
                        System.Diagnostics.Debug.WriteLine("✓ Background texture applied from embedded resource");
                    }
                    
                    // Crear brush para bordes
                    var borderTextureBrush = new ImageBrush(new BitmapImage(backgroundUri))
                    {
                        Stretch = Stretch.UniformToFill,
                        Opacity = 1.0
                    };
                    
                    ApplyTextureToGoldBorders(borderTextureBrush);
                    ApplyTextureToNamedBorders(borderTextureBrush);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR loading background texture: {ex.Message}");
            }

            // Cargar imagen del dragón desde recursos embebidos
            try
            {
                Uri? dragonUri = null;
                
                // Intentar dragon.png primero
                try
                {
                    dragonUri = new Uri("pack://application:,,,/Assets/images/dragon.png", UriKind.Absolute);
                    var testStream = Application.GetResourceStream(dragonUri);
                    if (testStream == null) throw new Exception("Resource not found");
                    testStream.Stream.Close();
                }
                catch
                {
                    // Fallback a placeholder
                    try
                    {
                        dragonUri = new Uri("pack://application:,,,/Assets/images/dragon_silhouette_placeholder.png", UriKind.Absolute);
                        var testStream = Application.GetResourceStream(dragonUri);
                        if (testStream == null) throw new Exception("Resource not found");
                        testStream.Stream.Close();
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Dragon image resource not found");
                    }
                }
                
                if (dragonUri != null)
                {
                    var dragonImage = this.FindName("DragonImage") as Image;
                    if (dragonImage != null)
                    {
                        dragonImage.Source = new BitmapImage(dragonUri);
                        dragonImage.Opacity = 0.15;
                        System.Diagnostics.Debug.WriteLine("✓ Dragon image loaded from embedded resource");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR loading dragon image: {ex.Message}");
            }
        }
        
        private void ApplyTextureToGoldBorders(ImageBrush textureBrush)
        {
            // Aplicar gradiente dorado sólido sin transparencia
            // Solo gradiente dorado, sin textura
            
            // Crear gradiente dorado sólido
            var goldGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            goldGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E6C87E"), 0));
            goldGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B8860B"), 0.5));
            goldGradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8F6E07"), 1));
            
            // Aplicar directamente el gradiente sin textura
            ApplyBrushToBorders("GoldGradientBrush", goldGradient);
        }
        
        private void ApplyBrushToBorders(string resourceKey, Brush brush)
        {
            // Buscar y aplicar el brush a los bordes que usan el recurso dorado
            // Esto se hace dinámicamente recorriendo los elementos visuales
            try
            {
                // Actualizar el recurso en el diccionario de recursos de la aplicación
                if (Application.Current.Resources.Contains(resourceKey))
                {
                    // No podemos reemplazar directamente, así que aplicamos a elementos específicos
                    ApplyBrushToElement(this, brush);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying texture brush: {ex.Message}");
            }
        }
        
        private void ApplyBrushToElement(DependencyObject element, Brush brush)
        {
            // Recursivamente buscar bordes y aplicar el brush
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(element, i);
                
                if (child is Border border)
                {
                    // Si el borde usa un brush dorado, aplicar textura
                    if (border.BorderBrush is LinearGradientBrush || 
                        border.BorderBrush is SolidColorBrush solidBrush && 
                        (solidBrush.Color.ToString().Contains("B8860B") || solidBrush.Color.ToString().Contains("D4AF37")))
                    {
                        // Crear un nuevo brush con textura
                        var newBrush = CreateTexturedGoldBrush(brush);
                        border.BorderBrush = newBrush;
                    }
                }
                
                ApplyBrushToElement(child, brush);
            }
        }
        
        private Brush CreateTexturedGoldBrush(Brush textureBrush)
        {
            // Crear gradiente dorado sólido sin transparencia
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E6C87E"), 0));
            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B8860B"), 0.5));
            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8F6E07"), 1));
            
            // Retornar solo el gradiente, sin textura
            return gradient;
        }

        private void ApplyTextureToNamedBorders(ImageBrush textureBrush)
        {
            // Aplicar gradiente dorado sólido directamente a bordes específicos nombrados
            var borders = new[] { "ServerRatesBorder", "LeftColumnBorder", "MainBorder" };
            
            foreach (var borderName in borders)
            {
                var border = this.FindName(borderName) as Border;
                if (border != null && border.BorderBrush != null)
                {
                    // Crear gradiente dorado sólido sin transparencia
                    var gradient = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1)
                    };
                    gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#E6C87E"), 0));
                    gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B8860B"), 0.5));
                    gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#8F6E07"), 1));
                    
                    // Aplicar directamente el gradiente sin textura
                    border.BorderBrush = gradient;
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

            private void CloseButton_Click(object sender, RoutedEventArgs e)
            {
                this.Close();
            }

            private void Header_MouseDown(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    this.DragMove();
                }
            }
        }
    }


