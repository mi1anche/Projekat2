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
    public class SpaceXApiService
    {
        private readonly HttpClient _client;
        private const string BaseUrl = "https://api.spacexdata.com/v5/launches/past";

        public SpaceXApiService(HttpClient client)
        {
            _client = client;
        }

        // async umesto .Result blokiranja - ovde Task ima smisla jer cekamo I/O
        public async Task<List<LaunchResult>> FetchAndFilterAsync(Dictionary<string, string> filters)
        {
            Logger.Info("Saljem async zahtev ka SpaceX API-u...");

            HttpResponseMessage response = await _client.GetAsync(BaseUrl);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            var launches = JArray.Parse(body);

            Logger.Info($"Primljeno {launches.Count} letova sa API-a, primenjujem filtere...");

            var results = new List<LaunchResult>();

            foreach (var launch in launches)
            {
                var mapped = MapLaunch(launch);
                if (mapped != null && MatchesFilters(mapped, filters))
                    results.Add(mapped);
            }

            Logger.Info($"[API] Nakon filtriranja: {results.Count} letova.");
            return results;
        }

        private LaunchResult? MapLaunch(JToken token)
        {
            try
            {
                return new LaunchResult
                {
                    Id = token["id"]?.ToString(),
                    Name = token["name"]?.ToString(),
                    DateUtc = token["date_utc"]?.ToString(),
                    Success = token["success"]?.ToObject<bool?>(),
                    Upcoming = token["upcoming"]?.ToObject<bool>() ?? false,
                    Details = token["details"]?.ToString(),
                    FlightNumber = token["flight_number"]?.ToObject<int>() ?? 0,
                    RocketId = token["rocket"]?.ToString(),
                    LaunchpadId = token["launchpad"]?.ToString(),
                    WebcastUrl = token["links"]?["webcast"]?.ToString(),
                    ArticleUrl = token["links"]?["article"]?.ToString(),
                    WikipediaUrl = token["links"]?["wikipedia"]?.ToString()
                };
            }
            catch { return null; }
        }

        private bool MatchesFilters(LaunchResult launch, Dictionary<string, string> filters)
        {
            foreach (var filter in filters)
            {
                switch (filter.Key.ToLower())
                {
                    case "name":
                        if (launch.Name == null ||
                            !launch.Name.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
                            return false;
                        break;
                    case "success":
                        if (bool.TryParse(filter.Value, out bool sv) && launch.Success != sv)
                            return false;
                        break;
                    case "flight_number":
                        if (int.TryParse(filter.Value, out int fn) && launch.FlightNumber != fn)
                            return false;
                        break;
                    case "year":
                        if (int.TryParse(filter.Value, out int yr) &&
                            (!launch.DateUtc?.StartsWith(yr.ToString()) ?? true))
                            return false;
                        break;
                }
            }
            return true;
        }
    }
}
