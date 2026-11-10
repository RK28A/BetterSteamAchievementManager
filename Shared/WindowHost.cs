// An isolated Steam client per game, hosted inside the library window.
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SAM.UI
{
    internal static class WindowHost
    {
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);

        public static void Attach(Form form, IntPtr parent, int ownerPid)
        {
            form.FormBorderStyle = FormBorderStyle.None;
            form.MinimumSize = System.Drawing.Size.Empty;
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Opacity = 0;
            Timer lifetime = new() { Interval = 300 };
            void Sync()
            {
                bool alive;
                try { using var owner = Process.GetProcessById(ownerPid); alive = !owner.HasExited; }
                catch (ArgumentException) { alive = false; }
                if (!alive || !IsWindow(parent)) { form.Close(); return; }
                if (GetClientRect(parent, out var rect)) MoveWindow(form.Handle, 0, 0, rect.Right, rect.Bottom, true);
            }
            form.Shown += (_, _) =>
            {
                // Both binaries are x86; HWND values therefore fit in a 32-bit window style.
                SetWindowLong(form.Handle, -16, (GetWindowLong(form.Handle, -16) & ~unchecked((int)0x80000000)) | 0x40000000);
                SetParent(form.Handle, parent);
                Sync();
                form.Opacity = 1;
                lifetime.Start();
            };
            lifetime.Tick += (_, _) => Sync();
            form.FormClosed += (_, _) => lifetime.Dispose();
        }
    }
}
