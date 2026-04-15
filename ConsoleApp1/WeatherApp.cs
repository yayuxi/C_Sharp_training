namespace ConsoleApp1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

class WeatherApp {
    static readonly HttpClient client = new HttpClient();

    // ── Geocoding models ──────────────────────────────────────────────────────

    class GeoResult {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }
    }

    class GeoResponse {
        [JsonPropertyName("results")]
        public List<GeoResult> Results { get; set; }
    }

    // ── Forecast models ───────────────────────────────────────────────────────

    class HourlyData {
        [JsonPropertyName("time")]
        public List<string> Time { get; set; }

        [JsonPropertyName("temperature_2m")]
        public List<double> Temperature { get; set; }

        [JsonPropertyName("precipitation_probability")]
        public List<int> PrecipitationProbability { get; set; }

        [JsonPropertyName("weathercode")]
        public List<int> WeatherCode { get; set; }
    }

    class ForecastResponse {
        [JsonPropertyName("hourly")]
        public HourlyData Hourly { get; set; }
    }

    // ── Weather code → human readable ─────────────────────────────────────────

    static string DescribeWeatherCode(int code) => code switch {
        0           => "Clear sky",
        1           => "Mainly clear",
        2           => "Partly cloudy",
        3           => "Overcast",
        45 or 48    => "Foggy",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow",
        80 or 81 or 82 => "Rain showers",
        95          => "Thunderstorm",
        _           => "Unknown"
    };

    // ── Step 1: resolve city name → coordinates ────────────────────────────────

    static async Task<GeoResult> GetCoordinatesAsync(string city) {
        string url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1";
        string json = await client.GetStringAsync(url);

        GeoResponse geo = JsonSerializer.Deserialize<GeoResponse>(json);

        if (geo.Results == null || geo.Results.Count == 0)
            throw new WeatherApiException($"City '{city}' not found.");

        return geo.Results[0];
    }

    // ── Step 2: fetch 7-day hourly forecast ───────────────────────────────────

    static async Task<ForecastResponse> GetForecastAsync(double lat, double lon) {
        string url = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "https://api.open-meteo.com/v1/forecast" +
            "?latitude={0}&longitude={1}" +
            "&hourly=temperature_2m,precipitation_probability,weathercode" +
            "&forecast_days=7",
            lat, lon);

        string json = await client.GetStringAsync(url);
        return JsonSerializer.Deserialize<ForecastResponse>(json);
    }

    // ── Main ──────────────────────────────────────────────────────────────────

    static async Task Main(string[] args) {
        try {
            Console.Write("Enter city name: ");
            string city = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(city)) {
                throw new WeatherApiException("City name cannot be empty.");
            }
            
            // Geocode
            GeoResult location = await GetCoordinatesAsync(city);
            Console.WriteLine($"\nShowing 7-day forecast for {location.Name}, {location.Country}\n");

            // Fetch weather
            ForecastResponse forecast = await GetForecastAsync(location.Latitude, location.Longitude);
            HourlyData hourly = forecast.Hourly;

            // LINQ: zip time + weather data into one collection, then group by day
            var dailySummaries = hourly.Time
                .Select((time, i) => new {
                    Date = DateTime.Parse(time).Date,
                    Temp = hourly.Temperature[i],
                    PrecipChance = hourly.PrecipitationProbability[i],
                    WeatherCode = hourly.WeatherCode[i]
                })
                .GroupBy(h => h.Date)
                .Select(day => new {
                    Date = day.Key,
                    MinTemp = day.Min(h => h.Temp),
                    MaxTemp = day.Max(h => h.Temp),
                    AvgPrecipChance = (int)day.Average(h => h.PrecipChance),
                    // Most frequent weather code = dominant condition for the day
                    Condition = DescribeWeatherCode(
                        day.GroupBy(h => h.WeatherCode)
                           .OrderByDescending(g => g.Count())
                           .First().Key
                    )
                });

            // Print
            var allHours = forecast.Hourly.Temperature;
            
            var hottestDay = dailySummaries.MaxBy(d => d.MaxTemp);
            var coldestDay = dailySummaries.MinBy(d => d.MinTemp);

            Console.WriteLine($"Highest temperature of the week: {hottestDay.Date:dddd} at {hottestDay.MaxTemp:F1}°C");
            Console.WriteLine($"Lowest temperature of the week: {coldestDay.Date:dddd} at {coldestDay.MinTemp:F1}°C");
            Console.WriteLine($"\n{"Day",-12} {"Condition",-20} {"Min°C",6} {"Max°C",6} {"Rain%",6}");
            Console.WriteLine(new string('─', 56));

            foreach (var day in dailySummaries) {
                Console.WriteLine(
                    $"{day.Date:ddd dd MMM,-12} {day.Condition,-16} {day.MinTemp,5:F1}° {day.MaxTemp,5:F1}° {day.AvgPrecipChance,5}%"
                );
            }
            
            // Build an anonymous object matching the JSON structure you want
            var report = new {
                city = location.Name,
                country = location.Country,
                searchedAt = DateTime.Now,
                weeklyHigh = new { day = hottestDay.Date.ToString("dddd"), tempC = hottestDay.MaxTemp },
                weeklyLow  = new { day = coldestDay.Date.ToString("dddd"), tempC = coldestDay.MinTemp },
                days = dailySummaries.Select(d => new {
                    date = d.Date.ToString("yyyy-MM-dd"),
                    condition = d.Condition,
                    minTempC = d.MinTemp,
                    maxTempC = d.MaxTemp,
                    precipitationChance = d.AvgPrecipChance
                }).ToList()
            };

// Serialize to JSON with indentation so it's human-readable
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            
            string filePath = "C:\\Users\\elias\\RiderProjects\\træning\\ConsoleApp1\\søgninger.json";

// Load existing searches, or start with an empty list
            List<object> allReports;
            if (File.Exists(filePath) && new FileInfo(filePath).Length > 0) {
                string existing = File.ReadAllText(filePath);
                allReports = JsonSerializer.Deserialize<List<object>>(existing);
            } else {
                allReports = new List<object>();
            }

// Add the new report
            allReports.Add(report);

// Write the whole list back
            File.WriteAllText(filePath, JsonSerializer.Serialize(allReports, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    public class WeatherApiException : Exception {
        public WeatherApiException(string message) : base(message) {
        }
    }
}