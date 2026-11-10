// Offline regression tests: no Steam account, Steam client or writes required.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SAM.Game;

internal static class Program
{
    private static int _Assertions;
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr parent, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
        _Assertions++;
    }
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    private static void Pump(int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        do { Application.DoEvents(); Thread.Sleep(15); } while (clock.ElapsedMilliseconds < milliseconds);
    }
    private static void Capture(Form form, string path)
    {
        using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
        image.Save(path);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory("test-results");
        try
        {
            using (var manager = new Manager(620, null, true))
            {
                manager.Show(); Pump(200);
                var cards = manager._Cards;
                Check(cards.Items.Count == 7, "all demo achievements render");
                Check(cards.VisibleAchievements.Count(i => i.IsAchieved) == 2, "confirmed Steam state is separate");
                cards.SelectVisible("all");
                Check(cards.SelectedIds.Count == 6, "select all excludes protected achievements");
                Check(!manager.HasPendingChanges, "selection alone never stages a write");
                cards.SelectVisible("invert");
                Check(cards.SelectedIds.Count == 0, "invert deselects selected achievements");
                cards.SelectVisible("locked");
                Check(cards.SelectedIds.Count == 4, "select locked excludes achieved and protected");
                manager.StageSelected(true);
                Check(manager.HasPendingChanges, "unlock selection stages pending changes");
                Check(cards.VisibleAchievements.Count(i => i.Item.Checked && !i.IsAchieved) == 4, "four writes staged without changing confirmed state");
                Field<ToolStripTextBox>(manager, "_MatchingStringTextBox").Text = "Collectionneur";
                Check(cards.Items.Count == 1, "search filters visible cards");
                Check(cards.SelectedIds.Count == 4 && manager.HasPendingChanges, "search retains hidden selection and pending writes");
                Field<ToolStripTextBox>(manager, "_MatchingStringTextBox").Text = "";
                Check(cards.Items.Count == 7, "clearing search restores cards");
                cards.SelectVisible("none");
                Check(cards.SelectedIds.Count == 0 && manager.HasPendingChanges, "deselection leaves staged writes intact");
                var locked = Field<ToolStripButton>(manager, "_DisplayLockedOnlyButton");
                locked.PerformClick();
                Check(cards.Items.Count == 5, "locked filter uses confirmed state, includes read-only achievements");
                var unlocked = Field<ToolStripButton>(manager, "_DisplayUnlockedOnlyButton");
                unlocked.PerformClick();
                Check(cards.Items.Count == 2 && !locked.Checked, "achieved filter is mutually exclusive");
                unlocked.PerformClick();
                Check(cards.Items.Count == 7, "both filters off shows all achievements");
                Check(Field<NumericUpDown>(manager, "_MinimumMinutes").Value == 3 && Field<NumericUpDown>(manager, "_MaximumMinutes").Value == 10, "original timing defaults preserved");
                Check(!Field<Button>(manager, "_StartQueue").Enabled && !Field<ToolStripButton>(manager, "_StoreButton").Enabled, "demo cannot send Steam writes");
                Invoke(manager, "StopQueue", "Test d’arrêt");
                Check(!Field<bool>(manager, "_QueueRunning") && Field<System.Collections.Generic.Queue<SAM.Game.Stats.AchievementDefinition>>(manager, "_PendingAchievements").Count == 0, "stop clears all future queued sends");
                manager.Size = new Size(1000, 760); Pump(100);
                Check(cards.ClientSize.Width > 600 && cards.ClientSize.Height > 100, "cards fit regular desktop layout");
                Capture(manager, "test-results/achievements.png");
                manager.Size = new Size(760, 620); Pump(100);
                Check(cards.ClientSize.Width > 500 && cards.ClientSize.Height > 70, "cards remain usable at minimum width");
                Capture(manager, "test-results/achievements-compact.png");
                Field<TabControl>(manager, "_MainTabControl").SelectedIndex = 2; Pump(100);
                Capture(manager, "test-results/timing.png");
                manager.Close();
            }
            // Verify the real cross-process hosting path using an offline child view.
            using (var host = new Form { ClientSize = new Size(960, 720) })
            {
                host.Show();
                using var process = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"),
                    "620 --demo --host " + host.Handle.ToInt64() + " --owner " + Process.GetCurrentProcess().Id) { UseShellExecute = false });
                try
                {
                    IntPtr child = IntPtr.Zero;
                    var clock = Stopwatch.StartNew();
                    while (clock.Elapsed < TimeSpan.FromSeconds(20) && child == IntPtr.Zero && !process.HasExited)
                    { Pump(100); child = GetWindow(host.Handle, 5); }
                    Check(child != IntPtr.Zero && GetParent(child) == host.Handle, "game process embeds into library host");
                    host.ClientSize = new Size(840, 600); Pump(800);
                    GetClientRect(child, out var bounds);
                    Check(bounds.Right == 840 && bounds.Bottom == 600, "embedded game follows library resize");
                    host.Close();
                    clock.Restart();
                    while (!process.HasExited && clock.Elapsed < TimeSpan.FromSeconds(10)) Pump(100);
                    Check(process.HasExited, "game process exits when its library host closes");
                }
                finally { if (!process.HasExited) process.Kill(); }
            }
            using (var library = new SAM.Picker.GamePicker(null, true))
            {
                library.Show(); Pump(200);
                Check(Field<SAM.Picker.MyListView>(library, "_GameListView").VirtualListSize == 3, "library renders games without Steam");
                Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = "Portal";
                Check(Field<SAM.Picker.MyListView>(library, "_GameListView").VirtualListSize == 2, "library search filters games");
                Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = "";
                Capture(library, "test-results/library.png");
                library.Close();
            }
            Console.WriteLine($"{_Assertions} assertions passed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
