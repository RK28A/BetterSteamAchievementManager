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
    private sealed class FakeStats : IStatsGateway
    {
        internal int Sets, Stores;
        internal bool ReadOk = true, SetOk = true, StoreOk = true;
        public bool ReadAchievement(string id, out bool achieved) { achieved = false; return ReadOk; }
        public bool SetAchievement(string id, bool achieved) { Sets++; return SetOk; }
        public bool Store() { Stores++; return StoreOk; }
    }
    private static void SetField(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    private static void Due(Manager manager)
    {
        SetField(manager, "_QueueDelay", TimeSpan.Zero);
        Invoke(manager, "TickQueue", null, EventArgs.Empty);
    }
    private static Manager Queued(FakeStats stats)
    {
        var manager = new Manager(620, null, true, stats);
        var choices = Field<CheckedListBox>(manager, "_QueueChoices");
        choices.SetItemChecked(0, true);
        choices.SetItemChecked(1, true);
        Invoke(manager, "StartQueue", null, EventArgs.Empty);
        return manager;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr parent, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    private static IntPtr FindEmbedded(Panel panel, Process process)
    {
        for (IntPtr child = GetWindow(panel.Handle, 5); child != IntPtr.Zero; child = GetWindow(child, 2))
        {
            GetWindowThreadProcessId(child, out uint processId);
            if (processId == process.Id) return child;
        }
        return IntPtr.Zero;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")] private static extern IntPtr ChildWindowFromPointEx(IntPtr parent, Point point, uint flags);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr context, uint flags);
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
    private static void Capture(Form form, string path, Panel embedded = null, IntPtr embeddedWindow = default)
    {
        using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
        if (embedded != null)
        {
            var child = embeddedWindow;
            if (child != IntPtr.Zero)
            {
                using var childImage = new Bitmap(embedded.ClientSize.Width, embedded.ClientSize.Height);
                using (var graphics = Graphics.FromImage(childImage))
                {
                    IntPtr dc = graphics.GetHdc();
                    try { PrintWindow(child, dc, 0); } finally { graphics.ReleaseHdc(dc); }
                }
                using var canvas = Graphics.FromImage(image);
                canvas.DrawImage(childImage, form.PointToClient(embedded.PointToScreen(Point.Empty)));
            }
        }
        image.Save(path);
    }

    private static void CaptureScreen(Form form, string path)
    {
        form.BringToFront(); form.Activate(); Pump(250);
        var screenBounds = form.RectangleToScreen(form.ClientRectangle);
        var visible = Rectangle.Intersect(screenBounds, Screen.FromControl(form).Bounds);
        using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        using (var g = Graphics.FromImage(image))
        {
            g.Clear(Color.FromArgb(27, 32, 40));
            g.CopyFromScreen(visible.Location, new Point(visible.Left - screenBounds.Left, visible.Top - screenBounds.Top), visible.Size);
        }
        image.Save(path);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory("test-results");
        try
        {
            OrderTests.Run(Check);
            Console.WriteLine("START achievement view construction");
            using (var manager = new Manager(620, null, true))
            {
                Console.WriteLine("START achievement view display");
                manager.Show();
                Console.WriteLine("START achievement view message loop");
                Pump(200);
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
            using (var queue = Queued(new FakeStats()))
            {
                var stats = (FakeStats)Field<IStatsGateway>(queue, "_StatsGateway");
                Invoke(queue, "TickQueue", null, EventArgs.Empty);
                Check(stats.Sets == 0, "first achievement waits for its delay");
                Due(queue);
                Check(stats.Sets == 1 && stats.Stores == 1, "deadline sends exactly one achievement");
                Due(queue);
                Check(stats.Sets == 1, "no second send before Steam confirms");
                Invoke(queue, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 400, Result = 1 });
                Check(Field<SAM.Game.Stats.AchievementDefinition>(queue, "_SavingAchievement") != null, "confirmation for another game is ignored");
                Invoke(queue, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 620, Result = 1 });
                Invoke(queue, "TickQueue", null, EventArgs.Empty);
                Check(stats.Sets == 1, "next achievement receives a new full delay");
                Invoke(queue, "StopQueue", "Test");
                Due(queue);
                Check(stats.Sets == 1 && !Field<bool>(queue, "_QueueRunning"), "stopping while waiting prevents the next write");
            }
            using (var queue = Queued(new FakeStats()))
            {
                Due(queue);
                Invoke(queue, "StopQueue", "Test");
                Invoke(queue, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 620, Result = 1 });
                Due(queue);
                Check(((FakeStats)Field<IStatsGateway>(queue, "_StatsGateway")).Sets == 1, "stopping during confirmation never advances the queue");
            }
            using (var queue = Queued(new FakeStats()))
            {
                Due(queue);
                Invoke(queue, "OnQueueStored", new SAM.API.Types.UserStatsStored { GameId = 620, Result = 2 });
                Due(queue);
                Check(((FakeStats)Field<IStatsGateway>(queue, "_StatsGateway")).Sets == 1 && !Field<bool>(queue, "_QueueRunning"), "Steam rejection halts the queue");
            }
            foreach (string failure in new[] { "read", "set", "store" })
            {
                var stats = new FakeStats { ReadOk = failure != "read", SetOk = failure != "set", StoreOk = failure != "store" };
                using var queue = Queued(stats);
                Due(queue);
                int count = stats.Sets;
                Due(queue);
                Check(!Field<bool>(queue, "_QueueRunning") && stats.Sets == count, failure + " failure prevents later sends");
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
                library.Show(); library.Bounds = Screen.PrimaryScreen.WorkingArea; Pump(200);
                Check(library.FormBorderStyle == FormBorderStyle.None, "library has no native white window border");
                Check(library.Controls.Cast<Control>().Any(c => c.AccessibleName == "Barre de fenêtre"), "library supplies dark caption with window actions");
                Check(Field<SAM.Picker.MyListView>(library, "_GameListView").VirtualListSize == 3, "library renders games without Steam");
                Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = "Portal";
                Check(Field<SAM.Picker.MyListView>(library, "_GameListView").VirtualListSize == 2, "library search filters games");
                Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = "";
                var sessions = Field<System.Collections.Generic.Dictionary<uint, (Panel Panel, Process Process)>>(library, "_Sessions");
                try
                {
                    Invoke(library, "OpenGame", 2);
                    var first = sessions[620];
                    var clock = Stopwatch.StartNew();
                    while (clock.Elapsed < TimeSpan.FromSeconds(20) && FindEmbedded(first.Panel, first.Process) == IntPtr.Zero && !first.Process.HasExited) Pump(100);
                    Check(FindEmbedded(first.Panel, first.Process) != IntPtr.Zero, "library click opens embedded achievement view");
                    Invoke(library, "OpenGame", 1);
                    var second = sessions[400];
                    clock.Restart();
                    while (clock.Elapsed < TimeSpan.FromSeconds(20) && FindEmbedded(second.Panel, second.Process) == IntPtr.Zero && !second.Process.HasExited) Pump(100);
                    Check(FindEmbedded(second.Panel, second.Process) != IntPtr.Zero, "another game opens an isolated view");
                    Invoke(library, "OpenGame", 2); Pump(200);
                    Check(sessions.Count == 2 && sessions[620].Process.Id == first.Process.Id, "switching games reuses the existing session");
                    CaptureScreen(library, "test-results/library.png");
                    var cartStore = new SAM.API.Orders.JsonStore<SAM.API.Orders.OrderCart>(Field<string>(library, "_CartPath"));
                    SAM.API.Orders.OrderPaths.Add(cartStore, new SAM.API.Orders.OrderGroup { GameId = 620, Name = "Portal 2", Achievements = new System.Collections.Generic.List<SAM.API.Orders.OrderAchievement> { new() { Id = "A", Name = "Meilleur ami de l’homme" }, new() { Id = "B", Name = "Collectionneur" } } });
                    SAM.API.Orders.OrderPaths.Add(cartStore, new SAM.API.Orders.OrderGroup { GameId = 400, Name = "Portal", Achievements = new System.Collections.Generic.List<SAM.API.Orders.OrderAchievement> { new() { Id = "C", Name = "Le début de l’aventure" } } });
                    Field<Button>(library, "_OrderButton").PerformClick(); Pump(200);
                    Check(Field<SAM.Picker.OrderRows>(library, "_OrderRows").Items.Count == 5, "cart renders game groups and achievements");
                    var editor = Field<SAM.Picker.RangeEditor>(library, "_SuccessRange");
                    Field<ComboBox>(editor, "_Unit").SelectedIndex = 1;
                    Field<TrackBar>(editor, "_Min").Value = 2;
                    Field<TrackBar>(editor, "_Max").Value = 8;
                    Check(cartStore.Read().Games[0].Delay.Unit == SAM.API.Orders.TimeUnit.Hours && cartStore.Read().Games[0].Delay.Minimum == 2 && cartStore.Read().Games[0].Delay.Maximum == 8, "cart sliders persist per-game hour ranges");
                    Field<CheckBox>(library, "_BetweenGames").Checked = true;
                    Check(cartStore.Read().BetweenGames, "cart enables the inter-game timer");
                    var gameRange = Field<SAM.Picker.RangeEditor>(library, "_GameRange");
                    var settings = (ScrollableControl)gameRange.Parent;
                    settings.ScrollControlIntoView(gameRange); Pump(100);
                    Check(gameRange.Bottom <= settings.ClientSize.Height && gameRange.Top >= 0, "inter-game sliders remain reachable in a compact window");
                    Check(!Field<Button>(library, "_RunOrder").Enabled, "cart demonstration cannot execute Steam writes");
                    var orderPanel = Field<Panel>(library, "_OrderPanel");
                    var hostPanel = Field<Panel>(library, "_GameHost");
                    Check(ChildWindowFromPointEx(hostPanel.Handle, new Point(20, 20), 1) == orderPanel.Handle, "cart is actually in front of embedded game windows");
                    CaptureScreen(library, "test-results/achievement-order.png");
                }
                finally
                {
                    foreach (var session in sessions.Values)
                    {
                        if (!session.Process.HasExited) session.Process.Kill();
                        session.Process.WaitForExit(5000);
                    }
                    Pump(100);
                }
                library.Close();
            }
            Console.WriteLine($"{_Assertions} assertions passed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
