using System;
using System.Diagnostics;

namespace ShutdownPcTray
{
    /// <summary>
    /// Executes Windows power management commands via shutdown.exe / rundll32.
    /// </summary>
    static class PowerManager
    {
        public static string ExecutePowerAction(string action)
        {
            try
            {
                var psi = new ProcessStartInfo("shutdown", GetArgs(action))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(10000);
                    string stderr = proc.StandardError.ReadToEnd();
                    if (proc.ExitCode == 0)
                        return null; // success
                    return string.IsNullOrWhiteSpace(stderr)
                        ? string.Format("shutdown.exe exited with code {0}", proc.ExitCode)
                        : stderr.Trim();
                }
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Fire-and-forget: start shutdown.exe without waiting.
        /// Used for scheduled actions like "shutdown -s -t 30".
        /// </summary>
        public static void FirePowerAction(string action, int timeout, bool force)
        {
            string args;
            switch (action)
            {
                case "shutdown":
                    args = string.Format("-s -t {0}{1}", timeout, force ? " -f" : "");
                    break;
                case "restart":
                    args = string.Format("-r -t {0}{1}", timeout, force ? " -f" : "");
                    break;
                case "hibernate":
                    args = "-h";
                    break;
                case "logout":
                    args = "-l";
                    break;
                default:
                    return;
            }

            try
            {
                var psi = new ProcessStartInfo("shutdown", args)
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                Process.Start(psi);
            }
            catch { }
        }

        /// <summary>
        /// Trigger sleep via rundll32.
        /// </summary>
        public static void Sleep()
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c rundll32.exe powrprof.dll,SetSuspendState 0 1 0")
                {
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
            }
            catch { }
        }

        /// <summary>
        /// Cancel scheduled shutdown.
        /// </summary>
        public static string Cancel()
        {
            return ExecutePowerAction("cancel");
        }

        private static string GetArgs(string action)
        {
            switch (action)
            {
                case "cancel": return "-a";
                default: return "-a";
            }
        }
    }
}
