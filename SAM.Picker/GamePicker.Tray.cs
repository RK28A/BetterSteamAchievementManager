using System;
using System.Windows.Forms;

namespace SAM.Picker
{
    internal partial class GamePicker
    {
        private NotifyIcon _Tray;
        private bool _ExitRequested, _TrayHintShown;
        private void InitializeTray()
        {
            var menu = new ContextMenuStrip(components);
            menu.Items.Add("Ouvrir Better Achievements", null, (_, _) => RestoreFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quitter complètement", null, (_, _) => ExitApplication());
            SAM.UI.SteamTheme.Apply(menu);
            _Tray = new NotifyIcon(components) { Icon = Icon ?? System.Drawing.SystemIcons.Application, Text = "Better Achievements · actif", ContextMenuStrip = menu, Visible = true };
            _Tray.DoubleClick += (_, _) => RestoreFromTray();
            _Tray.BalloonTipClicked += (_, _) => RestoreFromTray();
            FormClosed += (_, _) => { _Tray.Visible = false; _Tray.Dispose(); };
        }
        internal void RestoreFromTray()
        {
            Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            ShowInTaskbar = true; Activate(); BringToFront();
        }
        internal void ExitApplication()
        {
            _ExitRequested = true; Close();
            if (!IsDisposed) _ExitRequested = false;
        }
        private bool HideInTray(FormClosingEventArgs e)
        {
            if (_ExitRequested || e.CloseReason != CloseReason.UserClosing) return false;
            e.Cancel = true; Hide(); ShowInTaskbar = false;
            if (!_TrayHintShown)
            {
                _TrayHintShown = true;
                _Tray.ShowBalloonTip(3000, "Better Achievements reste actif", "Vos commandes et minuteurs continuent. Double-cliquez sur cette icône pour rouvrir, ou choisissez Quitter complètement.", ToolTipIcon.Info);
            }
            return true;
        }
    }
}
