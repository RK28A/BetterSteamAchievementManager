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
            UpgradeTests.Run(Check);
            ExperienceTests.Run(Check);
            ImmediateOrderTests.Run(Check);
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
                Check(Field<TabControl>(manager, "_MainTabControl").TabPages.Count == 2, "achievement timing tab has been removed");
                Check(!Field<ToolStripButton>(manager, "_StoreButton").Enabled, "demo cannot send Steam writes");
                manager.Size = new Size(1000, 760); Pump(100);
                Check(cards.ClientSize.Width > 600 && cards.ClientSize.Height > 100, "cards fit regular desktop layout");
                Capture(manager, "test-results/achievements.png");
                manager.Size = new Size(760, 620); Pump(100);
                Check(cards.ClientSize.Width > 500 && cards.ClientSize.Height > 70, "cards remain usable at minimum width");
                Capture(manager, "test-results/achievements-compact.png");
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
                library.Show(); library.Bounds = Screen.PrimaryScreen.WorkingArea; Pump(200);
                Check(library.FormBorderStyle == FormBorderStyle.None, "library has no native white window border");
                Check(library.Controls.Cast<Control>().Any(c => c.AccessibleName == "Barre de fenêtre"), "library supplies dark caption with window actions");
                var chrome = library.Controls.Cast<Control>().First(c => c.AccessibleName == "Barre de fenêtre");
                var maximize = chrome.Controls.OfType<Button>().First(b => b.AccessibleName == "Agrandir ou restaurer");
                maximize.PerformClick();
                Check(library.WindowState == FormWindowState.Maximized, "custom caption maximizes the library");
                maximize.PerformClick();
                Check(library.WindowState == FormWindowState.Normal, "custom caption restores the library");
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
                    Field<CheckBox>(library, "_ParallelOrder").Checked = true;
                    Field<NumericUpDown>(library, "_ConcurrentGames").Value = 3;
                    Check(cartStore.Read().Parallel && cartStore.Read().ConcurrentGames == 3, "cart persists parallel preparation without achievement timers");
                    Check(!Field<Button>(library, "_RunOrder").Enabled, "cart demonstration cannot execute Steam writes");
                    var orderPanel = Field<Panel>(library, "_OrderPanel");
                    var hostPanel = Field<Panel>(library, "_GameHost");
                    Check(ChildWindowFromPointEx(hostPanel.Handle, new Point(20, 20), 1) == orderPanel.Handle, "cart is actually in front of embedded game windows");
                    Check(Field<Label>(library, "_OrderEstimate").Text.Contains("aucun délai"), "cart clearly shows immediate processing");
                    var rows = Field<SAM.Picker.OrderRows>(library, "_OrderRows");
                    Check(rows.AllowDrop, "stopped cart exposes drag and drop");
                    rows.TopIndex = 0;
                    var dragSource = (SAM.Picker.OrderRows.Row)rows.Items[2];
                    var targetBounds = rows.GetItemRectangle(1);
                    var targetPoint = rows.PointToScreen(new Point(20, targetBounds.Top + 8));
                    var drag = new DragEventArgs(new DataObject(dragSource), 1, targetPoint.X, targetPoint.Y, DragDropEffects.Move, DragDropEffects.None);
                    Invoke(rows, "OnDragOver", drag);
                    Check(drag.Effect == DragDropEffects.Move, "cart drag target accepts an achievement from the same game");
                    Invoke(rows, "OnDragDrop", drag);
                    Check(cartStore.Read().Games[0].Achievements[0].Id == "B", "dropping an achievement persists the new order through the UI");
                    rows.TopIndex = 0;
                    var groupSource = rows.Items.Cast<SAM.Picker.OrderRows.Row>().First(r => r.Game.GameId == 400 && r.Achievement == null);
                    targetPoint = rows.PointToScreen(new Point(20, 8));
                    drag = new DragEventArgs(new DataObject(groupSource), 1, targetPoint.X, targetPoint.Y, DragDropEffects.Move, DragDropEffects.None);
                    Invoke(rows, "OnDragOver", drag); Invoke(rows, "OnDragDrop", drag);
                    Check(cartStore.Read().Games[0].GameId == 400, "dropping a game moves its whole group through the UI");
                    cartStore.Update(cart => SAM.API.Orders.OrderPlanning.Record(cart, "Succès", "Confirmé par Steam", new SAM.API.Orders.OrderStep { GameId = 620, GameName = "Portal 2", Achievement = new() { Id = "A", Name = "Meilleur ami de l’homme" } }));
                    Invoke(library, "RefreshOrderCart");
                    Check(Field<ListBox>(library, "_HistoryRows").Items.Count == 1, "history view shows persisted order events");
                    CaptureScreen(library, "test-results/achievement-order.png");
                    Field<Panel>(library, "_HistoryPanel").BringToFront();
                    CaptureScreen(library, "test-results/history.png");
                    var preferences = Field<SAM.API.Orders.LibraryPreferences>(library, "_LibraryPreferences");
                    library.ToggleFavorite(620);
                    Check(preferences.Favorites.Contains(620) && Field<System.Collections.Generic.List<SAM.Picker.GameInfo>>(library, "_FilteredGames")[0].Id == 620, "favorite games are saved and placed first in the library");
                    var filters = Field<ToolStrip>(library, "_LibraryFilters");
                    ((ToolStripButton)filters.Items[1]).Checked = true;
                    Check(Field<System.Collections.Generic.List<SAM.Picker.GameInfo>>(library, "_FilteredGames").Count == 2, "installed filter hides non-installed games");
                    ((ToolStripButton)filters.Items[0]).Checked = true;
                    Check(Field<System.Collections.Generic.List<SAM.Picker.GameInfo>>(library, "_FilteredGames").Count == 1, "favorite and installed library filters combine");
                    Check(Field<SAM.API.Orders.JsonStore<SAM.API.Orders.LibraryPreferences>>(library, "_PreferencesStore").Read().Favorites.Contains(620), "favorites survive preference reload");
                    Pump(2200);
                    Check(Field<SAM.API.Orders.JsonStore<SAM.API.Orders.LibraryProgress>>(library, "_ProgressStore").Read().Games.Any(g => g.GameId == 620 && g.Unlocked == 2 && g.Total == 7), "library receives confirmed achievement progress from its game process");
                    for (int i = 0; i < 8; i++)
                    {
                        ((ToolStripButton)filters.Items[0]).Checked = !((ToolStripButton)filters.Items[0]).Checked;
                        Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = i % 2 == 0 ? "missing game" : "";
                        Pump(20);
                    }
                    Field<ToolStripTextBox>(library, "_SearchGameTextBox").Text = "";
                    Check(Field<ListView>(library, "_GameListView").VirtualListSize == Field<System.Collections.Generic.List<SAM.Picker.GameInfo>>(library, "_FilteredGames").Count, "repeated library filter changes keep native indices synchronized");
                    Invoke(library, "OpenGame", 0); Pump(200);
                    CaptureScreen(library, "test-results/library-favorites.png");
                    Field<Button>(library, "_TimeButton").PerformClick(); Pump(200);
                    Check(Field<CheckedListBox>(library, "_TimeGames").Items.Count == 3, "GameTime lists owned games independently of library filters");
                    Field<CheckBox>(library, "_TimeRandom").Checked = true;
                    Field<CheckedListBox>(library, "_TimeGames").SetItemChecked(0, true);
                    Invoke(library, "StartGameTime");
                    Pump(1600);
                    var jobs = Field<System.Collections.IDictionary>(library, "_TimeJobs");
                    Check(jobs.Count == 1, "GameTime starts the selected simulated game");
                    var job = jobs.Values.Cast<object>().Single();
                    var timeProcess = Field<Process>(job, "Process");
                    library.Close(); Pump(1600);
                    Check(!library.Visible && !library.IsDisposed && !timeProcess.HasExited && Field<Label>(library, "_TimeSummary").Text.Contains("1 session"), "tray keeps GameTime and its background polling alive");
                    library.RestoreFromTray();
                    CaptureScreen(library, "test-results/gametime.png");
                    Invoke(library, "StopAllGameTime");
                    Check(timeProcess.WaitForExit(5000), "stopping GameTime terminates its owned worker");
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
                chrome.Controls.OfType<Button>().First(b => b.AccessibleName == "Fermer").PerformClick();
                Check(!library.IsDisposed && !library.Visible, "custom close button hides the library without stopping its message loop");
                Check(Field<NotifyIcon>(library, "_Tray").Visible, "hidden library remains accessible in the Windows tray");
                library.RestoreFromTray();
                Check(library.Visible, "tray action restores the existing library window");
                library.ExitApplication();
                Check(library.IsDisposed, "explicit tray quit exits the application");
            }
            Console.WriteLine($"{_Assertions} assertions passed.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
