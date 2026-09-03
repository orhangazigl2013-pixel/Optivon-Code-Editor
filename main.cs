using System;
using System.IO;
using System.Drawing;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace OptivonCodeEditor
{
    public class MainForm : Form
    {
        private WebView2 webView;
        private string currentFilePath = "";
        private bool isWebViewReady = false;
        private string fileToOpenOnStartup = "";

        public MainForm(string[] args)
        {
            // UI Kasmasını önlemek için çift ara bellekleme (Double Buffering)
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            this.Text = "Optivon Code Editor - [Yeni Dosya]";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);

            // Dinamik Pencere İkonunu Atama
            this.Icon = CreateOptivonIcon();

            // logo.svg Yoksa Otomatik Oluştur
            EnsureLogoSvgExists();

            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                fileToOpenOnStartup = args[0];
            }

            InitMenu();
            InitWebView();
        }

        private void InitMenu()
        {
            MenuStrip menuStrip = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Dosya");
            fileMenu.DropDownItems.Add("Yeni (Ctrl+N)", null, async (s, e) => await NewFile());
            fileMenu.DropDownItems.Add("Aç (Ctrl+O)", null, async (s, e) => await OpenFile());
            fileMenu.DropDownItems.Add("Kaydet (Ctrl+S)", null, async (s, e) => await SaveFile(false));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Çıkış", null, (s, e) => Application.Exit());

            ToolStripMenuItem runMenu = new ToolStripMenuItem("Çalıştır");
            runMenu.DropDownItems.Add("Çalıştır & Hata Ayıkla (F5)", null, async (s, e) => await SaveFile(true));

            ToolStripMenuItem toolsMenu = new ToolStripMenuItem("Araçlar");
            toolsMenu.DropDownItems.Add("Terminal Aç", null, (s, e) => OpenTerminal());

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(runMenu);
            menuStrip.Items.Add(toolsMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        // Hızlı Başlatma İçi Optimize Edilmiş WebView2 Yükleyicisi
        private async void InitWebView()
        {
            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(webView);
            webView.BringToFront();

            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OptivonCodeEditor");
                
                // Gereksiz arka plan servislerini ve güncellemeleri kapatarak hızlı açılış sağlama
                var options = new CoreWebView2EnvironmentOptions(
                    "--disable-features=Translate,Autofill,CalculateNativeWinOcclusion --disable-component-update --enable-begin-frame-scheduling"
                );
                
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // Performans ayarları
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                webView.NavigationCompleted += async (s, e) =>
                {
                    isWebViewReady = true;

                    if (!string.IsNullOrEmpty(fileToOpenOnStartup))
                    {
                        await LoadFileDirectly(fileToOpenOnStartup);
                        fileToOpenOnStartup = "";
                    }
                };

                string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
                if (File.Exists(htmlPath))
                {
                    webView.Source = new Uri(htmlPath);
                }
                else
                {
                    MessageBox.Show("index.html dosyası bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 Başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task LoadFileDirectly(string filePath)
        {
            if (!File.Exists(filePath)) return;

            currentFilePath = filePath;
            
            string content = await Task.Run(() => File.ReadAllText(filePath, Encoding.UTF8));
            string mode = GetAceMode(currentFilePath);

            await SetCodeAsync(content, mode);
            this.Text = "Optivon Code Editor - " + currentFilePath;
        }

        private async Task SetCodeAsync(string code, string mode)
        {
            if (!isWebViewReady || webView == null || webView.CoreWebView2 == null) return;

            string escaped = await Task.Run(() => 
                code.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t")
            );

            string script = string.Format("setEditorContent(\"{0}\", \"{1}\");", escaped, mode);
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async Task NewFile()
        {
            currentFilePath = "";
            await SetCodeAsync("", "text");
            this.Text = "Optivon Code Editor - [Yeni Dosya]";
        }

        private async Task OpenFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Tüm Desteklenenler|*.cpp;*.c;*.h;*.cs;*.py;*.js;*.html;*.css;*.json;*.sql;*.php;*.java;*.rs;*.go;*.ts;*.sh;*.bat;*.xml;*.yaml;*.yml;*.txt|C/C++ (*.cpp;*.c;*.h)|*.cpp;*.c;*.h|C# (*.cs)|*.cs|Python (*.py)|*.py|Web (*.html;*.css;*.js)|*.html;*.css;*.js|Tüm Dosyalar (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    await LoadFileDirectly(ofd.FileName);
                }
            }
        }

        private async Task SaveFile(bool executeAfterSave)
        {
            if (!isWebViewReady || webView == null || webView.CoreWebView2 == null) return;

            if (string.IsNullOrEmpty(currentFilePath))
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "C++ (*.cpp)|*.cpp|C# (*.cs)|*.cs|Python (*.py)|*.py|JavaScript (*.js)|*.js|HTML (*.html)|*.html|Tüm Dosyalar (*.*)|*.*";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        currentFilePath = sfd.FileName;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            try
            {
                string result = await webView.CoreWebView2.ExecuteScriptAsync("getEditorContent()");
                
                if (result.StartsWith("\"") && result.EndsWith("\""))
                {
                    result = result.Substring(1, result.Length - 2);
                }
                
                await Task.Run(() => {
                    string unescaped = System.Text.RegularExpressions.Regex.Unescape(result);
                    File.WriteAllText(currentFilePath, unescaped, Encoding.UTF8);
                });

                this.Text = "Optivon Code Editor - " + currentFilePath;

                if (executeAfterSave)
                {
                    ExecuteCodeWithDebug(currentFilePath);
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Bu alana yazma izniniz yok! Dosyayı Masaüstü veya Belgeler gibi erişilebilir bir konuma kaydetmeyi deneyin.", "Erişim Engellendi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dosya kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Hızlı Hata Ayıklama & Terminal Çalıştırma (Debugging Engine Entegrasyonu)
        private async void ExecuteCodeWithDebug(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Debugging.cs içerisindeki DebuggerEngine modülünü çağırır
            DebugResult debugResult = await DebuggerEngine.RunAndDebugAsync(filePath);

            if (!debugResult.IsSuccess)
            {
                MessageBox.Show("--- DERLEME / ÇALIŞTIRMA HATASI ---\n\n" + debugResult.Error, "Hata Ayıklama (Debug Error)", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                // Başarılı ise CMD Terminal Pencersini Açıp Sonucu Gösterir
                string ext = Path.GetExtension(filePath).ToLower();
                string folder = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                string cmdArgs = "";
                if (ext == ".cpp" || ext == ".c")
                    cmdArgs = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && run.exe\"", folder);
                else if (ext == ".py")
                    cmdArgs = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && python \"{1}\"\"", folder, fileName);
                else if (ext == ".js")
                    cmdArgs = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && node \"{1}\"\"", folder, fileName);
                else if (ext == ".cs")
                    cmdArgs = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && \"{1}.exe\"\"", folder, Path.GetFileNameWithoutExtension(fileName));

                if (!string.IsNullOrEmpty(cmdArgs))
                {
                    Process.Start("cmd.exe", cmdArgs);
                }
            }
        }

        private void OpenTerminal()
        {
            string targetFolder = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                targetFolder = Path.GetDirectoryName(currentFilePath);
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = string.Format("/k \"cd /d \"{0}\"\"", targetFolder),
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        private Icon CreateOptivonIcon()
        {
            using (Bitmap bmp = new Bitmap(64, 64))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    using (Brush bgBrush = new SolidBrush(Color.FromArgb(20, 20, 30)))
                        g.FillRectangle(bgBrush, 0, 0, 64, 64);

                    using (Pen cyanPen = new Pen(Color.FromArgb(0, 210, 255), 5))
                        g.DrawEllipse(cyanPen, 8, 8, 48, 48);

                    using (Font iconFont = new Font("Consolas", 18, FontStyle.Bold))
                    using (Brush orangeBrush = new SolidBrush(Color.FromArgb(255, 128, 0)))
                    {
                        StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("</>", iconFont, orangeBrush, new RectangleF(0, 0, 64, 64), sf);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private void EnsureLogoSvgExists()
        {
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.svg");
            if (!File.Exists(logoPath))
            {
                string svgContent = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 512 512"" width=""100%"" height=""100%"">
  <defs>
    <linearGradient id=""bgGlow"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#1e1e2e""/>
      <stop offset=""100%"" stop-color=""#0f0f17""/>
    </linearGradient>
    <linearGradient id=""optivonCyan"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#00d2ff""/>
      <stop offset=""100%"" stop-color=""#3a7bd5""/>
    </linearGradient>
    <linearGradient id=""codeOrange"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" stop-color=""#ff9900""/>
      <stop offset=""100%"" stop-color=""#ff5500""/>
    </linearGradient>
  </defs>
  <rect width=""512"" height=""512"" rx=""110"" fill=""url(#bgGlow)""/>
  <circle cx=""256"" cy=""256"" r=""170"" fill=""none"" stroke=""url(#optivonCyan)"" stroke-width=""36"" stroke-dasharray=""800 200"" stroke-linecap=""round""/>
  <path d=""M 210 180 L 140 256 L 210 332"" fill=""none"" stroke=""url(#codeOrange)"" stroke-width=""32"" stroke-linecap=""round"" stroke-linejoin=""round""/>
  <path d=""M 275 170 L 237 342"" fill=""none"" stroke=""#ffffff"" stroke-width=""26"" stroke-linecap=""round"" opacity=""0.9""/>
  <path d=""M 302 180 L 372 256 L 302 332"" fill=""none"" stroke=""url(#codeOrange)"" stroke-width=""32"" stroke-linecap=""round"" stroke-linejoin=""round""/>
</svg>";
                try { File.WriteAllText(logoPath, svgContent, Encoding.UTF8); } catch { }
            }
        }

        private string GetAceMode(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".cs": return "csharp";
                case ".cpp":
                case ".c":
                case ".h": return "c_cpp";
                case ".py": return "python";
                case ".js": return "javascript";
                case ".ts": return "typescript";
                case ".html": return "html";
                case ".css": return "css";
                case ".json": return "json";
                case ".sql": return "sql";
                case ".php": return "php";
                case ".java": return "java";
                case ".rs": return "rust";
                case ".go": return "golang";
                case ".sh": return "sh";
                case ".bat": return "batchfile";
                case ".xml": return "xml";
                default: return "text";
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));
        }
    }
}
