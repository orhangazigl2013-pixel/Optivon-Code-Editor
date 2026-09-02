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
            this.Text = "Optivon Code Editor - [Yeni Dosya]";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(30, 30, 30);

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

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
            runMenu.DropDownItems.Add("Çalıştır (F5)", null, async (s, e) => await SaveFile(true));

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(runMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

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
                
                var options = new CoreWebView2EnvironmentOptions("--disable-features=Translate,Autofill --disable-component-update");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                
                await webView.EnsureCoreWebView2Async(env);

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
                ExecuteCode(currentFilePath);
            }
        }

        // SİYAH EKRAN / CMD PENCERESİ OLMADAN ARKA PLANDA ÇALIŞTIRMA
        private void ExecuteCode(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            string ext = Path.GetExtension(filePath).ToLower();
            string folder = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);

            string command = "";

            if (ext == ".cpp" || ext == ".c")
            {
                command = string.Format("/c cd /d \"{0}\" && g++ \"{1}\" -o run.exe && run.exe", folder, fileName);
            }
            else if (ext == ".py")
            {
                command = string.Format("/c cd /d \"{0}\" && python \"{1}\"", folder, fileName);
            }
            else if (ext == ".js")
            {
                command = string.Format("/c cd /d \"{0}\" && node \"{1}\"", folder, fileName);
            }

            if (!string.IsNullOrEmpty(command))
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    UseShellExecute = false,
                    CreateNoWindow = true, // Siyah CMD penceresini tamamen gizler
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);
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
                case ".h":
                case ".hpp": return "c_cpp";
                case ".py": return "python";
                case ".js": return "javascript";
                case ".ts": return "typescript";
                case ".html":
                case ".htm": return "html";
                case ".css": return "css";
                case ".json": return "json";
                case ".sql": return "sql";
                case ".php": return "php";
                case ".java": return "java";
                case ".rs": return "rust";
                case ".go": return "golang";
                case ".sh":
                case ".bash": return "sh";
                case ".bat":
                case ".cmd": return "batchfile";
                case ".xml": return "xml";
                case ".yaml":
                case ".yml": return "yaml";
                case ".md": return "markdown";
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
