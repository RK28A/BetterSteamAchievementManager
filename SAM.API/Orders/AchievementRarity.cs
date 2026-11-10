using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SAM.API.Orders
{
    internal static class AchievementRarity
    {
        public sealed class Response { public Percentages achievementpercentages { get; set; } }
        public sealed class Percentages { public List<Entry> achievements { get; set; } }
        public sealed class Entry { public string name { get; set; } public double? percent { get; set; } }
        internal static Dictionary<string, double> Parse(string json)
        {
            var response = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 }.Deserialize<Response>(json);
            var items = response?.achievementpercentages?.achievements;
            if (items == null) throw new InvalidOperationException("Rareté mondiale indisponible.");
            return items.Where(a => a != null && !string.IsNullOrEmpty(a.name) && a.percent.HasValue && !double.IsNaN(a.percent.Value) && a.percent >= 0 && a.percent <= 100)
                .GroupBy(a => a.name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().percent.Value, StringComparer.Ordinal);
        }
        internal static async Task<Dictionary<string, double>> Fetch(uint gameId, CancellationToken cancel)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 8 * 1024 * 1024 };
            using var response = await client.GetAsync("https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid=" + gameId, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }
}
