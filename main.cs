using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization; // Dahili JSON dönüştürücü
using Microsoft.Web.WebView2.WinForms;

namespace OptivonCodeEditor
{
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private WebView2 webView;
        private string currentFilePath = "";
        private JavaScriptSerializer jsonSerializer = new JavaScriptSerializer();

        public MainForm(string initialPath)
        {
            this.Text = "Optivon Code Editor";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.KeyPreview = true;

            int useDarkMode = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref useDarkMode, sizeof(int));

            MenuStrip menuBar = new MenuStrip { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White };
            
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Dosya") { ForeColor = Color.White };
            fileMenu.DropDownItems.Add("Yeni (Ctrl+N)", null, async (s, e) => await NewFile());
            fileMenu.DropDownItems.Add("Aç (Ctrl+O)", null, async (s, e) => await OpenFile());
            fileMenu.DropDownItems.Add("Kaydet (Ctrl+S)", null, async (s, e) => await SaveFile());
            fileMenu.DropDownItems.Add("-");
            fileMenu.DropDownItems.Add("Çıkış", null, (s, e) => Application.Exit());

            ToolStripMenuItem runMenu = new ToolStripMenuItem("Çalıştır") { ForeColor = Color.White };
            runMenu.DropDownItems.Add("Çalıştır (F5)", null, async (s, e) => await RunCode());

            menuBar.Items.Add(fileMenu);
            menuBar.Items.Add(runMenu);
            this.MainMenuStrip = menuBar;
            this.Controls.Add(menuBar);

            webView = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(webView);
            webView.BringToFront();

            this.KeyDown += async (s, e) => {
                if (e.KeyCode == Keys.F5) await RunCode();
                else if (e.Control && e.KeyCode == Keys.S) await SaveFile();
                else if (e.Control && e.KeyCode == Keys.N) await NewFile();
                else if (e.Control && e.KeyCode == Keys.O) await OpenFile();
            };

            InitWebView(initialPath);
        }

        private async void InitWebView(string initialPath)
        {
            await webView.EnsureCoreWebView2Async(null);
            
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            if (File.Exists(htmlPath))
            {
                webView.CoreWebView2.Navigate(htmlPath);
            }
            else
            {
                MessageBox.Show("index.html dosyası bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            webView.CoreWebView2.WebMessageReceived += async (s, e) => {
                string msg = e.TryGetWebMessageAsString();
                if (msg == "save")
                {
                    await SaveFile();
                }
            };

            if (!string.IsNullOrEmpty(initialPath) && File.Exists(initialPath))
            {
                currentFilePath = initialPath;
                string content = File.ReadAllText(initialPath);
                await SetCodeAsync(content, GetMode(initialPath));
                this.Text = "Optivon Code Editor - " + Path.GetFileName(initialPath);
            }
        }

        private async Task<string> GetCodeAsync()
        {
            string json = await webView.ExecuteScriptAsync("getEditorContent()");
            if (string.IsNullOrEmpty(json) || json == "null") return "";
            return jsonSerializer.Deserialize<string>(json);
        }

        private async Task SetCodeAsync(string code, string mode)
        {
            string escapedCode = jsonSerializer.Serialize(code);
            await webView.ExecuteScriptAsync(string.Format("setEditorContent({0}, '{1}')", escapedCode, mode));
        }

        private async Task NewFile()
        {
            currentFilePath = "";
            await SetCodeAsync("", "c_cpp");
            this.Text = "Optivon Code Editor - [Yeni Dosya]";
        }

        private async Task OpenFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Tüm Desteklenenler|*.cpp;*.c;*.h;*.cs;*.py;*.txt|C++ (*.cpp)|*.cpp|C# (*.cs)|*.cs|Python (*.py)|*.py|Tüm Dosyalar (*.*)|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = ofd.FileName;
                    string content = File.ReadAllText(currentFilePath);
                    await SetCodeAsync(content, GetMode(currentFilePath));
                    this.Text = "Optivon Code Editor - " + Path.GetFileName(currentFilePath);
                }
            }
        }

        private async Task<bool> SaveFile()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                using (SaveFileDialog sfd = new SaveFileDialog { Filter = "C++ (*.cpp)|*.cpp|C# (*.cs)|*.cs|Python (*.py)|*.py|Tüm Dosyalar (*.*)|*.*" })
                {
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        currentFilePath = sfd.FileName;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            string code = await GetCodeAsync();
            File.WriteAllText(currentFilePath, code);
            this.Text = "Optivon Code Editor - " + Path.GetFileName(currentFilePath);
            return true;
        }

        private async Task RunCode()
        {
            bool saved = await SaveFile();
            if (saved && !string.IsNullOrEmpty(currentFilePath))
            {
                string ext = Path.GetExtension(currentFilePath).ToLower();
                string folder = Path.GetDirectoryName(currentFilePath);
                string fileName = Path.GetFileName(currentFilePath);

                if (ext == ".cpp" || ext == ".c")
                {
                    string args = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && g++ \"{1}\" -o run.exe && run.exe\"", folder, fileName);
                    Process.Start(new ProcessStartInfo {
                        FileName = "cmd.exe",
                        Arguments = args,
                        UseShellExecute = true
                    });
                }
                else if (ext == ".py")
                {
                    string args = string.Format("/c start cmd.exe /k \"cd /d \"{0}\" && python \"{1}\"\"", folder, fileName);
                    Process.Start(new ProcessStartInfo {
                        FileName = "cmd.exe",
                        Arguments = args,
                        UseShellExecute = true
                    });
                }
            }
        }

        private string GetMode(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".cs") return "csharp";
            if (ext == ".py") return "python";
            if (ext == ".js") return "javascript";
            if (ext == ".html") return "html";
            return "c_cpp";
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : ""));
        }
    }
}