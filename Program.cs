using DrugiProjekat.Services;
using DrugiProjekat.Utils;

namespace DrugiProjekat
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Info("===========================================");
            Logger.Info("              SpaceX Server                ");
            Logger.Info("===========================================");

            string prefix = "http://localhost:8080/";
            int radneNiti = 4;
            int cacheSize = 10;

            Logger.Info($"Radne niti (Tasks): {radneNiti}");
            Logger.Info($"Maks. velicina kesa: {cacheSize} unosa");

            var httpClient = new HttpClient();
            var queue = new RequestQueue();
            var cache = new LaunchCache(cacheSize);
            var apiService = new SpaceXApiService(httpClient);
            var processor = new RequestProcessor(queue, cache, apiService, radneNiti);
            var server = new WebServer(prefix, queue, processor);

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Logger.Info("Gasenje servera...");
                server.Stop();
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