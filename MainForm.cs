using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Lineage2Launcher
{
    public enum ButtonState
    {
        Checking,
        Downloading,
        Paused,
        Ready,
        Disabled
    }

    public partial class MainForm : Form
    {
        private Label lblTitle = null!;
        private Label lblStatus = null!;
        private ProgressBar progressBar = null!;
        private Button btnMain = null!; // Botón principal único
        private Panel panelLog = null!;
        private RichTextBox txtLog = null!;
        private string gamePath = string.Empty;
        private string serverUrl = string.Empty;
        private string manifestUrl = string.Empty;
        private List<FileManifest> fileManifest = null!;
        private HttpClient httpClient = null!;
        
        // Control de pausa
        private bool isDownloadPaused = false;
        private ButtonState currentButtonState = ButtonState.Disabled;
        
        // Paleta Épica Fantasy
        private readonly Color colorCharcoal = Color.FromArgb(11, 11, 15); // #0B0B0F - Negro carbón
        private readonly Color colorMetallicGray = Color.FromArgb(30, 34, 38); // #1E2226 - Gris metálico
        private readonly Color colorOldGold = Color.FromArgb(184, 134, 11); // #B8860B - Oro viejo
        private readonly Color colorBrightGold = Color.FromArgb(212, 175, 55); // #D4AF37 - Oro brillante
        private readonly Color colorBloodRed = Color.FromArgb(122, 11, 11); // #7A0B0B - Rojo sangre
        private readonly Color colorLightGray = Color.FromArgb(192, 192, 192); // #C0C0C0 - Gris claro
        private readonly Color colorMediumGray = Color.FromArgb(128, 128, 128); // #808080 - Gris medio
        
        // Estado colors - Todos en estilo épico dorado
        private readonly Color colorChecking = Color.FromArgb(184, 134, 11); // Oro viejo
        private readonly Color colorCheckingEnd = Color.FromArgb(212, 175, 55); // Oro brillante
        private readonly Color colorDownloading = Color.FromArgb(184, 134, 11); // Oro viejo
        private readonly Color colorDownloadingEnd = Color.FromArgb(212, 175, 55); // Oro brillante
        private readonly Color colorPaused = Color.FromArgb(150, 120, 10); // Oro apagado
        private readonly Color colorPausedEnd = Color.FromArgb(170, 140, 15);
        private readonly Color colorReady = Color.FromArgb(184, 134, 11); // Oro viejo
        private readonly Color colorReadyEnd = Color.FromArgb(212, 175, 55); // Oro brillante
        private readonly Color colorDisabled = Color.FromArgb(60, 60, 65); // Gris oscuro
        private readonly Color colorDisabledEnd = Color.FromArgb(80, 80, 85); // Gris medio

        public MainForm()
        {
            InitializeComponent();
            // Fondo épico negro carbón
            this.BackColor = colorCharcoal;
            LoadConfiguration();
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromMinutes(30);
            
            // Iniciar verificación automáticamente al abrir
            this.Shown += async (s, e) =>
            {
                await StartAutoVerification();
            };
        }
        
        private async Task StartAutoVerification()
        {
            progressBar.Value = 0;
            txtLog.Clear();
            UpdateButtonState(ButtonState.Checking);

            try
            {
                await CheckAndDownloadFiles();
                UpdateButtonState(ButtonState.Ready);
                lblStatus.Text = "Everything ready to play!";
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
                UpdateButtonState(ButtonState.Disabled);
            }
        }
        
        // Métodos auxiliares para diseño profesional
        private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            
            return path;
        }
        
        private void DrawGlowEffect(Graphics g, Rectangle rect, Color glowColor, int blur)
        {
            using (var pen = new Pen(glowColor, 2))
            {
                for (int i = 1; i <= blur; i++)
                {
                    pen.Color = Color.FromArgb(255 / (i + 1), glowColor);
                    var glowRect = new Rectangle(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);
                    g.DrawRectangle(pen, glowRect);
                }
            }
        }
        
        private void DrawTextWithShadow(Graphics g, string text, Font font, Rectangle rect, Color textColor, StringFormat format)
        {
            // Sombra
            using (var shadowBrush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                var shadowRect = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width, rect.Height);
                g.DrawString(text, font, shadowBrush, shadowRect, format);
            }
            // Texto principal
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(text, font, textBrush, rect, format);
            }
        }
        
        private LinearGradientBrush CreateAdvancedGradient(Rectangle rect, Color start, Color end, float angle = 90f)
        {
            return new LinearGradientBrush(rect, start, end, angle);
        }
        
        
        private void UpdateButtonState(ButtonState state)
        {
            if (btnMain == null) return;
            
            currentButtonState = state;
            Color baseColor;
            Color endColor;
            Color hoverColor;
            string text;
            bool enabled;
            
            switch (state)
            {
                case ButtonState.Checking:
                    baseColor = colorChecking;
                    endColor = colorCheckingEnd;
                    hoverColor = Color.FromArgb(Math.Min(255, colorChecking.R + 30), Math.Min(255, colorChecking.G + 30), Math.Min(255, colorChecking.B + 30));
                    text = "🔍 COMPROBANDO...";
                    enabled = false;
                    break;
                case ButtonState.Downloading:
                    baseColor = colorDownloading;
                    endColor = colorDownloadingEnd;
                    hoverColor = Color.FromArgb(Math.Min(255, colorDownloading.R + 30), Math.Min(255, colorDownloading.G + 30), Math.Min(255, colorDownloading.B + 30));
                    text = "⏸ PAUSAR";
                    enabled = true;
                    break;
                case ButtonState.Paused:
                    baseColor = colorPaused;
                    endColor = colorPausedEnd;
                    hoverColor = Color.FromArgb(Math.Min(255, colorPaused.R + 30), Math.Min(255, colorPaused.G + 30), Math.Min(255, colorPaused.B + 30));
                    text = "▶ REANUDAR";
                    enabled = true;
                    break;
                case ButtonState.Ready:
                    baseColor = colorReady;
                    endColor = colorReadyEnd;
                    hoverColor = Color.FromArgb(Math.Min(255, colorReady.R + 30), Math.Min(255, colorReady.G + 30), Math.Min(255, colorReady.B + 30));
                    text = "▶ JUGAR";
                    enabled = true;
                    break;
                default: // Disabled
                    baseColor = colorDisabled;
                    endColor = colorDisabledEnd;
                    hoverColor = colorDisabled;
                    text = "⏸ ESPERANDO...";
                    enabled = false;
                    break;
            }
            
            btnMain.BackColor = baseColor;
            btnMain.ForeColor = enabled ? colorCharcoal : Color.FromArgb(100, 100, 100); // Texto negro sobre oro cuando enabled
            btnMain.Text = text;
            btnMain.Enabled = enabled;
            btnMain.Cursor = enabled ? Cursors.Hand : Cursors.No;
            btnMain.FlatAppearance.MouseOverBackColor = enabled ? hoverColor : baseColor;
            
            // Guardar colores para el Paint event
            btnMain.Tag = new { StartColor = baseColor, EndColor = endColor };
            
            // Forzar repintado con gradiente
            btnMain.Invalidate();
        }
        
        private Button CreateMainButton(Point location, Size size)
        {
            var button = new Button
            {
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(new FontFamily("Segoe UI"), 16F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
                ForeColor = colorCharcoal // Texto negro sobre oro
            };
            
            button.FlatAppearance.BorderSize = 0;
            
            // Crear región con bordes redondeados
            var path = GetRoundedRectanglePath(new Rectangle(0, 0, button.Width, button.Height), 6);
            button.Region = new Region(path);
            
            // Pintar con gradiente épico oro
            button.Paint += (s, e) =>
            {
                var btn = s as Button;
                if (btn == null) return;
                
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                
                // Obtener colores del gradiente
                Color startColor = btn.BackColor;
                Color endColor = btn.BackColor;
                if (btn.Tag != null && btn.Tag.GetType().GetProperty("StartColor") != null)
                {
                    var tag = btn.Tag as dynamic;
                    startColor = tag?.StartColor ?? btn.BackColor;
                    endColor = tag?.EndColor ?? btn.BackColor;
                }
                else
                {
                    // Default: gradiente oro épico
                    startColor = colorOldGold;
                    endColor = colorBrightGold;
                }
                
                // Crear gradiente oro vertical
                using (var brush = CreateAdvancedGradient(btn.ClientRectangle, startColor, endColor, 90f))
                {
                    e.Graphics.FillPath(brush, path);
                }
                
                // Borde dorado épico (2px)
                using (var pen = new Pen(colorOldGold, 2))
                {
                    e.Graphics.DrawPath(pen, path);
                }
                
                // Dibujar texto
                var textRect = btn.ClientRectangle;
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using (var textBrush = new SolidBrush(btn.ForeColor))
                {
                    e.Graphics.DrawString(btn.Text, btn.Font, textBrush, textRect, sf);
                }
            };
            
            return button;
        }

        private void InitializeComponent()
        {
            this.lblTitle = new Label();
            this.lblStatus = new Label();
            this.progressBar = new ProgressBar();
            this.btnMain = new Button();
            this.panelLog = new Panel();
            this.txtLog = new RichTextBox();
            this.SuspendLayout();

            // lblTitle - Épico: LINEAGE 2 en gris, L2TITAN en oro viejo
            this.lblTitle.AutoSize = false;
            this.lblTitle.Font = new Font(new FontFamily("Segoe UI"), 36F, FontStyle.Bold);
            this.lblTitle.ForeColor = colorLightGray; // Gris claro para LINEAGE 2
            this.lblTitle.Location = new Point(62, 60); // Centrado en 1024px
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(900, 60);
            this.lblTitle.TabIndex = 1;
            this.lblTitle.Text = "LINEAGE 2 L2TITAN";
            this.lblTitle.BackColor = Color.Transparent;
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            // Pintar título épico con L2TITAN en oro viejo
            this.lblTitle.Paint += (s, e) =>
            {
                var label = s as Label;
                if (label == null) return;
                
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                
                var font = label.Font;
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                
                // Medir texto
                var lineage2Size = e.Graphics.MeasureString("LINEAGE 2 ", font);
                var totalWidth = e.Graphics.MeasureString("LINEAGE 2 L2TITAN", font).Width;
                var startX = (label.Width - totalWidth) / 2;
                
                // Dibujar "LINEAGE 2 " en gris claro
                using (var brush = new SolidBrush(colorLightGray))
                {
                    e.Graphics.DrawString("LINEAGE 2 ", font, brush, new RectangleF(startX, 0, label.Width, label.Height), sf);
                }
                
                // Dibujar "L2TITAN" en oro viejo
                var l2titanX = startX + lineage2Size.Width;
                using (var brush = new SolidBrush(colorOldGold))
                {
                    e.Graphics.DrawString("L2TITAN", font, brush, new RectangleF(l2titanX, 0, label.Width - l2titanX, label.Height), sf);
                }
            };

            // Separador épico dorado
            var separator = new Panel
            {
                Location = new Point(62, 140),
                Size = new Size(900, 1),
                BackColor = Color.FromArgb(153, colorOldGold.R, colorOldGold.G, colorOldGold.B), // 60% opacidad
                Enabled = false
            };

            // lblStatus - Épico
            this.lblStatus.AutoSize = false;
            this.lblStatus.Font = new Font(new FontFamily("Segoe UI"), 12F, FontStyle.Regular);
            this.lblStatus.ForeColor = colorLightGray;
            this.lblStatus.Location = new Point(62, 160);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new Size(900, 25);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "Ready to verify...";
            this.lblStatus.BackColor = Color.Transparent;
            this.lblStatus.TextAlign = ContentAlignment.MiddleCenter;

            // progressBar - Épica con estilo gótico
            this.progressBar.Location = new Point(62, 200);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new Size(900, 40);
            this.progressBar.TabIndex = 3;
            this.progressBar.Style = ProgressBarStyle.Continuous;
            this.progressBar.ForeColor = colorOldGold;
            // Progress bar épica
            this.progressBar.Paint += (s, e) =>
            {
                var pb = s as ProgressBar;
                if (pb == null) return;
                
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                
                int radius = 4;
                var path = GetRoundedRectanglePath(pb.ClientRectangle, radius);
                
                // Fondo gris metálico
                using (var bgBrush = new SolidBrush(colorMetallicGray))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
                
                // Borde oro viejo sutil
                using (var borderPen = new Pen(Color.FromArgb(100, colorOldGold), 1))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
                
                // Progreso con gradiente oro épico
                if (pb.Value > 0)
                {
                    var progressWidth = (int)(pb.Width * pb.Value / 100.0);
                    if (progressWidth > 0)
                    {
                        var progressRect = new Rectangle(0, 0, progressWidth, pb.Height);
                        var progressPath = GetRoundedRectanglePath(progressRect, radius);
                        
                        using (var progressBrush = new LinearGradientBrush(
                            progressRect,
                            colorOldGold,
                            colorBrightGold,
                            LinearGradientMode.Horizontal))
                        {
                            e.Graphics.FillPath(progressBrush, progressPath);
                        }
                        progressPath.Dispose();
                    }
                }
                
                // Texto de porcentaje
                var percentText = $"{pb.Value}%";
                var font = new Font("Segoe UI", 11F, FontStyle.Bold);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using (var textBrush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(percentText, font, textBrush, pb.ClientRectangle, sf);
                }
                font.Dispose();
                path.Dispose();
            };

            // btnMain - Botón épico "JUGAR"
            this.btnMain = CreateMainButton(new Point(362, 260), new Size(300, 60));
            this.btnMain.Name = "btnMain";
            this.btnMain.TabIndex = 4;
            this.btnMain.Click += BtnMain_Click;

            // panelLog - Épico con borde dorado
            this.panelLog.Location = new Point(62, 340);
            this.panelLog.Name = "panelLog";
            this.panelLog.Size = new Size(900, 300);
            this.panelLog.BackColor = colorMetallicGray;
            this.panelLog.BorderStyle = BorderStyle.None;
            this.panelLog.Paint += (s, e) =>
            {
                var panel = s as Panel;
                if (panel == null) return;
                
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                
                int radius = 6;
                var path = GetRoundedRectanglePath(panel.ClientRectangle, radius);
                
                // Fondo gris metálico
                using (var bgBrush = new SolidBrush(colorMetallicGray))
                {
                    e.Graphics.FillPath(bgBrush, path);
                }
                
                // Borde dorado épico (2px)
                using (var borderPen = new Pen(colorOldGold, 2))
                {
                    e.Graphics.DrawPath(borderPen, path);
                }
                
                path.Dispose();
            };

            // txtLog - Épico con texto dorado
            this.txtLog.Location = new Point(20, 20);
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.Size = new Size(860, 260);
            this.txtLog.TabIndex = 6;
            this.txtLog.BackColor = colorMetallicGray;
            this.txtLog.ForeColor = colorOldGold; // Oro viejo
            this.txtLog.Font = new Font(new FontFamily("Consolas"), 10F);
            this.txtLog.BorderStyle = BorderStyle.None;
            this.txtLog.Dock = DockStyle.Fill;
            this.panelLog.Controls.Add(this.txtLog);

            // MainForm - Ventana épica 1024x680
            this.ClientSize = new Size(1024, 680);
            this.Padding = new Padding(0);
            this.Controls.Add(this.panelLog);
            this.Controls.Add(this.btnMain);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(separator);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Lineage 2 l2Titan Launcher";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void LoadConfiguration()
        {
            try
            {
                // Obtener la carpeta donde está el ejecutable del launcher
                string exePath = Application.ExecutablePath;
                string exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;
                
                // Buscar config.json en múltiples ubicaciones
                string? configPath = FindConfigFile();
                
                if (configPath != null && File.Exists(configPath))
                {
                    var config = JsonConvert.DeserializeObject<LauncherConfig>(File.ReadAllText(configPath));
                    // SIEMPRE usar la carpeta del launcher como GamePath (portable)
                    // Esto permite que el launcher funcione desde cualquier ubicación
                    gamePath = exeDir;
                    serverUrl = config?.ServerUrl ?? "http://tu-servidor.com/lineage2";
                    manifestUrl = config?.ManifestUrl ?? $"{serverUrl}/manifest.json";
                }
                else
                {
                    // Valores por defecto: usar la carpeta donde está el launcher
                    gamePath = exeDir;
                    serverUrl = "http://tu-servidor.com/lineage2";
                    manifestUrl = $"{serverUrl}/manifest.json";
                    
                    // Crear archivo de configuración por defecto en AppData
                    string appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Lineage2Launcher"
                    );
                    Directory.CreateDirectory(appDataPath);
                    string defaultConfigPath = Path.Combine(appDataPath, "config.json");
                    
                    var defaultConfig = new LauncherConfig
                    {
                        GamePath = gamePath,
                        ServerUrl = serverUrl,
                        ManifestUrl = manifestUrl,
                        GameExecutable = "system\\L2.exe",
                        GameParameters = ""
                    };
                    File.WriteAllText(defaultConfigPath, JsonConvert.SerializeObject(defaultConfig, Formatting.Indented));
                    LogMessage($"config.json file created at: {defaultConfigPath}");
                    LogMessage($"Game will be downloaded to: {gamePath}");
                    LogMessage("Please configure your server in config.json if necessary.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading configuration: {ex.Message}");
            }
        }

        private string? FindConfigFile()
        {
            // 1. Junto al ejecutable (para archivos únicos)
            string exePath = Application.ExecutablePath;
            string exeDir = Path.GetDirectoryName(exePath) ?? string.Empty;
            string configInExeDir = Path.Combine(exeDir, "config.json");
            if (File.Exists(configInExeDir))
            {
                return configInExeDir;
            }

            // 2. En AppData del usuario (ubicación persistente)
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lineage2Launcher",
                "config.json"
            );
            if (File.Exists(appDataPath))
            {
                return appDataPath;
            }

            // 3. En el directorio actual (compatibilidad hacia atrás)
            if (File.Exists("config.json"))
            {
                return Path.GetFullPath("config.json");
            }

            return null;
        }

        private async void BtnMain_Click(object? sender, EventArgs e)
        {
            if (currentButtonState == ButtonState.Ready)
            {
                // Iniciar juego
                BtnPlay_Click(sender, e);
                return;
            }
            
            if (currentButtonState == ButtonState.Downloading)
            {
                // Pausar descarga
                isDownloadPaused = true;
                UpdateButtonState(ButtonState.Paused);
                LogMessage("Download paused by user.");
                lblStatus.Text = "Download paused...";
                return;
            }
            
            if (currentButtonState == ButtonState.Paused)
            {
                // Reanudar descarga
                isDownloadPaused = false;
                UpdateButtonState(ButtonState.Downloading);
                LogMessage("Download resumed by user.");
                lblStatus.Text = "Resuming download...";
                return;
            }
            
            // Iniciar verificación/descarga
            progressBar.Value = 0;
            txtLog.Clear();
            UpdateButtonState(ButtonState.Checking);

            try
            {
                await CheckAndDownloadFiles();
                UpdateButtonState(ButtonState.Ready);
                lblStatus.Text = "Everything ready to play!";
            }
            catch (Exception ex)
            {
                LogMessage($"Error: {ex.Message}");
                MessageBox.Show($"Error during verification: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateButtonState(ButtonState.Disabled);
            }
        }

        private async Task CheckAndDownloadFiles()
        {
            LogMessage("Connecting to server...");
            lblStatus.Text = "Connecting to server...";

            // Descargar manifest con reintentos
            string manifestJson = string.Empty;
            const int manifestRetries = 3;
            for (int i = 0; i < manifestRetries; i++)
            {
                try
                {
                    if (i > 0)
                    {
                        LogMessage($"Retrying manifest download ({i}/{manifestRetries - 1})...");
                        await Task.Delay(2000 * i);
                    }

                    LogMessage($"Downloading manifest from: {manifestUrl}");
                    manifestJson = await httpClient.GetStringAsync(manifestUrl);
                    break;
                }
                catch (Exception ex)
                {
                    if (i == manifestRetries - 1)
                        throw new Exception($"Could not download manifest after {manifestRetries} attempts: {ex.Message}");
                    LogMessage($"Error downloading manifest: {ex.Message}");
                }
            }

            if (string.IsNullOrEmpty(manifestJson))
            {
                throw new Exception("Could not download manifest.");
            }

            fileManifest = JsonConvert.DeserializeObject<List<FileManifest>>(manifestJson) ?? new List<FileManifest>();

            if (fileManifest == null || fileManifest.Count == 0)
            {
                throw new Exception("The manifest is empty or invalid.");
            }

            LogMessage($"✓ Manifest downloaded successfully.");
            LogMessage($"Found {fileManifest.Count} files in manifest.");
            LogMessage($"Game path: {gamePath}");

            // Crear directorio del juego si no existe
            if (!Directory.Exists(gamePath))
            {
                Directory.CreateDirectory(gamePath);
                LogMessage($"Game directory created: {gamePath}");
            }

            int totalFiles = fileManifest.Count;
            int processedFiles = 0;
            int filesToDownload = 0;
            int filesOk = 0;
            long totalSizeToDownload = 0;

            // Primera pasada: verificar y contar
            LogMessage("\nVerifying local files...");
            foreach (var fileInfo in fileManifest)
            {
                var filePath = Path.Combine(gamePath, fileInfo.Path);
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

            LogMessage($"\nSummary:");
            LogMessage($"  Files OK: {filesOk}");
            LogMessage($"  Files to download: {filesToDownload}");
            if (totalSizeToDownload > 0)
            {
                LogMessage($"  Total size to download: {totalSizeToDownload / 1024 / 1024:F2} MB");
            }

            if (filesToDownload == 0)
            {
                LogMessage("\n✓ All files are up to date!");
                return;
            }

            LogMessage("\nStarting file download...");
            UpdateButtonState(ButtonState.Downloading);
            isDownloadPaused = false;

            // Segunda pasada: descargar
            foreach (var fileInfo in fileManifest)
            {
                // Verificar si está pausado
                while (isDownloadPaused)
                {
                    await Task.Delay(100);
                }
                
                processedFiles++;
                var filePath = Path.Combine(gamePath, fileInfo.Path);
                var fileUrl = $"{serverUrl}/{fileInfo.Path.Replace('\\', '/')}";

                lblStatus.Text = $"Verifying: {fileInfo.Path} ({processedFiles}/{totalFiles})";
                progressBar.Value = (int)((processedFiles * 100) / totalFiles);

                // Crear directorio si no existe
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
                    // Verificar checksum
                    var localHash = CalculateFileHash(filePath);
                    if (localHash != fileInfo.Hash)
                    {
                        needsDownload = true;
                    }
                    else
                    {
                        // Solo mostrar cada 10 archivos OK para no saturar el log
                        if (filesOk <= 10 || processedFiles % 10 == 0)
                        {
                            LogMessage($"✓ {fileInfo.Path} - OK");
                        }
                    }
                }

                if (needsDownload)
                {
                    await DownloadFile(fileUrl, filePath, fileInfo.Hash);
                }
            }

            LogMessage("\n✓ Verification and download completed!");
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
                        LogMessage($"Retry {retryCount}/{maxRetries - 1} for: {Path.GetFileName(filePath)}");
                        await Task.Delay(1000 * retryCount); // Esperar antes de reintentar
                    }

                    LogMessage($"Downloading: {Path.GetFileName(filePath)}");
                    lblStatus.Text = $"Downloading: {Path.GetFileName(filePath)}";

                    var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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
                                progressBar.Value = Math.Min(progress, 100);

                                // Mostrar velocidad de descarga cada 1MB
                                if (downloadedBytes % 1048576 < 8192)
                                {
                                    var elapsed = (DateTime.Now - startTime).TotalSeconds;
                                    if (elapsed > 0)
                                    {
                                        var speed = downloadedBytes / elapsed / 1024; // KB/s
                                        lblStatus.Text = $"Downloading: {Path.GetFileName(filePath)} ({speed:F0} KB/s)";
                                    }
                                }
                            }
                        }
                    }

                    // Verificar hash después de descargar
                    LogMessage($"Verifying integrity: {Path.GetFileName(filePath)}");
                    var downloadedHash = CalculateFileHash(filePath);
                    if (downloadedHash != expectedHash)
                    {
                        File.Delete(filePath);
                        throw new Exception($"Downloaded file does not match expected hash. Local hash: {downloadedHash.Substring(0, 8)}..., expected: {expectedHash.Substring(0, 8)}...");
                    }

                    LogMessage($"✓ Downloaded and verified: {Path.GetFileName(filePath)} ({(totalBytes > 0 ? $"{totalBytes / 1024 / 1024:F2} MB" : "unknown size")})");
                    return; // Éxito, salir del loop de reintentos
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
                        LogMessage($"✗ Error after {maxRetries} attempts: {Path.GetFileName(filePath)}");
                        throw new Exception($"Error downloading {Path.GetFileName(filePath)} after {maxRetries} attempts: {ex.Message}");
                    }
                }
            }

            // No debería llegar aquí, pero por si acaso
            throw lastException ?? new Exception($"Unknown error downloading {Path.GetFileName(filePath)}");
        }

        private string CalculateFileHash(string filePath)
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

        private void BtnPlay_Click(object? sender, EventArgs e)
        {
            try
            {
                // Buscar el ejecutable en system\L2.exe relativo a gamePath
                var exePath = Path.Combine(gamePath, "system", "L2.exe");
                
                LogMessage($"Looking for game executable: {exePath}");
                LogMessage($"Game path: {gamePath}");

                if (!File.Exists(exePath))
                {
                    MessageBox.Show($"Game executable not found: {exePath}\n\nMake sure the game files are downloaded and the executable exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogMessage($"✗ Game executable not found: {exePath}");
                    return;
                }

                LogMessage($"Starting game: {exePath}");
                lblStatus.Text = "Starting game...";

                // Cargar parámetros del juego desde config si existe
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
                    WorkingDirectory = gamePath,
                    Arguments = gameParameters,
                    UseShellExecute = true,
                    Verb = "runas" // Request administrator privileges
                };

                try
                {
                    Process.Start(startInfo);
                    LogMessage("Game started!");
                }
                catch (System.ComponentModel.Win32Exception winEx)
                {
                    // User cancelled UAC prompt or access denied
                    if (winEx.NativeErrorCode == 1223) // ERROR_CANCELLED
                    {
                        LogMessage("Game launch cancelled by user.");
                        MessageBox.Show("Game launch was cancelled. The game requires administrator privileges to run.", "Launch Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        throw; // Re-throw other Win32 exceptions
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                string detailedMessage = $"Error starting game: {errorMessage}";
                
                // Provide more helpful message for elevation errors
                if (errorMessage.Contains("elevation") || errorMessage.Contains("requires elevation") || errorMessage.Contains("La operación solicitada requiere elevación"))
                {
                    detailedMessage = "The game requires administrator privileges to run.\n\n" +
                                     "Please run the launcher as administrator, or right-click the game executable and select 'Run as administrator'.";
                }
                
                MessageBox.Show(detailedMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"Error: {errorMessage}");
            }
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogMessage), message);
                return;
            }

            // Colores épicos según el tipo de mensaje
            Color textColor = colorOldGold; // Por defecto oro viejo
            
            if (message.Contains("✓") || message.Contains("successfully") || message.Contains("completed"))
            {
                textColor = colorBrightGold; // Oro brillante para éxito
            }
            else if (message.Contains("✗") || message.Contains("Error") || message.Contains("error"))
            {
                textColor = colorBloodRed; // Rojo sangre para errores
            }
            else if (message.Contains("Warning") || message.Contains("warning"))
            {
                textColor = Color.FromArgb(255, 200, 100); // Amarillo para advertencias
            }
            
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = textColor;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            txtLog.SelectionColor = txtLog.ForeColor;
            txtLog.ScrollToCaret();
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

