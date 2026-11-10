// Altered source: timed achievement queue added by BetterSteamAchievementManager.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Game
{
    internal partial class Manager
    {
        private readonly CheckedListBox _QueueChoices = new() { Dock = DockStyle.Fill, CheckOnClick = true };
        private readonly NumericUpDown _MinimumMinutes = new() { Minimum = 1, Maximum = 1440, Value = 3, Width = 70 };
        private readonly NumericUpDown _MaximumMinutes = new() { Minimum = 1, Maximum = 1440, Value = 10, Width = 70 };
        private readonly Button _StartQueue = new() { Text = "Démarrer", AutoSize = true };
        private readonly Button _StopQueue = new() { Text = "Arrêter", AutoSize = true, Enabled = false };
        private readonly Label _QueueStatus = new() { AutoSize = true, Text = "Sélectionnez les succès à débloquer." };
        private readonly ListBox _QueueLog = new() { Dock = DockStyle.Fill };
        private readonly Queue<Stats.AchievementDefinition> _PendingAchievements = new();
        private readonly Random _QueueRandom = new();
        private readonly Stopwatch _QueueClock = new();
        private Timer _QueueTimer;
        private API.Callbacks.UserStatsStored _QueueStoredCallback;
        private Stats.AchievementDefinition _SavingAchievement;
        private TimeSpan _QueueDelay;
        private bool _QueueRunning;
        private bool _QueueReady;
        private int _QueueCompleted;
        private int _QueueTotal;

        private void InitializeScheduler()
        {
            var page = new TabPage("Déblocage progressif") { Padding = new Padding(10) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            layout.Controls.Add(new Label { AutoSize = true, Text = "Un succès à la fois, dans l’ordre affiché. Délai aléatoire avant chaque succès." });
            var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
            controls.Controls.Add(new Label { Text = "Entre (min)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            controls.Controls.Add(this._MinimumMinutes);
            controls.Controls.Add(new Label { Text = "et (min)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
            controls.Controls.Add(this._MaximumMinutes);
            var selectAll = new Button { Text = "Tout sélectionner", AutoSize = true };
            var selectNone = new Button { Text = "Tout désélectionner", AutoSize = true };
            selectAll.Click += (_, _) => this.CheckQueueChoices(true);
            selectNone.Click += (_, _) => this.CheckQueueChoices(false);
            controls.Controls.Add(selectAll);
            controls.Controls.Add(selectNone);
            controls.Controls.Add(this._StartQueue);
            controls.Controls.Add(this._StopQueue);
            layout.Controls.Add(controls);
            layout.Controls.Add(this._QueueChoices);
            layout.Controls.Add(this._QueueStatus);
            layout.Controls.Add(this._QueueLog);
            page.Controls.Add(layout);
            this._MainTabControl.TabPages.Add(page);
            this._QueueChoices.FormattingEnabled = true;
            this._QueueChoices.Format += (_, e) =>
            {
                if (e.ListItem is Stats.AchievementDefinition def) e.Value = def.Name + " — " + def.Id;
            };
            this._StartQueue.Click += this.StartQueue;
            this._StopQueue.Click += (_, _) => this.StopQueue("Arrêt demandé. Les succès déjà envoyés restent débloqués.");
            this._QueueTimer = new Timer(this.components) { Interval = 250 };
            this._QueueTimer.Tick += this.TickQueue;
            this._QueueStoredCallback = this._SteamClient.CreateAndRegisterCallback<API.Callbacks.UserStatsStored>();
            this._QueueStoredCallback.OnRun += this.OnQueueStored;
            this.FormClosing += (_, _) => { this._QueueTimer.Stop(); this._QueueRunning = false; };
        }

        private void CheckQueueChoices(bool value)
        {
            if (!this._QueueChoices.Enabled) return;
            for (int i = 0; i < this._QueueChoices.Items.Count; i++) this._QueueChoices.SetItemChecked(i, value);
        }

        private void SetSchedulerReady(bool ready)
        {
            this._QueueReady = ready;
            if (this._StartQueue == null) return;
            this._StartQueue.Enabled = ready && !this._QueueRunning && this._SavingAchievement == null;
            if (!ready || this._QueueRunning || this._SavingAchievement != null) return;
            var selected = this._QueueChoices.CheckedItems.Cast<Stats.AchievementDefinition>().Select(x => x.Id).ToList();
            this._QueueChoices.Items.Clear();
            foreach (var def in this._AchievementDefinitions)
            {
                if (string.IsNullOrEmpty(def.Id) || (def.Permission & 3) != 0) continue;
                if (this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(def.Id, out bool achieved, out _) && !achieved)
                    this._QueueChoices.Items.Add(def, selected.Contains(def.Id));
            }
        }

        private void SetQueueInput(bool running)
        {
            this._QueueChoices.Enabled = !running;
            this._MinimumMinutes.Enabled = this._MaximumMinutes.Enabled = !running;
            this._AchievementsTabPage.Enabled = this._StatisticsTabPage.Enabled = !running;
            this._MainToolStrip.Enabled = !running;
            this._StartQueue.Enabled = !running && this._QueueReady;
            this._StopQueue.Enabled = running;
        }

        private void StartQueue(object sender, EventArgs e)
        {
            if (this._QueueRunning || this._SavingAchievement != null || !this._QueueReady) return;
            if (this._MinimumMinutes.Value > this._MaximumMinutes.Value)
            {
                MessageBox.Show(this, "Le délai minimum doit être inférieur ou égal au maximum.");
                return;
            }
            var choices = this._QueueChoices.CheckedItems.Cast<Stats.AchievementDefinition>().ToList();
            if (choices.Count == 0) { MessageBox.Show(this, "Sélectionnez au moins un succès."); return; }
            this._PendingAchievements.Clear();
            foreach (var choice in choices) this._PendingAchievements.Enqueue(choice);
            this._QueueTotal = choices.Count;
            this._QueueCompleted = 0;
            this._QueueRunning = true;
            this.SetQueueInput(true);
            this.ScheduleNextAchievement();
            this._QueueTimer.Start();
        }

        private void ScheduleNextAchievement()
        {
            double min = (double)this._MinimumMinutes.Value * 60;
            double max = (double)this._MaximumMinutes.Value * 60;
            this._QueueDelay = TimeSpan.FromSeconds(min + this._QueueRandom.NextDouble() * (max - min));
            this._QueueClock.Restart();
        }

        private void TickQueue(object sender, EventArgs e)
        {
            if (this._SavingAchievement != null)
            {
                if (this._QueueClock.Elapsed > TimeSpan.FromSeconds(60))
                {
                    // Keep writes blocked until a late callback or a restart: outcome is unknown.
                    this._QueueTimer.Stop();
                    this._QueueStatus.Text = "Steam ne répond pas. Enregistrement non confirmé ; attendez ou relancez l’application.";
                    this._QueueRunning = false;
                    this._PendingAchievements.Clear();
                    this._StopQueue.Enabled = false;
                }
                return;
            }
            if (!this._QueueRunning) return;
            var remaining = this._QueueDelay - this._QueueClock.Elapsed;
            this._QueueStatus.Text = $"{this._QueueCompleted}/{this._QueueTotal} — Prochain : {this._PendingAchievements.Peek().Name} — {Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds))} s";
            if (remaining > TimeSpan.Zero) return;
            var next = this._PendingAchievements.Dequeue();
            if (!this._SteamClient.SteamUserStats.GetAchievementAndUnlockTime(next.Id, out bool achieved, out _))
            { this.StopQueue("Impossible de lire le succès : " + next.Id); return; }
            if (achieved) { this.AdvanceQueue("Déjà débloqué : " + next.Name); return; }
            if (!this._SteamClient.SteamUserStats.SetAchievement(next.Id, true))
            { this.StopQueue("Échec du déblocage : " + next.Id); return; }
            this._SavingAchievement = next;
            this._QueueClock.Restart();
            this._QueueStatus.Text = "Confirmation Steam : " + next.Name;
            if (!this._SteamClient.SteamUserStats.StoreStats())
            {
                this._SteamClient.SteamUserStats.SetAchievement(next.Id, false);
                this._SavingAchievement = null;
                this.StopQueue("Steam a refusé l’enregistrement : " + next.Id);
            }
        }

        private void OnQueueStored(API.Types.UserStatsStored result)
        {
            if (result.GameId != (ulong)this._GameId || this._SavingAchievement == null) return;
            var saved = this._SavingAchievement;
            this._SavingAchievement = null;
            if (result.Result != 1)
            {
                this.StopQueue("Échec de confirmation Steam (" + result.Result + ") : " + saved.Id);
                this.RefreshStats();
                return;
            }
            this.AdvanceQueue("Débloqué : " + saved.Name);
        }

        private void AdvanceQueue(string message)
        {
            this._QueueCompleted++;
            this._QueueLog.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + " — " + message);
            if (!this._QueueRunning || this._PendingAchievements.Count == 0)
            {
                this.StopQueue($"Terminé : {this._QueueCompleted}/{this._QueueTotal}.");
                this.RefreshStats();
            }
            else this.ScheduleNextAchievement();
        }

        private void StopQueue(string message)
        {
            this._QueueRunning = false;
            this._PendingAchievements.Clear();
            this._QueueStatus.Text = message;
            this._QueueLog.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + " — " + message);
            if (this._SavingAchievement != null)
            {
                this._StopQueue.Enabled = false;
                this._QueueStatus.Text += " Confirmation de l’envoi en cours…";
                return;
            }
            this._QueueTimer.Stop();
            this.SetQueueInput(false);
            this.SetSchedulerReady(this._QueueReady);
        }
    }
}
