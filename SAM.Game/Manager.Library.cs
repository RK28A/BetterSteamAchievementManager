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
        private SteamProgress _GameProgress;
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
            // The backing ListView is no longer interactive; enforce permissions at staging and write boundaries.
            _AchievementListView.ItemCheck -= OnCheckAchievement;
            _ = _AchievementListView.Handle;
            _Cards = new AchievementCards { Icons = _AchievementImageList };
            _Cards.IconProvider = info => _Demo || string.IsNullOrEmpty(info.IconNormal ?? info.IconLocked) ? null : _AchievementArtwork.Get($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_GameId}/{info.IconNormal ?? info.IconLocked}");
            _AchievementArtwork.Loaded += (_, _) => { if (!_Cards.IsDisposed) _Cards.Invalidate(); };
            _Cards.SelectionEdited += (_, _) => UpdateSelectionSummary();
            _AchievementsTabPage.Controls.Add(_Cards);
            _Cards.BringToFront();
            _EmptyAchievements = SteamTheme.Label("Chargement des succès…", 12, SteamTheme.Muted);
            _EmptyAchievements.Dock = DockStyle.Bottom;
            _AchievementsTabPage.Controls.Add(_EmptyAchievements);
            _SelectionActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(10, 4, 10, 4), WrapContents = true };
            void Action(string text, EventHandler click)
            {
                var button = new SAM.UI.ModernButton { Text = text, AutoSize = true, Margin = new Padding(3) };
                button.Click += click;
                _SelectionActions.Controls.Add(button);
            }
            var selection = new ContextMenuStrip(components);
            selection.Items.Add("Tout sélectionner", null, (_, _) => _Cards.SelectVisible("all"));
            selection.Items.Add("Tout désélectionner", null, (_, _) => { _Cards.SelectedIds.Clear(); _Cards.NotifySelection(); });
            selection.Items.Add("Inverser la sélection", null, (_, _) => _Cards.SelectVisible("invert"));
            selection.Items.Add("Sélectionner les non débloqués", null, (_, _) => _Cards.SelectVisible("locked"));
            SteamTheme.Apply(selection);
            Action("Sélection  ▾", (sender, _) => { var button = (Control)sender; selection.Show(button, new Point(0, -selection.PreferredSize.Height)); });
            Action("Débloquer", (_, _) => StageSelected(true));
            Action("Verrouiller", (_, _) => StageSelected(false));
            Action("Annuler", (_, _) =>
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
            _SelectionText.Dock = DockStyle.Bottom; _SelectionText.Padding = new Padding(14, 2, 0, 2);
            _AchievementsTabPage.Controls.Add(_SelectionActions);
            _AchievementsTabPage.Controls.Add(_SelectionText);
            _AchievementsToolStrip.Items.Clear();
            _AchievementsToolStrip.Items.AddRange(new ToolStripItem[] { new ToolStripLabel("Rechercher"), _MatchingStringTextBox, new ToolStripSeparator(), new ToolStripLabel("Afficher"), _DisplayLockedOnlyButton, _DisplayUnlockedOnlyButton });
            InitializeDiscovery();
            _MatchingStringTextBox.Width = 150;
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
            var hero = new HeroPanel { Dock = DockStyle.Top, Height = 86, Padding = new Padding(22, 12, 22, 10) };
            _GameHeading = SteamTheme.Label(name, 22);
            _GameHeading.Dock = DockStyle.Top;
            _ProgressText = SteamTheme.Label("Chargement de votre progression…", 10, SteamTheme.Muted);
            _ProgressText.Dock = DockStyle.Top;
            _GameProgress = new SteamProgress { Dock = DockStyle.Bottom, Height = 3, Maximum = 1000 };
            var overline = SteamTheme.Label("PROGRESSION DES SUCCÈS", 9, SteamTheme.Accent);
            overline.Dock = DockStyle.Top;
            hero.Controls.Add(_GameProgress);
            hero.Controls.Add(_ProgressText);
            hero.Controls.Add(_GameHeading);
            overline.Dispose();
            var navigation = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 0, 0, 0), WrapContents = false };
            var navigationButtons = new System.Collections.Generic.List<Button>();
            for (int index = 0; index < _MainTabControl.TabPages.Count; index++)
            {
                int pageIndex = index;
                var button = new SAM.UI.ModernButton { Text = _MainTabControl.TabPages[index].Text, AutoSize = true, Margin = new Padding(0, 0, 6, 0), Height = 36 };
                button.Click += (_, _) => _MainTabControl.SelectedIndex = pageIndex;
                navigation.Controls.Add(button);
                navigationButtons.Add(button);
            }
            void UpdateNavigation()
            {
                for (int i = 0; i < navigationButtons.Count; i++)
                {
                    navigationButtons[i].BackColor = i == _MainTabControl.SelectedIndex ? SteamTheme.Surface : SteamTheme.Background;
                    navigationButtons[i].ForeColor = i == _MainTabControl.SelectedIndex ? SteamTheme.Accent : SteamTheme.Muted;
                }
            }
            _MainTabControl.SelectedIndexChanged += (_, _) => UpdateNavigation();
            Controls.Add(navigation);
            Controls.Add(hero);
            _MainTabControl.BringToFront();
            SteamTheme.Apply(this);
            foreach (Control child in hero.Controls) if (child is Label) child.BackColor = Color.Transparent;
            UpdateNavigation();
            _CallbackTimer.Enabled = !_Demo;
            FormClosed += (_, _) => { _AchievementArtwork.Dispose(); _AchievementListView.Dispose(); };
            FormClosing += (_, e) =>
            {
                // Hosted views are closed by their owning library after its single confirmation.
                if (_Embedded || _Demo) return;
                if ((HasPendingChanges || _ManualSaving) &&
                    MessageBox.Show(this, "Fermer ce jeu et abandonner les modifications ou la file en cours ?", "Fermer", MessageBoxButtons.YesNo) != DialogResult.Yes) e.Cancel = true;
            };
            ResumeLayout(true);
        }

        internal void StageSelected(bool achieved)
        {
            if (!_StatsReady || _ManualSaving || _CartSaving) return;
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
            foreach (var info in SortAchievements(_AchievementListView.Items.Cast<ListViewItem>().Select(i => (Stats.AchievementInfo)i.Tag)))
            {
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
            _SelectionText.Text = $"{_Cards.SelectedIds.Count} sélectionné(s)  ·  {pending} modification(s) en attente";
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
                var info = new Stats.AchievementInfo { Id = "DEMO_" + i, Name = names[i], Description = "Explorez, découvrez et accomplissez un nouvel objectif.", IsAchieved = i < 2, Permission = i == 6 ? 3 : 0, GlobalPercent = i == 6 ? null : 72.5 / (i + 1), UnlockTime = i < 2 ? DateTime.Now.AddDays(-3 + i) : null };
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
            EnableInput();
            _StoreButton.Enabled = _ResetButton.Enabled = false;
            _RarityStatus.Text = "Rareté de démonstration";
            RefilterCards(); PublishLibraryProgress();
        }
    }
}
