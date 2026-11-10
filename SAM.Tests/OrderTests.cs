using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SAM.API.Orders;
using SAM.Game;

internal static class OrderTests
{
    private static void Invoke(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, args);
    private sealed class Fake : IStatsGateway
    {
        internal int Sets, Stores;
        public bool ReadAchievement(string id, out bool achieved) { achieved = false; return true; }
        public bool SetAchievement(string id, bool achieved) { Sets++; return true; }
        public bool Store() { Stores++; return true; }
    }
    private static OrderGroup Group(uint game, params string[] ids)
    {
        var group = new OrderGroup { GameId = game, Name = "Jeu " + game, Delay = new DelayRange { Minimum = 3, Maximum = 3 } };
        foreach (string id in ids) group.Achievements.Add(new OrderAchievement { Id = id, Name = id });
        return group;
    }
    internal static void Run(Action<bool, string> check)
    {
        check(new DelayRange { Minimum = 2, Maximum = 4, Unit = TimeUnit.Hours }.Sample(0.5) == TimeSpan.FromHours(3), "cart hour sliders convert to hours");
        check(new DelayRange { Minimum = 1, Maximum = 3, Unit = TimeUnit.Days }.Sample(1) == TimeSpan.FromDays(3), "cart day sliders support upper bound");
        bool invalid = false;
        try { new DelayRange { Minimum = 10, Maximum = 3 }.Sample(0); } catch (InvalidOperationException) { invalid = true; }
        check(invalid, "invalid delay ranges are rejected");
        var cart = new OrderCart { Games = new List<OrderGroup> { Group(620, "A", "B"), Group(400, "C") }, BetweenGames = true, GameDelay = new DelayRange { Minimum = 5, Maximum = 5 } };
        var runner = new OrderRunner(() => 0.5);
        runner.Start(cart, TimeSpan.Zero);
        check(runner.Tick(TimeSpan.FromSeconds(179)) == null, "cart delays the first achievement");
        check(runner.Tick(TimeSpan.FromMinutes(3)).Achievement.Id == "A", "cart sends first selected achievement");
        check(runner.Tick(TimeSpan.FromDays(1)) == null, "cart never sends a second command while awaiting confirmation");
        check(!runner.Confirm(400, "A", true, TimeSpan.FromMinutes(3)), "cart ignores confirmation from another game");
        runner.Confirm(620, "A", true, TimeSpan.FromMinutes(3));
        check(runner.Tick(TimeSpan.FromMinutes(5)) == null, "cart applies another full achievement delay");
        check(runner.Tick(TimeSpan.FromMinutes(6)).Achievement.Id == "B", "cart preserves within-game achievement order");
        runner.Confirm(620, "B", true, TimeSpan.FromMinutes(6));
        check(runner.Phase == OrderPhase.GameDelay && runner.Deadline == TimeSpan.FromMinutes(11), "cart inserts configured pause between games");
        check(runner.Tick(TimeSpan.FromMinutes(11)) == null && runner.Deadline == TimeSpan.FromMinutes(14), "next game also receives its achievement delay");
        check(runner.Tick(TimeSpan.FromMinutes(14)).GameId == 400, "cart advances to the next game");
        runner.Stop();
        check(runner.Phase == OrderPhase.Stopping, "cart stop waits for an outstanding confirmation");
        runner.Confirm(400, "C", true, TimeSpan.FromMinutes(14));
        check(!runner.Running && runner.Phase == OrderPhase.Stopped && runner.Completed == 3, "cart stop never schedules another command");
        runner.Start(cart, TimeSpan.Zero); runner.Tick(TimeSpan.FromMinutes(3)); runner.Confirm(620, "A", false, TimeSpan.FromMinutes(3));
        check(runner.Phase == OrderPhase.Failed && runner.Tick(TimeSpan.FromDays(3)) == null, "cart stops on Steam failure without advancing");
        string root = Path.Combine(Path.GetTempPath(), "BSAM-OrderTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var selectionStore = new JsonStore<OrderCart>(Path.Combine(root, "selection.json"));
            using (var selection = new Manager(620, null, true, cartPath: selectionStore.Path))
            {
                selection._Cards.SelectVisible("all"); selection.AddSelectionToCart();
                check(selectionStore.Read().Count == 4, "cart selection excludes achieved and protected cards");
                selection.AddSelectionToCart();
                check(selectionStore.Read().Count == 4, "repeated add-to-cart never duplicates selected cards");
            }
            var store = new JsonStore<OrderCart>(Path.Combine(root, "cart.json"));
            check(OrderPaths.Add(store, Group(620, "A", "B")) == 2, "cart adds selected achievements");
            check(OrderPaths.Add(store, Group(620, "A")) == 0 && store.Read().Count == 2, "cart de-duplicates game and achievement pairs");
            OrderPaths.Add(store, Group(400, "A"));
            check(store.Read().Games.Count == 2 && store.Read().Count == 3, "cart keeps equal achievement IDs distinct across games");
            store.Update(data => { data.ActiveRun = "test"; data.LeaseUntilUtc = DateTime.UtcNow.AddMinutes(1); });
            bool frozen = false;
            try { OrderPaths.Add(store, Group(400, "B")); } catch (InvalidOperationException) { frozen = true; }
            check(frozen && store.Read().Count == 3, "cart cannot be edited while an order is running");
            store.Update(data =>
            {
                foreach (string id in new[] { "DEMO_2", "DEMO_3", "DEMO_6" }) data.Games[0].Achievements.Add(new OrderAchievement { Id = id, Name = id });
            });
            var commands = new JsonStore<OrderCommand>(Path.Combine(root, "command.json"));
            commands.Update(command => { command.Id = "one"; command.RunId = "test"; command.GameId = 620; command.AchievementId = "DEMO_2"; });
            var fake = new Fake();
            using (var manager = new Manager(620, null, true, fake, store.Path, commands.Path))
            {
                Invoke(manager, "PollOrderCommand"); Invoke(manager, "PollOrderCommand");
                check(fake.Sets == 1 && fake.Stores == 1, "cart worker accepts a command exactly once");
                Invoke(manager, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 620, Result = 1 });
                check(commands.Read().State == "done", "cart worker reports only confirmed success");
                Invoke(manager, "EnableInput"); // Offline equivalent of the completed Steam stats refresh.
                commands.Update(command => { command.Id = "two"; command.AchievementId = "DEMO_6"; command.State = "requested"; });
                Invoke(manager, "PollOrderCommand");
                check(commands.Read().State == "failed" && fake.Sets == 1, "cart worker rejects protected achievements");
                commands.Update(command => { command.Id = "three"; command.AchievementId = "DEMO_3"; command.State = "requested"; });
                manager._Cards.SelectVisible("locked"); manager.StageSelected(true);
                Invoke(manager, "PollOrderCommand");
                check(commands.Read().State == "failed" && fake.Sets == 1, "cart worker refuses to commit unrelated manual changes");
                check(manager.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None, "standalone game view uses custom dark window chrome");
            }
            string corrupted = "{this is not valid json";
            File.WriteAllText(store.Path, corrupted);
            bool rejected = false;
            try { store.Read(); } catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllText(store.Path) == corrupted, "corrupt cart is reported without overwriting the original file");
        }
        finally { Directory.Delete(root, true); }
    }
}
