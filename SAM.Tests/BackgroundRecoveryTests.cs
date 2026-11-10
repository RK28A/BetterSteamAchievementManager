using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SAM.API.Orders;
using SAM.Picker;

internal static class BackgroundRecoveryTests
{
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    private static void Call(object obj, string name) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, null);
    private static void Pump(int milliseconds) { var clock = Stopwatch.StartNew(); while (clock.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); } }
    internal static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "BSAM-Resume-" + Guid.NewGuid().ToString("N"));
        GamePicker first = null, second = null;
        try
        {
            first = new GamePicker(null, true, folder); first.Show();
            Field<HashSet<uint>>(first, "_TimeSelection").Add(620); Call(first, "StartGameTime"); Pump(2200);
            var state = Field<BackgroundState>(first, "_Background"); double before = state.Sessions[0].ElapsedSeconds;
            first.Close(); Pump(1800);
            check(!first.IsDisposed && !first.Visible && state.Sessions[0].ElapsedSeconds > before, "tray hiding keeps an active worker and its counter running");
            first.ExitApplication();
            var saved = new JsonStore<BackgroundState>(Path.Combine(folder, "background-sessions.json")).Read();
            double checkpoint = saved.Sessions[0].ElapsedSeconds;
            check(saved.Sessions[0].Resume && checkpoint > 0 && saved.Sessions[0].GoalSeconds == 3600, "explicit exit preserves the unfinished goal for a later launch");
            Pump(2200); // Simulate downtime: it must not be added to the checkpoint.
            second = new GamePicker(null, true, folder); second.Show();
            var recovered = Field<BackgroundState>(second, "_Background");
            check(recovered.Sessions[0].ElapsedSeconds == checkpoint, "reopening does not count time spent outside the application");
            Pump(2400);
            check(recovered.Sessions[0].ElapsedSeconds > checkpoint && recovered.Sessions[0].ElapsedSeconds < checkpoint + 4, "unfinished sessions automatically resume from their accumulated active time");
            check(Field<IDictionary>(second, "_TimeJobs").Count == 1, "recovery creates exactly one session for the saved game");
            Call(second, "StopAllGameTime"); second.ExitApplication();
            var stopped = new JsonStore<BackgroundState>(Path.Combine(folder, "background-sessions.json")).Read();
            check(!stopped.Sessions[0].Resume, "manual stop remains stopped after persistence");
        }
        finally
        {
            if (first != null && !first.IsDisposed) first.ExitApplication();
            if (second != null && !second.IsDisposed) second.ExitApplication();
            first?.Dispose(); second?.Dispose(); if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
