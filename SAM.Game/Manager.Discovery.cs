using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Game
{
    internal partial class Manager
    {
        private ComboBox _AchievementSort;
        private Label _RarityStatus;
        private Dictionary<string, double> _Rarity = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _RarityCancel = new();
        private bool _RarityLoading, _RarityLoaded;
        private void InitializeDiscovery()
        {
            _AchievementSort = new SteamComboBox { Width = 165, AccessibleName = "Trier les succès du jeu" };
            _AchievementSort.Items.AddRange(new object[] { "Ordre du jeu", "Nom A–Z", "Les plus rares", "Les plus courants", "Non débloqués d’abord", "Débloqués récemment" });
            _AchievementSort.SelectedIndex = 0;
            _AchievementSort.SelectedIndexChanged += (_, _) => RefilterCards();
            _RarityStatus = SteamTheme.Label("Rareté mondiale · chargement à l’ouverture", 9, SteamTheme.Muted);
            _AchievementsToolStrip.Items.Add(new ToolStripSeparator());
            _AchievementsToolStrip.Items.Add(new ToolStripControlHost(_AchievementSort) { AutoSize = false, Width = 165, Height = 30 });
            _RarityStatus.Visible = false;
            _Cards.BringToFront();
            FormClosed += (_, _) => { _RarityCancel.Cancel(); _RarityCancel.Dispose(); };
        }
        internal IEnumerable<Stats.AchievementInfo> SortAchievements(IEnumerable<Stats.AchievementInfo> items)
        {
            return (_AchievementSort?.SelectedIndex ?? 0) switch
            {
                1 => items.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
                2 => items.OrderBy(a => !a.GlobalPercent.HasValue).ThenBy(a => a.GlobalPercent).ThenBy(a => a.Name),
                3 => items.OrderBy(a => !a.GlobalPercent.HasValue).ThenByDescending(a => a.GlobalPercent).ThenBy(a => a.Name),
                4 => items.OrderBy(a => a.IsAchieved).ThenBy(a => a.Name),
                5 => items.OrderBy(a => !a.UnlockTime.HasValue).ThenByDescending(a => a.UnlockTime).ThenBy(a => a.Name),
                _ => items,
            };
        }
        private async void LoadRarity()
        {
            if (_Demo || _RarityLoading || _RarityLoaded) return;
            _RarityLoading = true; _RarityStatus.Text = "Rareté mondiale · chargement…";
            try
            {
                var data = await AchievementRarity.Fetch((uint)_GameId, _RarityCancel.Token);
                if (IsDisposed || Disposing) return;
                _Rarity = data; _RarityLoaded = true;
                foreach (ListViewItem item in _AchievementListView.Items)
                    if (item.Tag is Stats.AchievementInfo info) info.GlobalPercent = data.TryGetValue(info.Id, out double value) ? value : null;
                _RarityStatus.Text = "Rareté mondiale · source Steam";
                RefilterCards();
            }
            catch (Exception e) when (e is System.Net.Http.HttpRequestException || e is OperationCanceledException || e is InvalidOperationException || e is ArgumentException)
            { if (!IsDisposed && !Disposing) _RarityStatus.Text = "Rareté indisponible · Actualiser pour réessayer"; }
            finally { _RarityLoading = false; }
        }
        private void PublishLibraryProgress()
        {
            if (_CartStore == null) return;
            try
            {
                var all = _AchievementListView.Items.Cast<ListViewItem>().Select(i => (Stats.AchievementInfo)i.Tag).ToList();
                var store = new JsonStore<LibraryProgress>(Path.Combine(Path.GetDirectoryName(_CartStore.Path), "library-progress.json"));
                store.Update(data =>
                {
                    data.Games ??= new(); data.Games.RemoveAll(g => g.GameId == (uint)_GameId);
                    data.Games.Add(new GameProgress { GameId = (uint)_GameId, Total = all.Count, Unlocked = all.Count(a => a.IsAchieved), UpdatedUtc = DateTime.UtcNow });
                });
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _GameStatusLabel.Text = "Progression locale non sauvegardée : " + e.Message; }
        }
    }
}
