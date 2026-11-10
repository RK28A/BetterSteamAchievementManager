using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SAM.Picker
{
    internal sealed class CompletionResult
    {
        public double Hours { get; set; }
        public string Url { get; set; }
        public DateTime RetrievedUtc { get; set; }
    }
    internal static class CompletionSource
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 4 * 1024 * 1024 };
        internal static bool ValidUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https" &&
            u.Host.Equals("howlongtobeat.com", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(u.AbsolutePath, @"^/game/\d+/?$");
        internal static async Task<CompletionResult> Lookup(uint appId, string name, string url)
        {
            string data;
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!ValidUrl(url)) throw new InvalidOperationException("Utilisez une fiche https://howlongtobeat.com/game/1234.");
                using var response = await Http.GetAsync(url); response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(html, "<script[^>]*id=[\"']__NEXT_DATA__[\"'][^>]*>(.*?)</script>", RegexOptions.Singleline);
                if (!match.Success) throw new InvalidOperationException("Cette fiche ne fournit pas de données lisibles. Saisissez la moyenne manuellement.");
                data = match.Groups[1].Value;
            }
            else
            {
                var request = new { searchType = "games", searchTerms = name.Split(' '), searchPage = 1, size = 20,
                    searchOptions = new { games = new { userId = 0, platform = "", sortCategory = "name", rangeCategory = "main", rangeTime = new { min = 0, max = 0 }, gameplay = new { perspective = "", flow = "", genre = "" }, modifier = "" }, users = new { }, filter = "", sort = 0, randomizer = 0 } };
                using var body = new StringContent(new JavaScriptSerializer().Serialize(request), Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync("https://howlongtobeat.com/api/search", body);
                response.EnsureSuccessStatusCode(); data = await response.Content.ReadAsStringAsync();
            }
            return Parse(data, appId, name, url);
        }
        internal static CompletionResult Parse(string json, uint appId, string name, string explicitUrl = null)
        {
            var root = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.DeserializeObject(json);
            foreach (var row in Objects(root))
            {
                if (!row.TryGetValue("comp_100", out var value) || !double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || seconds <= 0 || seconds > 8760 * 3600) continue;
                bool exactSteam = row.TryGetValue("profile_steam", out var steam) && Convert.ToString(steam, CultureInfo.InvariantCulture) == appId.ToString(CultureInfo.InvariantCulture);
                bool hasDifferentSteam = row.TryGetValue("profile_steam", out var other) && uint.TryParse(Convert.ToString(other, CultureInfo.InvariantCulture), out uint linked) && linked != 0 && linked != appId;
                bool exactName = row.TryGetValue("game_name", out var title) && string.Equals(Convert.ToString(title)?.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase);
                if (hasDifferentSteam || (!exactSteam && !exactName)) continue;
                if (!row.TryGetValue("game_id", out var id) || !int.TryParse(Convert.ToString(id), out int gameId) || gameId <= 0) continue;
                string source = "https://howlongtobeat.com/game/" + gameId;
                if (explicitUrl != null && new Uri(explicitUrl).AbsolutePath.TrimEnd('/') != new Uri(source).AbsolutePath) continue;
                return new CompletionResult { Hours = seconds / 3600, Url = source, RetrievedUtc = DateTime.UtcNow };
            }
            throw new InvalidOperationException("Moyenne 100 % indisponible ou jeu non identifié avec certitude. Saisie manuelle possible.");
        }
        private static IEnumerable<Dictionary<string, object>> Objects(object obj)
        {
            if (obj is Dictionary<string, object> map) { yield return map; foreach (var value in map.Values) foreach (var nested in Objects(value)) yield return nested; }
            else if (obj is object[] list) foreach (var value in list) foreach (var nested in Objects(value)) yield return nested;
        }
        // Read only the current account's cached playtime. Never upload localconfig.vdf.
        internal static double? SteamHours(string install, ulong steamId, uint gameId)
        {
            if (string.IsNullOrEmpty(install) || steamId == 0) return null;
            try
            {
                string path = Path.Combine(install, "userdata", ((uint)steamId).ToString(CultureInfo.InvariantCulture), "config", "localconfig.vdf");
                using var input = new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                return ParseSteamHours(input.ReadToEnd(), gameId);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return null; }
        }
        internal static double? ParseSteamHours(string vdf, uint gameId)
        {
            var tokens = Regex.Matches(vdf, "//[^\\r\\n]*|\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"|[{}]").Cast<Match>().Where(m => !m.Value.StartsWith("//")).ToArray();
            var path = new List<string>();
            for (int i = 0; i < tokens.Length;)
            {
                if (tokens[i].Value == "}") { if (path.Count == 0) return null; path.RemoveAt(path.Count - 1); i++; continue; }
                string key = tokens[i++].Groups[1].Value;
                if (i >= tokens.Length) break;
                if (tokens[i].Value == "{") { path.Add(key); i++; continue; }
                string val = tokens[i++].Groups[1].Value;
                string expected = "UserLocalConfigStore/Software/Valve/Steam/apps/" + gameId;
                if (string.Equals(string.Join("/", path), expected, StringComparison.OrdinalIgnoreCase) && key.Equals("Playtime", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double minutes) && minutes >= 0 && !double.IsInfinity(minutes)) return minutes / 60;
            }
            return null;
        }
    }
}
