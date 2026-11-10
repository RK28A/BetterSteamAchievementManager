/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using System.Xml.XPath;
using static SAM.Picker.InvariantShorthand;
using APITypes = SAM.API.Types;

namespace SAM.Picker
{
    internal partial class GamePicker : Form
    {
        private readonly API.Client _SteamClient;

        private readonly Dictionary<uint, GameInfo> _Games;
        private readonly List<GameInfo> _FilteredGames;
        private bool _RefreshingGames;

        private readonly SAM.UI.CachedArtwork _LibraryArtwork = new();

        private readonly API.Callbacks.AppDataChanged _AppDataChangedCallback;

        public GamePicker(API.Client client, bool demo = false)
        {
            this._Demo = demo;
            this._Games = new();
            this._FilteredGames = new();
            this.InitializeComponent();
            _LibraryArtwork.Loaded += (_, _) => { if (!_GameListView.IsDisposed) _GameListView.Invalidate(); };
            FormClosed += (_, _) => _LibraryArtwork.Dispose();
            API.ImageCache.Shared.Trim();

            Bitmap blank = new(this._LogoImageList.ImageSize.Width, this._LogoImageList.ImageSize.Height);
            using (var g = Graphics.FromImage(blank))
            {
                g.Clear(Color.DimGray);
            }

            this._LogoImageList.Images.Add("Blank", blank);

            SAM.UI.WindowChrome.Apply(this);
            this.InitializeOrderCart();
            this.InitializeTray();
            this._SteamClient = client;
            InitializeLibraryPreferences();

            this.FormClosing += this.OnLibraryClosing;
            if (demo) { this._CallbackTimer.Stop(); this.LoadDemoLibrary(); return; }
            this._AppDataChangedCallback = client.CreateAndRegisterCallback<API.Callbacks.AppDataChanged>();
            this._AppDataChangedCallback.OnRun += this.OnAppDataChanged;

            this.AddGames();
        }

        private void OnAppDataChanged(APITypes.AppDataChanged param)
        {
            if (param.Result == false)
            {
                return;
            }

            if (this._Games.TryGetValue(param.Id, out var game) == false)
            {
                return;
            }

            game.Name = this._SteamClient.SteamApps001.GetAppData(game.Id, "name");
            this._GameListView.Invalidate();

            this.AddGameToLogoQueue(game);
            this.DownloadNextLogo();
        }

        private void DoDownloadList(object sender, DoWorkEventArgs e)
        {


            byte[] bytes;
            using (WebClient downloader = new())
            {
                bytes = downloader.DownloadData(new Uri("https://gib.me/sam/games.xml"));
            }

            List<KeyValuePair<uint, string>> pairs = new();
            using (MemoryStream stream = new(bytes, false))
            {
                XPathDocument document = new(stream);
                var navigator = document.CreateNavigator();
                var nodes = navigator.Select("/games/game");
                while (nodes.MoveNext() == true)
                {
                    string type = nodes.Current.GetAttribute("type", "");
                    if (string.IsNullOrEmpty(type) == true)
                    {
                        type = "normal";
                    }
                    pairs.Add(new((uint)nodes.Current.ValueAsLong, type));
                }
            }

            e.Result = pairs;
        }

        private void OnDownloadList(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed || _ClosingLibrary) return;
            if (e.Error != null || e.Cancelled)
            {
                this.AddDefaultGames();
                this._PickerStatusLabel.Text = "Liste en ligne indisponible. Ajoutez un jeu avec son App ID.";
            }
            else if (e.Result is List<KeyValuePair<uint, string>> pairs)
            {
                this.CheckCatalogInBatches(pairs);
                return;
            }

            this.RefreshGames();
            this._RefreshGamesButton.Enabled = true;
            this.DownloadNextLogo();
        }

