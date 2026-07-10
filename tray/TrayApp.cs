using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ShutdownPcTray
{
    /// <summary>
    /// System tray wrapper: creates the tray icon, context menu,
    /// starts the HTTP server, and manages the lifecycle.
    /// </summary>
    class TrayApp : IDisposable
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private NotifyIcon _tray;
        private HttpServer _server;
        private int _port;

        public TrayApp(int port)
        {
            _port = port;

            // Hide console
            var handle = GetConsoleWindow();
            ShowWindow(handle, 0);

            // Initialize embedded static files
            EmbeddedFiles.Setup();

            // Start HTTP server
            _server = new HttpServer(port);
            _server.Start();

            CreateTray();
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
            menu.Items.Add("取消计划操作", null, (s, e) => CancelOperation());
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

        private void OnOpen(object sender, EventArgs e)
        {
            try
            {
                string url = string.Format("http://localhost:{0}/", _port);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private void CancelOperation()
        {
            string error = PowerManager.Cancel();
            if (error == null)
                _tray.ShowBalloonTip(3000, "Shutdown My PC", "已取消计划操作", ToolTipIcon.Info);
            else
                _tray.ShowBalloonTip(3000, "Shutdown My PC", "没有待取消的操作", ToolTipIcon.Info);
        }

        private void Quit()
        {
            _tray.Visible = false;
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
            Application.Exit();
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

        public void Dispose()
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }
        }
    }
}
