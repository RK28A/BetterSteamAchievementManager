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
            internal Process Process;
            internal JsonStore<PlaytimeStatus> Store;
            internal TimeSpan Duration;
            internal Stopwatch Clock = Stopwatch.StartNew();
            internal PlaytimeStatus Status = new();
            internal bool Running;
            internal TimeSpan Remaining => TimeSpan.FromSeconds(Math.Max(0, Duration.TotalSeconds - (Status.StartedUtc == default ? 0 : (DateTime.UtcNow - Status.StartedUtc).TotalSeconds)));
            public override string ToString() => Game.Name + " · " + Status.Message;
        }
        private readonly Dictionary<uint, TimeJob> _TimeJobs = new();
        private readonly HashSet<uint> _TimeSelection = new();
        private Panel _TimePanel;
        private CheckedListBox _TimeGames;
        private ListBox _TimeRuns;
        private TextBox _TimeSearch;
        private CheckBox _TimeRandom;
        private RangeEditor _TimeRange;
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
            var options = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 195, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _TimeRandom = new SteamCheckBox { Text = "Durée aléatoire pour chaque jeu", AutoSize = false, Height = 30, Width = 330 };
            _TimeRange = new RangeEditor("Durée de session") { Width = 330, Dock = DockStyle.None };
            _TimeRange.Bind(new DelayRange { Minimum = 30, Maximum = 60 });
            _TimeRandom.CheckedChanged += (_, _) => { _TimeRange.FixedDuration = !_TimeRandom.Checked; options.Height = _TimeRandom.Checked ? 195 : 155; };
            _TimeRange.FixedDuration = true; options.Height = 155;
            options.Controls.Add(_TimeRandom); options.Controls.Add(_TimeRange);
            options.SizeChanged += (_, _) => { _TimeRange.Width = Math.Max(200, options.ClientSize.Width - 10); _TimeRandom.Width = _TimeRange.Width; };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true };
            var start = new Button { Text = "Démarrer la sélection", AutoSize = true, Tag = "primary" }; start.Click += (_, _) => StartGameTime();
            var stop = new Button { Text = "Arrêter la session", AutoSize = true }; stop.Click += (_, _) => { if (_TimeRuns.SelectedItem is TimeJob job) StopGameTime(job); };
            var stopAll = new Button { Text = "Tout arrêter", AutoSize = true }; stopAll.Click += (_, _) => StopAllGameTime();
            actions.Controls.Add(start); actions.Controls.Add(stop); actions.Controls.Add(stopAll);
            split.Panel2.Controls.Add(_TimeRuns); split.Panel2.Controls.Add(actions); split.Panel2.Controls.Add(options);
            _TimeSummary = SteamTheme.Label("Aucune session active", 10, SteamTheme.Accent); _TimeSummary.Dock = DockStyle.Bottom;
            var note = SteamTheme.Label("Le minuteur arrête ses propres sessions. Une vue de succès ouverte peut maintenir le jeu actif.\nFermer l’application arrête ces sessions. Le temps crédité dépend de Steam.", 9, SteamTheme.Muted);
            note.Dock = DockStyle.Bottom; note.AutoSize = false; note.Height = 55;
            _TimePanel.Controls.Add(split); _TimePanel.Controls.Add(_TimeSummary); _TimePanel.Controls.Add(note); _TimePanel.Controls.Add(subtitle); _TimePanel.Controls.Add(title);
            _GameHost.Controls.Add(_TimePanel); _TimePanel.SendToBack(); SteamTheme.Apply(_TimePanel);
            var timer = new Timer(components) { Interval = 1000, Enabled = true }; timer.Tick += (_, _) => TickGameTime();
            FormClosed += (_, _) => { StopAllGameTime(); foreach (var job in _TimeJobs.Values) job.Process?.Dispose(); };
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
        private void StartGameTime()
        {
            try
            {
                var settings = new PlaytimeSettings { Randomized = _TimeRandom.Checked, Duration = _TimeRange.Range };
                settings.Sample(0); // Validate before starting any process.
                int requested = _TimeSelection.Count(id => _Games.ContainsKey(id) && (!_TimeJobs.TryGetValue(id, out var active) || !active.Running));
                if (requested + _TimeJobs.Values.Count(j => j.Running) > 10) throw new InvalidOperationException("Vous pouvez maintenir jusqu’à 10 sessions GameTime simultanées.");
                foreach (uint id in _TimeSelection.ToArray())
                {
                    if (!_Games.TryGetValue(id, out var game) || (_TimeJobs.TryGetValue(id, out var existing) && existing.Running)) continue;
                    var duration = settings.Sample(_TimeRandomSource.NextDouble());
                    string statusPath = Path.Combine(Path.GetDirectoryName(_CommandPath), "time-" + id + "-" + Guid.NewGuid().ToString("N") + ".json");
                    var job = new TimeJob { Game = game, Duration = duration, Store = new JsonStore<PlaytimeStatus>(statusPath) };
                    using var owner = Process.GetCurrentProcess();
                    var start = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"),
                        id + " --playtime --seconds " + duration.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " --owner " + owner.Id +
                        " --owner-start " + owner.StartTime.ToUniversalTime().Ticks + " --status \"" + statusPath + "\"" + (_Demo ? " --demo" : ""))
                    { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory };
                    job.Process = Process.Start(start); job.Running = true;
                    existing?.Process?.Dispose(); _TimeJobs[id] = job;
                }
                TickGameTime();
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.ComponentModel.Win32Exception || e is IOException)
            { MessageBox.Show(this, e.Message, "GameTime Spoofer"); }
            finally
            {
                _TimeRuns.BeginUpdate(); _TimeRuns.Items.Clear();
                foreach (var job in _TimeJobs.Values) _TimeRuns.Items.Add(job);
                _TimeRuns.EndUpdate();
            }
        }
        private void TickGameTime()
        {
            if (_ClosingLibrary || IsDisposed) return;
            RefreshTimeGames();
            foreach (var job in _TimeJobs.Values.Where(j => j.Running))
            {
                try
                {
                    job.Status = job.Store.Read();
                    if (job.Process.HasExited)
                    {
                        job.Running = false;
                        if (job.Status.State == "starting" || job.Status.State == "running") { job.Status.State = "failed"; job.Status.Message = "Session interrompue"; }
                    }
                    else if (job.Status.State == "starting" && job.Clock.Elapsed > TimeSpan.FromSeconds(45)) StopGameTime(job, "Connexion Steam trop longue");
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException) { StopGameTime(job, e.Message); }
            }
            int active = _TimeJobs.Values.Count(j => j.Running);
            _TimeSummary.Text = (_Demo ? "SIMULATION · " : "") + active + " session(s) active(s) · arrêt automatique à la fin de chaque durée";
            _TimeButton.Text = "GameTime" + (active > 0 ? " · " + active : " Spoofer");
            _TimeRuns.Invalidate();
        }
        private void StopGameTime(TimeJob job, string reason = "Arrêt manuel")
        {
            if (!job.Running) return;
            try { if (!job.Process.HasExited) job.Process.Kill(); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception e) { job.Status.Message = e.Message; return; }
            job.Running = false; job.Status.State = "stopped"; job.Status.Message = reason;
            _TimeRuns?.Invalidate();
        }
        private void StopAllGameTime() { foreach (var job in _TimeJobs.Values) StopGameTime(job); }
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
