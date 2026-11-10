using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using SAM.API.Orders;

namespace SAM.Game
{
    internal sealed class PlaytimeWorker : ApplicationContext
    {
        private readonly Timer _Timer = new() { Interval = 250 };
        private readonly Stopwatch _Clock = Stopwatch.StartNew();
        private readonly Process _Owner;
        private readonly API.Client _Client;
        private readonly JsonStore<PlaytimeStatus> _Status;
        private readonly TimeSpan _Duration;
        private int _LastReport = -1;
        private bool _Finished;
        private double _Elapsed, _PreviousTick;
        private PlaytimeWorker(API.Client client, Process owner, JsonStore<PlaytimeStatus> status, double seconds, double elapsed)
        {
            _Client = client; _Owner = owner; _Status = status; _Duration = TimeSpan.FromSeconds(seconds); _Elapsed = elapsed;
            _Status.Update(s => { s.State = "running"; s.StartedUtc = DateTime.UtcNow; s.ElapsedSeconds = _Elapsed; s.HeartbeatUtc = DateTime.UtcNow; s.Message = client == null ? "Simulation" : "Session Steam active"; });
            _Timer.Tick += (_, _) =>
            {
                try
                {
                    if (_Owner.HasExited) { Finish("stopped", "Application fermée"); return; }
                    double now = _Clock.Elapsed.TotalSeconds;
                    _Elapsed += BackgroundPlanning.ActiveDelta(now - _PreviousTick); _PreviousTick = now;
                    if (_Client != null && !_Client.SteamUser.IsLoggedIn()) { Finish("failed", "Steam déconnecté · reprise automatique en attente"); return; }
                    if (_Elapsed >= _Duration.TotalSeconds) { Finish("completed", "Durée terminée"); return; }
                    _Client?.RunCallbacks(false);
                    int report = (int)_Clock.Elapsed.TotalSeconds;
                    if (report != _LastReport) { _LastReport = report; _Status.Update(s => { s.ElapsedSeconds = Math.Min(_Elapsed, _Duration.TotalSeconds); s.HeartbeatUtc = DateTime.UtcNow; }); }
                }
                catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
                { Finish("failed", e.Message); }
            };
            _Timer.Start();
        }
        private void Finish(string state, string message)
        {
            if (_Finished) return; _Finished = true; _Timer.Stop();
            try { _Status.Update(s => { s.State = state; s.Message = message; s.ElapsedSeconds = Math.Min(_Elapsed, _Duration.TotalSeconds); s.HeartbeatUtc = DateTime.UtcNow; }); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { }
            ExitThread();
        }
        internal static void Run(long gameId, string[] args, bool demo)
        {
            string Arg(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            JsonStore<PlaytimeStatus> status = null;
            try
            {
                if (!double.TryParse(Arg("--seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || double.IsNaN(seconds) || seconds <= 0 || seconds > BackgroundPlanning.MaxHours * 3600 ||
                    !int.TryParse(Arg("--owner"), out int ownerId) || !long.TryParse(Arg("--owner-start"), out long ownerStart) || string.IsNullOrEmpty(Arg("--status")))
                    throw new InvalidOperationException("Paramètres de session invalides.");
                status = new JsonStore<PlaytimeStatus>(Arg("--status"));
                double elapsed = 0;
                if (Arg("--elapsed") != null && (!double.TryParse(Arg("--elapsed"), NumberStyles.Float, CultureInfo.InvariantCulture, out elapsed) || double.IsNaN(elapsed) || elapsed < 0 || elapsed >= seconds))
                    throw new InvalidOperationException("Progression invalide.");
                using var owner = Process.GetProcessById(ownerId);
                if (owner.HasExited || owner.StartTime.ToUniversalTime().Ticks != ownerStart) throw new InvalidOperationException("La bibliothèque propriétaire est fermée.");
                using var client = demo ? null : new API.Client();
                if (!demo) client.Initialize(gameId);
                using var worker = new PlaytimeWorker(client, owner, status, seconds, elapsed);
                Application.Run(worker);
            }
            catch (Exception e)
            {
                Environment.ExitCode = 1;
                try { status?.Update(s => { s.State = "failed"; s.Message = e.Message; }); } catch (Exception) { }
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) _Timer.Dispose(); base.Dispose(disposing); }
    }
}
