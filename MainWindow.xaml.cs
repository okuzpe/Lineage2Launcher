using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace L2TitanLauncher
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            // Release the WebView2 host process and user-data-folder lock on close.
            this.Closed += (s, e) => LauncherWebView?.Dispose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                // Keep WebView2 profile/cache out of the launcher/game folder.
                string webViewDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "L2TitanLauncher",
                    "WebView2"
                );
                Directory.CreateDirectory(webViewDataPath);

                var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: webViewDataPath
                );
                await LauncherWebView.EnsureCoreWebView2Async(webViewEnvironment);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 custom init failed: {ex.Message}");
                try
                {
                    // Fallback: try default initialization in case custom userDataFolder fails.
                    await LauncherWebView.EnsureCoreWebView2Async();
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 default init failed: {fallbackEx.Message}");
                    LauncherWebView.Visibility = Visibility.Collapsed;
                    WebViewFallback.Visibility = Visibility.Visible;

                    if (FallbackMessage != null)
                    {
                        FallbackMessage.Text =
                            "WebView2 Runtime not found.\n" +
                            "Install Microsoft Edge WebView2 Runtime and restart the launcher.";
                    }
                    return;
                }
            }

            try
            {
                // CoreWebView2 can still be null/disposed here even if the calls above
                // did not throw; fall back gracefully instead of escaping the async void.
                if (LauncherWebView.CoreWebView2 == null)
                    throw new InvalidOperationException("CoreWebView2 is null after initialization.");

                var settings = LauncherWebView.CoreWebView2.Settings;
                settings.IsStatusBarEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                // Hide the raw WebView2 error page in favor of the styled fallback panel.
                settings.IsBuiltInErrorPageEnabled = false;

                // The XAML Source was removed; navigate here so the custom userDataFolder
                // environment is honored. NavigationCompleted handles load failures.
                LauncherWebView.CoreWebView2.NavigationCompleted += LauncherWebView_NavigationCompleted;
                LauncherWebView.CoreWebView2.Navigate("https://l2-titan.com/");

                LauncherWebView.Visibility = Visibility.Visible;
                WebViewFallback.Visibility = Visibility.Collapsed;
            }
            catch (Exception settingsEx)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 settings init failed: {settingsEx.Message}");
                LauncherWebView.Visibility = Visibility.Collapsed;
                WebViewFallback.Visibility = Visibility.Visible;

                if (FallbackMessage != null)
                {
                    FallbackMessage.Text =
                        "Failed to initialize embedded browser.\n" +
                        "Visit l2-titan.com in your browser.";
                }
            }
        }

        // Only the initial top-level load should toggle the fallback panel; failed
        // in-site sub-navigations must not collapse the whole embedded browser.
        private bool _firstNavigationHandled;

        private void LauncherWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                LauncherWebView.Visibility = Visibility.Visible;
                WebViewFallback.Visibility = Visibility.Collapsed;
            }
            else if (!_firstNavigationHandled)
            {
                LauncherWebView.Visibility = Visibility.Collapsed;
                WebViewFallback.Visibility = Visibility.Visible;
                if (FallbackMessage != null)
                    FallbackMessage.Text = "Could not load content — check your connection.";
            }

            _firstNavigationHandled = true;
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            // Keep the gold border + drop shadow of MainBorder from being clipped off-screen
            // when maximized by insetting it by the OS resize-border thickness.
            if (MainBorder != null)
                MainBorder.Margin = WindowState == WindowState.Maximized
                    ? new Thickness(7)
                    : new Thickness(0);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Cancel news/status/download loops and dispose the CTS + HttpClient so
            // background work stops when the window closes.
            (DataContext as L2TitanLauncher.ViewModels.MainViewModel)?.Shutdown();
            base.OnClosing(e);
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // DragMove() is ignored on a maximized borderless window. Restore to
                // Normal and reposition under the cursor first so the title bar stays
                // draggable (and supports restore-by-drag) after MaximizeButton is used.
                if (WindowState == WindowState.Maximized)
                {
                    var mouse = PointToScreen(e.GetPosition(this));
                    WindowState = WindowState.Normal;
                    Left = mouse.X - (Width / 2);
                    Top = mouse.Y - 16;
                }

                this.DragMove();
            }
        }
    }
}
