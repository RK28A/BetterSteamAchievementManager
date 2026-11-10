using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private LibraryPreferences _LibraryPreferences = new();
        private LibraryProgress _LibraryProgress = new();
        private JsonStore<LibraryPreferences> _PreferencesStore;
        private JsonStore<LibraryProgress> _ProgressStore;
        private ToolStrip _LibraryFilters;
        private readonly Font _LibraryDetailFont = new("Segoe UI", 8);
        private void InitializeLibraryPreferences()
        {
            string folder = Path.GetDirectoryName(_CartPath);
            _PreferencesStore = new JsonStore<LibraryPreferences>(Path.Combine(folder, "library-preferences.json"));
            _ProgressStore = new JsonStore<LibraryProgress>(Path.Combine(folder, "library-progress.json"));
            try { _LibraryPreferences = _PreferencesStore.Read(); _LibraryPreferences.Favorites ??= new(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _PickerStatusLabel.Text = e.Message; }
            var favorites = new ToolStripButton("★ Favoris") { CheckOnClick = true, Checked = _LibraryPreferences.FavoritesOnly };
            var installed = new ToolStripButton("Installés") { CheckOnClick = true, Checked = _LibraryPreferences.InstalledOnly };
            var toggle = new ToolStripButton("☆ / ★") { ToolTipText = "Ajouter ou retirer le jeu sélectionné des favoris (Ctrl+D)" };
            favorites.CheckedChanged += (_, _) => { _LibraryPreferences.FavoritesOnly = favorites.Checked; SaveLibraryPreferences(); };
            installed.CheckedChanged += (_, _) => { _LibraryPreferences.InstalledOnly = installed.Checked; SaveLibraryPreferences(); };
            toggle.Click += (_, _) => ToggleFavorite(_ActiveGameId);
            _LibraryFilters.Items.AddRange(new ToolStripItem[] { favorites, installed, toggle });
            SteamTheme.Apply(_LibraryFilters);
            var menu = new ContextMenuStrip(components);
            menu.Items.Add("Ajouter / retirer des favoris", null, (_, _) => ToggleFavorite(_ActiveGameId));
            SteamTheme.Apply(menu); _GameListView.ContextMenuStrip = menu;
            _GameListView.KeyDown += (_, e) => { if (e.Control && e.KeyCode == Keys.D) { ToggleFavorite(_ActiveGameId); e.SuppressKeyPress = true; } };
            var timer = new Timer(components) { Interval = 2000, Enabled = true };
            timer.Tick += (_, _) =>
            {
                try { _LibraryProgress = _ProgressStore.Read(); _LibraryProgress.Games ??= new(); _GameListView.Invalidate(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _PickerStatusLabel.Text = "Progression en cache indisponible : " + e.Message; }
            };
            FormClosed += (_, _) => _LibraryDetailFont.Dispose();
        }
        internal void ToggleFavorite(uint gameId)
        {
            if (gameId == 0 || !_Games.ContainsKey(gameId)) return;
            if (!_LibraryPreferences.Favorites.Remove(gameId)) _LibraryPreferences.Favorites.Add(gameId);
            SaveLibraryPreferences();
        }
        private void SaveLibraryPreferences()
        {
            try
            {
                _PreferencesStore.Update(data => { data.Favorites = _LibraryPreferences.Favorites; data.InstalledOnly = _LibraryPreferences.InstalledOnly; data.FavoritesOnly = _LibraryPreferences.FavoritesOnly; });
                RefreshGames();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { _PickerStatusLabel.Text = e.Message; }
        }
    }
}
