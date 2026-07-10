using System;
using System.Net.Sockets;
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

            // Hide console window
            var handle = GetConsoleWindow();
            ShowWindow(handle, 0);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            int port = PromptForPortIfBusy(3021);
            if (port < 0) return;

            var app = new TrayApp(port);
            Application.Run();
            app.Dispose();
        }

        /// <summary>
        /// Check if a TCP port is free. If not, prompt user for a new port.
        /// Returns -1 if cancelled, or an available port number.
        /// </summary>
        private static int PromptForPortIfBusy(int defaultPort)
        {
            int port = defaultPort;
            while (port > 0)
            {
                if (IsPortFree(port))
                    return port;

                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    string.Format("端口 {0} 已被占用，请输入新的监听端口：", port),
                    "端口冲突",
                    (port + 1).ToString(),
                    -1, -1);

                if (string.IsNullOrWhiteSpace(input))
                    return -1;

                if (!int.TryParse(input.Trim(), out port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("无效端口号（1-65535），请重试。", "输入错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    port = -2;
                }
            }
            return -1;
        }

        private static bool IsPortFree(int port)
        {
            try
            {
                using (var tcp = new TcpClient())
                {
                    var ar = tcp.BeginConnect("127.0.0.1", port, null, null);
                    bool connected = ar.AsyncWaitHandle.WaitOne(500);
                    if (connected)
                    {
                        tcp.EndConnect(ar);
                        return false;
                    }
                    return true;
                }
            }
            catch
            {
                return true;
            }
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
