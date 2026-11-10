using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private NotifyIcon _Tray;
        private bool _ExitRequested;
        private ToolStripMenuItem _StartupMenu;
        private void InitializeTray()
        {
            var menu = new ContextMenuStrip(components);
            menu.Items.Add("Ouvrir BetterSteamAchievementManager", null, (_, _) => RestoreFromTray());
            _StartupMenu = new ToolStripMenuItem("Reprise au démarrage de Windows") { CheckOnClick = true, Checked = _Background.Startup };
            _StartupMenu.CheckedChanged += (_, _) =>
            {
                try { SetStartup(_StartupMenu.Checked); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException)
                { MessageBox.Show(this, e.Message, "Démarrage automatique"); }
            };
            menu.Items.Add(_StartupMenu);
            menu.Items.Add("Tout mettre en pause", null, (_, _) => { PauseParallel(); StopAllGameTime(); if (_Runner.Running) PauseOrder(); });
            menu.Items.Add("Quitter", null, (_, _) => ExitApplication());
            _Tray = new NotifyIcon(components) { Icon = Icon ?? System.Drawing.SystemIcons.Application, Text = "BetterSteamAchievementManager", ContextMenuStrip = menu, Visible = true };
            _Tray.DoubleClick += (_, _) => RestoreFromTray();
            FormClosed += (_, _) => { _Tray.Visible = false; _Tray.Dispose(); };
            if (Program.BackgroundLaunch) Shown += (_, _) => HideToTray();
        }
        private void SetStartup(bool enabled)
        {
            if (!_Demo)
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (enabled) key.SetValue("BetterSteamAchievementManager", "\"" + Application.ExecutablePath + "\" --background");
                else key.DeleteValue("BetterSteamAchievementManager", false);
            }
            _Background.Startup = enabled; SaveBackground();
        }
        private void EnableBackgroundRecovery()
        {
            if (_StartupMenu == null) return;
            if (!_StartupMenu.Checked) _StartupMenu.Checked = true;
        }
        internal void RestoreFromTray() { ShowInTaskbar = true; Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); }
        private void HideToTray() { Hide(); ShowInTaskbar = false; }
        internal void ExitApplication() { _ExitRequested = true; Close(); if (!IsDisposed) _ExitRequested = false; }
    }
}
