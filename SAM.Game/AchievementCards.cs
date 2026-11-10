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
        internal Func<Stats.AchievementInfo, Image> IconProvider;
        internal event EventHandler SelectionEdited;
        private readonly Font _TitleFont = new("Segoe UI", 11, FontStyle.Bold);
        private readonly Font _SmallFont = new("Segoe UI", 9);

        public AchievementCards()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 100;
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
            bool selected = SelectedIds.Contains(info.Id), pending = info.Item.Checked != info.IsAchieved;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var canvas = new SolidBrush(BackColor)) e.Graphics.FillRectangle(canvas, e.Bounds);
            var bounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y + 5, Math.Max(1, e.Bounds.Width - 26), e.Bounds.Height - 10);
            using var shape = SteamTheme.Rounded(bounds, 10);
            using (var bg = new SolidBrush(selected ? Color.FromArgb(43, 37, 70) : SteamTheme.Surface)) e.Graphics.FillPath(bg, shape);
            using (var border = new Pen(selected ? SteamTheme.Blue : Color.FromArgb(39, 43, 57))) e.Graphics.DrawPath(border, shape);
            var imageRect = new Rectangle(bounds.X + 14, bounds.Y + 17, 56, 56);
            var cachedIcon = IconProvider?.Invoke(info);
            if (cachedIcon != null) e.Graphics.DrawImage(cachedIcon, imageRect);
            else if (Icons != null && info.ImageIndex > 0 && info.ImageIndex < Icons.Images.Count) e.Graphics.DrawImage(Icons.Images[info.ImageIndex], imageRect);
            else
            {
                using var tile = new SolidBrush(Color.FromArgb(39, 37, 62)); using var tileShape = SteamTheme.Rounded(imageRect, 9);
                e.Graphics.FillPath(tile, tileShape);
                TextRenderer.DrawText(e.Graphics, "◇", _TitleFont, imageRect, SteamTheme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int x = imageRect.Right + 16, width = Math.Max(1, bounds.Right - x - 16);
            TextRenderer.DrawText(e.Graphics, info.Name, _TitleFont, new Rectangle(x, bounds.Y + 10, width - 32, 24), SteamTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(e.Graphics, info.Description, _SmallFont, new Rectangle(x, bounds.Y + 36, width, 20), SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string rarity = info.GlobalPercent.HasValue ? info.GlobalPercent.Value.ToString("0.0") + " % des joueurs" : "Rareté indisponible";
            TextRenderer.DrawText(e.Graphics, rarity, _SmallFont, new Rectangle(x, bounds.Y + 62, Math.Max(1, width - 155), 19), info.GlobalPercent < 10 ? SteamTheme.Accent : SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string status = (info.Permission & 3) != 0 ? "Protégé" : pending ? "À enregistrer" : info.IsAchieved ? "✓ Débloqué" : "Non débloqué";
            TextRenderer.DrawText(e.Graphics, status, _SmallFont, new Rectangle(bounds.Right - 160, bounds.Y + 62, 144, 19), pending ? SteamTheme.Accent : info.IsAchieved ? SteamTheme.Green : SteamTheme.Muted, TextFormatFlags.Right | TextFormatFlags.SingleLine);
            SteamTheme.DrawCheck(e.Graphics, new Rectangle(bounds.Right - 32, bounds.Y + 14, 16, 16), selected, (info.Permission & 3) == 0 && Enabled);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _TitleFont.Dispose(); _SmallFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
