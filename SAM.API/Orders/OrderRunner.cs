// Pure state machine: at most one outstanding Steam command, using a monotonic clock supplied by the UI.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.API.Orders
{
    internal enum OrderPhase { Idle, AchievementDelay, GameDelay, Confirmation, Pausing, Paused, Stopping, Stopped, Completed, Failed }
    internal sealed class OrderRunner
    {
        private List<OrderStep> _Steps = new();
        private int _Index;
        private bool _BetweenGames;
        private DelayRange _GameDelay;
        private readonly Func<double> _Random;
        private TimeSpan _PausedRemaining;
        private OrderPhase _PausedPhase;
        internal OrderPhase Phase { get; private set; }
        internal TimeSpan Deadline { get; private set; }
        internal OrderStep Current => _Index < _Steps.Count ? _Steps[_Index] : null;
        internal int Completed { get; private set; }
        internal int Total => _Steps.Count;
        internal bool Running => Phase == OrderPhase.AchievementDelay || Phase == OrderPhase.GameDelay || Phase == OrderPhase.Confirmation || Phase == OrderPhase.Pausing || Phase == OrderPhase.Stopping;
        internal OrderRunner(Func<double> random = null) { _Random = random ?? new Random().NextDouble; }
        internal void Start(OrderCart cart, TimeSpan now)
        {
            if (Running) throw new InvalidOperationException("Une commande est déjà en cours.");
            _Steps = cart.Games.SelectMany(g => g.Achievements.Select(a => new OrderStep { GameId = g.GameId, GameName = g.Name, Achievement = a, Delay = g.Delay.Copy() })).ToList();
            if (_Steps.Count == 0) throw new InvalidOperationException("Le panier est vide.");
            foreach (var step in _Steps) step.Delay.Sample(0);
            cart.GameDelay.Sample(0);
            _Index = Completed = 0;
            _BetweenGames = cart.BetweenGames;
            _GameDelay = cart.GameDelay.Copy();
            Phase = OrderPhase.AchievementDelay;
            Deadline = now + Current.Delay.Sample(_Random());
            var saved = cart.Checkpoint;
            if (saved != null && saved.GameId == Current.GameId && saved.AchievementId == Current.Achievement.Id &&
                !double.IsNaN(saved.RemainingSeconds) && !double.IsInfinity(saved.RemainingSeconds) && saved.RemainingSeconds >= 0 && saved.RemainingSeconds <= TimeSpan.FromDays(30).TotalSeconds)
            {
                Phase = saved.BetweenGames ? OrderPhase.GameDelay : OrderPhase.AchievementDelay;
                Deadline = now + TimeSpan.FromSeconds(saved.RemainingSeconds);
            }
        }
        internal OrderCheckpoint Snapshot(TimeSpan now)
        {
            if (Current == null || Phase == OrderPhase.Completed || Phase == OrderPhase.Idle) return null;
            var phase = Phase == OrderPhase.Paused ? _PausedPhase : Phase;
            var remaining = Phase == OrderPhase.Paused ? _PausedRemaining : Deadline - now;
            return new OrderCheckpoint { GameId = Current.GameId, AchievementId = Current.Achievement.Id,
                BetweenGames = phase == OrderPhase.GameDelay,
                RemainingSeconds = phase == OrderPhase.Confirmation || phase == OrderPhase.Pausing || phase == OrderPhase.Stopping ? 0 : Math.Max(0, remaining.TotalSeconds) };
        }
        internal void Pause(TimeSpan now)
        {
            if (Phase == OrderPhase.Confirmation) { Phase = OrderPhase.Pausing; return; }
            if (Phase != OrderPhase.AchievementDelay && Phase != OrderPhase.GameDelay) return;
            _PausedPhase = Phase;
            _PausedRemaining = Deadline > now ? Deadline - now : TimeSpan.Zero;
            Phase = OrderPhase.Paused;
        }
        internal OrderStep Tick(TimeSpan now)
        {
            if (now < Deadline) return null;
            if (Phase == OrderPhase.GameDelay)
            {
                Phase = OrderPhase.AchievementDelay;
                Deadline = now + Current.Delay.Sample(_Random());
                return null;
            }
            if (Phase != OrderPhase.AchievementDelay) return null;
            Phase = OrderPhase.Confirmation;
            return Current;
        }
        internal bool Confirm(uint gameId, string achievementId, bool success, TimeSpan now)
        {
            if ((Phase != OrderPhase.Confirmation && Phase != OrderPhase.Pausing && Phase != OrderPhase.Stopping) || Current == null || Current.GameId != gameId || Current.Achievement.Id != achievementId) return false;
            if (!success) { Phase = OrderPhase.Failed; return true; }
            Completed++;
            bool stop = Phase == OrderPhase.Stopping;
            bool pause = Phase == OrderPhase.Pausing;
            uint previous = Current.GameId;
            _Index++;
            if (stop) Phase = OrderPhase.Stopped;
            else if (_Index == _Steps.Count) Phase = OrderPhase.Completed;
            else
            {
                bool newGame = _BetweenGames && previous != Current.GameId;
                Phase = newGame ? OrderPhase.GameDelay : OrderPhase.AchievementDelay;
                Deadline = now + (newGame ? _GameDelay : Current.Delay).Sample(_Random());
                if (pause) Pause(now);
            }
            return true;
        }
        internal void Stop()
        {
            if (Phase == OrderPhase.Confirmation || Phase == OrderPhase.Pausing) Phase = OrderPhase.Stopping;
            else if (Phase != OrderPhase.Stopping) Phase = OrderPhase.Stopped;
        }
        internal void Fail() { Phase = OrderPhase.Failed; }
    }
}
