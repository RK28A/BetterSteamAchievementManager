using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using SAM.API.Orders;

internal static class PlaytimeTests
{
    internal static void Run(Action<bool, string> check)
    {
        var settings = new PlaytimeSettings { Duration = new DelayRange { Minimum = 2, Maximum = 5, Unit = TimeUnit.Hours } };
        check(settings.Sample(1) == TimeSpan.FromHours(2), "fixed GameTime uses exactly the configured duration");
        settings.Randomized = true;
        check(settings.Sample(0.5) == TimeSpan.FromHours(3.5), "random GameTime samples within the requested range");
        check(settings.Sample(0) == TimeSpan.FromHours(2) && settings.Sample(1) == TimeSpan.FromHours(5), "GameTime honors both random duration bounds");
        bool zero = false;
        try { new PlaytimeSettings { Duration = new DelayRange { Minimum = 0, Maximum = 0 } }.Sample(0); } catch (InvalidOperationException) { zero = true; }
        check(zero, "zero GameTime duration is rejected before starting processes");
        string folder = Path.Combine(Path.GetTempPath(), "BSAM-Playtime-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        using var owner = Process.GetCurrentProcess();
        Process Start(string file, int seconds, bool validOwner = true) => Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"),
            "620 --demo --playtime --seconds " + seconds + " --owner " + owner.Id + " --owner-start " + (validOwner ? owner.StartTime.ToUniversalTime().Ticks : 1).ToString(CultureInfo.InvariantCulture) + " --status \"" + Path.Combine(folder, file) + "\"") { UseShellExecute = false, CreateNoWindow = true });
        try
        {
            using (var timed = Start("timed.json", 1))
            {
                if (!timed.WaitForExit(15000)) { timed.Kill(); throw new InvalidOperationException("Timed worker did not stop"); }
                var status = new JsonStore<PlaytimeStatus>(Path.Combine(folder, "timed.json")).Read();
                check(timed.ExitCode == 0 && status.State == "completed" && status.ElapsedSeconds >= 1, "GameTime stops itself when its duration expires");
            }
            using (var invalid = Start("invalid.json", 1, false))
            {
                if (!invalid.WaitForExit(15000)) { invalid.Kill(); throw new InvalidOperationException("Invalid owner worker did not stop"); }
                check(invalid.ExitCode != 0 && new JsonStore<PlaytimeStatus>(Path.Combine(folder, "invalid.json")).Read().State == "failed", "GameTime refuses a mismatched owner process");
            }
            using var first = Start("first.json", 30); using var second = Start("second.json", 30);
            try
            {
                var one = new JsonStore<PlaytimeStatus>(Path.Combine(folder, "first.json")); var two = new JsonStore<PlaytimeStatus>(Path.Combine(folder, "second.json"));
                var wait = Stopwatch.StartNew();
                while (wait.Elapsed < TimeSpan.FromSeconds(15) && (one.Read().State != "running" || two.Read().State != "running")) Thread.Sleep(50);
                check(one.Read().State == "running" && two.Read().State == "running" && !first.HasExited && !second.HasExited, "multiple GameTime workers run simultaneously");
                first.Kill(); first.WaitForExit(5000);
                check(!second.HasExited, "stopping one GameTime session leaves the other session active");
            }
            finally
            {
                if (!first.HasExited) first.Kill(); if (!second.HasExited) second.Kill();
                first.WaitForExit(5000); second.WaitForExit(5000);
            }
        }
        finally { Directory.Delete(folder, true); }
    }
}
