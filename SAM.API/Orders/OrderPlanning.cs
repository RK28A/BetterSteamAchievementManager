using System;
using System.Linq;

namespace SAM.API.Orders
{
    internal static class OrderPlanning
    {
        internal static (TimeSpan Minimum, TimeSpan Maximum) Estimate(OrderCart cart)
        {
            var groups = cart.Games.Where(g => g.Achievements.Any(a => a.Status != "done")).ToList();
            double min = 0, max = 0;
            foreach (var group in groups)
            {
                int count = group.Achievements.Count(a => a.Status != "done");
                min += group.Delay.Sample(0).TotalSeconds * count;
                max += group.Delay.Sample(1).TotalSeconds * count;
            }
            if (cart.BetweenGames && groups.Count > 1)
            {
                min += cart.GameDelay.Sample(0).TotalSeconds * (groups.Count - 1);
                max += cart.GameDelay.Sample(1).TotalSeconds * (groups.Count - 1);
            }
            var saved = cart.Checkpoint;
            if (groups.Count > 0 && saved != null && saved.GameId == groups[0].GameId && saved.AchievementId == groups[0].Achievements.First(a => a.Status != "done").Id)
            {
                double remaining = Math.Max(0, saved.RemainingSeconds);
                if (!saved.BetweenGames)
                {
                    min -= groups[0].Delay.Sample(0).TotalSeconds;
                    max -= groups[0].Delay.Sample(1).TotalSeconds;
                }
                min += remaining; max += remaining;
            }
            return (TimeSpan.FromSeconds(min), TimeSpan.FromSeconds(max));
        }
        internal static void Record(OrderCart cart, string action, string detail, OrderStep step = null)
        {
            cart.History ??= new();
            cart.History.Add(new OrderHistoryEntry { Event = action, Detail = detail, GameId = step?.GameId ?? 0,
                GameName = step?.GameName, AchievementName = step?.Achievement.Name });
            if (cart.History.Count > 2000) cart.History.RemoveRange(0, cart.History.Count - 2000);
        }
        // Drag a game as one group, or move an achievement inside its own game.
        internal static bool Move(OrderCart cart, uint sourceGame, string sourceAchievement, uint targetGame, string targetAchievement, bool after)
        {
            OrderPaths.EnsureEditable(cart);
            var source = cart.Games.FirstOrDefault(g => g.GameId == sourceGame);
            var target = cart.Games.FirstOrDefault(g => g.GameId == targetGame);
            if (source == null || target == null) return false;
            if (sourceAchievement == null)
            {
                if (source == target) return false;
                cart.Games.Remove(source);
                cart.Games.Insert(cart.Games.IndexOf(target) + (after ? 1 : 0), source);
            }
            else
            {
                if (source != target || sourceAchievement == targetAchievement) return false;
                var item = source.Achievements.FirstOrDefault(a => a.Id == sourceAchievement);
                var destination = source.Achievements.FirstOrDefault(a => a.Id == targetAchievement);
                if (item == null || (targetAchievement != null && destination == null)) return false;
                source.Achievements.Remove(item);
                source.Achievements.Insert(destination == null ? 0 : source.Achievements.IndexOf(destination) + (after ? 1 : 0), item);
            }
            cart.Checkpoint = null;
            return true;
        }
    }
}
