using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using SAM.API.Orders;
using SAM.Game;
using SAM.Picker;

internal static class BackgroundTests
{
    private sealed class Fake : IStatsGateway
    {
        internal int Writes;
        public bool ReadAchievement(string id, out bool achieved) { achieved = false; return true; }
        public bool SetAchievement(string id, bool value) { Writes++; return true; }
        public bool Store() => true;
    }
    private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
    internal static void Run(Action<bool, string> check)
    {
        var culture = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); check(BackgroundPlanning.Hours("1,5") == 1.5 && BackgroundPlanning.Hours("1.5") == 1.5, "hour inputs accept local and invariant decimal separators"); }
        finally { CultureInfo.CurrentCulture = culture; }
        foreach (string value in new[] { "NaN", "Infinity", "-1", "0", "8761", "oops" })
        {
            bool refused = false; try { BackgroundPlanning.Hours(value); } catch (InvalidOperationException) { refused = true; }
            check(refused, "invalid hour duration rejected: " + value);
        }
        check(BackgroundPlanning.ActiveDelta(0.25) == 0.25 && BackgroundPlanning.ActiveDelta(3600) == 0 && BackgroundPlanning.ActiveDelta(-1) == 0, "sleep or suspended machine time is never counted as active session time");
        check(BackgroundPlanning.DefaultWindowHours(40, 15 + 2.0 / 60) == 24.96, "default unlock window never rounds beyond the remaining hours from Steam's minute precision");
        var sequence = BackgroundPlanning.Schedule(new[] { "A", "B", "A", "C" }, 60, 600, () => 0.5);
        check(sequence.Count == 3 && sequence.Select(a => a.Id).Distinct().Count() == 3 && sequence[0].AtSeconds >= 60 && sequence.Last().AtSeconds <= 600 && sequence.Zip(sequence.Skip(1), (a,b) => b.AtSeconds > a.AtSeconds).All(x => x), "random schedule de-duplicates and spreads achievements inside the chosen period");
        var spaced = BackgroundPlanning.Schedule(new[] { "A", "B", "C" }, 0, 100 * 3600, () => 0.5);
        check(spaced.Count == 3 && spaced[0].AtSeconds / 3600 >= 20 && spaced[0].AtSeconds / 3600 <= 32 &&
            spaced[1].AtSeconds / 3600 >= 53 && spaced[1].AtSeconds / 3600 <= 66 &&
            spaced[2].AtSeconds / 3600 >= 86 && spaced[2].AtSeconds / 3600 <= 99 &&
            spaced.Zip(spaced.Skip(1), (a, b) => (b.AtSeconds - a.AtSeconds) / 3600 >= 20).All(x => x),
            "three achievements cover a 100-hour session with generous gaps");
        int randomIndex = 0;
        var extremes = BackgroundPlanning.Schedule(new[] { "A", "B", "C" }, 0, 100 * 3600,
            () => randomIndex++ % 2 == 0 ? 0 : 1);
        check(extremes.Zip(extremes.Skip(1), (a, b) => b.AtSeconds - a.AtSeconds >= 0.65 * (100 * 3600 / 3.0)).All(x => x),
            "even extreme random values keep achievements well separated");
        string folder = Path.Combine(Path.GetTempPath(), "BSAM-Background-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var store = new JsonStore<BackgroundState>(Path.Combine(folder, "sessions.json"));
            store.Update(s => { s.Sessions.Add(new TimePlan { GameId = 620, GoalSeconds = 7200, ElapsedSeconds = 3600, Resume = true }); s.Games.Add(new GamePlan { GameId = 620, Achievements = sequence }); });
            var restored = new JsonStore<BackgroundState>(store.Path).Read();
            check(restored.Sessions[0].ElapsedSeconds == 3600 && restored.Sessions[0].Resume && restored.Games[0].Achievements[1].AtSeconds == sequence[1].AtSeconds, "restart preserves accumulated time and the sampled schedule exactly");
            var twenty = new BackgroundState { Sessions = Enumerable.Range(1, 20).Select(i => new TimePlan { GameId = (uint)i, GoalSeconds = 1, Resume = true }).ToList() }; BackgroundPlanning.Validate(twenty);
            twenty.Sessions.Add(new TimePlan { GameId = 21, GoalSeconds = 1, Resume = true });
            bool limit = false; try { BackgroundPlanning.Validate(twenty); } catch (InvalidDataException) { limit = true; }
            check(limit, "session limit accepts 20 and rejects a 21st active plan");
            twenty.Sessions.RemoveAt(20);
            twenty.Sessions.AddRange(Enumerable.Range(101, 10).Select(i => new TimePlan { GameId = (uint)i, GoalSeconds = 1, Resume = true, Order = true }));
            BackgroundPlanning.Validate(twenty);
            check(twenty.Sessions.Count == 30, "20 standalone sessions and 10 Order games have independent capacities");
            twenty.Sessions.Add(new TimePlan { GameId = 111, GoalSeconds = 1, Resume = true, Order = true });
            bool orderLimit = false; try { BackgroundPlanning.Validate(twenty); } catch (InvalidDataException) { orderLimit = true; }
            check(orderLimit, "parallel Order refuses an eleventh simultaneous game");
            var cart = new JsonStore<OrderCart>(Path.Combine(folder, "cart.json"));
            cart.Update(c => { c.ActiveRun = "parallel"; c.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(1); foreach (uint id in new uint[] { 620, 400 }) c.Games.Add(new OrderGroup { GameId = id, Achievements = new List<OrderAchievement> { new() { Id = "DEMO_2" } } }); });
            var one = new JsonStore<OrderCommand>(Path.Combine(folder, "620.json")); var two = new JsonStore<OrderCommand>(Path.Combine(folder, "400.json"));
            one.Update(c => { c.Id = "one"; c.RunId = "parallel"; c.GameId = 620; c.AchievementId = "DEMO_2"; });
            two.Update(c => { c.Id = "two"; c.RunId = "parallel"; c.GameId = 400; c.AchievementId = "DEMO_2"; });
            var first = new Fake(); var second = new Fake();
            using var m1 = new Manager(620, null, true, first, cart.Path, one.Path);
            using var m2 = new Manager(400, null, true, second, cart.Path, two.Path);
            Call(m1, "PollOrderCommand"); Call(m2, "PollOrderCommand");
            check(first.Writes == 1 && second.Writes == 1 && one.Read().State == "requested" && two.Read().State == "requested", "two games can have simultaneous independent Steam confirmations in flight");
            Call(m2, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 400, Result = 1 });
            check(two.Read().State == "done" && one.Read().State == "requested", "second game's confirmation never completes the first game's command");
            Call(m1, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 620, Result = 1 });
            check(one.Read().State == "done" && first.Writes == 1 && second.Writes == 1, "out-of-order confirmations complete each game exactly once");
        }
        finally { Directory.Delete(folder, true); }
        var hltb = CompletionSource.Parse("{\"data\":[{\"game_id\":1,\"game_name\":\"Portal 2\",\"profile_steam\":620,\"comp_100\":72000}]}", 620, "Portal 2");
        check(hltb.Hours == 20 && hltb.Url == "https://howlongtobeat.com/game/1", "HLTB completionist seconds become sourced total hours");
        bool wrong = false; try { CompletionSource.Parse("{\"data\":[{\"game_id\":1,\"game_name\":\"Portal 2\",\"profile_steam\":400,\"comp_100\":72000}]}", 620, "Portal 2"); } catch (InvalidOperationException) { wrong = true; }
        check(wrong, "HLTB name match cannot override a conflicting Steam app ID");
        check(!CompletionSource.ValidUrl("https://example.com/game/1") && !CompletionSource.ValidUrl("file:///etc/passwd"), "manual source only accepts HLTB game pages");
        string vdf = "\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { \"620\" { \"Playtime\" \"180\" } \"400\" { \"Playtime\" \"20\" } } } } } }";
        check(CompletionSource.ParseSteamHours(vdf, 620) == 3 && CompletionSource.ParseSteamHours(vdf, 999) == null, "Steam cached playtime is read only for the requested app and missing data stays unknown");
    }
}
