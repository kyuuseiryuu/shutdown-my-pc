using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShutdownPcTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Ensure single instance
            bool createdNew;
            using (new Mutex(true, "ShutdownMyPC-SingleInstance", out createdNew))
            {
                if (!createdNew)
                    return;
            }

            // Hide console window immediately
            var handle = GetConsoleWindow();
            ShowWindow(handle, 0);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var app = new TrayApp();
            Application.Run();
            app.Dispose();
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }

    partial class TrayApp : IDisposable
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        // ── Fields ─────────────────────────────────────────────────
        private NotifyIcon _tray;
        private HttpClient _http;
        private Process _serverProc;
        private string _serverExePath;
        private string _baseUrl = "http://localhost:3021";

        // EMBEDDED_SERVER_LEN is defined by build.js which generates ServerSize.cs
        // with the actual value. If ServerSize.cs doesn't exist, use 0 (standalone mode).
#if EMBEDDED_SERVER_LEN
        private const long SERVER_LEN = SERVER_LEN_BUILD;
#else
        private const long SERVER_LEN = 0;
#endif

        public TrayApp()
        {
            _http = new HttpClient();
            _http.Timeout = TimeSpan.FromSeconds(5);

            var handle = GetConsoleWindow();
            ShowWindow(handle, 0);

            _serverExePath = ExtractServerExe();
            if (_serverExePath == null)
            {
                // Fallback: look for adjacent file
                _serverExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shutdown-my-pc.exe");
                if (!File.Exists(_serverExePath))
                    _serverExePath = "shutdown-my-pc.exe";
            }

            StartServer();
            CreateTray();
        }

        /// <summary>
        /// Extract embedded server exe.
        /// Structure: [tray exe N bytes] + [server exe SERVER_LEN bytes]
        /// No marker, no padding — the server data is simply appended after the tray exe.
        /// </summary>
        private string ExtractServerExe()
        {
            if (SERVER_LEN <= 0)
                return null;

            try
            {
                string selfPath = Assembly.GetExecutingAssembly().Location;
                long selfLen = new FileInfo(selfPath).Length;
                long trayLen = selfLen - SERVER_LEN;

                if (trayLen <= 0 || trayLen > selfLen)
                    return null;

                // Extract server portion: from trayLen to end of file
                string tempDir = Path.Combine(Path.GetTempPath(), "ShutdownMyPc");
                Directory.CreateDirectory(tempDir);
                string tempExe = Path.Combine(tempDir, "shutdown-my-pc.exe");

                using (var src = File.OpenRead(selfPath))
                using (var dst = new FileStream(tempExe, FileMode.Create, FileAccess.Write))
                {
                    src.Seek(trayLen, SeekOrigin.Begin);
                    byte[] buf = new byte[1024 * 1024]; // 1 MB buffer
                    long remaining = SERVER_LEN;
                    while (remaining > 0)
                    {
                        int read = src.Read(buf, 0, (int)Math.Min(buf.Length, remaining));
                        if (read <= 0) break;
                        dst.Write(buf, 0, read);
                        remaining -= read;
                    }
                }

                return tempExe;
            }
            catch
            {
                return null;
            }
        }

        private void StartServer()
        {
            try
            {
                var psi = new ProcessStartInfo(_serverExePath);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                _serverProc = Process.Start(psi);
                _serverProc.BeginOutputReadLine();
                _serverProc.BeginErrorReadLine();
            }
            catch { }
        }

        public void WaitForServer()
        {
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    var resp = _http.GetAsync(_baseUrl + "/api/cancel")
                        .GetAwaiter().GetResult();
                    if (resp.StatusCode == System.Net.HttpStatusCode.OK ||
                        resp.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                        return;
                }
                catch { }
                Thread.Sleep(500);
            }
        }

        private void CreateTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = LoadTrayIcon();
            _tray.Text = "Shutdown My PC";
            _tray.Visible = true;

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开面板", null, OnOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("取消计划操作", null, (s, e) => CallApi("/api/cancel", ""));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => Quit());

            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += OnOpen;

            // Startup balloon after 2s
            var startupTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    _tray.ShowBalloonTip(3000, "Shutdown My PC",
                        "程序已启动，托盘菜单可管理电源操作", ToolTipIcon.Info);
                }
                catch { }
            }, null, 2000, System.Threading.Timeout.Infinite);
        }

        private Icon LoadTrayIcon()
        {
            try
            {
                byte[] data = Convert.FromBase64String(ICON_B64);
                using (var ms = new MemoryStream(data))
                    return new Icon(ms);
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private static readonly string ICON_B64 =
            "AAABAAEAQEAAAAEAIADTBQAAFgAAAIlQTkcNChoKAAAADUlIRFIAAABAAAAAQAgGAAAAqmlx3gAAAAFz" +
            "UkdCAK7OHOkAAAAEZ0FNQQAAsY8L/GEFAAAACXBIWXMAAA7DAAAOwwHHb6hkAAAFaElEQVR4Xu2bPWzc" +
            "NhTHgwBBgiBIHbQI3BRtjX6g7gdaF03S9AO1i6CFcZZEjxk9evSY0VtHjxk7ZsyYsWNGjxkzZkw2d4lY" +
            "/Kh7F97TSSdK1Fl3KIEfYJxJifzz8b1HSrpw4f/SrlhjNuye2bGpObCpOT4v8iQ7dP1Ikk3dx6jFGrPG" +
            "YPPUPM5Tc2ZTY4dInpinTpTRaF2PoVUZD/z4TZK99m/0/Pf79p+ff7GPf7htj7/YPDf+3vre9eN0e2dK" +
            "iDep+TdPspNOQmBaeZK9kos++/U3e/jxht24etXy76GxdumSPfjwI/vkzt23FpGas3wve6DHNrdg7qjI" +
            "RVB39+bN0g2HzNb1d6aFSLITPcbKMnZsruGjb7+zVy5eLN1gWTj65FPfGh7psZYKZi8zzxrTF1xGsN6z" +
            "vTQfW8KhHvOk4DDE2THz+kLLDL5LnKMdmS09dlec1xyv+WU2+yqY1GIpZE/02N3sS3zfefe9UuNVYP3y" +
            "ZZZC4RO0FbjkITX26Y8/lRquEidffzPbIZJB8Q/iqG60ShAexwK8nAyebE88P8mEbrRqvLj/x/Qy4A9x" +
            "frryKkL67KxAMkSbmH1+IHPSlVcR8pvxMjgq1n9qjhYd+9lPEG2EzWvXSnX6wssOjwsLKPbWvWZ+OJ+/" +
            "vvyqtGvTsMvEU/e59xALWIgA++vvzx10FS//3HWzFTspW4gAmHXbgWsQImZ47l0AybvrkAMVgbMGXUcT" +
            "y0f1JgCmKvn2LDg9Yibrcg3WPj7g1e6o1B4Qq659E3oToGrwxN2qUySWyqw1ziARYpK7e2Ats9o0pRcB" +
            "/IMHgVms8uYMgGUg9dio6DpA9JhkbkpUXbcp0QVgFnUH6XRdbNd+4uFnn5fqCFgDpq/vgei6bhOiC6C9" +
            "PTNaN3jwOuGYd29EEIsRWB5VllNHVAGI836noMrsfUIFAPyIdo74CV1vHlEF0LPftENtBADta9pYQTQB" +
            "9NoP6UxbAXCeJEZ+21BfEE0AOV0RQjxzWwEAh+m3xUHqOnVEE0CHJ/yBrlNFFwHwBX5bLC8kOYoiAKau" +
            "OxGSnHQRAHRECDnIjSKAnK0JoadJXQWQUx0hZLMURQBCnd+B0NPkrgLo9nWJlCaKACjudyDEAYIeQMi9" +
            "QWeSIe2jCKATIHZ6uk4dXQXo0j6KAPdu3JjqQIdQFDwA0DvPhfsAHYpIUXWdOroKgM/x2zdJv4UoAoDO" +
            "y4kMuk4VOpkJuTfhVp8TVJ03zCKaADoUhbSfelCZGrekdJ0qtP/pEIK7CfDg1gdTHSFHD0mG2DJzzxDz" +
            "Bf/VF+C4XdepI5oApJ/aFEM3JqHo/ANClh5EEwD0hggrCFmPIWBdevvd5nFeVAH0Woauh5ZVkGv494HQ" +
            "2YeoAqgLTgjNDOehD0IgNPkSogsA2jFByBZ1HnrrzW6w7fV7EUCvz5DToSb4p0DkH138TC8CAAPG9BEi" +
            "JDVtAt4f30LK3Wbd+5QFSLKH/ND0QHPZKb0fwKsi/NDWqSwbJQvI0/QeP2BiuvIq4u0kD4olMBqti3Pp" +
            "I34Pjcmj+D2z4wQorCA75cfQvHzZkMNc91bs9vaViQASCWK9hDBUxAGW3hfmoyP+ETt+D4mpp0qJ2Z8S" +
            "gIIqqxwOvdk/1WN3Rd4YhZCnPMvA+Mmy+2hi5uxLkaSIpdA14xoKfppeWvuzCt8GjsNiHvLIaYgw828H" +
            "b55Pef6qQiXxB0Dm1HbndZ6wHxGzd4M3ZkOPtbZIaAR5a7PLLmwRyLeD/vbZTaYxa3p8jUrx8aR5JhcD" +
            "MimsAnhEJX+fBwxW/tbPCvIke9Hqo8lZBc859O+GhfHXL0We30cpvtZ20YIlMhzoV6Cp/wdxn/1gCcDr" +
            "HAAAAABJRU5ErkJggg==";

        private void OnOpen(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_baseUrl) { UseShellExecute = true });
            }
            catch { }
        }

        private async void CallApi(string path, string query)
        {
            try
            {
                string url = _baseUrl + path + "?" + query;
                var resp = await _http.GetAsync(url);
                string body = await resp.Content.ReadAsStringAsync();
                string msg = resp.IsSuccessStatusCode ? "操作已执行" : body;
                var icon = resp.IsSuccessStatusCode ? ToolTipIcon.Info : ToolTipIcon.Error;
                _tray.ShowBalloonTip(3000, "Shutdown My PC", msg, icon);
            }
            catch (Exception ex)
            {
                _tray.ShowBalloonTip(3000, "请求失败", ex.Message, ToolTipIcon.Error);
            }
        }

        private void Quit()
        {
            _tray.Visible = false;
            try { _serverProc.Kill(); } catch { }
            try { if (_serverExePath != null && _serverExePath.Contains(Path.GetTempPath())) File.Delete(_serverExePath); } catch { }
            Application.Exit();
        }

        public void Dispose()
        {
            if (_tray != null) _tray.Dispose();
            if (_http != null) _http.Dispose();
            if (_serverProc != null) _serverProc.Dispose();
        }
    }
}
