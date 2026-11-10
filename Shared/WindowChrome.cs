// Borderless Steam-inspired caption, with drag, resize and the standard window actions.
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SAM.UI
{
    internal static class WindowChrome
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, ResizeBorder> Borders = new();
        private static void AttachBorder(Form form)
        {
            if (Borders.TryGetValue(form, out var previous)) { previous.ReleaseHandle(); Borders.Remove(form); }
            Borders.Add(form, new ResizeBorder(form));
        }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        internal static void Apply(Form form)
        {
            form.FormBorderStyle = FormBorderStyle.None;
            form.Padding = new Padding(3);
            form.BackColor = SteamTheme.Background;
            var caption = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = SteamTheme.Background, AccessibleName = "Barre de fenêtre" };
            var title = new Label { Dock = DockStyle.Fill, Text = "BETTER STEAM ACHIEVEMENT MANAGER", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0), Font = new Font("Segoe UI", 9), ForeColor = SteamTheme.Muted };
            void Maximize()
            {
                    form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            }
            void Drag(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                if (e.Clicks == 2) { Maximize(); return; }
                ReleaseCapture();
                SendMessage(form.Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            }
            caption.MouseDown += Drag;
            title.MouseDown += Drag;
            Button Action(string text, string name, Action click)
            {
                var button = new Button { Text = text, AccessibleName = name, Dock = DockStyle.Right, Width = 46, FlatStyle = FlatStyle.Flat, ForeColor = SteamTheme.Muted,
                    BackColor = SteamTheme.Background, Font = new Font("Segoe UI", 13), TabStop = false, Cursor = Cursors.Hand };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = SteamTheme.Surface;
                button.Click += (_, _) => click();
                return button;
            }
            var close = Action("×", "Fermer", form.Close);
            var maximize = Action("□", "Agrandir ou restaurer", Maximize);
            var minimize = Action("—", "Réduire", () => form.WindowState = FormWindowState.Minimized);
            caption.Controls.Add(title);
            caption.Controls.Add(minimize);
            caption.Controls.Add(maximize);
            caption.Controls.Add(close);
            form.Controls.Add(caption);
            // Keep the caption at the back of docking order so it reserves its height first.
            caption.SendToBack();
            form.HandleCreated += (_, _) => AttachBorder(form);
            if (form.IsHandleCreated) AttachBorder(form);
        }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrack, MaxTrack; }
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        private sealed class ResizeBorder : NativeWindow
        {
            private readonly Form _Form;
            internal ResizeBorder(Form form) { _Form = form; AssignHandle(form.Handle); }
            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x24)
                {
                    var monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                    if (GetMonitorInfo(MonitorFromWindow(_Form.Handle, 2), ref monitor))
                    {
                        var limits = (MinMaxInfo)Marshal.PtrToStructure(message.LParam, typeof(MinMaxInfo));
                        limits.MaxPosition = new Point(monitor.Work.Left - monitor.Monitor.Left, monitor.Work.Top - monitor.Monitor.Top);
                        limits.MaxSize = new Point(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
                        Marshal.StructureToPtr(limits, message.LParam, false);
                    }
                }
                if (message.Msg != 0x84 || _Form.WindowState != FormWindowState.Normal) return;
                long coordinates = message.LParam.ToInt64();
                var point = _Form.PointToClient(new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff)));
                const int edge = 6;
                bool left = point.X < edge, right = point.X >= _Form.ClientSize.Width - edge;
                bool top = point.Y < edge, bottom = point.Y >= _Form.ClientSize.Height - edge;
                int hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 1;
                message.Result = new IntPtr(hit);
            }
        }
    }
}
