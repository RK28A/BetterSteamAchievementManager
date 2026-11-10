// Immediate dispatch: one Steam write at a time, optionally alternating 2–5 prepared games.
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.API.Orders
{
    internal enum OrderPhase { Idle, Ready, Confirmation, Pausing, Paused, Stopping, Stopped, Completed, Failed }
    internal sealed class OrderRunner
    {
        private sealed class Lane
        {
            internal OrderGroup Group;
            internal Queue<OrderStep> Steps;
        }
        private readonly List<Lane> _Active = new();
        private readonly Queue<OrderGroup> _Waiting = new();
        private int _Cursor, _Limit;
        private bool _Parallel;
        internal OrderPhase Phase { get; private set; }
        internal OrderStep Current => _Active.Count == 0 ? null : _Active[_Cursor].Steps.Peek();
        internal int Completed { get; private set; }
        internal int Total { get; private set; }
        internal bool Running => Phase == OrderPhase.Ready || Phase == OrderPhase.Confirmation || Phase == OrderPhase.Pausing || Phase == OrderPhase.Stopping;
        internal IEnumerable<OrderGroup> ActiveGroups => _Active.Select(l => l.Group);
        internal OrderRunner() { }
        internal void Start(OrderCart cart, TimeSpan now)
        {
            if (Running) throw new InvalidOperationException("Une commande est déjà en cours.");
            if (cart.ConcurrentGames < 2 || cart.ConcurrentGames > 5) throw new InvalidOperationException("Choisissez entre 2 et 5 jeux.");
            _Active.Clear(); _Waiting.Clear(); _Cursor = Completed = Total = 0;
            _Parallel = cart.Parallel; _Limit = cart.Parallel ? cart.ConcurrentGames : 1;
            foreach (var group in cart.Games)
            {
                var remaining = group.Achievements.Where(a => a.Status != "done").ToList();
                if (remaining.Count == 0) continue;
                _Waiting.Enqueue(new OrderGroup { GameId = group.GameId, Name = group.Name, ImageUrl = group.ImageUrl, Achievements = remaining });
                Total += remaining.Count;
            }
            if (Total == 0) throw new InvalidOperationException("Le panier ne contient aucun succès en attente.");
            while (_Active.Count < _Limit && _Waiting.Count > 0) AddLane();
            if (cart.Checkpoint != null)
            {
                int restored = _Active.FindIndex(l => l.Group.GameId == cart.Checkpoint.GameId && l.Steps.Peek().Achievement.Id == cart.Checkpoint.AchievementId);
                if (restored >= 0) _Cursor = restored;
            }
            Phase = OrderPhase.Ready;
        }
        private void AddLane()
        {
            var group = _Waiting.Dequeue();
            _Active.Add(new Lane { Group = group, Steps = new Queue<OrderStep>(group.Achievements.Select(a => new OrderStep { GameId = group.GameId, GameName = group.Name, Achievement = a })) });
        }
        internal OrderStep Tick(TimeSpan now)
        {
            if (Phase != OrderPhase.Ready) return null;
            Phase = OrderPhase.Confirmation; return Current;
        }
        internal bool Confirm(uint gameId, string achievementId, bool success, TimeSpan now)
        {
            if ((Phase != OrderPhase.Confirmation && Phase != OrderPhase.Pausing && Phase != OrderPhase.Stopping) || Current == null || Current.GameId != gameId || Current.Achievement.Id != achievementId) return false;
            if (!success) { Phase = OrderPhase.Failed; return true; }
            bool pause = Phase == OrderPhase.Pausing, stop = Phase == OrderPhase.Stopping;
            Completed++; _Active[_Cursor].Steps.Dequeue();
            if (_Active[_Cursor].Steps.Count == 0)
            {
                _Active.RemoveAt(_Cursor);
                if (_Waiting.Count > 0) AddLane();
                if (_Cursor >= _Active.Count) _Cursor = 0;
            }
            else if (_Active.Count > 1) _Cursor = (_Cursor + 1) % _Active.Count;
            Phase = stop ? OrderPhase.Stopped : _Active.Count == 0 ? OrderPhase.Completed : pause ? OrderPhase.Paused : OrderPhase.Ready;
            return true;
        }
        internal void Pause(TimeSpan now)
        {
            if (Phase == OrderPhase.Confirmation) Phase = OrderPhase.Pausing;
            else if (Phase == OrderPhase.Ready) Phase = OrderPhase.Paused;
        }
        internal void Stop() { Phase = Phase == OrderPhase.Confirmation || Phase == OrderPhase.Pausing ? OrderPhase.Stopping : OrderPhase.Stopped; }
        internal void Fail() { Phase = OrderPhase.Failed; }
        internal OrderCheckpoint Snapshot(TimeSpan now) => Current == null || Phase == OrderPhase.Idle ? null : new OrderCheckpoint { GameId = Current.GameId, AchievementId = Current.Achievement.Id, Parallel = _Parallel };
    }
}
