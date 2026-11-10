using System;
using System.Linq;
using SAM.API.Orders;

internal static class ImmediateOrderTests
{
    private static OrderCart Cart(bool parallel = false) => new()
    {
        Parallel = parallel, ConcurrentGames = 2,
        Games = new()
        {
            new() { GameId = 1, Name = "One", Achievements = new() { new() { Id = "A" }, new() { Id = "B" } } },
            new() { GameId = 2, Name = "Two", Achievements = new() { new() { Id = "C" }, new() { Id = "D" } } },
            new() { GameId = 3, Name = "Three", Achievements = new() { new() { Id = "E" } } }
        }
    };
    internal static void Run(Action<bool, string> check)
    {
        var runner = new OrderRunner(); var cart = Cart();
        runner.Start(cart, TimeSpan.Zero);
        check(runner.Tick(TimeSpan.Zero).Achievement.Id == "A", "first achievement dispatches immediately without a delay");
        check(runner.Tick(TimeSpan.FromDays(1)) == null, "elapsed time never permits a second unconfirmed write");
        check(!runner.Confirm(2, "A", true, TimeSpan.Zero), "confirmation from another game cannot advance the order");
        runner.Confirm(1, "A", true, TimeSpan.Zero);
        check(runner.Tick(TimeSpan.Zero).Achievement.Id == "B", "next achievement becomes available immediately after confirmation");
        runner.Stop(); check(runner.Phase == OrderPhase.Stopping, "stop waits for an outstanding Steam write");
        runner.Confirm(1, "B", true, TimeSpan.Zero);
        check(runner.Phase == OrderPhase.Stopped && runner.Tick(TimeSpan.Zero) == null, "stop cannot start another game after confirmation");
        runner.Start(Cart(true), TimeSpan.Zero);
        check(runner.ActiveGroups.Count() == 2, "parallel preparation opens only the configured active group count");
        runner.Tick(TimeSpan.Zero); runner.Confirm(1, "A", true, TimeSpan.Zero);
        check(runner.Tick(TimeSpan.Zero).Achievement.Id == "C", "parallel order alternates games after every acknowledgement");
        check(runner.Tick(TimeSpan.Zero) == null, "parallel games still share one write confirmation gate");
        runner.Pause(TimeSpan.Zero);
        check(runner.Phase == OrderPhase.Pausing, "pause waits for the current write in parallel mode");
        runner.Confirm(2, "C", true, TimeSpan.Zero);
        check(runner.Phase == OrderPhase.Paused && runner.Current.Achievement.Id == "B", "pause remembers the next alternating game");
        cart = Cart(true); cart.Games[0].Achievements[0].Status = "done"; cart.Games[1].Achievements[0].Status = "done";
        cart.Checkpoint = runner.Snapshot(TimeSpan.Zero);
        var resumed = new OrderRunner(); resumed.Start(cart, TimeSpan.FromDays(1));
        check(resumed.Tick(TimeSpan.FromDays(1)).Achievement.Id == "B", "resume excludes confirmed achievements and preserves the next target");
        resumed.Confirm(1, "B", true, TimeSpan.Zero);
        check(resumed.ActiveGroups.Count() == 2 && resumed.ActiveGroups.Any(g => g.GameId == 3), "a completed lane is replaced by the next queued game");
        var next = resumed.Tick(TimeSpan.Zero); resumed.Confirm(next.GameId, next.Achievement.Id, false, TimeSpan.Zero);
        check(resumed.Phase == OrderPhase.Failed && resumed.Tick(TimeSpan.FromDays(10)) == null, "a failed write stops all parallel dispatches");
        cart = Cart(); cart.Checkpoint = new() { GameId = 1, AchievementId = "A", RemainingSeconds = 86400 };
        runner = new OrderRunner(); runner.Start(cart, TimeSpan.Zero);
        check(runner.Tick(TimeSpan.Zero) != null, "legacy saved delays are ignored by the new immediate order");
    }
}
