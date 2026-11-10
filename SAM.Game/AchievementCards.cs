// Altered source: virtual, owner-drawn achievement cards; selection is independent of Steam state.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SAM.UI;

namespace SAM.Game
{
    internal sealed class AchievementCards : ListBox
    {
        internal readonly HashSet<string> SelectedIds = new(StringComparer.Ordinal);
        internal ImageList Icons;
        internal event EventHandler SelectionEdited;
        private readonly Font _TitleFont = new("Segoe UI", 11, FontStyle.Bold);
        private readonly Font _SmallFont = new("Segoe UI", 9);

        public AchievementCards()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 106;
            IntegralHeight = false;
            BorderStyle = BorderStyle.None;
            Dock = DockStyle.Fill;
            AccessibleName = "Succès du jeu : espace pour sélectionner";
        }
        internal IEnumerable<Stats.AchievementInfo> VisibleAchievements => Items.Cast<Stats.AchievementInfo>();
        internal void SelectVisible(string mode)
        {
            foreach (var info in VisibleAchievements)
            {
                if ((info.Permission & 3) != 0) continue;
                bool selected = mode == "all" || (mode == "locked" && !info.IsAchieved) || (mode == "invert" && !SelectedIds.Contains(info.Id));
                if (selected) SelectedIds.Add(info.Id); else SelectedIds.Remove(info.Id);
            }
            NotifySelection();
        }
        internal void NotifySelection() { Invalidate(); SelectionEdited?.Invoke(this, EventArgs.Empty); }
        private void Toggle(int index)
        {
            if (!Enabled || index < 0 || index >= Items.Count) return;
            var info = (Stats.AchievementInfo)Items[index];
            if ((info.Permission & 3) != 0) return;
            if (!SelectedIds.Add(info.Id)) SelectedIds.Remove(info.Id);
            NotifySelection();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Toggle(IndexFromPoint(e.Location));
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Toggle(SelectedIndex); e.Handled = true; e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.A) { SelectVisible("all"); e.Handled = true; e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var info = (Stats.AchievementInfo)Items[e.Index];
            bool selected = SelectedIds.Contains(info.Id);
            bool pending = info.Item.Checked != info.IsAchieved;
            var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, Math.Max(0, e.Bounds.Width - 14), e.Bounds.Height - 8);
            using (var bg = new SolidBrush(selected ? Color.FromArgb(39, 64, 83) : SteamTheme.Surface)) e.Graphics.FillRectangle(bg, bounds);
            if (selected) { using var edge = new SolidBrush(SteamTheme.Accent); e.Graphics.FillRectangle(edge, bounds.X, bounds.Y, 3, bounds.Height); }
            var imageRect = new Rectangle(bounds.X + 14, bounds.Y + 13, 70, 70);
            if (Icons != null && info.ImageIndex > 0 && info.ImageIndex < Icons.Images.Count) e.Graphics.DrawImage(Icons.Images[info.ImageIndex], imageRect);
            else
            {
                using var tile = new SolidBrush(Color.FromArgb(29, 42, 53)); e.Graphics.FillRectangle(tile, imageRect);
                TextRenderer.DrawText(e.Graphics, "★", _TitleFont, imageRect, SteamTheme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int width = Math.Max(0, bounds.Width - 151);
            var title = new Rectangle(bounds.X + 100, bounds.Y + 12, width, 24);
            TextRenderer.DrawText(e.Graphics, info.Name, _TitleFont, title, SteamTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            var description = new Rectangle(title.X, bounds.Y + 39, width, 22);
            TextRenderer.DrawText(e.Graphics, info.Description, Font, description, SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string status = (info.Permission & 3) != 0 ? "PROTÉGÉ · lecture seule" : pending ? (info.Item.Checked ? "À DÉBLOQUER · en attente d’enregistrement" : "À VERROUILLER · en attente d’enregistrement") : info.IsAchieved ? "DÉBLOQUÉ" + (info.UnlockTime.HasValue ? " · " + info.UnlockTime.Value.ToString("g") : "") : "NON DÉBLOQUÉ";
            TextRenderer.DrawText(e.Graphics, status, _SmallFont, new Rectangle(title.X, bounds.Y + 67, width, 20), pending ? SteamTheme.Accent : info.IsAchieved ? SteamTheme.Green : SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            ControlPaint.DrawCheckBox(e.Graphics, new Rectangle(bounds.Right - 35, bounds.Top + 36, 18, 18),
                (selected ? ButtonState.Checked : ButtonState.Normal) | ((info.Permission & 3) != 0 || !Enabled ? ButtonState.Inactive : ButtonState.Normal));
            if ((e.State & DrawItemState.Focus) != 0 && Focused) ControlPaint.DrawFocusRectangle(e.Graphics, bounds, SteamTheme.Accent, SteamTheme.Surface);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _TitleFont.Dispose(); _SmallFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
