using DrugiProjekat.Models;
using DrugiProjekat.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DrugiProjekat.Services
{
    public class RequestProcessor
    {
        private readonly RequestQueue _queue;
        private readonly LaunchCache _cache;
        private readonly NasaApiService _apiService;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxParalelnihObrada;
        private readonly Thread _dispatcherThread;

        private int _processedCount = 0;
        private int _errorCount = 0;

        public RequestProcessor(RequestQueue queue, LaunchCache cache, NasaApiService apiService, int maxParalelnihObrada = 4)
        {
            _queue = queue;
            _cache = cache;
            _apiService = apiService;
            _maxParalelnihObrada = maxParalelnihObrada;
            _semaphore = new SemaphoreSlim(maxParalelnihObrada, maxParalelnihObrada);

            _dispatcherThread = new Thread(DispatchLoop)
            {
                Name = "Dispatcher",
                IsBackground = true
            };
        }

        public void Start()
        {
            _dispatcherThread.Start();
            Logger.Info($"[Proces] Dispecer pokrenut. Maks. paralelnih obrada: {_maxParalelnihObrada}");
        }

        private void DispatchLoop()
        {
            while (true)
            {
                ClientRequest? request = _queue.Dequeue();
                if (request == null)
                    break;

                _semaphore.Wait();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessRequestAsync(request);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
            }

            Logger.Info("[Proces] Dispecer se gasi.");
        }

        private async Task ProcessRequestAsync(ClientRequest request)
        {
            int taskId = Task.CurrentId ?? -1;
            Logger.Req($"[Task {taskId}] Obrada zahteva {request.RequestId} | query='{request.Query}'");

            try
            {
                string cacheKey = BuildCacheKey(request.Filters);

                var cached = _cache.TryGet(cacheKey);
                if (cached != null)
                {
                    Logger.Req($"[Task {taskId}] Zahtev {request.RequestId} opsluzem iz kesa ({cached.Count} solova)");
                    request.ResponseSource.SetResult((200, SerializeResults(cached)));
                    Interlocked.Increment(ref _processedCount);
                    return;
                }

                _cache.ReservePlaceholder(cacheKey);

                try
                {
                    var fetchTask = _apiService.FetchAndFilterAsync(request.Filters);

                    var loggedTask = fetchTask.ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                            Logger.Info($"[ContinueWith] API vratio {t.Result.Count} solova za kljuc '{cacheKey}'");
                        else
                            Logger.Warn($"[ContinueWith] API poziv nije uspeo: {t.Exception?.InnerException?.Message}");

                        if (t.IsFaulted)
                            throw t.Exception!.InnerException!;

                        return t.Result;
                    }, TaskContinuationOptions.ExecuteSynchronously);

                    var results = await loggedTask;

                    _cache.Set(cacheKey, results);
                    request.ResponseSource.SetResult((200, SerializeResults(results)));
                    Interlocked.Increment(ref _processedCount);
                    Logger.Req($"[Task {taskId}] Zahtev {request.RequestId} uspesno obradjen ({results.Count} solova)");
                }
                catch (Exception ex)
                {
                    _cache.RemovePlaceholder(cacheKey);
                    throw new Exception($"Greska pri pozivu Nasa API-a: {ex.Message}", ex);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"[Task {taskId}] HTTP greska za zahtev {request.RequestId}: {ex.Message}");
                Interlocked.Increment(ref _errorCount);
                request.ResponseSource.SetResult((502, $"{{\"error\": \"Nasa API nije dostupan: {EscapeJson(ex.Message)}\"}}"));
            }
            catch (Exception ex)
            {
                Logger.Error($"[Task {taskId}] Greska pri obradi zahteva {request.RequestId}: {ex.Message}");
                Interlocked.Increment(ref _errorCount);
                request.ResponseSource.SetResult((500, $"{{\"error\": \"Interna greska servera: {EscapeJson(ex.Message)}\"}}"));
            }
        }

        private string BuildCacheKey(Dictionary<string, string> filters)
        {
            if (filters.Count == 0) return "ALL";
            var sorted = filters.OrderBy(kvp => kvp.Key)
                                .Select(kvp => $"{kvp.Key}={kvp.Value.ToLower()}");
            return string.Join("&", sorted);
        }

        private string SerializeResults(List<WeatherResult> results)
        {
            if (results.Count == 0)
                return "{\"message\": \"Nisu pronadjeni solovi koji odgovaraju zadatim filterima.\", \"count\": 0, \"results\": []}";
            return JsonConvert.SerializeObject(new { count = results.Count, results }, Newtonsoft.Json.Formatting.Indented);
        }

        private string EscapeJson(string s) => s.Replace("\"", "\\\"").Replace("\n", " ");

        public void Stop()
        {
            _dispatcherThread.Join();

            for (int i = 0; i < _maxParalelnihObrada; i++)
                _semaphore.Wait();

            Logger.Info("[Proces] Dispecer zaustavljen, sve obrade dovrsene.");
        }

        public void PrintStats()
        {
            Logger.Info($"Statistike -> obradjena: {_processedCount} | greske: {_errorCount}");
            _cache.PrintStats();
        }
    }
}