using System;
using System.Drawing;
using System.Windows.Forms;

namespace SAM.UI
{
    internal sealed class ModernButton : Button
    {
        private bool _Hover;
        internal ModernButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; DoubleBuffered = true; Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _Hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _Hover = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? SteamTheme.Background);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var shape = SteamTheme.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 7);
            using var fill = new SolidBrush(_Hover && Enabled ? SteamTheme.Blue : BackColor);
            e.Graphics.FillPath(fill, shape);
            if (Focused && ShowFocusCues) { using var pen = new Pen(SteamTheme.Accent); e.Graphics.DrawPath(pen, shape); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? ForeColor : SteamTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
    internal sealed class SteamComboBox : ComboBox
    {
        internal SteamComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
            BackColor = SteamTheme.Surface; ForeColor = SteamTheme.Text;
            ItemHeight = 24; IntegralHeight = false; DropDownHeight = 220;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SteamTheme.Blue : SteamTheme.Surface);
            e.Graphics.FillRectangle(bg, e.Bounds);
            string text = e.Index >= 0 ? GetItemText(Items[e.Index]) : Text;
            var rect = e.Bounds; rect.Inflate(-6, 0);
            TextRenderer.DrawText(e.Graphics, text, Font, rect, Enabled ? SteamTheme.Text : SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0x000F && m.Msg != 0x0318) || !IsHandleCreated) return;
            using var graphics = m.Msg == 0x0318 && m.WParam != IntPtr.Zero ? Graphics.FromHdc(m.WParam) : Graphics.FromHwnd(Handle);
            int arrowWidth = Math.Max(22, SystemInformation.VerticalScrollBarWidth + 4);
            using var background = new SolidBrush(SteamTheme.Surface);
            graphics.FillRectangle(background, Width - arrowWidth, 0, arrowWidth, Height);
            using var border = new Pen(Focused ? SteamTheme.Accent : SteamTheme.Sidebar, 2);
            graphics.DrawRectangle(border, 1, 1, Math.Max(0, Width - 2), Math.Max(0, Height - 2));
            using var arrow = new Pen(Enabled ? SteamTheme.Text : SteamTheme.Muted, 1.5f);
            int x = Width - arrowWidth / 2, y = Height / 2;
            graphics.DrawLines(arrow, new[] { new Point(x - 4, y - 2), new Point(x, y + 2), new Point(x + 4, y - 2) });
        }
    }
    internal sealed class SteamCheckBox : CheckBox
    {
        internal SteamCheckBox() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int size = Math.Max(16, (int)(16 * DeviceDpi / 96f));
            var box = new Rectangle(3, (Height - size) / 2, size, size);
            SteamTheme.DrawCheck(e.Graphics, box, Checked, Enabled);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(box.Right + 7, 0, Math.Max(0, Width - box.Right - 8), Height), Enabled ? ForeColor : SteamTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, SteamTheme.Accent, BackColor);
        }
        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    }
    internal sealed class SteamSlider : TrackBar
    {
        internal SteamSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AutoSize = false; Height = 32; TickStyle = TickStyle.None;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int trackWidth = Math.Max(1, Width - 24), y = Height / 2;
            int x = 12 + (Maximum == Minimum ? 0 : (int)((Value - Minimum) * (double)trackWidth / (Maximum - Minimum)));
            using var track = new Pen(SteamTheme.Surface, 4);
            using var fill = new Pen(Enabled ? SteamTheme.Accent : SteamTheme.Muted, 4);
            using var knob = new SolidBrush(Enabled ? SteamTheme.Accent : SteamTheme.Muted);
            e.Graphics.DrawLine(track, 12, y, Width - 12, y);
            e.Graphics.DrawLine(fill, 12, y, x, y);
            e.Graphics.FillEllipse(knob, x - 6, y - 6, 12, 12);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, SteamTheme.Accent, BackColor);
        }
        private void Pick(int x) => Value = Math.Max(Minimum, Math.Min(Maximum, Minimum + (int)Math.Round((x - 12) * (double)(Maximum - Minimum) / Math.Max(1, Width - 24))));
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && Enabled) { Focus(); Capture = true; Pick(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture && e.Button == MouseButtons.Left && Enabled) Pick(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
        protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }
    internal sealed class SteamCheckedListBox : CheckedListBox
    {
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            using var bg = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SteamTheme.Surface : SteamTheme.Sidebar);
            e.Graphics.FillRectangle(bg, e.Bounds);
            int size = Math.Min(18, e.Bounds.Height - 4);
            var box = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height - size) / 2, size, size);
            SteamTheme.DrawCheck(e.Graphics, box, GetItemChecked(e.Index), Enabled);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(box.Right + 6, e.Bounds.Top, Math.Max(0, e.Bounds.Width - box.Width - 14), e.Bounds.Height), Enabled ? SteamTheme.Text : SteamTheme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
        }
    }
}
