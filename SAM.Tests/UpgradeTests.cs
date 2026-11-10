using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.Game;

internal static class UpgradeTests
{
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static OrderCart Cart() => new()
    {
        Games = new List<OrderGroup>
        {
            new() { GameId = 620, Name = "Portal 2", Delay = new() { Minimum = 3, Maximum = 10 }, Achievements = new() { new() { Id = "A" }, new() { Id = "B" } } },
            new() { GameId = 400, Name = "Portal", Delay = new() { Minimum = 1, Maximum = 2, Unit = TimeUnit.Hours }, Achievements = new() { new() { Id = "C" } } }
        },
        BetweenGames = true, GameDelay = new() { Minimum = 5, Maximum = 15 }
    };
    internal static void Run(Action<bool, string> check)
    {
        var cart = Cart(); cart.Checkpoint = new() { GameId = 620, AchievementId = "A", RemainingSeconds = 60 };
        check(OrderPlanning.Move(cart, 620, "A", 620, "B", true) && cart.Games[0].Achievements[1].Id == "A" && cart.Checkpoint == null, "dragging achievements changes order and invalidates the old checkpoint");
        check(!OrderPlanning.Move(cart, 620, "A", 400, "C", false), "dragging cannot move achievements into another game");
        check(OrderPlanning.Move(cart, 400, null, 620, null, false) && cart.Games[0].GameId == 400 && cart.Games[1].Achievements.Count == 2, "dragging a game preserves all its achievements");
        cart.ActiveRun = "running"; cart.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(1);
        bool locked = false;
        try { OrderPlanning.Move(cart, 400, null, 620, null, false); } catch (InvalidOperationException) { locked = true; }
        check(locked, "dragging is blocked while an order is active");
        var rarity = AchievementRarity.Parse("{\"achievementpercentages\":{\"achievements\":[{\"name\":\"rare\",\"percent\":1.2},{\"name\":\"unknown\"},{\"name\":\"bad\",\"percent\":101},{\"name\":\"common\",\"percent\":99}]}}");
        check(rarity.Count == 2 && rarity["rare"] == 1.2, "global rarity accepts valid Steam values and rejects absent or invalid percentages");
        using (var manager = new Manager(620, null, true))
        {
            var sort = Field<ComboBox>(manager, "_AchievementSort");
            manager._Cards.SelectVisible("locked"); manager.StageSelected(true);
            sort.SelectedIndex = 2;
            var items = manager._Cards.VisibleAchievements.ToList();
            check(items.First().Id == "DEMO_5" && items.Last().GlobalPercent == null, "right-hand rarity sort orders rarest first and keeps unknown rarity last");
            sort.SelectedIndex = 3;
            check(manager._Cards.VisibleAchievements.First().Id == "DEMO_0", "right-hand rarity sort also supports most common first");
            sort.SelectedIndex = 5;
            check(manager._Cards.VisibleAchievements.First().Id == "DEMO_1", "recent-unlock sort uses unlock dates");
            check(manager._Cards.SelectedIds.Count == 4 && manager.HasPendingChanges, "changing sort preserves selections and staged changes");
        }
        string path = Path.Combine(Path.GetTempPath(), "BSAM-ResumeTest-" + Guid.NewGuid().ToString("N"), "cart.json");
        try
        {
            var store = new JsonStore<OrderCart>(path);
            store.Update(data => { data.Games = Cart().Games; data.Checkpoint = new() { GameId = 620, AchievementId = "A", RemainingSeconds = 75 }; OrderPlanning.Record(data, "Pause", "Test"); });
            var reloaded = new JsonStore<OrderCart>(path).Read();
            check(reloaded.Checkpoint.RemainingSeconds == 75 && reloaded.History.Single().Event == "Pause", "checkpoint and history survive a JSON round trip");
            for (int i = 0; i < 2100; i++) OrderPlanning.Record(reloaded, "Test", i.ToString());
            check(reloaded.History.Count == 2000 && reloaded.History[0].Detail == "100", "history retains its 2000 most recent events");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path), true); }
    }
}
