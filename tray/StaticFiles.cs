using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ShutdownPcTray
{
    /// <summary>
    /// Serves embedded static files (HTML, CSS, JS) from resources
    /// or from a dist/ directory next to the executable.
    /// </summary>
    static class StaticFiles
    {
        private static string _distDir;

        public static void Initialize()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            _distDir = Path.Combine(exeDir, "dist");

            if (!Directory.Exists(_distDir))
            {
                // Try parent directory (for dev mode)
                _distDir = Path.Combine(Path.GetDirectoryName(exeDir) ?? ".", "dist");
            }
        }

        public static bool Exists(string relativePath)
        {
            if (string.IsNullOrEmpty(_distDir))
                return false;
            string filePath = Path.Combine(_distDir, relativePath ?? "");
            return File.Exists(filePath);
        }

        public static byte[] Read(string relativePath)
        {
            string filePath = Path.Combine(_distDir, relativePath ?? "");
            return File.ReadAllBytes(filePath);
        }

        public static DateTime GetLastModified(string relativePath)
        {
            string filePath = Path.Combine(_distDir, relativePath ?? "");
            return File.GetLastWriteTimeUtc(filePath);
        }

        public static long GetLength(string relativePath)
        {
            string filePath = Path.Combine(_distDir, relativePath ?? "");
            return new FileInfo(filePath).Length;
        }

        public static string GetMimeType(string path)
        {
            string ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            string mime;
            if (_mimeMap.TryGetValue(ext, out mime))
                return mime;
            return "application/octet-stream";
        }

        private static readonly Dictionary<string, string> _mimeMap = new Dictionary<string, string>
        {
            { ".html", "text/html; charset=utf-8" },
            { ".css", "text/css; charset=utf-8" },
            { ".js", "application/javascript; charset=utf-8" },
            { ".svg", "image/svg+xml" },
            { ".png", "image/png" },
            { ".ico", "image/x-icon" },
            { ".json", "application/json" },
            { ".woff2", "font/woff2" },
            { ".woff", "font/woff" },
            { ".ttf", "font/ttf" },
        };
    }
}
