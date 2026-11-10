using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private sealed class TimeJob
        {
            internal GameInfo Game;
            internal TimePlan Plan;
            internal DateTime RetryUtc;
            internal Process Process;
            internal JsonStore<PlaytimeStatus> Store;
            internal TimeSpan Duration;
            internal Stopwatch Clock = Stopwatch.StartNew();
            internal PlaytimeStatus Status = new();
            internal bool Running;
            internal TimeSpan Remaining => TimeSpan.FromSeconds(Math.Max(0, Duration.TotalSeconds - Plan.ElapsedSeconds));
            public override string ToString() => Game.Name + " · " + Status.Message;
        }
        private readonly Dictionary<uint, TimeJob> _TimeJobs = new();
        private readonly HashSet<uint> _TimeSelection = new();
        private Panel _TimePanel;
        private CheckedListBox _TimeGames;
        private ListBox _TimeRuns;
        private TextBox _TimeSearch;
        private CheckBox _TimeRandom;
        private TextBox _TimeHours, _TimeMaxHours;
        private JsonStore<BackgroundState> _BackgroundStore;
        private BackgroundState _Background = new();
        private bool _BackgroundReady;
        private string _BackgroundFolder;
        private Label _TimeSummary;
        private Button _TimeButton;
        private bool _BindingTime;
        private readonly Random _TimeRandomSource = new();
        private string _TimeCatalogSignature;
        private void InitializeGameTime(Panel navigation)
        {
            _TimeButton = new Button { Text = "GameTime Spoofer", Dock = DockStyle.Right, Width = 175 };
            _TimeButton.Click += (_, _) => { RefreshTimeGames(); _TimePanel.BringToFront(); };
            navigation.Controls.Add(_TimeButton); SteamTheme.Apply(_TimeButton);
            _TimePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 10, 18, 10) };
            var title = SteamTheme.Label("GameTime Spoofer", 23); title.Dock = DockStyle.Top;
            var subtitle = SteamTheme.Label("Des sessions à votre rythme. Choisissez les jeux et leur durée.", 10, SteamTheme.Muted); subtitle.Dock = DockStyle.Top; subtitle.Padding = new Padding(0, 0, 0, 14);
            var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 12, Size = new Size(800, 450), SplitterDistance = 265, Panel1MinSize = 210, Panel2MinSize = 300 };
            _TimeGames = new SteamCheckedListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, CheckOnClick = true, IntegralHeight = false, AccessibleName = "Jeux à maintenir actifs", Font = new Font("Segoe UI", 11) };
            _TimeSearch = new TextBox { Dock = DockStyle.Top, AccessibleName = "Rechercher un jeu pour GameTime" };
            _TimeSearch.TextChanged += (_, _) => RefreshTimeGames(true);
            _TimeGames.ItemCheck += (_, e) =>
            {
                if (_BindingTime) return;
                var game = (GameInfo)_TimeGames.Items[e.Index];
                if (e.NewValue == CheckState.Checked) _TimeSelection.Add(game.Id); else _TimeSelection.Remove(game.Id);
            };
            var choose = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44 };
            void Select(bool value) { for (int i = 0; i < _TimeGames.Items.Count; i++) _TimeGames.SetItemChecked(i, value); }
            var all = new Button { Text = "Tout", AutoSize = true }; all.Click += (_, _) => Select(true);
            var none = new Button { Text = "Aucun", AutoSize = true }; none.Click += (_, _) => { _TimeSelection.Clear(); Select(false); };
            choose.Controls.Add(all); choose.Controls.Add(none);
            split.Panel1.Controls.Add(_TimeGames); split.Panel1.Controls.Add(choose); split.Panel1.Controls.Add(_TimeSearch);
            _TimeRuns = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 90, IntegralHeight = false, BorderStyle = BorderStyle.None, AccessibleName = "Sessions GameTime" };
            _TimeRuns.DrawItem += DrawTimeJob;
            var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 125, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _TimeRandom = new SteamCheckBox { Text = "Durée aléatoire entre les deux valeurs", AutoSize = false, Height = 30, Width = 360 };
            var values = new FlowLayoutPanel { AutoSize = true };
            _TimeHours = new TextBox { Text = "1", Width = 85, AccessibleName = "Durée en heures" };
            _TimeMaxHours = new TextBox { Text = "2", Width = 85, Enabled = false, AccessibleName = "Durée maximale en heures" };
            values.Controls.Add(SteamTheme.Label("Heures", 10)); values.Controls.Add(_TimeHours);
            values.Controls.Add(SteamTheme.Label("à", 10)); values.Controls.Add(_TimeMaxHours);
            _TimeRandom.CheckedChanged += (_, _) => _TimeMaxHours.Enabled = _TimeRandom.Checked;
            options.Controls.Add(_TimeRandom); options.Controls.Add(values);
            options.Controls.Add(SteamTheme.Label("20 jeux maximum · décimales acceptées (ex. 1,5 h)", 9, SteamTheme.Muted));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
            var start = new Button { Text = "Démarrer la sélection", AutoSize = true, Tag = "primary" }; start.Click += (_, _) => StartGameTime();
            var stop = new Button { Text = "Arrêter la session", AutoSize = true }; stop.Click += (_, _) => ParallelAction(() => { if (_TimeRuns.SelectedItem is TimeJob job) StopGameTime(job); });
            var resume = new Button { Text = "Reprendre", AutoSize = true }; resume.Click += (_, _) => ParallelAction(() => { if (_TimeRuns.SelectedItem is TimeJob job && !job.Plan.Order && job.Plan.ElapsedSeconds < job.Plan.GoalSeconds) { if (_TimeJobs.Values.Count(j => j.Plan.Resume || j.Running) >= 20) throw new InvalidOperationException("20 sessions maximum."); job.Plan.Resume = true; job.RetryUtc = DateTime.MinValue; SaveBackground(); } });
            var stopAll = new Button { Text = "Tout arrêter", AutoSize = true }; stopAll.Click += (_, _) => ParallelAction(StopAllGameTime);
            actions.Controls.Add(start); actions.Controls.Add(stop); actions.Controls.Add(stopAll); actions.Controls.Add(resume);
            split.Panel2.Controls.Add(_TimeRuns); split.Panel2.Controls.Add(actions); split.Panel2.Controls.Add(options);
            _TimeSummary = SteamTheme.Label("Aucune session active", 10, SteamTheme.Accent); _TimeSummary.Dock = DockStyle.Bottom;
            var note = SteamTheme.Label("Le minuteur arrête ses propres sessions. Une vue de succès ouverte peut maintenir le jeu actif.\nLa croix masque l’application. Reprise sauvegardée ; le temps hors ligne ne compte pas. Le crédit final dépend de Steam.", 9, SteamTheme.Muted);
            note.Dock = DockStyle.Bottom; note.AutoSize = false; note.Height = 55;
            _TimePanel.Controls.Add(split); _TimePanel.Controls.Add(_TimeSummary); _TimePanel.Controls.Add(note); _TimePanel.Controls.Add(subtitle); _TimePanel.Controls.Add(title);
            _GameHost.Controls.Add(_TimePanel); _TimePanel.SendToBack(); SteamTheme.Apply(_TimePanel);
            var timer = new Timer(components) { Interval = 1000, Enabled = true }; timer.Tick += (_, _) => TickGameTime();
            _BackgroundFolder = Path.GetDirectoryName(_CartPath);
            _BackgroundStore = new JsonStore<BackgroundState>(Path.Combine(_BackgroundFolder, "background-sessions.json"));
            try { _Background = _BackgroundStore.Read(); _BackgroundReady = true; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _TimeSummary.Text = e.Message; }
            if (_BackgroundReady) foreach (var plan in _Background.Sessions)
            {
                var job = MakeTimeJob(new GameInfo(plan.GameId, "normal") { Name = plan.Name }, plan);
                try { ObserveTimeJob(job); } catch (IOException) { }
                _TimeJobs[plan.GameId] = job;
            }
            RebuildTimeRows();
            FormClosed += (_, _) => { foreach (var job in _TimeJobs.Values) job.Process?.Dispose(); };
        }
        private void RefreshTimeGames(bool force = false)
        {
            string signature = _Games.Count + ":" + _TimeSearch.Text;
            if (!force && signature == _TimeCatalogSignature) return;
            _TimeCatalogSignature = signature; _BindingTime = true;
            _TimeGames.BeginUpdate(); _TimeGames.Items.Clear();
            foreach (var game in _Games.Values.OrderBy(g => g.Name))
                if (game.Name.IndexOf(_TimeSearch.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) _TimeGames.Items.Add(game, _TimeSelection.Contains(game.Id));
            _TimeGames.EndUpdate(); _BindingTime = false;
        }
        private void SaveBackground()
        {
            if (!_BackgroundReady) throw new IOException("Sauvegarde indisponible : les sessions ne peuvent pas démarrer.");
            BackgroundPlanning.Validate(_Background);
            _BackgroundStore.Update(s => { s.Sessions = _Background.Sessions; s.Games = _Background.Games; s.Startup = _Background.Startup; s.ParallelEnabled = _Background.ParallelEnabled; s.ParallelLimit = _Background.ParallelLimit; });
        }
        private TimeJob MakeTimeJob(GameInfo game, TimePlan plan) => new TimeJob { Game = game, Plan = plan,
            Duration = TimeSpan.FromSeconds(plan.GoalSeconds), Store = new JsonStore<PlaytimeStatus>(Path.Combine(_BackgroundFolder, "sessions", plan.Token + ".json")) };
        private void RebuildTimeRows()
        {
            _TimeRuns.BeginUpdate(); _TimeRuns.Items.Clear();
            foreach (var job in _TimeJobs.Values) _TimeRuns.Items.Add(job);
            _TimeRuns.EndUpdate();
        }
        private TimeJob AddTimeJob(GameInfo game, double seconds, bool order)
        {
            if (_TimeJobs.TryGetValue(game.Id, out var old) && (old.Running || old.Plan.Resume))
                throw new InvalidOperationException("Une session existe déjà pour " + game.Name + ". Arrêtez-la avant de changer de mode.");
            if (_TimeJobs.Values.Count(j => j.Running || j.Plan.Resume) >= BackgroundPlanning.SessionLimit)
                throw new InvalidOperationException("20 sessions maximum, y compris les jeux d’Achievement Order.");
            var plan = new TimePlan { GameId = game.Id, Name = game.Name, GoalSeconds = seconds, Resume = true, Order = order };
            var job = MakeTimeJob(game, plan);
            _Background.Sessions.RemoveAll(s => s.GameId == game.Id); _Background.Sessions.Add(plan);
            SaveBackground(); // Persist intent before any worker can start.
            old?.Process?.Dispose(); _TimeJobs[game.Id] = job; RebuildTimeRows();
            return job;
        }
        private void LaunchTimeJob(TimeJob job)
        {
            if (job.Plan.ElapsedSeconds >= job.Plan.GoalSeconds) { job.Plan.Resume = false; return; }
            job.Store.Update(s => { s.State = "starting"; s.ElapsedSeconds = job.Plan.ElapsedSeconds; s.Message = "Connexion à Steam…"; s.HeartbeatUtc = default; });
            using var owner = Process.GetCurrentProcess();
            var start = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"),
                job.Game.Id + " --playtime --seconds " + job.Plan.GoalSeconds.ToString(CultureInfo.InvariantCulture) +
                " --elapsed " + job.Plan.ElapsedSeconds.ToString(CultureInfo.InvariantCulture) + " --owner " + owner.Id +
                " --owner-start " + owner.StartTime.ToUniversalTime().Ticks + " --status \"" + job.Store.Path + "\"" + (_Demo ? " --demo" : ""))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory };
            job.Process?.Dispose(); job.Process = Process.Start(start); job.Running = true; job.Clock.Restart();
        }
        private void StartGameTime()
        {
            try
            {
                double min = BackgroundPlanning.Hours(_TimeHours.Text), max = _TimeRandom.Checked ? BackgroundPlanning.Hours(_TimeMaxHours.Text) : min;
                if (max < min) throw new InvalidOperationException("Le maximum doit dépasser le minimum.");
                var requested = _TimeSelection.Where(id => _Games.ContainsKey(id) && (!_TimeJobs.TryGetValue(id, out var job) || (!job.Running && !job.Plan.Resume))).ToArray();
                if (requested.Length + _TimeJobs.Values.Count(j => j.Running || j.Plan.Resume) > BackgroundPlanning.SessionLimit) throw new InvalidOperationException("20 sessions maximum, y compris Achievement Order.");
                if (requested.Length == 0) return;
                foreach (uint id in requested) AddTimeJob(_Games[id], (min + (max - min) * _TimeRandomSource.NextDouble()) * 3600, false);
                EnableBackgroundRecovery(); TickGameTime();
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is IOException || e is UnauthorizedAccessException)
            { MessageBox.Show(this, e.Message, "GameTime Spoofer"); }
        }
        private void ObserveTimeJob(TimeJob job)
        {
            var state = job.Store.Read(); job.Status = state;
            if (!double.IsNaN(state.ElapsedSeconds) && !double.IsInfinity(state.ElapsedSeconds) && state.ElapsedSeconds >= 0)
                job.Plan.ElapsedSeconds = Math.Min(job.Plan.GoalSeconds, Math.Max(job.Plan.ElapsedSeconds, state.ElapsedSeconds));
            if (job.Plan.ElapsedSeconds >= job.Plan.GoalSeconds || state.State == "completed") job.Plan.Resume = false;
        }
        private void TickGameTime()
        {
            if (_ClosingLibrary || IsDisposed || !_BackgroundReady) return;
            RefreshTimeGames();
            try
            {
                foreach (var job in _TimeJobs.Values.ToArray())
                {
                    try
                    {
                        if (job.Running)
                        {
                            ObserveTimeJob(job);
                            if (job.Process.HasExited) { job.Running = false; job.RetryUtc = DateTime.UtcNow.AddSeconds(30); }
                            else if ((job.Status.State == "starting" && job.Clock.Elapsed.TotalSeconds > 45) ||
                                (job.Status.State == "running" && (DateTime.UtcNow - job.Status.HeartbeatUtc).TotalSeconds > 20))
                            { KillTimeWorker(job); job.RetryUtc = DateTime.UtcNow.AddSeconds(30); job.Status.Message = "Session interrompue · nouvelle tentative dans 30 s"; }
                        }
                        if (!job.Running && job.Plan.Resume && (!job.Plan.Order || _Background.ParallelEnabled) && DateTime.UtcNow >= job.RetryUtc)
                            LaunchTimeJob(job);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
                    { job.Status.Message = e.Message + " · nouvelle tentative dans 30 s"; job.RetryUtc = DateTime.UtcNow.AddSeconds(30); }
                }
                SaveBackground();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _TimeSummary.Text = "Sauvegarde interrompue : " + e.Message; return; }
            int active = _TimeJobs.Values.Count(j => j.Running);
            _TimeSummary.Text = (_Demo ? "SIMULATION · " : "") + active + "/20 sessions · progression sauvegardée · arrêt à l’objectif";
            _TimeButton.Text = "GameTime" + (active > 0 ? " · " + active : " Spoofer"); _TimeRuns.Invalidate();
        }
        private void KillTimeWorker(TimeJob job)
        {
            if (!job.Running) return;
            if (!job.Process.HasExited) { job.Process.Kill(); job.Process.WaitForExit(3000); }
            job.Running = false;
        }
        private void StopGameTime(TimeJob job, string reason = "Arrêt manuel")
        {
            if (job.Plan.Order && _Background.ParallelEnabled) { PauseParallel(); return; }
            job.Plan.Resume = false; SaveBackground();
            KillTimeWorker(job); ObserveTimeJob(job); job.Plan.Resume = false;
            job.Status.State = "stopped"; job.Status.Message = reason; SaveBackground(); _TimeRuns?.Invalidate();
        }
        private void StopAllGameTime()
        {
            if (_Background.ParallelEnabled) PauseParallel();
            foreach (var job in _TimeJobs.Values) StopGameTime(job);
        }
        private void SuspendTimeWorkers()
        {
            foreach (var job in _TimeJobs.Values)
            {
                try { KillTimeWorker(job); ObserveTimeJob(job); } catch (Exception e) when (e is IOException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception) { }
            }
            SaveBackground();
        }
        private void DrawTimeJob(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return; var job = (TimeJob)_TimeRuns.Items[e.Index];
            var rect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 5, e.Bounds.Width - 12, 80);
            using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SteamTheme.Blue : SteamTheme.Surface);
            e.Graphics.FillRectangle(bg, rect);
            TextRenderer.DrawText(e.Graphics, job.Game.Name, Font, new Rectangle(rect.X + 14, rect.Y + 10, rect.Width - 28, 24), SteamTheme.Text, TextFormatFlags.EndEllipsis);
            string state = job.Running && job.Status.State == "running" ? "● Active · " + FormatRemaining(job.Remaining) + " restant" : job.Status.Message ?? "Connexion à Steam…";
            TextRenderer.DrawText(e.Graphics, state, Font, new Rectangle(rect.X + 14, rect.Y + 43, rect.Width - 28, 24), job.Running ? SteamTheme.Green : SteamTheme.Muted, TextFormatFlags.EndEllipsis);
        }
    }
}
