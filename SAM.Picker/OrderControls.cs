using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.UI;

namespace SAM.Picker
{
    internal sealed class RangeEditor : UserControl
    {
        private readonly ComboBox _Unit = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 105 };
        private readonly TrackBar _Min = new() { TickStyle = TickStyle.None, Dock = DockStyle.Top, Height = 35 };
        private readonly TrackBar _Max = new() { TickStyle = TickStyle.None, Dock = DockStyle.Top, Height = 35 };
        private readonly Label _Label;
        private bool _Binding;
        internal event EventHandler Edited;
        internal RangeEditor(string title)
        {
            Height = 155; Dock = DockStyle.Top; Padding = new Padding(10, 8, 10, 8);
            _Unit.Items.AddRange(new object[] { "Minutes", "Heures", "Jours" });
            var row = new Panel { Dock = DockStyle.Top, Height = 32 };
            var caption = SteamTheme.Label(title, 9, SteamTheme.Muted);
            caption.AutoSize = false; caption.Dock = DockStyle.Fill; caption.AutoEllipsis = true; caption.TextAlign = ContentAlignment.MiddleLeft;
            _Unit.Dock = DockStyle.Right; _Unit.Width = 90;
            row.Controls.Add(caption); row.Controls.Add(_Unit);
            _Min.AccessibleName = "Délai minimum";
            _Max.AccessibleName = "Délai maximum";
            _Label = SteamTheme.Label("", 10, SteamTheme.Accent); _Label.Dock = DockStyle.Top;
            Controls.Add(_Max); Controls.Add(_Min); Controls.Add(_Label); Controls.Add(row);
            _Unit.SelectedIndexChanged += (_, _) =>
            {
                if (_Binding) return;
                _Binding = true;
                _Min.Maximum = _Max.Maximum = DelayRange.Limit((TimeUnit)_Unit.SelectedIndex);
                _Binding = false;
                Changed();
            };
            _Min.ValueChanged += (_, _) => { if (_Binding) return; if (_Min.Value > _Max.Value) _Max.Value = _Min.Value; Changed(); };
            _Max.ValueChanged += (_, _) => { if (_Binding) return; if (_Max.Value < _Min.Value) _Min.Value = _Max.Value; Changed(); };
            Bind(new DelayRange());
        }
        internal DelayRange Range => new() { Minimum = _Min.Value, Maximum = _Max.Value, Unit = (TimeUnit)Math.Max(0, _Unit.SelectedIndex) };
        internal void Bind(DelayRange value)
        {
            _Binding = true;
            _Unit.SelectedIndex = (int)value.Unit;
            _Min.Maximum = _Max.Maximum = DelayRange.Limit(value.Unit);
            _Min.Value = Math.Min(_Min.Maximum, Math.Max(0, value.Minimum));
            _Max.Value = Math.Min(_Max.Maximum, Math.Max(_Min.Value, value.Maximum));
            _Binding = false; UpdateLabel();
        }
        private void UpdateLabel() => _Label.Text = $"De {_Min.Value} à {_Max.Value} {_Unit.Text.ToLowerInvariant()}";
        private void Changed() { UpdateLabel(); Edited?.Invoke(this, EventArgs.Empty); }
    }
    internal sealed class OrderArtwork : IDisposable
    {
        private readonly WebClient _Downloader = new();
        private readonly Queue<string> _Queue = new();
        private readonly Dictionary<string, Image> _Images = new(StringComparer.Ordinal);
        private readonly HashSet<string> _Requested = new(StringComparer.Ordinal);
        private bool _Disposed;
        internal event EventHandler Loaded;
        internal OrderArtwork()
        {
            _Downloader.DownloadDataCompleted += (_, e) =>
            {
                if (_Disposed) return;
                if (e.Error == null && !e.Cancelled)
                    try { using var input = new MemoryStream(e.Result); using var source = new Bitmap(input); _Images[(string)e.UserState] = new Bitmap(source); }
                    catch (Exception error) when (error is ArgumentException || error is OutOfMemoryException) { }
                Loaded?.Invoke(this, EventArgs.Empty); Next();
            };
        }
        internal Image Get(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (_Images.TryGetValue(url, out var image)) return image;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && _Requested.Add(url)) { _Queue.Enqueue(url); Next(); }
            return null;
        }
        private void Next()
        {
            if (_Disposed || _Downloader.IsBusy || _Queue.Count == 0) return;
            string url = _Queue.Dequeue();
            _Downloader.DownloadDataAsync(new Uri(url), url);
        }
        public void Dispose() { _Disposed = true; _Downloader.CancelAsync(); _Downloader.Dispose(); foreach (var image in _Images.Values) image.Dispose(); }
    }
    internal sealed class OrderRows : ListBox
    {
        internal sealed class Row
        {
            internal OrderGroup Game;
            internal OrderAchievement Achievement;
            public override string ToString() => Achievement == null ? Game.Name : Achievement.Name;
        }
        private readonly OrderArtwork _Art = new();
        private readonly Font _Title = new("Segoe UI", 11, FontStyle.Bold);
        internal OrderRows()
        {
            DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 88; IntegralHeight = false; BorderStyle = BorderStyle.None; Dock = DockStyle.Fill;
            _Art.Loaded += (_, _) => Invalidate();
        }
        internal void Bind(OrderCart cart)
        {
            int selected = SelectedIndex, top = Items.Count > 0 ? TopIndex : 0;
            BeginUpdate(); Items.Clear();
            foreach (var game in cart.Games)
            {
                Items.Add(new Row { Game = game });
                foreach (var achievement in game.Achievements) Items.Add(new Row { Game = game, Achievement = achievement });
            }
            if (selected >= 0 && selected < Items.Count) SelectedIndex = selected;
            if (Items.Count > 0) TopIndex = Math.Min(top, Items.Count - 1);
            EndUpdate();
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var row = (Row)Items[e.Index];
            bool header = row.Achievement == null;
            var rect = new Rectangle(e.Bounds.X + (header ? 0 : 12), e.Bounds.Y + 3, e.Bounds.Width - (header ? 8 : 20), 82);
            using (var bg = new SolidBrush(header ? Color.FromArgb(35, 56, 73) : (e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(39, 64, 83) : SteamTheme.Surface)) e.Graphics.FillRectangle(bg, rect);
            var icon = new Rectangle(rect.X + 12, rect.Y + 12, header ? 90 : 56, 56);
            var image = _Art.Get(header ? row.Game.ImageUrl : row.Achievement.ImageUrl);
            if (image != null) e.Graphics.DrawImage(image, icon);
            else
            {
                using var bg = new SolidBrush(SteamTheme.Sidebar); e.Graphics.FillRectangle(bg, icon);
                TextRenderer.DrawText(e.Graphics, header ? "JEU" : "★", Font, icon, SteamTheme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            int x = icon.Right + 14;
            TextRenderer.DrawText(e.Graphics, header ? row.Game.Name : row.Achievement.Name, _Title, new Rectangle(x, rect.Y + 14, Math.Max(0, rect.Right - x - 12), 25), SteamTheme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            string detail = header ? row.Game.Achievements.Count + " succès · régler les délais à droite" : row.Achievement.Status == "done" ? "DÉBLOQUÉ" : row.Achievement.Status == "sending" ? "CONFIRMATION STEAM…" : row.Achievement.Status == "failed" ? "ARRÊT SUR ERREUR" : "EN ATTENTE";
            TextRenderer.DrawText(e.Graphics, detail, Font, new Rectangle(x, rect.Y + 46, Math.Max(0, rect.Right - x - 12), 22), row.Achievement?.Status == "done" ? SteamTheme.Green : SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
        protected override void Dispose(bool disposing) { if (disposing) { _Art.Dispose(); _Title.Dispose(); } base.Dispose(disposing); }
    }
}
