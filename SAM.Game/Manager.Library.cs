// Altered source: achievement library view and confirmed manual writes.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SAM.UI;

namespace SAM.Game
{
    internal partial class Manager
    {
        internal AchievementCards _Cards;
        private Label _GameHeading, _ProgressText, _SelectionText, _EmptyAchievements;
        private ProgressBar _GameProgress;
        private FlowLayoutPanel _SelectionActions;
        private bool _StatsReady, _ManualSaving, _AwaitingStats;
        private int _ManualAchievementCount, _ManualStatCount;
        private string _LastStoreMessage;
        private readonly Stopwatch _ManualClock = new();
        private readonly bool _Demo;
        internal bool HasPendingChanges => _AchievementListView.Items.Cast<ListViewItem>().Any(i => i.Tag is Stats.AchievementInfo info && i.Checked != info.IsAchieved) || _Statistics.Any(s => s.IsModified);

        private void InitializeLibraryView(string name)
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            Font = new Font("Segoe UI", 10);
            Size = new Size(1060, 740);
            MinimumSize = new Size(740, 540);
            _MainTabControl.Dock = DockStyle.Fill;
            _AchievementsTabPage.Controls.Remove(_AchievementListView);
            _Cards = new AchievementCards { Icons = _AchievementImageList };
            _Cards.SelectionEdited += (_, _) => UpdateSelectionSummary();
            _AchievementsTabPage.Controls.Add(_Cards);
            _Cards.BringToFront();
            _EmptyAchievements = SteamTheme.Label("Chargement des succès…", 12, SteamTheme.Muted);
            _EmptyAchievements.Dock = DockStyle.Bottom;
            _AchievementsTabPage.Controls.Add(_EmptyAchievements);
            _SelectionActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8), WrapContents = true };
            void Action(string text, EventHandler click)
            {
                var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
                button.Click += click;
                _SelectionActions.Controls.Add(button);
            }
            Action("Tout sélectionner", (_, _) => _Cards.SelectVisible("all"));
            Action("Tout désélectionner", (_, _) => { _Cards.SelectedIds.Clear(); _Cards.NotifySelection(); });
            Action("Inverser", (_, _) => _Cards.SelectVisible("invert"));
            Action("Non débloqués", (_, _) => _Cards.SelectVisible("locked"));
            Action("Débloquer la sélection", (_, _) => StageSelected(true));
            Action("Verrouiller la sélection", (_, _) => StageSelected(false));
            Action("Déblocage progressif", (_, _) =>
            {
                for (int i = 0; i < _QueueChoices.Items.Count; i++)
                    _QueueChoices.SetItemChecked(i, _Cards.SelectedIds.Contains(((Stats.AchievementDefinition)_QueueChoices.Items[i]).Id));
                _MainTabControl.SelectedIndex = 2;
            });
            Action("Annuler les modifications", (_, _) =>
            {
                foreach (ListViewItem item in _AchievementListView.Items) if (item.Tag is Stats.AchievementInfo info) item.Checked = info.IsAchieved;
                foreach (var stat in _Statistics)
                {
                    if (stat is Stats.IntStatInfo integer) integer.IntValue = integer.OriginalValue;
                    if (stat is Stats.FloatStatInfo floating) floating.FloatValue = floating.OriginalValue;
                }
                _Statistics.ResetBindings();
                _Cards.Invalidate(); UpdateSelectionSummary();
            });
            _SelectionText = SteamTheme.Label("Aucun succès sélectionné", 9, SteamTheme.Muted);
            _SelectionText.Dock = DockStyle.Bottom;
            _AchievementsTabPage.Controls.Add(_SelectionActions);
            _AchievementsTabPage.Controls.Add(_SelectionText);
            _AchievementsToolStrip.Items.Clear();
            _AchievementsToolStrip.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("Rechercher"), _MatchingStringTextBox, new ToolStripSeparator(), new ToolStripLabel("Afficher"), _DisplayLockedOnlyButton, _DisplayUnlockedOnlyButton });
            _MatchingStringTextBox.Width = 220;
            _MatchingStringTextBox.ToolTipText = "Nom, description ou identifiant du succès";
            _MatchingStringTextBox.TextChanged += (_, _) => RefilterCards();
            _DisplayLockedOnlyButton.Text = "Non débloqués";
            _DisplayUnlockedOnlyButton.Text = "Débloqués";
            _StoreButton.Text = "Enregistrer les modifications";
            _ReloadButton.Text = "Actualiser";
            _ResetButton.Text = "Réinitialiser…";
            _MainToolStrip.Dock = DockStyle.Bottom;
            _AchievementsTabPage.Text = "Succès";
            _StatisticsTabPage.Text = "Statistiques";
            _StatisticsDataGridView.Dock = DockStyle.Fill;
            _EnableStatsEditingCheckBox.Dock = DockStyle.Bottom;
            _EnableStatsEditingCheckBox.Text = "Activer l’édition des statistiques (modifications avancées)";
            _StatisticsDataGridView.BringToFront();
            var hero = new HeroPanel { Dock = DockStyle.Top, Height = 155, Padding = new Padding(24, 12, 24, 12) };
            _GameHeading = SteamTheme.Label(name, 23);
            _GameHeading.Dock = DockStyle.Top;
            _ProgressText = SteamTheme.Label("Récupération de votre progression", 11, SteamTheme.Muted);
            _ProgressText.Dock = DockStyle.Top;
            _GameProgress = new ProgressBar { Dock = DockStyle.Bottom, Height = 5, Maximum = 1000, Style = ProgressBarStyle.Continuous };
            var overline = SteamTheme.Label("PROGRESSION DES SUCCÈS", 9, SteamTheme.Accent);
            overline.Dock = DockStyle.Top;
            hero.Controls.Add(_GameProgress);
            hero.Controls.Add(_ProgressText);
            hero.Controls.Add(_GameHeading);
            hero.Controls.Add(overline);
            Controls.Add(hero);
            _MainTabControl.BringToFront();
            SteamTheme.Apply(this);
            foreach (Control child in hero.Controls) if (child is Label) child.BackColor = Color.Transparent;
            _QueueStatus.MaximumSize = new Size(850, 0);
            _CallbackTimer.Enabled = !_Demo;
            FormClosed += (_, _) => { _IconDownloader.CancelAsync(); _IconDownloader.Dispose(); _AchievementListView.Dispose(); };
            FormClosing += (_, e) =>
            {
                // Hosted views are closed by their owning library after its single confirmation.
                if (FormBorderStyle == FormBorderStyle.None || _Demo) return;
                if ((HasPendingChanges || _QueueRunning || _SavingAchievement != null || _ManualSaving) &&
                    MessageBox.Show(this, "Fermer ce jeu et abandonner les modifications ou la file en cours ?", "Fermer", MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true;
            };
            ResumeLayout(true);
        }

        internal void StageSelected(bool achieved)
        {
            if (!_StatsReady || _ManualSaving || _QueueRunning || _SavingAchievement != null) return;
            foreach (ListViewItem item in _AchievementListView.Items)
                if (item.Tag is Stats.AchievementInfo info && (info.Permission & 3) == 0 && _Cards.SelectedIds.Contains(info.Id)) item.Checked = achieved;
            _Cards.Invalidate();
            UpdateSelectionSummary();
        }

        internal void RefilterCards()
        {
            if (_Cards == null) return;
            var search = _MatchingStringTextBox.Text.Trim();
            _Cards.BeginUpdate();
            _Cards.Items.Clear();
            foreach (ListViewItem item in _AchievementListView.Items)
            {
                if (item.Tag is not Stats.AchievementInfo info) continue;
                if (_DisplayLockedOnlyButton.Checked && info.IsAchieved) continue;
                if (_DisplayUnlockedOnlyButton.Checked && !info.IsAchieved) continue;
                if (search.Length > 0 && (info.Name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (info.Description ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && info.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _Cards.Items.Add(info);
            }
            _Cards.EndUpdate();
            _EmptyAchievements.Visible = _Cards.Items.Count == 0;
            _EmptyAchievements.Text = _AchievementListView.Items.Count == 0 ? "Aucun succès disponible pour ce jeu." : "Aucun succès ne correspond aux filtres.";
            UpdateSelectionSummary();
        }

        private void UpdateSelectionSummary()
        {
            if (_Cards == null) return;
            int pending = _AchievementListView.Items.Cast<ListViewItem>().Count(i => i.Tag is Stats.AchievementInfo info && i.Checked != info.IsAchieved);
            _SelectionText.Text = $"{_Cards.SelectedIds.Count} sélectionné(s) · {pending} modification(s) en attente · les actions de sélection portent sur les cartes affichées";
            var infos = _AchievementListView.Items.Cast<ListViewItem>().Select(i => (Stats.AchievementInfo)i.Tag).ToList();
            int unlocked = infos.Count(i => i.IsAchieved);
            _ProgressText.Text = $"{unlocked} / {infos.Count} succès débloqués  ·  {(infos.Count == 0 ? 0 : 100 * unlocked / infos.Count)} % terminés";
            _GameProgress.Value = infos.Count == 0 ? 0 : 1000 * unlocked / infos.Count;
        }

        private void SetViewInput(bool ready)
        {
            _StatsReady = ready;
            if (_Cards == null) return;
            _Cards.Enabled = _SelectionActions.Enabled = _AchievementsToolStrip.Enabled = ready;
            _StatisticsTabPage.Enabled = ready;
            _ResetButton.Enabled = ready && !_Demo;
        }

        private void InitializeDemo()
        {
            var names = new[] { "Meilleur ami de l’homme", "Le début de l’aventure", "Hors des sentiers battus", "Collectionneur", "Un travail d’équipe", "Jusqu’au bout", "Succès protégé" };
            _IsUpdatingAchievementList = true;
            for (int i = 0; i < names.Length; i++)
            {
                var info = new Stats.AchievementInfo { Id = "DEMO_" + i, Name = names[i], Description = "Explorez, découvrez et accomplissez un nouvel objectif.", IsAchieved = i < 2, Permission = i == 6 ? 3 : 0 };
                info.Item = new ListViewItem(info.Name) { Tag = info, Checked = info.IsAchieved };
                _AchievementListView.Items.Add(info.Item);
                using var icon = new Bitmap(64, 64);
                using (var g = Graphics.FromImage(icon)) { g.Clear(Color.FromArgb(40 + i * 15, 95, 135)); using var f = new Font("Segoe UI", 26); g.DrawString("★", f, Brushes.LightSkyBlue, 8, 8); }
                _AchievementImageList.Images.Add(icon);
                info.ImageIndex = _AchievementImageList.Images.Count - 1;
                _AchievementDefinitions.Add(new Stats.AchievementDefinition { Id = info.Id, Name = info.Name, Permission = info.Permission });
            }
            _IsUpdatingAchievementList = false;
            _GameStatusLabel.Text = "DÉMONSTRATION · aucune connexion Steam · aucune écriture réelle";
            _QueueStatus.Text = "La file reste désactivée dans la démonstration.";
            EnableInput();
            _StoreButton.Enabled = _StartQueue.Enabled = _ResetButton.Enabled = false;
            RefilterCards();
        }
    }
}
