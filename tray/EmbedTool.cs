using System;
using System.IO;

namespace ShutdownPcTray
{
    /// <summary>
    /// Helper to embed the Bun server exe as a resource inside the tray exe.
    /// Usage (post-build):
    ///   EmbedTool.exe tray.exe server.exe  →  tray-merged.exe
    /// </summary>
    static class EmbedTool
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: EmbedTool <tray-exe> <server-exe> [output-exe]");
                return 1;
            }

            string trayExe = args[0];
            string serverExe = args[1];
            string outputExe = args.Length > 2 ? args[2] : "ShutdownPcTray-merged.exe";

            if (!File.Exists(trayExe))
            {
                Console.Error.WriteLine($"Tray exe not found: {trayExe}");
                return 1;
            }
            if (!File.Exists(serverExe))
            {
                Console.Error.WriteLine($"Server exe not found: {serverExe}");
                return 1;
            }

            // Read the server exe bytes
            byte[] serverData = File.ReadAllBytes(serverExe);
            long serverLen = serverData.Length;

            // Read the tray exe
            byte[] trayData = File.ReadAllBytes(trayExe);

            // Write output: tray exe + embedded marker + server length + server data
            using (var outStream = File.Create(outputExe))
            {
                // 1. Write the tray exe
                outStream.Write(trayData, 0, trayData.Length);

                // 2. Write alignment padding (align to 4KB boundary)
                long padding = (4096 - (outStream.Position % 4096)) % 4096;
                outStream.Write(new byte[padding], 0, (int)padding);

                // 3. Write embedded marker "SMPC_SRV\x00"
                byte[] marker = new byte[] {
                    (byte)'S', (byte)'M', (byte)'P', (byte)'C',
                    (byte)'_', (byte)'S', (byte)'R', (byte)'V', 0
                };
                outStream.Write(marker, 0, marker.Length);

                // 4. Write server exe length (8 bytes, little-endian)
                byte[] lenBytes = BitConverter.GetBytes(serverLen);
                outStream.Write(lenBytes, 0, lenBytes.Length);

                // 5. Write server exe data
                outStream.Write(serverData, 0, serverData.Length);
            }

            Console.WriteLine($"✅ Merged: {outputExe} (tray={trayData.Length} + server={serverLen} = {new FileInfo(outputExe).Length})");
            return 0;
        }
    }
}
