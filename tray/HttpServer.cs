using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ShutdownPcTray
{
    /// <summary>
    /// Lightweight HTTP server built on HttpListener.
    /// Serves the frontend SPA and provides the power management API.
    /// </summary>
    class HttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly int _port;
        private Thread _listenThread;
        private volatile bool _running;

        public HttpServer(int port)
        {
            _port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add(string.Format("http://localhost:{0}/", port));
        }

        public void Start()
        {
            try
            {
                _listener.Start();
                _running = true;
                _listenThread = new Thread(ListenLoop);
                _listenThread.IsBackground = true;
                _listenThread.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("Failed to start HTTP server on port {0}: {1}", _port, ex.Message), ex);
            }
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
                catch
                {
                    // Ignore transient errors
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath.TrimEnd('/');
                if (string.IsNullOrEmpty(path))
                    path = "/";

                // --- API routes ---
                if (path == "/api/power")
                {
                    HandlePowerApi(context);
                    return;
                }
                if (path == "/api/cancel")
                {
                    HandleCancelApi(context);
                    return;
                }

                // --- Static files ---
                ServeStaticFile(context, path);
            }
            catch (Exception ex)
            {
                WriteError(context, 500, ex.Message);
            }
        }

        private void HandlePowerApi(HttpListenerContext context)
        {
            var query = context.Request.QueryString;
            string action = query["action"] ?? "shutdown";
            string timeoutStr = query["timeout"] ?? "30";
            string forceStr = query["force"];

            int timeout = 30;
            int.TryParse(timeoutStr, out timeout);
            timeout = Math.Max(0, Math.Min(600, timeout));

            bool force = forceStr != "false";

            switch (action)
            {
                case "shutdown":
                    PowerManager.FirePowerAction("shutdown", timeout, force);
                    WriteJson(context, 200, new { ok = true, action, message = string.Format("Shut down in {0} seconds", timeout) });
                    return;

                case "restart":
                    PowerManager.FirePowerAction("restart", timeout, force);
                    WriteJson(context, 200, new { ok = true, action, message = string.Format("Restart in {0} seconds", timeout) });
                    return;

                case "hibernate":
                    PowerManager.FirePowerAction("hibernate", 0, false);
                    WriteJson(context, 200, new { ok = true, action, message = "Hibernating..." });
                    return;

                case "sleep":
                    PowerManager.Sleep();
                    WriteJson(context, 200, new { ok = true, action, message = "Sleeping..." });
                    return;

                case "logout":
                    PowerManager.FirePowerAction("logout", 0, false);
                    WriteJson(context, 200, new { ok = true, action, message = "Logging off..." });
                    return;

                default:
                    WriteJson(context, 400, new { ok = false, error = string.Format("Unknown action \"{0}\". Valid: shutdown, restart, hibernate, sleep, logout", action) });
                    return;
            }
        }

        private void HandleCancelApi(HttpListenerContext context)
        {
            string error = PowerManager.Cancel();
            if (error == null)
            {
                WriteJson(context, 200, new { ok = true, message = "Scheduled operation has been cancelled" });
            }
            else
            {
                WriteJson(context, 500, new { ok = false, message = "No pending operation to cancel, or cancellation failed", details = error });
            }
        }

        private void ServeStaticFile(HttpListenerContext context, string path)
        {
            // Normalize: remove leading "/", default to index.html
            string relativePath = path.TrimStart('/');
            if (string.IsNullOrEmpty(relativePath))
                relativePath = "index.html";

            if (!StaticFiles.Exists(relativePath))
            {
                // SPA fallback: serve index.html for unknown routes
                relativePath = "index.html";
                if (!StaticFiles.Exists(relativePath))
                {
                    WriteHtml(context, 200, "<h1>⚠️ Build not found</h1><p>Run <code>bun run build:frontend</code> first, or place <code>dist/</code> next to the executable.</p>");
                    return;
                }
            }

            try
            {
                byte[] data = StaticFiles.Read(relativePath);
                string mime = StaticFiles.GetMimeType(relativePath);

                context.Response.ContentType = mime;
                context.Response.ContentLength64 = data.Length;
                context.Response.OutputStream.Write(data, 0, data.Length);
                context.Response.OutputStream.Flush();
                context.Response.StatusCode = 200;
            }
            catch
            {
                WriteError(context, 500, "Failed to read file");
            }
            finally
            {
                try { context.Response.OutputStream.Close(); } catch { }
            }
        }

        private void WriteJson(HttpListenerContext context, int statusCode, object data)
        {
            string json = JsonSerialize(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
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

        private void WriteHtml(HttpListenerContext context, int statusCode, string html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        private void WriteError(HttpListenerContext context, int statusCode, string message)
        {
            WriteJson(context, statusCode, new { ok = false, error = message });
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }
}
