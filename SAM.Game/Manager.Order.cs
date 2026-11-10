// Cart selection and one-command-at-a-time integration with the library order runner.
using System;
using System.Linq;
using System.Windows.Forms;
using SAM.API.Orders;

namespace SAM.Game
{
    internal partial class Manager
    {
        private JsonStore<OrderCart> _CartStore;
        private JsonStore<OrderCommand> _OrderCommands;
        private string _CartCommandId;
        private bool _CartSaving, _CartTimeoutReported;
        private string _CartAchievementId;
        private bool _Embedded;
        private readonly System.Diagnostics.Stopwatch _CartConfirmationClock = new();

        private void InitializeOrder(string cartPath, string commandPath, bool embedded)
        {
            _Embedded = embedded;
            string fallback = _Demo ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BSAM-Demo-" + System.Diagnostics.Process.GetCurrentProcess().Id, "cart.json") : OrderPaths.Cart;
            _CartStore = new JsonStore<OrderCart>(cartPath ?? fallback);
            if (commandPath != null) _OrderCommands = new JsonStore<OrderCommand>(commandPath);
            var add = new Button { Text = "+ Achievement Order", AutoSize = true, Margin = new Padding(3), BackColor = SAM.UI.SteamTheme.Blue, ForeColor = SAM.UI.SteamTheme.Text, FlatStyle = FlatStyle.Flat, Padding = new Padding(10, 5, 10, 5) };
            add.FlatAppearance.BorderSize = 0;
            add.Click += (_, _) => AddSelectionToCart();
            _SelectionActions.Controls.Add(add);
            if (!embedded) SAM.UI.WindowChrome.Apply(this);
        }
        internal void AddSelectionToCart()
        {
            try
            {
                var group = new OrderGroup { GameId = (uint)_GameId, Name = _GameHeading.Text,
                    ImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{_GameId}/header.jpg" };
                foreach (ListViewItem item in _AchievementListView.Items)
                    if (item.Tag is Stats.AchievementInfo info && _Cards.SelectedIds.Contains(info.Id) && !info.IsAchieved && (info.Permission & 3) == 0)
                        group.Achievements.Add(new OrderAchievement { Id = info.Id, Name = info.Name,
                            ImageUrl = string.IsNullOrEmpty(info.IconNormal) ? null : $"https://cdn.steamstatic.com/steamcommunity/public/images/apps/{_GameId}/{info.IconNormal}" });
                int added = OrderPaths.Add(_CartStore, group);
                _GameStatusLabel.Text = $"{added} succès ajouté(s) à Achievement Order · doublons et succès déjà débloqués exclus";
            }
            catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
            { MessageBox.Show(this, e.Message, "Achievement Order"); }
        }
        private void PollOrderCommand()
        {
            if (_OrderCommands == null || _CartSaving || _CartCommandId != null) return;
            OrderCommand command;
            try { command = _OrderCommands.Read(); }
            catch (System.IO.IOException) { return; }
            if (command.GameId != (uint)_GameId || command.State != "requested" || string.IsNullOrEmpty(command.Id)) return;
            try
            {
                var cart = _CartStore.Read();
                if (!cart.IsRunning || cart.ActiveRun != command.RunId) { FinishOrderCommand(command.Id, "failed", "La commande a été arrêtée."); return; }
                if (!cart.Games.Any(g => g.GameId == command.GameId && g.Achievements.Any(a => a.Id == command.AchievementId)))
                { FinishOrderCommand(command.Id, "failed", "Le succès ne fait plus partie du panier."); return; }
                if (_AwaitingStats) return;
                if (!_StatsReady || _QueueRunning || _ManualSaving || _SavingAchievement != null || HasPendingChanges)
                { FinishOrderCommand(command.Id, "failed", "Ce jeu est occupé ou possède des changements non enregistrés."); return; }
                var definition = _AchievementDefinitions.FirstOrDefault(a => a.Id == command.AchievementId);
                if (definition == null || (definition.Permission & 3) != 0)
                { FinishOrderCommand(command.Id, "failed", "Succès indisponible ou protégé."); return; }
                if (_StatsGateway == null) { FinishOrderCommand(command.Id, "failed", "Aucune écriture Steam en mode démonstration."); return; }
                if (!_StatsGateway.ReadAchievement(command.AchievementId, out bool achieved))
                { FinishOrderCommand(command.Id, "failed", "Lecture Steam impossible."); return; }
                if (achieved) { FinishOrderCommand(command.Id, "done", "Déjà débloqué"); return; }
                _CartCommandId = command.Id;
                _CartAchievementId = command.AchievementId;
                _CartTimeoutReported = false;
                _CartSaving = true;
                _CartConfirmationClock.Restart();
                DisableInput();
                bool staged = _StatsGateway.SetAchievement(command.AchievementId, true);
                if (!staged || !_StatsGateway.Store())
                {
                    if (staged) _StatsGateway.SetAchievement(command.AchievementId, false);
                    _CartSaving = false; _CartCommandId = null;
                    FinishOrderCommand(command.Id, "failed", "Steam a refusé l’enregistrement.");
                    RefreshStats();
                }
            }
            catch (Exception e) when (e is System.IO.IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            { FinishOrderCommand(command.Id, "failed", e.Message); }
        }
        private void FinishOrderCommand(string id, string state, string message)
        {
            _OrderCommands.Update(command => { if (command.Id == id) { command.State = state; command.Message = message; } });
        }
        private bool OnCartStored(API.Types.UserStatsStored result)
        {
            if (!_CartSaving || result.GameId != (ulong)_GameId) return false;
            string id = _CartCommandId;
            if (result.Result != 1) _StatsGateway.SetAchievement(_CartAchievementId, false);
            _CartSaving = false; _CartCommandId = null;
            FinishOrderCommand(id, result.Result == 1 ? "done" : "failed", result.Result == 1 ? "Confirmé par Steam" : "Refus Steam : " + result.Result);
            RefreshStats();
            return true;
        }
    }
}
