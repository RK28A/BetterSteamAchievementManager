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
        private sealed class ParallelLane
        {
            internal GamePlan Plan;
            internal string Pending;
            internal readonly Stopwatch Wait = new();
        }
        private readonly Dictionary<uint, ParallelLane> _ParallelLanes = new();
        private readonly HashSet<uint> _CompletionAttempted = new();
        private string _ParallelRunId;
        private Panel _ParallelPanel;
        private ListBox _ParallelGames;
        private TextBox _ParallelCount, _TargetHours, _WindowFrom, _WindowTo, _SourceUrl;
        private FlowLayoutPanel _ParallelSettings;
        private Label _ParallelStatus, _CompletionLabel, _SteamHoursLabel;
        private Button _ParallelStart, _LookupCompletion;
        private GamePlan SelectedPlan => _ParallelGames.SelectedItem as GamePlan;
        private string GameCommandPath(uint id) => Path.Combine(Path.GetDirectoryName(_CommandPath), "command-" + id + ".json");
        private void InitializeParallelOrder(Panel header)
        {
            var mode = new Button { Text = "Mode simultané →", Dock = DockStyle.Right, Width = 185 };
            header.Controls.Add(mode); mode.BringToFront(); SteamTheme.Apply(mode);
            mode.Click += (_, _) => ParallelAction(() => { RefreshParallelGames(); _ParallelPanel.BringToFront(); });
            _ParallelPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
            var heading = SteamTheme.Label("Achievement Order / Simultané", 21); heading.Dock = DockStyle.Top;
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46 };
            var back = new Button { Text = "← Panier et mode séquentiel", AutoSize = true }; back.Click += (_, _) => _OrderPanel.BringToFront();
            navigation.Controls.Add(back); navigation.Controls.Add(SteamTheme.Label("Jeux en parallèle (1–10)", 10));
            _ParallelCount = new TextBox { Width = 45, Text = _Background.ParallelLimit.ToString(), AccessibleName = "Nombre de jeux simultanés" }; navigation.Controls.Add(_ParallelCount);
            var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(850, 440), SplitterDistance = 510, FixedPanel = FixedPanel.Panel2, Panel1MinSize = 250, Panel2MinSize = 270, SplitterWidth = 8 };
            _ParallelGames = new ListBox { Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 136, BorderStyle = BorderStyle.None, IntegralHeight = false, AccessibleName = "Jeux du panier simultané" };
            _ParallelGames.DrawItem += DrawParallelGame; _ParallelGames.SelectedIndexChanged += (_, _) => BindParallelGame();
            split.Panel1.Controls.Add(_ParallelGames);
            _ParallelSettings = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            TextBox Field(string label, string accessible)
            {
                _ParallelSettings.Controls.Add(SteamTheme.Label(label, 10));
                var field = new TextBox { Width = 240, AccessibleName = accessible }; _ParallelSettings.Controls.Add(field); return field;
            }
            _SteamHoursLabel = SteamTheme.Label("Temps Steam en cache : indisponible", 10, SteamTheme.Muted); _SteamHoursLabel.AutoSize = false; _SteamHoursLabel.Size = new Size(240, 44); _ParallelSettings.Controls.Add(_SteamHoursLabel);
            _CompletionLabel = SteamTheme.Label("HowLongToBeat · moyenne 100 %", 10, SteamTheme.Accent); _CompletionLabel.AutoSize = false; _CompletionLabel.Size = new Size(240, 60); _ParallelSettings.Controls.Add(_CompletionLabel);
            _SourceUrl = Field("Lien HLTB (facultatif)", "Fiche HowLongToBeat");
            _LookupCompletion = new Button { Text = "Charger la moyenne 100 %", AutoSize = true }; _LookupCompletion.Click += async (_, _) => await LoadCompletion(SelectedPlan); _ParallelSettings.Controls.Add(_LookupCompletion);
            var visit = new Button { Text = "Ouvrir HowLongToBeat", AutoSize = true }; visit.Click += (_, _) =>
            {
                string url = CompletionSource.ValidUrl(_SourceUrl.Text) ? _SourceUrl.Text : "https://howlongtobeat.com/?q=" + Uri.EscapeDataString(SelectedPlan?.Name ?? "");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }; _ParallelSettings.Controls.Add(visit);
            _TargetHours = Field("Objectif total en heures", "Objectif de temps total");
            _WindowFrom = Field("Premier créneau · heure de session", "Début de la période de déblocage");
            _WindowTo = Field("Dernier créneau · heure de session", "Fin de la période de déblocage");
            var save = new Button { Text = "Enregistrer ce jeu", AutoSize = true }; save.Click += (_, _) => ParallelAction(SaveParallelGame); _ParallelSettings.Controls.Add(save);
            var hint = SteamTheme.Label("Les créneaux sont répartis au hasard dans cette période de temps actif.\n\nL’objectif inclut le temps Steam déjà connu. Sans donnée Steam, il correspond à la durée à effectuer.\n\nModifier une configuration remet son planning local à zéro ; les succès confirmés restent acquis.", 9, SteamTheme.Muted); hint.AutoSize = false; hint.Size = new Size(240, 185); _ParallelSettings.Controls.Add(hint);
            split.Panel2.Controls.Add(_ParallelSettings);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
            _ParallelStart = new Button { Text = "Démarrer / reprendre", AutoSize = true }; _ParallelStart.Click += (_, _) => ParallelAction(StartParallel);
            var pause = new Button { Text = "Pause", AutoSize = true }; pause.Click += (_, _) => ParallelAction(PauseParallel);
            actions.Controls.Add(_ParallelStart); actions.Controls.Add(pause);
            _ParallelStatus = SteamTheme.Label("Ajoutez des succès au panier, puis configurez chaque jeu.", 10, SteamTheme.Accent); _ParallelStatus.Dock = DockStyle.Bottom; _ParallelStatus.AutoSize = false; _ParallelStatus.Height = 65;
            _ParallelPanel.Controls.Add(split); _ParallelPanel.Controls.Add(actions); _ParallelPanel.Controls.Add(_ParallelStatus); _ParallelPanel.Controls.Add(navigation); _ParallelPanel.Controls.Add(heading);
            _GameHost.Controls.Add(_ParallelPanel); _ParallelPanel.SendToBack(); SteamTheme.Apply(_ParallelPanel);
            var timer = new Timer(components) { Interval = 1000, Enabled = true }; timer.Tick += (_, _) => TickParallel();
        }
        private void ParallelAction(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            { _ParallelStatus.Text = e.Message; }
        }
        private void RefreshParallelGames()
        {
            var cart = _OrderStore.Read();
            foreach (var group in cart.Games)
                if (!_Background.Games.Any(g => g.GameId == group.GameId))
                {
                    double? hours = _Demo ? null : CompletionSource.SteamHours(API.Steam.GetInstallPath(), _SteamClient.SteamUser.GetSteamId(), group.GameId);
                    _Background.Games.Add(new GamePlan { GameId = group.GameId, Name = group.Name, SteamHours = hours, TargetHours = Math.Max(10, (hours ?? 0) + 1), WindowEndHours = 0 });
                }
            uint selected = SelectedPlan?.GameId ?? 0;
            _ParallelGames.BeginUpdate(); _ParallelGames.Items.Clear();
            foreach (var group in cart.Games) _ParallelGames.Items.Add(_Background.Games.First(g => g.GameId == group.GameId));
            _ParallelGames.EndUpdate();
            _ParallelGames.SelectedIndex = _ParallelGames.Items.Cast<GamePlan>().ToList().FindIndex(g => g.GameId == selected);
            if (_ParallelGames.SelectedIndex < 0 && _ParallelGames.Items.Count > 0) _ParallelGames.SelectedIndex = 0;
            _ParallelStart.Enabled = !_Demo && !_Runner.Running; SaveBackground();
        }
        private void BindParallelGame()
        {
            var plan = SelectedPlan; if (plan == null) return;
            _SteamHoursLabel.Text = plan.SteamHours.HasValue ? $"Temps Steam en cache : {plan.SteamHours:0.##} h" : "Temps Steam indisponible · objectif = heures à effectuer";
            _CompletionLabel.Text = plan.CompletionHours.HasValue ? $"HowLongToBeat · 100 % : {plan.CompletionHours:0.##} h\nMoyenne communautaire, pas une durée garantie." : "HowLongToBeat · moyenne indisponible\nVous pouvez saisir votre propre objectif.";
            _TargetHours.Text = plan.TargetHours.ToString("0.##"); _WindowFrom.Text = plan.WindowStartHours.ToString("0.##");
            _WindowTo.Text = (plan.WindowEndHours > 0 ? plan.WindowEndHours : Math.Max(0, plan.TargetHours - (plan.SteamHours ?? 0))).ToString("0.##");
            _SourceUrl.Text = plan.SourceUrl ?? "";
            if (!_Demo && !_Background.ParallelEnabled && !plan.Ready && !plan.CompletionHours.HasValue && _CompletionAttempted.Add(plan.GameId)) _ = LoadCompletion(plan);
        }
        private async System.Threading.Tasks.Task LoadCompletion(GamePlan plan)
        {
            if (plan == null || _Background.ParallelEnabled || _Demo) return;
            _LookupCompletion.Enabled = false; string url = SelectedPlan == plan ? _SourceUrl.Text.Trim() : plan.SourceUrl;
            try
            {
                var result = await CompletionSource.Lookup(plan.GameId, plan.Name, url);
                if (IsDisposed || _ClosingLibrary || _Background.ParallelEnabled || plan.Ready) return;
                plan.CompletionHours = result.Hours; plan.SourceUrl = result.Url; plan.TargetHours = result.Hours; plan.WindowEndHours = Math.Max(0, plan.TargetHours - (plan.SteamHours ?? 0));
                SaveBackground(); if (SelectedPlan == plan) BindParallelGame();
            }
            catch (Exception e) when (e is System.Net.Http.HttpRequestException || e is System.Threading.Tasks.TaskCanceledException || e is InvalidOperationException || e is ArgumentException || e is IOException)
            { if (!IsDisposed && SelectedPlan == plan) _CompletionLabel.Text = "HLTB indisponible · ouvrez la fiche ou saisissez l’objectif manuellement."; }
            finally { if (!IsDisposed) _LookupCompletion.Enabled = true; }
        }
        private void SaveParallelGame()
        {
            if (_Background.ParallelEnabled || _Runner.Running) throw new InvalidOperationException("Mettez les commandes en pause avant de modifier les objectifs.");
            var plan = SelectedPlan; if (plan == null) return;
            double target = BackgroundPlanning.Hours(_TargetHours.Text), from = BackgroundPlanning.Hours(_WindowFrom.Text, true), to = BackgroundPlanning.Hours(_WindowTo.Text);
            double remaining = target - (plan.SteamHours ?? 0);
            if (remaining <= 0 || to > remaining || from >= to) throw new InvalidOperationException("La période doit être comprise dans les heures restantes : de 0 à " + Math.Max(0, remaining).ToString("0.##") + " h. Augmentez l’objectif si nécessaire.");
            if (!string.IsNullOrWhiteSpace(_SourceUrl.Text) && !CompletionSource.ValidUrl(_SourceUrl.Text.Trim())) throw new InvalidOperationException("Lien HLTB invalide.");
            plan.TargetHours = target; plan.WindowStartHours = from; plan.WindowEndHours = to; plan.SourceUrl = _SourceUrl.Text.Trim();
            plan.Ready = true; plan.Completed = false; plan.Error = null; plan.Achievements.Clear();
            if (_TimeJobs.TryGetValue(plan.GameId, out var job) && job.Plan.Order) { KillTimeWorker(job); _TimeJobs.Remove(plan.GameId); _Background.Sessions.Remove(job.Plan); RebuildTimeRows(); }
            SaveBackground(); _ParallelStatus.Text = plan.Name + " · configuration enregistrée"; _ParallelGames.Invalidate();
        }
        private void StartParallel()
        {
            if (_Demo || _Runner.Running) return;
            if (!int.TryParse(_ParallelCount.Text, out int count) || count < 1 || count > 10) throw new InvalidOperationException("Choisissez entre 1 et 10 jeux simultanés.");
            if (_Background.ParallelEnabled) return;
            var cart = _OrderStore.Read(); OrderPaths.EnsureEditable(cart);
            var plans = _Background.Games.Where(p => cart.Games.Any(g => g.GameId == p.GameId && g.Achievements.Any(a => a.Status != "done")) || (p.Ready && !p.Completed && _TimeJobs.TryGetValue(p.GameId, out var session) && session.Plan.Order)).ToArray();
            if (plans.Length == 0) throw new InvalidOperationException("Ajoutez des succès au panier.");
            if (plans.Any(p => !p.Ready)) throw new InvalidOperationException("Enregistrez l’objectif et la période de chaque jeu avant de démarrer.");
            if (plans.Any(p => _TimeJobs.TryGetValue(p.GameId, out var job) && !job.Plan.Order && (job.Running || job.Plan.Resume))) throw new InvalidOperationException("Un jeu du panier tourne déjà dans GameTime. Arrêtez cette session avant de démarrer Order.");
            foreach (var plan in plans)
            {
                plan.Error = null;
                if (plan.Achievements.Count == 0)
                {
                    var group = cart.Games.First(g => g.GameId == plan.GameId);
                    plan.Achievements = BackgroundPlanning.Schedule(group.Achievements.Where(a => a.Status != "done").Select(a => a.Id), plan.WindowStartHours * 3600, plan.WindowEndHours * 3600, _TimeRandomSource.NextDouble);
                }
                else if (cart.Games.First(g => g.GameId == plan.GameId).Achievements.Any(a => a.Status != "done" && !plan.Achievements.Any(s => s.Id == a.Id)))
                    throw new InvalidOperationException("Le panier de " + plan.Name + " a changé. Réenregistrez sa configuration pour créer un nouveau planning.");
            }
            _Background.ParallelLimit = count; _Background.ParallelEnabled = true; SaveBackground(); EnableBackgroundRecovery(); TickParallel();
        }
        private void AcquireParallelLease()
        {
            if (_ParallelRunId != null) return;
            var cart = _OrderStore.Read(); if (cart.IsRunning) return; // A previous crashed owner may retain a short lease.
            _OrderRunGate = new System.Threading.Mutex(false, "Local\\BSAM-AchievementOrder-Run");
            try { _OwnOrderRunGate = _OrderRunGate.WaitOne(0); } catch (System.Threading.AbandonedMutexException) { _OwnOrderRunGate = true; }
            if (!_OwnOrderRunGate) { _OrderRunGate.Dispose(); _OrderRunGate = null; return; }
            _ParallelRunId = Guid.NewGuid().ToString("N");
            _OrderStore.Update(c => { OrderPaths.EnsureEditable(c); c.ActiveRun = _ParallelRunId; c.LeaseUntilUtc = DateTime.UtcNow.AddSeconds(15); OrderPlanning.Record(c, "Simultané", "Démarrage ou reprise du planning enregistré."); });
        }
        private void TickParallel()
        {
            if (_ClosingLibrary || IsDisposed || !_BackgroundReady) return;
            _ParallelSettings.Enabled = !_Background.ParallelEnabled && !_Runner.Running;
            _ParallelCount.Enabled = !_Background.ParallelEnabled;
            if (!_Background.ParallelEnabled || _Demo) return;
            try
            {
                AcquireParallelLease(); if (_ParallelRunId == null) { _ParallelStatus.Text = "Attente de libération de la commande précédente…"; return; }
                _OrderStore.Update(c => { if (c.ActiveRun != _ParallelRunId) throw new InvalidOperationException("La commande a perdu sa réservation."); c.LeaseUntilUtc = DateTime.UtcNow.AddSeconds(15); });
                var cart = _OrderStore.Read();
                foreach (var plan in _Background.Games.Where(p => p.Ready && !p.Completed && p.Error == null && cart.Games.Any(g => g.GameId == p.GameId)))
                {
                    if (_ParallelLanes.Count >= _Background.ParallelLimit) break;
                    if (_ParallelLanes.ContainsKey(plan.GameId)) continue;
                    if (!_TimeJobs.TryGetValue(plan.GameId, out var job) || !job.Plan.Order)
                    {
                        if (_TimeJobs.Values.Count(j => j.Plan.Order && (j.Plan.Resume || j.Running)) >= 10) continue;
                        job = AddTimeJob(new GameInfo(plan.GameId, "normal") { Name = plan.Name }, (plan.TargetHours - (plan.SteamHours ?? 0)) * 3600, true);
                    }
                    job.Plan.Resume = job.Plan.ElapsedSeconds < job.Plan.GoalSeconds;
                    _ParallelLanes[plan.GameId] = new ParallelLane { Plan = plan };
                }
                foreach (var lane in _ParallelLanes.Values.ToArray()) TickParallelLane(lane, cart);
                if (_Background.Games.Where(p => p.Ready && cart.Games.Any(g => g.GameId == p.GameId)).All(p => p.Completed || p.Error != null))
                {
                    _Background.ParallelEnabled = false; SuspendParallel();
                    _ParallelStatus.Text = _Background.Games.Any(p => p.Error != null) ? "Des jeux sont en pause après une erreur. Consultez leurs cartes avant de reprendre." : "Objectifs atteints · toutes les sessions du panier sont arrêtées.";
                }
                else _ParallelStatus.Text = $"{_ParallelLanes.Count}/{_Background.ParallelLimit} jeux simultanés · planning enregistré · reprise automatique\nLes heures d’arrêt ou de veille du PC ne sont pas comptabilisées.";
                SaveBackground(); _ParallelGames.Invalidate();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception)
            { _ParallelStatus.Text = "Commande en pause : " + e.Message; PauseParallel(); }
        }
        private void TickParallelLane(ParallelLane lane, OrderCart cart)
        {
            var plan = lane.Plan; var job = _TimeJobs[plan.GameId]; var commands = new JsonStore<OrderCommand>(GameCommandPath(plan.GameId));
            var group = cart.Games.First(g => g.GameId == plan.GameId);
            foreach (var a in plan.Achievements) if (group.Achievements.Any(x => x.Id == a.Id && x.Status == "done")) a.Done = true;
            if (lane.Pending != null)
            {
                var command = commands.Read();
                if (command.Id == lane.Pending && command.State != "requested")
                {
                    if (command.State == "done")
                    {
                        plan.Achievements.First(a => a.Id == command.AchievementId).Done = true;
                        _OrderStore.Update(c => { var g = c.Games.First(x => x.GameId == plan.GameId); g.Achievements.First(a => a.Id == command.AchievementId).Status = "done"; OrderPlanning.Record(c, "Succès simultané", plan.Name + " · " + command.AchievementId + " · " + command.Message); });
                        lane.Pending = null;
                    }
                    else { FailParallelLane(lane, command.Message ?? "Confirmation Steam incertaine"); return; }
                }
                else if (lane.Wait.Elapsed.TotalSeconds > 120) { FailParallelLane(lane, "Confirmation Steam absente. Vérifiez le jeu puis reprenez."); return; }
                else if (!_Sessions.TryGetValue(plan.GameId, out var session) || session.Process.HasExited)
                    EnsureSession(job.Game, false); // Fresh process rechecks Steam before repeating a command.
                return;
            }
            if (job.Plan.ElapsedSeconds >= job.Plan.GoalSeconds && plan.Achievements.All(a => a.Done))
            {
                plan.Completed = true; job.Plan.Resume = false; KillTimeWorker(job); CloseOrderSession(plan.GameId); _ParallelLanes.Remove(plan.GameId); return;
            }
            bool active = job.Running && job.Status.State == "running" && (DateTime.UtcNow - job.Status.HeartbeatUtc).TotalSeconds < 10;
            if (!active && job.Plan.ElapsedSeconds < job.Plan.GoalSeconds) return;
            var due = plan.Achievements.FirstOrDefault(a => !a.Done && a.AtSeconds <= job.Plan.ElapsedSeconds);
            if (due == null) return;
            EnsureSession(job.Game, false);
            lane.Pending = Guid.NewGuid().ToString("N"); lane.Wait.Restart();
            commands.Update(c => { c.Id = lane.Pending; c.RunId = _ParallelRunId; c.GameId = plan.GameId; c.AchievementId = due.Id; c.State = "requested"; c.Message = null; });
        }
        private void FailParallelLane(ParallelLane lane, string message)
        {
            lane.Plan.Error = message; var job = _TimeJobs[lane.Plan.GameId]; job.Plan.Resume = false; KillTimeWorker(job);
            CloseOrderSession(lane.Plan.GameId); _ParallelLanes.Remove(lane.Plan.GameId);
        }
        private void CloseOrderSession(uint id)
        {
            if (!_Sessions.TryGetValue(id, out var session)) return;
            if (!session.Process.HasExited) { session.Process.Kill(); session.Process.WaitForExit(3000); }
            session.Process.Dispose(); session.Panel.Dispose(); _Sessions.Remove(id);
        }
        private void PauseParallel()
        {
            if (!_Background.ParallelEnabled && _ParallelRunId == null) return;
            _Background.ParallelEnabled = false;
            foreach (var job in _TimeJobs.Values.Where(j => j.Plan.Order)) { job.Plan.Resume = false; KillTimeWorker(job); ObserveTimeJob(job); }
            SuspendParallel(); SaveBackground(); _ParallelStatus.Text = "En pause · progression et créneaux conservés. Les envois déjà partis seront vérifiés auprès de Steam à la reprise.";
        }
        private void SuspendParallel()
        {
            if (_ParallelRunId != null)
            {
                _OrderStore.Update(c => { if (c.ActiveRun == _ParallelRunId) { c.ActiveRun = null; c.LeaseUntilUtc = default; } });
                _ParallelRunId = null;
                if (_OwnOrderRunGate) { _OrderRunGate.ReleaseMutex(); _OwnOrderRunGate = false; }
                _OrderRunGate?.Dispose(); _OrderRunGate = null;
            }
            foreach (uint id in _ParallelLanes.Keys.ToArray()) CloseOrderSession(id);
            _ParallelLanes.Clear();
        }
        private void DrawParallelGame(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _ParallelGames.Items.Count) return;
            var plan = (GamePlan)_ParallelGames.Items[e.Index];
            var rect = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 5, e.Bounds.Width - 9, e.Bounds.Height - 10);
            using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SteamTheme.Blue : SteamTheme.Surface); e.Graphics.FillRectangle(bg, rect);
            int x = rect.X + 15;
            if (_Games.TryGetValue(plan.GameId, out var game) && game.ImageIndex >= 0 && game.ImageIndex < _LogoImageList.Images.Count)
            { e.Graphics.DrawImage(_LogoImageList.Images[game.ImageIndex], new Rectangle(x, rect.Y + 15, 48, 48)); x += 60; }
            var progress = _LibraryProgress.Games.FirstOrDefault(g => g.GameId == plan.GameId);
            string count = progress == null ? plan.Achievements.Count + " succès planifiés" : progress.Unlocked + "/" + progress.Total + " succès Steam";
            string hours = plan.SteamHours.HasValue ? plan.SteamHours.Value.ToString("0.##") + " h Steam" : "Temps Steam inconnu";
            string status = plan.Error ?? (plan.Completed ? "Objectif atteint" : plan.Ready ? "Prêt / reprise enregistrée" : "Configuration à enregistrer");
            if (_TimeJobs.TryGetValue(plan.GameId, out var job) && job.Plan.Order)
                status = (job.Running ? "● En cours · " : "Progression · ") + (job.Plan.ElapsedSeconds / 3600).ToString("0.##") + "/" + (job.Plan.GoalSeconds / 3600).ToString("0.##") + " h de session" + (plan.Error != null ? " · " + plan.Error : "");
            TextRenderer.DrawText(e.Graphics, plan.Name, Font, new Rectangle(x, rect.Y + 12, rect.Right - x - 10, 24), SteamTheme.Text, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, hours + " · " + count, Font, new Rectangle(x, rect.Y + 40, rect.Right - x - 10, 24), SteamTheme.Muted, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, "Objectif " + plan.TargetHours.ToString("0.##") + " h · HLTB " + (plan.CompletionHours?.ToString("0.##") ?? "—") + " h", Font, new Rectangle(rect.X + 15, rect.Y + 70, rect.Width - 30, 23), SteamTheme.Accent, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, status, Font, new Rectangle(rect.X + 15, rect.Y + 97, rect.Width - 30, 22), plan.Error == null ? SteamTheme.Muted : Color.Salmon, TextFormatFlags.EndEllipsis);
        }
    }
}
