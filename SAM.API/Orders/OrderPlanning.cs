using System;
using System.Linq;

namespace SAM.API.Orders
{
    internal static class OrderPlanning
    {
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
