using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private ListBox _HistoryRows;
        private Panel _HistoryPanel;
        private string _HistorySignature;
        private void InitializeHistory(Panel bar)
        {
            _HistoryPanel = new Panel { Dock = DockStyle.Fill };
            var title = SteamTheme.Label("Historique des commandes", 24); title.Dock = DockStyle.Top; title.Padding = new Padding(20, 16, 20, 12);
            var caption = SteamTheme.Label("Les 2 000 derniers événements · heures locales · conservés sur cet ordinateur", 10, SteamTheme.Muted); caption.Dock = DockStyle.Top; caption.Padding = new Padding(20, 0, 20, 12);
            _HistoryRows = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 88, AccessibleName = "Historique des commandes" };
            _HistoryRows.DrawItem += (_, e) =>
            {
                if (e.Index < 0) return;
                var entry = (OrderHistoryEntry)_HistoryRows.Items[e.Index];
                var rect = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 4, Math.Max(0, e.Bounds.Width - 24), e.Bounds.Height - 8);
                using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SteamTheme.Blue : SteamTheme.Surface);
                e.Graphics.FillRectangle(bg, rect);
                var head = entry.Utc.ToLocalTime().ToString("g") + "  ·  " + entry.Event + (entry.GameName == null ? "" : "  ·  " + entry.GameName);
                TextRenderer.DrawText(e.Graphics, head, _HistoryRows.Font, new Rectangle(rect.X + 12, rect.Y + 8, rect.Width - 24, 23), entry.Event == "Erreur" ? Color.Salmon : SteamTheme.Accent, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, (entry.AchievementName == null ? "" : entry.AchievementName + " · ") + entry.Detail, _HistoryRows.Font, new Rectangle(rect.X + 12, rect.Y + 34, rect.Width - 24, 38), SteamTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.WordBreak);
            };
            _HistoryPanel.Controls.Add(_HistoryRows); _HistoryPanel.Controls.Add(caption); _HistoryPanel.Controls.Add(title);
            _GameHost.Controls.Add(_HistoryPanel); _HistoryPanel.SendToBack();
            var button = new SAM.UI.ModernButton { Text = "Historique", Dock = DockStyle.Right, Width = 130 };
            button.Click += (_, _) =>
            {
                try { RefreshHistory(_OrderStore.Read()); _HistoryPanel.BringToFront(); }
                catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException) { MessageBox.Show(this, e.Message, "Historique indisponible"); }
            };
            bar.Controls.Add(button);
            SteamTheme.Apply(_HistoryPanel); SteamTheme.Apply(button);
        }
        private void RefreshHistory(OrderCart cart)
        {
            if (_HistoryRows == null) return;
            string signature = cart.History.Count + ":" + cart.History.LastOrDefault()?.Utc.Ticks + ":" + cart.History.LastOrDefault()?.Detail;
            if (signature == _HistorySignature) return;
            _HistorySignature = signature;
            _HistoryRows.BeginUpdate(); _HistoryRows.Items.Clear();
            foreach (var item in cart.History.AsEnumerable().Reverse()) _HistoryRows.Items.Add(item);
            _HistoryRows.EndUpdate();
        }
        private void UpdateOrderEstimate(OrderCart cart)
        {
            int remaining = cart.Games.Sum(g => g.Achievements.Count(a => a.Status != "done"));
            _OrderEstimate.Text = remaining + " succès restant(s) · aucun délai ajouté\n" + (cart.Parallel ? cart.ConcurrentGames + " jeux préparés en parallèle · envois alternés" : "Jeux traités dans l’ordre du panier") + "\nLa durée totale dépend des réponses de Steam.";
        }
    }
}