        private void RefreshGames()
        {
            var nameSearch = this._SearchGameTextBox.Text.Length > 0
                ? this._SearchGameTextBox.Text
                : null;

            var wantNormals = this._FilterGamesMenuItem.Checked == true;
            var wantDemos = this._FilterDemosMenuItem.Checked == true;
            var wantMods = this._FilterModsMenuItem.Checked == true;
            var wantJunk = this._FilterJunkMenuItem.Checked == true;

            var filtered = new List<GameInfo>();
            foreach (var info in this._Games.Values.OrderByDescending(gi => _LibraryPreferences.Favorites.Contains(gi.Id)).ThenBy(gi => gi.Name))
            {
                if (nameSearch != null &&
                    info.Name.IndexOf(nameSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (_LibraryPreferences.FavoritesOnly && !_LibraryPreferences.Favorites.Contains(info.Id)) continue;
                if (_LibraryPreferences.InstalledOnly && !info.Installed) continue;
                bool wanted = info.Type switch
                {
                    "normal" => wantNormals,
                    "demo" => wantDemos,
                    "mod" => wantMods,
                    "junk" => wantJunk,
                    _ => true,
                };
                if (wanted == false)
                {
                    continue;
                }

                filtered.Add(info);
            }

            // Native ListView notifications can re-enter RetrieveVirtualItem during selection
            // changes. Keep the old model alive until Windows has released its old indices.
            _RefreshingGames = true;
            _GameListView.BeginUpdate();
            try
            {
                _GameListView.SelectedIndices.Clear();
                _GameListView.VirtualListSize = 0;
                _FilteredGames.Clear(); _FilteredGames.AddRange(filtered);
                _GameListView.VirtualListSize = _FilteredGames.Count;
                int active = _FilteredGames.FindIndex(g => g.Id == _ActiveGameId);
                if (active >= 0) _GameListView.SelectedIndices.Add(active);
            }
            finally { _GameListView.EndUpdate(); _RefreshingGames = false; }
            this._GameListView.Invalidate();
            this._LibraryCount.Text = "JEUX  (" + this._FilteredGames.Count + ")";
            this._PickerStatusLabel.Text =
                $"{this._FilteredGames.Count} jeux affichés · {this._Games.Count} jeux dans la bibliothèque";

        }

        private void OnGameListViewRetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _FilteredGames.Count) { e.Item = new ListViewItem(""); return; }
            var info = this._FilteredGames[e.ItemIndex];
            e.Item = info.Item = new()
            {
                Text = info.Name,
                ImageIndex = info.ImageIndex,
            };
        }

        private void OnGameListViewSearchForVirtualItem(object sender, SearchForVirtualItemEventArgs e)
        {
            if (e.Direction != SearchDirectionHint.Down || e.IsTextSearch == false)
            {
                return;
            }

            var count = this._FilteredGames.Count;
            if (count < 2)
            {
                return;
            }

            var text = e.Text;
            int startIndex = e.StartIndex;

            Predicate<GameInfo> predicate;
            /*if (e.IsPrefixSearch == true)*/
            {
                predicate = gi => gi.Name != null && gi.Name.StartsWith(text, StringComparison.CurrentCultureIgnoreCase);
            }
            /*else
            {
                predicate = gi => gi.Name != null && string.Compare(gi.Name, text, StringComparison.CurrentCultureIgnoreCase) == 0;
            }*/

            int index;
            if (e.StartIndex >= count)
            {
                // starting from the last item in the list
                index = this._FilteredGames.FindIndex(0, count, predicate);
            }
            else if (startIndex <= 0)
            {
                // starting from the first item in the list
                index = this._FilteredGames.FindIndex(0, count, predicate);
            }
            else
            {
                index = this._FilteredGames.FindIndex(startIndex, count - startIndex, predicate);
                if (index < 0)
                {
                    index = this._FilteredGames.FindIndex(0, startIndex - 1, predicate);
                }
            }

            e.Index = index < 0 ? -1 : index;
        }

        private void DownloadNextLogo() => _GameListView.Invalidate();
        private void AddGameToLogoQueue(GameInfo info) { info.ImageUrl = GetGameImageUrl(info.Id); }

        private string GetGameImageUrl(uint id)
        {
            string candidate;

            var currentLanguage = this._SteamClient.SteamApps008.GetCurrentGameLanguage();

            candidate = this._SteamClient.SteamApps001.GetAppData(id, _($"small_capsule/{currentLanguage}"));
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
            }

            if (currentLanguage != "english")
            {
                candidate = this._SteamClient.SteamApps001.GetAppData(id, "small_capsule/english");
                if (string.IsNullOrEmpty(candidate) == false)
                {
                    return _($"https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/{id}/{candidate}");
                }
            }

            candidate = this._SteamClient.SteamApps001.GetAppData(id, "logo");
            if (string.IsNullOrEmpty(candidate) == false)
            {
                return _($"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{id}/{candidate}.jpg");
            }

