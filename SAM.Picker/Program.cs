// Altered source: single instance, visible background recovery, and crash supervision.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using SAM.API.Orders;

namespace SAM.Picker
{
    internal static class Program
    {
        internal static bool BackgroundLaunch;
        [STAThread]
        private static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--watch") >= 0) { Watch(args); return; }
            bool demo = Array.IndexOf(args, "--demo") >= 0;
            BackgroundLaunch = Array.IndexOf(args, "--background") >= 0;
            using var gate = new Mutex(false, "Local\\BSAM-Library" + (demo ? "-Demo" : ""));
            bool owns;
            try { owns = gate.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
            if (!owns)
            {
                try { using var wake = EventWaitHandle.OpenExisting("Local\\BSAM-Show" + (demo ? "-Demo" : "")); wake.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                return;
            }
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\BSAM-Show" + (demo ? "-Demo" : ""));
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    try
                    {
                        string folder = Path.GetDirectoryName(OrderPaths.Cart); Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder, "last-crash.log"), DateTime.UtcNow.ToString("O") + Environment.NewLine + e.ExceptionObject);
                    }
                    catch (Exception) { }
                    Environment.Exit(1); // Let the supervisor recover; avoid a blocking WinForms error dialog.
                };
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                if (API.Steam.GetInstallPath() == Application.StartupPath && !demo) { MessageBox.Show("Déplacez l’application hors du dossier Steam."); return; }
                if (!demo)
                {
                    using var current = Process.GetCurrentProcess();
                    Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--watch " + current.Id + " " + current.StartTime.ToUniversalTime().Ticks) { UseShellExecute = false, CreateNoWindow = true });
                }
                using var startup = new StartupContext(demo, signal);
                Application.Run(startup);
            }
            finally { gate.ReleaseMutex(); }
        }
        private static void Watch(string[] args)
        {
            if (args.Length != 3 || !int.TryParse(args[1], out int id) || !long.TryParse(args[2], out long start)) return;
            try
            {
                if (!RecoverySupervisor.WaitForCrash(id, start)) return; // Explicit Quit must never be undone by recovery.
                var state = new JsonStore<BackgroundState>(Path.Combine(Path.GetDirectoryName(OrderPaths.Cart), "background-sessions.json")).Read();
                if (!state.Startup) return;
                Thread.Sleep(5000);
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--background") { UseShellExecute = false });
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception) { }
        }
        private sealed class StartupContext : ApplicationContext
        {
            private readonly System.Windows.Forms.Timer _Timer = new() { Interval = 5000 };
            private readonly NotifyIcon _Waiting;
            private readonly bool _Demo;
            private readonly EventWaitHandle _Signal;
            private API.Client _Client;
            private GamePicker _Picker;
            internal StartupContext(bool demo, EventWaitHandle signal)
            {
                _Demo = demo; _Signal = signal;
                var menu = new ContextMenuStrip();
                menu.Items.Add("Réessayer la connexion Steam", null, (_, _) => Connect());
                menu.Items.Add("Quitter", null, (_, _) => ExitThread());
                _Waiting = new NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "BSAM · attente de Steam", Visible = true, ContextMenuStrip = menu };
                _Timer.Tick += (_, _) => { if (_Picker == null) Connect(); else if (_Signal.WaitOne(0)) _Picker.RestoreFromTray(); };
                _Timer.Interval = 1000; _Timer.Start();
                Connect();
            }
            private DateTime _NextAttempt;
            private void Connect()
            {
                if (_Picker != null || DateTime.UtcNow < _NextAttempt) return;
                _NextAttempt = DateTime.UtcNow.AddSeconds(15);
                API.Client client = null;
                try { if (!_Demo) { client = new API.Client(); client.Initialize(0); } }
                catch (Exception e) when (e is API.ClientInitializeException || e is DllNotFoundException || e is InvalidOperationException)
                { client?.Dispose(); _Waiting.Text = "BSAM · démarrez Steam pour reprendre"; return; }
                _Client = client; _Picker = new GamePicker(client, _Demo); MainForm = _Picker;
                _Waiting.Visible = false; _Picker.Show();
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _Timer.Dispose(); _Waiting.ContextMenuStrip.Dispose(); _Waiting.Dispose(); _Client?.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
