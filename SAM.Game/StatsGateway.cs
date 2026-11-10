// Narrow boundary for verified one-at-a-time writes and offline regression tests.
namespace SAM.Game
{
    internal interface IStatsGateway
    {
        bool ReadAchievement(string id, out bool achieved);
        bool SetAchievement(string id, bool achieved);
        bool Store();
    }
    internal sealed class SteamStatsGateway : IStatsGateway
    {
        private readonly API.Wrappers.SteamUserStats013 _Stats;
        public SteamStatsGateway(API.Wrappers.SteamUserStats013 stats) { _Stats = stats; }
        public bool ReadAchievement(string id, out bool achieved) => _Stats.GetAchievementAndUnlockTime(id, out achieved, out _);
        public bool SetAchievement(string id, bool achieved) => _Stats.SetAchievement(id, achieved);
        public bool Store() => _Stats.StoreStats();
    }
}
