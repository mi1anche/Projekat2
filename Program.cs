using DrugiProjekat.Services;
using DrugiProjekat.Utils;

namespace DrugiProjekat
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Info("===========================================");
            Logger.Info("               Nasa Server                 ");
            Logger.Info("===========================================");

            string prefix = "http://localhost:8080/";
            int maxParalelnihObrada = 4;
            int cacheSize = 10;

            Logger.Info($"Maks. paralelnih obrada: {maxParalelnihObrada}");
            Logger.Info($"Maks. velicina kesa: {cacheSize} unosa");

            var httpClient = new HttpClient();
            var queue = new RequestQueue();
            var cache = new LaunchCache(cacheSize);
            var apiService = new NasaApiService(httpClient);
            var processor = new RequestProcessor(queue, cache, apiService, maxParalelnihObrada);
            var server = new WebServer(prefix, queue, processor);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Logger.Info("Gasenje servera...");
                server.Stop();
                processor.Stop();
                processor.PrintStats();
                Environment.Exit(0);
            };

            try
            {
                server.Start();
                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Logger.Error($"Fatalna greska: {ex.Message}");
                Logger.Error("Na Windows-u pokrenite kao administrator ili registrujte URL:");
                Logger.Error("netsh http add urlacl url=http://localhost:8080/ user=Everyone");
            }
        }
    }
}