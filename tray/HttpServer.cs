using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ShutdownPcTray
{
    /// <summary>
    /// Lightweight HTTP server built on TcpListener.
    /// Binds to 0.0.0.0 (all interfaces) without requiring admin rights
    /// or URL ACL configuration. Handles HTTP/1.0 and HTTP/1.1 requests.
    /// </summary>
    class HttpServer : IDisposable
    {
        private TcpListener _listener;
        private readonly int _port;
        private Thread _listenThread;
        private volatile bool _running;

        public HttpServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _running = true;
            _listenThread = new Thread(ListenLoop);
            _listenThread.IsBackground = true;
            _listenThread.Start();
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    // Ignore transient errors
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    // Read the request line and headers
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine))
                        return;

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        WriteRaw(stream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
                        return;
                    }

                    string method = parts[0];
                    string path = parts[1];

                    // Read headers to consume the full request
                    int contentLength = 0;
                    string line;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(line.Substring(15).Trim(), out contentLength);
                        }
                    }

                    // Consume body (if any)
                    if (contentLength > 0)
                    {
                        char[] body = new char[contentLength];
                        reader.Read(body, 0, contentLength);
                    }

                    if (method != "GET")
                    {
                        WriteRaw(stream, "HTTP/1.1 405 Method Not Allowed\r\nContent-Length: 0\r\n\r\n");
                        return;
                    }

                    // Normalize path
                    string uriPath = path;
                    int qIdx = path.IndexOf('?');
                    if (qIdx >= 0)
                        uriPath = path.Substring(0, qIdx);
                    if (uriPath.EndsWith("/"))
                        uriPath = uriPath.TrimEnd('/');
                    if (string.IsNullOrEmpty(uriPath))
                        uriPath = "/";

                    // Parse query string for API routes
                    var query = new Dictionary<string, string>();
                    if (qIdx >= 0 && qIdx + 1 < path.Length)
                    {
                        string qs = path.Substring(qIdx + 1);
                        foreach (var pair in qs.Split('&'))
                        {
                            var kv = pair.Split('=');
                            if (kv.Length == 2)
                                query[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                            else if (kv.Length == 1)
                                query[Uri.UnescapeDataString(kv[0])] = "";
                        }
                    }

                    // --- Route handling ---
                    if (uriPath == "/api/power")
                    {
                        HandlePowerApi(stream, query);
                    }
                    else if (uriPath == "/api/cancel")
                    {
                        HandleCancelApi(stream);
                    }
                    else
                    {
                        ServeStaticFile(stream, uriPath);
                    }
                }
            }
            catch
            {
                // Ignore client disconnects
            }
        }

        private void HandlePowerApi(NetworkStream stream, Dictionary<string, string> query)
        {
            string action;
            query.TryGetValue("action", out action);
            if (action == null) action = "shutdown";

            int timeout = 30;
            string timeoutStr;
            if (query.TryGetValue("timeout", out timeoutStr))
                int.TryParse(timeoutStr, out timeout);
            timeout = Math.Max(0, Math.Min(600, timeout));

            bool force = true;
            string forceStr;
            if (query.TryGetValue("force", out forceStr))
                force = forceStr != "false";

            switch (action)
            {
                case "shutdown":
                    PowerManager.FirePowerAction("shutdown", timeout, force);
                    WriteJson(stream, 200, new { ok = true, action, message = string.Format("Shut down in {0} seconds", timeout) });
                    return;

                case "restart":
                    PowerManager.FirePowerAction("restart", timeout, force);
                    WriteJson(stream, 200, new { ok = true, action, message = string.Format("Restart in {0} seconds", timeout) });
                    return;

                case "hibernate":
                    PowerManager.FirePowerAction("hibernate", 0, false);
                    WriteJson(stream, 200, new { ok = true, action, message = "Hibernating..." });
                    return;

                case "sleep":
                    PowerManager.Sleep();
                    WriteJson(stream, 200, new { ok = true, action, message = "Sleeping..." });
                    return;

                case "logout":
                    PowerManager.FirePowerAction("logout", 0, false);
                    WriteJson(stream, 200, new { ok = true, action, message = "Logging off..." });
                    return;

                default:
                    WriteJson(stream, 400, new { ok = false, error = string.Format("Unknown action \"{0}\". Valid: shutdown, restart, hibernate, sleep, logout", action) });
                    return;
            }
        }

        private void HandleCancelApi(NetworkStream stream)
        {
            string error = PowerManager.Cancel();
            if (error == null)
            {
                WriteJson(stream, 200, new { ok = true, message = "Scheduled operation has been cancelled" });
            }
            else
            {
                WriteJson(stream, 500, new { ok = false, message = "No pending operation to cancel, or cancellation failed", details = error });
            }
        }

        private void ServeStaticFile(NetworkStream stream, string uriPath)
        {
            string relativePath = uriPath.TrimStart('/');
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "index.html";

            if (!StaticFiles.Exists(relativePath))
            {
                // SPA fallback: serve index.html for unknown routes
                relativePath = "index.html";
                if (!StaticFiles.Exists(relativePath))
                {
                    string html = "<h1>⚠️ Build not found</h1><p>Run <code>bun run build:frontend</code> first.</p>";
                    WriteRaw(stream, StatusLine(200) + "Content-Type: text/html; charset=utf-8\r\nContent-Length: " + Encoding.UTF8.GetByteCount(html) + "\r\n\r\n" + html);
                    return;
                }
            }

            try
            {
                byte[] data = StaticFiles.Read(relativePath);
                string mime = StaticFiles.GetMimeType(relativePath);

                var sb = new StringBuilder();
                sb.Append(StatusLine(200));
                sb.Append("Content-Type: ").Append(mime).Append("\r\n");
                sb.Append("Content-Length: ").Append(data.Length).Append("\r\n");
                sb.Append("Connection: close\r\n\r\n");

                byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
                stream.Write(header, 0, header.Length);
                stream.Write(data, 0, data.Length);
                stream.Flush();
            }
            catch
            {
                WriteJson(stream, 500, new { ok = false, error = "Failed to read file" });
            }
        }

        // ── Response helpers ────────────────────────────────────────

        private static string StatusLine(int code)
        {
            string reason = code == 200 ? "OK" : code == 400 ? "Bad Request" : code == 500 ? "Internal Server Error" : "";
            return string.Format("HTTP/1.1 {0} {1}\r\n", code, reason);
        }

        private void WriteRaw(NetworkStream stream, string response)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(response);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private void WriteJson(NetworkStream stream, int statusCode, object data)
        {
            string json = JsonSerialize(data);
            byte[] body = Encoding.UTF8.GetBytes(json);

            var sb = new StringBuilder();
            sb.Append(StatusLine(statusCode));
            sb.Append("Content-Type: application/json; charset=utf-8\r\n");
            sb.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            sb.Append("Connection: close\r\n\r\n");

            byte[] header = Encoding.ASCII.GetBytes(sb.ToString());
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string JsonEncode(string s)
        {
            if (s == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.AppendFormat("\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string JsonSerialize(object data)
        {
            if (data == null)
                return "null";
            var sb = new StringBuilder();
            var type = data.GetType();
            sb.Append('{');
            bool first = true;
            foreach (var prop in type.GetProperties())
            {
                if (!first)
                    sb.Append(',');
                first = false;
                sb.Append(JsonEncode(prop.Name));
                sb.Append(':');
                object val = prop.GetValue(data, null);
                if (val == null)
                    sb.Append("null");
                else if (val is string)
                    sb.Append(JsonEncode((string)val));
                else if (val is bool)
                    sb.Append((bool)val ? "true" : "false");
                else if (val is int || val is long)
                    sb.Append(val.ToString());
                else
                    sb.Append(JsonEncode(val.ToString()));
            }
            sb.Append('}');
            return sb.ToString();
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
        }
    }
}
