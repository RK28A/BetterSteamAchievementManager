// Altered source: persistent per-game views hosted in the library shell.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SAM.UI;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private readonly Dictionary<uint, (Panel Panel, Process Process)> _Sessions = new();
        private uint _ActiveGameId;
        private bool _ClosingLibrary;
        private readonly bool _Demo;
        private bool _CatalogLoading;

        private void CheckCatalogInBatches(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<uint, string>> pairs)
        {
            int index = 0;
            _CatalogLoading = true;
            var timer = new Timer(components) { Interval = 15 };
            timer.Tick += (_, _) =>
            {
                if (_ClosingLibrary || IsDisposed) { timer.Dispose(); return; }
                var budget = Stopwatch.StartNew();
                do
                {
                    var entry = pairs[index++];
                    AddGame(entry.Key, entry.Value);
                } while (index < pairs.Count && budget.ElapsedMilliseconds < 20);
                _PickerStatusLabel.Text = $"Vérification de la bibliothèque · {index}/{pairs.Count}";
                if (index < pairs.Count) return;
                timer.Dispose();
                _CatalogLoading = false;
                RefreshGames();
                _RefreshGamesButton.Enabled = true;
                DownloadNextLogo();
            };
            if (pairs.Count == 0) { timer.Dispose(); _CatalogLoading = false; _RefreshGamesButton.Enabled = true; RefreshGames(); return; }
            timer.Start();
        }

        private void OpenGame(int index)
        {
            if (_ClosingLibrary || _RefreshingGames || index < 0 || index >= _FilteredGames.Count) return;
            var game = _FilteredGames[index];
            _ActiveGameId = game.Id;
            EnsureSession(game, true);
        }

        private void EnsureSession(GameInfo game, bool show)
        {
            if (_Sessions.TryGetValue(game.Id, out var existing) && !existing.Process.HasExited)
            {
                if (show) existing.Panel.BringToFront();
                return;
            }
            if (_Sessions.ContainsKey(game.Id))
            {
                _Sessions[game.Id].Panel.Dispose();
                _Sessions[game.Id].Process.Dispose();
                _Sessions.Remove(game.Id);
            }
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = SteamTheme.Background };
            var loading = SteamTheme.Label("Chargement de " + game.Name + "…", 16, SteamTheme.Muted);
            loading.Location = new Point(36, 48);
            panel.Controls.Add(loading);
            _GameHost.Controls.Add(panel);
            if (show) panel.BringToFront(); else panel.SendToBack();
            try
            {
                var start = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"),
                    game.Id.ToString(CultureInfo.InvariantCulture) + " --host " + panel.Handle.ToInt64().ToString(CultureInfo.InvariantCulture) +
                    " --owner " + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                    " --cart \"" + _CartPath + "\" --command \"" + GameCommandPath(game.Id) + "\"" + (_Demo ? " --demo" : ""))
                { UseShellExecute = false, WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory };
                var process = Process.Start(start);
                process.EnableRaisingEvents = true;
                process.Exited += (_, _) =>
                {
                    if (IsDisposed || _ClosingLibrary || !IsHandleCreated) return;
                    try { BeginInvoke((Action)(() => { if (!panel.IsDisposed) loading.Text = "La vue du jeu est fermée. Recliquez sur le jeu pour la rouvrir."; })); }
                    catch (InvalidOperationException) { }
                };
                _Sessions[game.Id] = (panel, process);
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception || e is InvalidOperationException)
            {
                panel.Dispose();
                MessageBox.Show(this, "Impossible d’ouvrir le jeu. Extrayez tous les fichiers ensemble.\n" + e.Message, "Ouverture du jeu");
            }
        }

        private void OnLibraryClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_ExitRequested) { e.Cancel = true; HideToTray(); return; }
            if (_Runner.Running) { _Runner.Pause(_OrderClock.Elapsed); SaveOrderCheckpoint("Fermeture", "Commande séquentielle sauvegardée."); }
            _ClosingLibrary = true;
            SuspendParallel();
            SuspendTimeWorkers();
            _Runner.Stop();
            ReleaseOrderLease();
            foreach (var session in _Sessions.Values)
            {
                try { if (!session.Process.HasExited) session.Process.Kill(); }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                session.Process.Dispose();
            }
        }

        private void LoadDemoLibrary()
        {
            _AddGameButton.Enabled = _AddGameTextBox.Enabled = _RefreshGamesButton.Enabled = false;
            foreach (var game in new[] { new GameInfo(620, "normal") { Name = "Portal 2", Installed = true }, new GameInfo(400, "normal") { Name = "Portal", Installed = true }, new GameInfo(730, "normal") { Name = "Counter-Strike 2" } })
                _Games.Add(game.Id, game);
            RefreshGames();
            _PickerStatusLabel.Text = "MODE DÉMONSTRATION — aucune connexion Steam, aucune modification de compte";
        }
    }
}
