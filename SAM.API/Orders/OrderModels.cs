// BetterSteamAchievementManager: durable cart and per-game timing configuration.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.API.Orders
{
    internal enum TimeUnit { Minutes, Hours, Days }
    internal sealed class DelayRange
    {
        public int Minimum { get; set; } = 3;
        public int Maximum { get; set; } = 10;
        public TimeUnit Unit { get; set; }
        public static int Limit(TimeUnit unit) => unit == TimeUnit.Minutes ? 120 : unit == TimeUnit.Hours ? 72 : 30;
        public TimeSpan Sample(double random)
        {
            if (!Enum.IsDefined(typeof(TimeUnit), Unit) || Minimum < 0 || Maximum < Minimum || Maximum > Limit(Unit)) throw new InvalidOperationException("Plage de temps invalide.");
            if (random < 0 || random > 1) throw new ArgumentOutOfRangeException(nameof(random));
            double value = Minimum + (Maximum - Minimum) * random;
            return Unit == TimeUnit.Minutes ? TimeSpan.FromMinutes(value) : Unit == TimeUnit.Hours ? TimeSpan.FromHours(value) : TimeSpan.FromDays(value);
        }
        public DelayRange Copy() => new() { Minimum = Minimum, Maximum = Maximum, Unit = Unit };
    }
    internal sealed class OrderAchievement
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string Status { get; set; } = "En attente";
    }
    internal sealed class OrderGroup
    {
        public uint GameId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public DelayRange Delay { get; set; } = new();
        public List<OrderAchievement> Achievements { get; set; } = new();
    }
    internal sealed class OrderCart
    {
        public int Version { get; set; } = 1;
        public List<OrderGroup> Games { get; set; } = new();
        public bool BetweenGames { get; set; }
        public bool Parallel { get; set; }
        public int ConcurrentGames { get; set; } = 2;
        public int SpacingMinutes { get; set; } = 1;
        public DelayRange GameDelay { get; set; } = new() { Minimum = 5, Maximum = 15 };
        public OrderCheckpoint Checkpoint { get; set; }
        public List<OrderHistoryEntry> History { get; set; } = new();
        public string ActiveRun { get; set; }
        public DateTime LeaseUntilUtc { get; set; }
        public bool IsRunning => !string.IsNullOrEmpty(ActiveRun) && LeaseUntilUtc > DateTime.UtcNow;
        public int Count => Games.Sum(g => g.Achievements.Count);
    }
    internal sealed class OrderLaneCheckpoint
    {
        public uint GameId { get; set; }
        public string AchievementId { get; set; }
        public double RemainingSeconds { get; set; }
    }
    internal sealed class OrderCheckpoint
    {
        public bool Parallel { get; set; }
        public List<OrderLaneCheckpoint> Lanes { get; set; } = new();
        public double GlobalRemainingSeconds { get; set; }
        public uint GameId { get; set; }
        public string AchievementId { get; set; }
        public bool BetweenGames { get; set; }
        public double RemainingSeconds { get; set; }
    }
    internal sealed class OrderHistoryEntry
    {
        public DateTime Utc { get; set; } = DateTime.UtcNow;
        public string Event { get; set; }
        public uint GameId { get; set; }
        public string GameName { get; set; }
        public string AchievementName { get; set; }
        public string Detail { get; set; }
        public override string ToString() => Utc.ToLocalTime().ToString("g") + " · " + Event + " · " + GameName + " · " + AchievementName + " · " + Detail;
    }
    internal sealed class OrderCommand
    {
        public string Id { get; set; }
        public string RunId { get; set; }
        public uint GameId { get; set; }
        public string AchievementId { get; set; }
        public string State { get; set; } = "requested";
        public string Message { get; set; }
    }
    internal sealed class OrderStep
    {
        public uint GameId;
        public string GameName;
        public OrderAchievement Achievement;
        public DelayRange Delay;
    }
}
