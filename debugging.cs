using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OptivonCodeEditor
{
    // Hata ayıklama ve derleme sonuçlarını taşıyan veri yapısı
    public class DebugResult
    {
        public bool IsSuccess { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
    }

    public static class DebuggerEngine
    {
        /// <summary>
        /// Verilen kaynak kod dosyasını türüne göre derler veya hata denetiminden geçirir.
        /// Arka planda sessizce çalışıp hata ve çıktı durumunu raporlar.
        /// </summary>
        public static async Task<DebugResult> RunAndDebugAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                DebugResult result = new DebugResult
                {
                    IsSuccess = false,
                    Output = "",
                    Error = "",
                    ExitCode = -1
                };

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    result.Error = "Dosya bulunamadı veya geçersiz yol.";
                    return result;
                }

                string ext = Path.GetExtension(filePath).ToLower();
                string folder = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);
                string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);

                string command = "";

                // Dillerin derleme / syntax kontrol komutları
                if (ext == ".cpp" || ext == ".c")
                {
                    // g++ ile derleyip run.exe oluşturmayı dener
                    command = string.Format("/c cd /d \"{0}\" && g++ \"{1}\" -o run.exe", folder, fileName);
                }
                else if (ext == ".py")
                {
                    // Python kodu için syntax (sözdizimi) kontrolü yapar
                    command = string.Format("/c cd /d \"{0}\" && python -m py_compile \"{1}\"", folder, fileName);
                }
                else if (ext == ".js")
                {
                    // Node.js ile sözdizimi kontrolü yapar
                    command = string.Format("/c cd /d \"{0}\" && node --check \"{1}\"", folder, fileName);
                }
                else if (ext == ".cs")
                {
                    // csc.exe ile dosyayı derlemeyi dener
                    command = string.Format("/c cd /d \"{0}\" && csc /target:exe /out:\"{1}.exe\" \"{2}\"", folder, fileNameNoExt, fileName);
                }
                else
                {
                    // Derleme gerektirmeyen metin/HTML vb. dosyalar için direkt başarılı döner
                    result.IsSuccess = true;
                    return result;
                }

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = command,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (Process process = new Process())
                    {
                        process.StartInfo = psi;
                        process.Start();

                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();

                        process.WaitForExit();

                        result.ExitCode = process.ExitCode;
                        result.Output = output;
                        result.Error = error;

                        // Çıkış kodu 0 ise ve hata akışı boşsa başarılı sayılır
                        result.IsSuccess = (process.ExitCode == 0 && string.IsNullOrWhiteSpace(error));
                    }
                }
                catch (Exception ex)
                {
                    result.IsSuccess = false;
                    result.Error = "Sistem Hatası (Derleyici Bulunamadı): " + ex.Message;
                }

                return result;
            });
        }
    }
}