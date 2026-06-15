using DrugiProjekat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DrugiProjekat.Utils;

namespace DrugiProjekat.Services
{
    public class WebServer
    {
        private readonly HttpListener _listener;
        private readonly RequestQueue _queue;
        private readonly RequestProcessor _processor;
        private bool _isRunning = false;
        private int _requestCounter = 0;

        public WebServer(string prefix, RequestQueue queue, RequestProcessor processor)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _queue = queue;
            _processor = processor;
        }

        public void Start()
        {
            _listener.Start();
            _isRunning = true;
            Logger.Info($"Server pokrenut. Slusa na: {string.Join(", ", _listener.Prefixes)}");
            Logger.Info("Dostupne rute:");
            Logger.Info("GET /launches           -> svi solovi");
            Logger.Info("GET /launches?sol=675   -> filtriranje po solu");
            Logger.Info("GET /launches?season=fall");
            Logger.Info("GET /launches?wind_direction=WNW");
            Logger.Info("GET /launches?min_avg_temp=-65");
            Logger.Info("GET /launches?max_avg_temp=-60");
            Logger.Info("GET /status             -> statistike servera");

            _processor.Start();
            _ = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = await _listener.GetContextAsync(); //ceka se novi zahtev bez blokiranja glavnog threada
                    _ = Task.Run(() => HandleContextAsync(context));
                }
                catch (HttpListenerException) when (!_isRunning)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Greska pri prijemu zahteva: {ex.Message}");
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var resp = context.Response;

            Logger.Req($"{req.HttpMethod} {req.Url?.PathAndQuery}");

            resp.Headers.Add("Content-Type", "application/json; charset=utf-8");
            resp.Headers.Add("Access-Control-Allow-Origin", "*");

            try
            {
                string path = req.Url?.AbsolutePath ?? "/";

                if (req.HttpMethod != "GET")
                {
                    SendResponse(resp, 405, "{\"error\": \"Samo GET metoda je podrzana.\"}");
                    return;
                }

                if (path == "/status")
                {
                    _processor.PrintStats();
                    SendResponse(resp, 200, "{\"status\": \"running\", \"message\": \"Statistike su ispisane u konzolu.\"}");
                    return;
                }

                if (path != "/launches")
                {
                    SendResponse(resp, 404, "{\"error\": \"Ruta nije pronadjena. Koristite /launches ili /status\"}");
                    return;
                }

                var filters = ParseFilters(req.Url?.Query ?? "");
                string requestId = $"REQ-{Interlocked.Increment(ref _requestCounter):D5}";
                string query = req.Url?.Query ?? "";

                var clientRequest = new ClientRequest(requestId, query, filters);
                bool enqueued = _queue.Enqueue(clientRequest);
                if (!enqueued)
                {
                    SendResponse(resp, 503, "{\"error\": \"Server preopterecen. Pokusajte ponovo.\"}");
                    return;
                }

                bool completed = await clientRequest.ResponseSource.Task
                    .WaitAsync(TimeSpan.FromSeconds(30))
                    .ContinueWith(t => t.IsCompletedSuccessfully);

                if (!completed)
                {
                    SendResponse(resp, 504, "{\"error\": \"Zahtev nije obradjen na vreme (timeout).\"}");
                    return;
                }

                var (statusCode, body) = clientRequest.ResponseSource.Task.Result;
                SendResponse(resp, statusCode, body);
            }
            catch (Exception ex)
            {
                Logger.Error($"Greska pri obradi zahteva: {ex.Message}");
                SendResponse(resp, 500, $"{{\"error\": \"Interna greska: {ex.Message.Replace("\"", "'")}\"}}");
            }
        }

        private Dictionary<string, string> ParseFilters(string queryString)
        {
            var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(queryString)) return filters;

            var qs = queryString.TrimStart('?');
            var parts = qs.Split('&', StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    string key = Uri.UnescapeDataString(kv[0]).Trim();
                    string value = Uri.UnescapeDataString(kv[1]).Trim();
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        filters[key] = value;
                }
            }

            if (filters.Count > 0)
                Logger.Info($"Filteri: {string.Join(", ", filters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            return filters;
        }

        private void SendResponse(HttpListenerResponse resp, int statusCode, string body)
        {
            try
            {
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(body);
                resp.StatusCode = statusCode;
                resp.ContentLength64 = buffer.Length;
                resp.OutputStream.Write(buffer, 0, buffer.Length);
                resp.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"Greska pri slanju odgovora: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
            _queue.Shutdown();
            Logger.Info("Server ugasen.");
        }
    }
}