            return null;
        }

        private bool OwnsGame(uint id)
        {
            return this._SteamClient.SteamApps008.IsSubscribedApp(id);
        }

        private void AddGame(uint id, string type)
        {
            if (this._Games.ContainsKey(id) == true)
            {
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                return;
            }

            GameInfo info = new(id, type);
            info.Name = this._SteamClient.SteamApps001.GetAppData(info.Id, "name");
            info.Installed = this._SteamClient.SteamApps008.IsAppInstalled(info.Id);
            this._Games.Add(id, info);
        }

        private void AddGames()
        {
            if (this._ListWorker.IsBusy || this._CatalogLoading || this._Demo) return;
            this._RefreshGamesButton.Enabled = false;
            this._ListWorker.RunWorkerAsync();
        }

        private void AddDefaultGames()
        {
            this.AddGame(480, "normal"); // Spacewar
        }

        private void OnTimer(object sender, EventArgs e)
        {
            this._CallbackTimer.Enabled = false;
            this._SteamClient.RunCallbacks(false);
            this._CallbackTimer.Enabled = true;
        }

        private void OnActivateGame(object sender, EventArgs e)
        {
            var focusedItem = (sender as MyListView)?.FocusedItem;
            var index = focusedItem != null ? focusedItem.Index : -1;
            if (index < 0 || index >= this._FilteredGames.Count)
            {
                return;
            }

            var info = this._FilteredGames[index];
            if (info == null)
            {
                return;
            }

            this.OpenGame(index);
        }

        private void OnRefresh(object sender, EventArgs e)
        {
            this._AddGameTextBox.Text = "";
            foreach (var game in _Games.Values) game.Installed = _SteamClient.SteamApps008.IsAppInstalled(game.Id);
            this.RefreshGames();
            this.AddGames();
        }

        private void OnAddGame(object sender, EventArgs e)
        {
            uint id;

            if (uint.TryParse(this._AddGameTextBox.Text, out id) == false)
            {
                MessageBox.Show(
                    this,
                    "Please enter a valid game ID.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (this.OwnsGame(id) == false)
            {
                MessageBox.Show(this, "You don't own that game.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this._AddGameTextBox.Text = "";
            this.AddGame(id, "normal");
            this._FilterGamesMenuItem.Checked = true;
            this.RefreshGames();
            this.DownloadNextLogo();
        }

        private void OnFilterUpdate(object sender, EventArgs e)
        {
            this.RefreshGames();

            // Compatibility with _GameListView SearchForVirtualItemEventHandler (otherwise _SearchGameTextBox loose focus on KeyUp)
            if (sender == this._SearchGameTextBox) this._SearchGameTextBox.Focus();
        }

        private void OnGameListViewDrawItem(object sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
            if (_RefreshingGames || e.ItemIndex < 0 || e.ItemIndex >= _FilteredGames.Count) return;
            bool selected = e.Item.Selected;
            using (var brush = new SolidBrush(selected ? SAM.UI.SteamTheme.Surface : SAM.UI.SteamTheme.Sidebar))
                e.Graphics.FillRectangle(brush, e.Bounds);
            var game = this._FilteredGames[e.ItemIndex];
            if (!_Demo && game.ImageUrl == null) game.ImageUrl = GetGameImageUrl(game.Id) ?? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.Id}/header.jpg";
            var artwork = _Demo ? null : _LibraryArtwork.Get(game.ImageUrl);
            var cover = new Rectangle(e.Bounds.Left + 14, e.Bounds.Top + 10, 44, 32);
            if (artwork != null) e.Graphics.DrawImage(artwork, cover);
            else
            {
                using var tile = new SolidBrush(SAM.UI.SteamTheme.Surface); e.Graphics.FillRectangle(tile, cover);
                TextRenderer.DrawText(e.Graphics, game.Name.Substring(0, 1).ToUpperInvariant(), Font, cover, SAM.UI.SteamTheme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            var progress = _LibraryProgress.Games.FirstOrDefault(g => g.GameId == game.Id);
            var textRect = new Rectangle(e.Bounds.Left + 70, e.Bounds.Top + 8, Math.Max(0, e.Bounds.Width - 82), 22);
            string name = (_LibraryPreferences.Favorites.Contains(game.Id) ? "★ " : "") + game.Name;
            TextRenderer.DrawText(e.Graphics, name, this.Font, textRect, selected ? SAM.UI.SteamTheme.Accent : SAM.UI.SteamTheme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string detail = (game.Installed ? "Installé" : "Non installé") + (progress == null ? " · succès —" : $" · {progress.Unlocked}/{progress.Total}");
            TextRenderer.DrawText(e.Graphics, detail, _LibraryDetailFont, new Rectangle(textRect.X, e.Bounds.Top + 32, textRect.Width, 18), SAM.UI.SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (progress != null && progress.Total > 0)
            {
                using var bg = new SolidBrush(SAM.UI.SteamTheme.Surface); using var fill = new SolidBrush(SAM.UI.SteamTheme.Accent);
                var bar = new Rectangle(textRect.X, e.Bounds.Bottom - 4, textRect.Width, 2);
                e.Graphics.FillRectangle(bg, bar); bar.Width = bar.Width * progress.Unlocked / progress.Total; e.Graphics.FillRectangle(fill, bar);
            }

        }
    }
}
