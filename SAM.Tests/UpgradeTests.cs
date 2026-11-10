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
        var cart = Cart();
        var runner = new OrderRunner(() => 0);
        runner.Start(cart, TimeSpan.Zero);
        runner.Pause(TimeSpan.FromMinutes(1));
        check(runner.Phase == OrderPhase.Paused && !runner.Running && runner.Tick(TimeSpan.FromDays(1)) == null, "pause sends no achievement even after its original deadline");
        cart.Checkpoint = runner.Snapshot(TimeSpan.FromDays(1));
        check(cart.Checkpoint.RemainingSeconds == 120, "pause freezes remaining delay instead of counting offline time");
        var resumed = new OrderRunner(() => 1);
        resumed.Start(cart, TimeSpan.FromHours(10));
        check(resumed.Deadline == TimeSpan.FromHours(10) + TimeSpan.FromMinutes(2), "a new process restores the saved countdown without resampling");
        check(resumed.Tick(TimeSpan.FromHours(10) + TimeSpan.FromSeconds(119)) == null, "resumed order cannot send early");
        resumed.Tick(resumed.Deadline);
        resumed.Pause(resumed.Deadline);
        check(resumed.Phase == OrderPhase.Pausing && resumed.Running, "pause during a write keeps waiting for acknowledgement");
        resumed.Confirm(620, "A", true, resumed.Deadline);
        check(resumed.Phase == OrderPhase.Paused && resumed.Current.Achievement.Id == "B", "pause after acknowledgement preserves the next achievement");
        check(resumed.Tick(TimeSpan.FromDays(20)) == null, "acknowledgement while pausing cannot dispatch the next achievement");
        var between = Cart();
        var across = new OrderRunner(() => 0);
        across.Start(between, TimeSpan.Zero);
        across.Tick(TimeSpan.FromMinutes(3)); across.Confirm(620, "A", true, TimeSpan.FromMinutes(3));
        across.Tick(TimeSpan.FromMinutes(6)); across.Confirm(620, "B", true, TimeSpan.FromMinutes(6));
        across.Pause(TimeSpan.FromMinutes(7));
        between.Games.RemoveAt(0); between.Checkpoint = across.Snapshot(TimeSpan.FromHours(3));
        var restored = new OrderRunner(() => 0);
        restored.Start(between, TimeSpan.Zero);
        check(restored.Phase == OrderPhase.GameDelay && restored.Deadline == TimeSpan.FromMinutes(4), "restart preserves an inter-game pause");
        check(restored.Tick(TimeSpan.FromMinutes(4)) == null && restored.Deadline == TimeSpan.FromMinutes(64), "resuming between games still applies the next achievement delay");
        using (var library = new SAM.Picker.GamePicker(null, true))
        {
            var coordinator = Field<OrderRunner>(library, "_Runner");
            var savedCart = Field<JsonStore<OrderCart>>(library, "_OrderStore");
            coordinator.Start(Cart(), TimeSpan.FromMinutes(-11));
            // Supply a deterministic checkpoint so this UI test does not depend on random delays.
            coordinator.Stop();
            var deterministic = Cart();
            deterministic.Games[0].Delay.Maximum = 3;
            deterministic.GameDelay.Maximum = 5;
            deterministic.Games[1].Delay.Maximum = 1;
            coordinator.Start(deterministic, TimeSpan.FromMinutes(-11));
            coordinator.Tick(TimeSpan.FromMinutes(-8)); coordinator.Confirm(620, "A", true, TimeSpan.FromMinutes(-8));
            coordinator.Tick(TimeSpan.FromMinutes(-5)); coordinator.Confirm(620, "B", true, TimeSpan.FromMinutes(-5));
            savedCart.Update(data => { data.Games = deterministic.Games; data.ActiveRun = "boundary-test"; data.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(1); data.Checkpoint = coordinator.Snapshot(TimeSpan.Zero); });
            typeof(SAM.Picker.GamePicker).GetField("_RunId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(library, "boundary-test");
            typeof(SAM.Picker.GamePicker).GetField("_LastLease", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(library, DateTime.UtcNow);
            typeof(SAM.Picker.GamePicker).GetMethod("TickOrder", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(library, null);
            var checkpoint = savedCart.Read().Checkpoint;
            check(checkpoint.GameId == 400 && !checkpoint.BetweenGames && checkpoint.RemainingSeconds > 3590,
                "coordinator saves a newly sampled countdown immediately at a game boundary");
            coordinator.Stop();
        }
        var bounds = OrderPlanning.Estimate(Cart());
        check(bounds.Minimum == TimeSpan.FromMinutes(71) && bounds.Maximum == TimeSpan.FromMinutes(155), "planning includes every achievement and each game transition exactly once");
        bounds = OrderPlanning.Estimate(between);
        check(bounds.Minimum == TimeSpan.FromMinutes(64) && bounds.Maximum == TimeSpan.FromMinutes(124), "planning includes a saved inter-game delay plus achievement bounds");
        cart = Cart(); cart.Games[0].Achievements[0].Status = "done";
        bounds = OrderPlanning.Estimate(cart);
        check(bounds.Minimum == TimeSpan.FromMinutes(68) && bounds.Maximum == TimeSpan.FromMinutes(145), "planning excludes completed achievements");
        cart = Cart(); cart.Checkpoint = new() { GameId = 620, AchievementId = "A", RemainingSeconds = 60 };
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
