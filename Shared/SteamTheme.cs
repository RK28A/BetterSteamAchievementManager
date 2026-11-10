// BetterSteamAchievementManager UI. Original Steam assets remain the property of their owners.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SAM.UI
{
    internal static class SteamTheme
    {
        public static readonly Color Background = Color.FromArgb(27, 32, 40);
        public static readonly Color Sidebar = Color.FromArgb(32, 39, 49);
        public static readonly Color Surface = Color.FromArgb(38, 49, 62);
        public static readonly Color Text = Color.FromArgb(221, 229, 237);
        public static readonly Color Muted = Color.FromArgb(145, 163, 181);
        public static readonly Color Accent = Color.FromArgb(102, 192, 244);
        public static readonly Color Blue = Color.FromArgb(39, 115, 168);
        public static readonly Color Green = Color.FromArgb(162, 209, 100);

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr window, string app, string sub);
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Themed = new();
        public static void DrawCheck(Graphics graphics, Rectangle bounds, bool selected, bool enabled)
        {
            using var fill = new SolidBrush(selected && enabled ? Blue : Sidebar);
            using var edge = new Pen(enabled ? Accent : Muted);
            graphics.FillRectangle(fill, bounds); graphics.DrawRectangle(edge, bounds);
            if (selected)
            {
                using var mark = new Pen(enabled ? Text : Muted, 2);
                graphics.DrawLines(mark, new[] { new Point(bounds.Left + 3, bounds.Top + bounds.Height / 2), new Point(bounds.Left + bounds.Width / 2 - 1, bounds.Bottom - 4), new Point(bounds.Right - 3, bounds.Top + 4) });
            }
        }
        private static void DarkNative(Control control)
        {
            try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        private static void DarkTitle(Form form)
        {
            try { int enabled = 1; DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public static void Apply(Control root)
        {
            root.BackColor = root is TextBoxBase || root is ListBox ? Sidebar : Background;
            if (root is not System.Windows.Forms.Label || root.ForeColor == SystemColors.ControlText) root.ForeColor = Text;
            if (!Themed.TryGetValue(root, out _))
            {
                Themed.Add(root, new object());
                root.HandleCreated += (_, _) => DarkNative(root);
                if (root.IsHandleCreated) DarkNative(root);
            }
            if (root is TextBox textBox) textBox.BorderStyle = BorderStyle.FixedSingle;
            if (root is NumericUpDown numeric) { numeric.BorderStyle = BorderStyle.FixedSingle; numeric.BackColor = Sidebar; }
            if (root is ComboBox combo) { combo.BackColor = Surface; combo.FlatStyle = FlatStyle.Flat; }
            if (root is Form form) { root.Font = new Font("Segoe UI", 10F); form.HandleCreated += (_, _) => DarkTitle(form); }
            if (root is Button b)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 0;
                b.BackColor = Surface;
                b.Padding = new Padding(10, 5, 10, 5);
                b.Cursor = Cursors.Hand;
                b.FlatAppearance.MouseOverBackColor = Blue;
                b.FlatAppearance.MouseDownBackColor = Sidebar;
            }
            if (root is ToolStrip strip)
            {
                strip.Renderer = new ToolStripProfessionalRenderer(new SteamColors());
                strip.GripStyle = ToolStripGripStyle.Hidden;
                strip.Padding = new Padding(8, 5, 8, 5);
                foreach (ToolStripItem item in strip.Items)
                {
                    item.ForeColor = Text;
                    if (item is ToolStripTextBox input) { input.BackColor = Sidebar; input.ForeColor = Text; input.BorderStyle = BorderStyle.FixedSingle; }
                    if (item is ToolStripDropDownItem menu) Apply(menu.DropDown);
                }
            }
            if (root is TabControl tabs && root is not SteamTabs)
            {
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.ItemSize = new Size(160, 36);
                tabs.SizeMode = TabSizeMode.Fixed;
                tabs.DrawItem += (_, e) =>
                {
                    using var brush = new SolidBrush(e.Index == tabs.SelectedIndex ? Surface : Background);
                    e.Graphics.FillRectangle(brush, e.Bounds);
                    TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds,
                        e.Index == tabs.SelectedIndex ? Accent : Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                };
            }
            if (root is DataGridView grid)
            {
                grid.BackgroundColor = Background;
                grid.BorderStyle = BorderStyle.None;
                grid.EnableHeadersVisualStyles = false;
                grid.GridColor = Surface;
                grid.DefaultCellStyle.BackColor = Sidebar;
                grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = Blue;
                grid.DefaultCellStyle.SelectionForeColor = Text;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                grid.RowHeadersVisible = false;
                grid.RowTemplate.Height = 32;
            }
            foreach (Control child in root.Controls) Apply(child);
        }

        public static Label Label(string text, float size = 10, Color? color = null)
            => new Label { Text = text, AutoSize = true, ForeColor = color ?? Text, Font = new Font("Segoe UI", size), Margin = new Padding(0, 4, 0, 4) };

        private sealed class SteamColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin => Background;
            public override Color ToolStripGradientMiddle => Background;
            public override Color ToolStripGradientEnd => Background;
            public override Color StatusStripGradientBegin => Background;
            public override Color StatusStripGradientEnd => Background;
            public override Color MenuItemSelected => Surface;
            public override Color MenuItemBorder => Blue;
            public override Color ToolStripDropDownBackground => Sidebar;
            public override Color ImageMarginGradientBegin => Sidebar;
            public override Color ImageMarginGradientMiddle => Sidebar;
            public override Color ImageMarginGradientEnd => Sidebar;
            public override Color ButtonSelectedGradientBegin => Surface;
            public override Color ButtonSelectedGradientMiddle => Surface;
            public override Color ButtonSelectedGradientEnd => Surface;
            public override Color ButtonCheckedGradientBegin => Blue;
            public override Color ButtonCheckedGradientMiddle => Blue;
            public override Color ButtonCheckedGradientEnd => Blue;
            public override Color ToolStripBorder => Background;
        }
    }

    internal sealed class HeroPanel : Panel
    {
        public HeroPanel() { DoubleBuffered = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(40, 69, 89), SteamTheme.Background, 15F);
            e.Graphics.FillRectangle(fill, ClientRectangle);
        }
    }
}

namespace SAM.UI
{
    // Pages fill the client area; the library supplies its own dark navigation bar.
    internal sealed class SteamTabs : System.Windows.Forms.TabControl
    {
        public override System.Drawing.Rectangle DisplayRectangle => new(0, 0, Width, Height);
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == 0x1328) { m.Result = System.IntPtr.Zero; return; }
            base.WndProc(ref m);
        }
    }
    internal sealed class SteamProgress : System.Windows.Forms.Control
    {
        private int _Value;
        public int Maximum { get; set; } = 1000;
        public int Value { get => _Value; set { _Value = System.Math.Max(0, System.Math.Min(Maximum, value)); Invalidate(); } }
        public SteamProgress() { DoubleBuffered = true; }
        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            e.Graphics.Clear(SteamTheme.Surface);
            if (Maximum <= 0) return;
            using var fill = new System.Drawing.SolidBrush(SteamTheme.Accent);
            e.Graphics.FillRectangle(fill, 0, 0, Width * Value / Maximum, Height);
        }
    }
}
