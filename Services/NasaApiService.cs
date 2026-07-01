using DrugiProjekat.Models;
using DrugiProjekat.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace DrugiProjekat.Services
{
    public class NasaApiService
    {
        private readonly HttpClient _client;
        private const string BaseUrl = "https://api.nasa.gov/insight_weather/?api_key=utICOTv0osRWGGHHNOyypagVwDPZB2aeuyFbpbWD&feedtype=json&ver=1.0";

        public NasaApiService(HttpClient client)
        {
            _client = client;
        }

        public async Task<List<WeatherResult>> FetchAndFilterAsync(Dictionary<string, string> filters)
        {
            Logger.Info("Saljem async zahtev ka NASA InSight API-u...");

            HttpResponseMessage response = await _client.GetAsync(BaseUrl);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);
            var solKeys = json["sol_keys"]?.ToObject<List<string>>() ?? new List<string>();

            Logger.Info($"Primljeno {solKeys.Count} solova sa API-a, primenjujem filtere...");

            var results = new List<WeatherResult>();

            foreach (var sol in solKeys)
            {
                var solData = json[sol];
                if (solData == null) continue;

                var mapped = MapSol(sol, solData);
                if (mapped != null && MatchesFilters(mapped, filters))
                    results.Add(mapped);
            }

            Logger.Info($"[API] Nakon filtriranja: {results.Count} solova.");
            return results;
        }

        private WeatherResult? MapSol(string sol, JToken token)
        {
            try
            {
                return new WeatherResult
                {
                    Sol = sol,
                    FirstUtc = token["First_UTC"]?.ToString(),
                    LastUtc = token["Last_UTC"]?.ToString(),
                    Season = token["Season"]?.ToString(),
                    AvgTemperature = token["AT"]?["av"]?.ToObject<double?>(),
                    MinTemperature = token["AT"]?["mn"]?.ToObject<double?>(),
                    MaxTemperature = token["AT"]?["mx"]?.ToObject<double?>(),
                    AvgWindSpeed = token["HWS"]?["av"]?.ToObject<double?>(),
                    MinWindSpeed = token["HWS"]?["mn"]?.ToObject<double?>(),
                    MaxWindSpeed = token["HWS"]?["mx"]?.ToObject<double?>(),
                    AvgPressure = token["PRE"]?["av"]?.ToObject<double?>(),
                    MinPressure = token["PRE"]?["mn"]?.ToObject<double?>(),
                    MaxPressure = token["PRE"]?["mx"]?.ToObject<double?>(),
                    WindDirection = token["WD"]?["most_common"]?["compass_point"]?.ToString()
                };
            }
            catch { return null; }
        }

        private bool MatchesFilters(WeatherResult w, Dictionary<string, string> filters)
        {
            foreach (var filter in filters)
            {
                switch (filter.Key.ToLower())
                {
                    case "sol":
                        if (w.Sol != filter.Value)
                            return false;
                        break;

                    case "season":
                        if (w.Season == null ||
                            !w.Season.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;

                    case "wind_direction":
                        if (w.WindDirection == null ||
                            !w.WindDirection.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;

                    case "min_avg_temp":
                        if (double.TryParse(filter.Value, out double minT) && w.AvgTemperature < minT)
                            return false;
                        break;

                    case "max_avg_temp":
                        if (double.TryParse(filter.Value, out double maxT) && w.AvgTemperature > maxT)
                            return false;
                        break;
                }
            }
            return true;
        }
    }
}
