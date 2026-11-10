using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.API.Orders
{
    internal sealed class BackgroundState
    {
        public int Version { get; set; } = 1;
        public bool Startup { get; set; }
        public int ParallelLimit { get; set; } = 2;
        public bool ParallelEnabled { get; set; }
        public List<TimePlan> Sessions { get; set; } = new();
        public List<GamePlan> Games { get; set; } = new();
    }
    internal sealed class TimePlan
    {
        public uint GameId { get; set; }
        public string Name { get; set; }
        public string Token { get; set; } = Guid.NewGuid().ToString("N");
        public double GoalSeconds { get; set; }
        public double ElapsedSeconds { get; set; }
        public bool Resume { get; set; }
        public bool Order { get; set; }
    }
    internal sealed class PlannedAchievement
    {
        public string Id { get; set; }
        public double AtSeconds { get; set; }
        public bool Done { get; set; }
    }
    internal sealed class GamePlan
    {
        public uint GameId { get; set; }
        public string Name { get; set; }
        public double? SteamHours { get; set; }
        public double? CompletionHours { get; set; }
        public string SourceUrl { get; set; }
        public double TargetHours { get; set; } = 10;
        public double WindowStartHours { get; set; }
        public double WindowEndHours { get; set; }
        public bool Ready { get; set; }
        public bool Completed { get; set; }
        public string Error { get; set; }
        public List<PlannedAchievement> Achievements { get; set; } = new();
    }
    internal static class BackgroundPlanning
    {
        public const int SessionLimit = 20;
        public const int OrderLimit = 10;
        public const double MaxHours = 8760;
        public static double Hours(string value, bool zero = false)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double n) &&
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
                throw new InvalidOperationException("Saisissez un nombre d’heures valide.");
            if (double.IsNaN(n) || double.IsInfinity(n) || n < 0 || (!zero && n == 0) || n > MaxHours)
                throw new InvalidOperationException("La durée doit être comprise entre 0 et 8 760 heures (strictement positive pour une session).");
            return n;
        }
        // Stratified sampling spreads achievements across the selected active-time window.
        // The sampled schedule is persisted once and never resampled on recovery.
        public static List<PlannedAchievement> Schedule(IEnumerable<string> ids, double from, double to, Func<double> random)
        {
            if (double.IsNaN(from) || double.IsNaN(to) || double.IsInfinity(to) || from < 0 || to <= from || to > MaxHours * 3600)
                throw new InvalidOperationException("La fin de la période doit dépasser son début et rester dans la durée de session.");
            var values = ids.Distinct().Select(id => new { Id = id, Key = random() }).OrderBy(x => x.Key).ToArray();
            return values.Select((x, i) => new PlannedAchievement { Id = x.Id, AtSeconds = from + (i + Math.Max(0, Math.Min(1, random()))) / values.Length * (to - from) }).ToList();
        }
        public static double ActiveDelta(double seconds) => seconds >= 0 && seconds <= 2 ? seconds : 0;
        public static void Validate(BackgroundState state)
        {
            if (state.Version != 1 || state.Sessions == null || state.Games == null || state.ParallelLimit < 1 || state.ParallelLimit > OrderLimit ||
                state.Sessions.Any(s => s == null) || state.Sessions.Count(s => s.Resume && !s.Order) > SessionLimit || state.Sessions.Count(s => s.Resume && s.Order) > OrderLimit || state.Sessions.Any(s => s == null || s.GameId == 0 || string.IsNullOrWhiteSpace(s.Token) ||
                s.Token.Length != 32 || s.Token.Any(c => !Uri.IsHexDigit(c)) || s.GoalSeconds <= 0 || s.GoalSeconds > MaxHours * 3600 || double.IsNaN(s.GoalSeconds) ||
                s.ElapsedSeconds < 0 || double.IsNaN(s.ElapsedSeconds) || double.IsInfinity(s.ElapsedSeconds)) ||
                state.Sessions.GroupBy(s => s.GameId).Any(g => g.Count() > 1) || state.Games.Any(g => g == null) || state.Games.GroupBy(g => g.GameId).Any(g => g.Count() > 1) ||
                state.Games.Any(g => g == null || g.GameId == 0 || g.Achievements == null || g.TargetHours <= 0 || g.TargetHours > MaxHours || double.IsNaN(g.TargetHours) ||
                double.IsNaN(g.WindowStartHours) || double.IsNaN(g.WindowEndHours) || g.WindowStartHours < 0 || g.WindowEndHours > MaxHours ||
                (g.Ready && (g.WindowEndHours <= g.WindowStartHours || g.WindowEndHours > g.TargetHours - (g.SteamHours ?? 0))) ||
                g.Achievements.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id) || double.IsNaN(a.AtSeconds) || a.AtSeconds < 0 || a.AtSeconds > MaxHours * 3600)))
                throw new System.IO.InvalidDataException("La sauvegarde des sessions est invalide. Elle est conservée pour récupération.");
        }
    }
}
