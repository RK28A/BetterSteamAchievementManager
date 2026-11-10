using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SAM.API;
using SAM.API.Orders;

internal static class ExperienceTests
{
    internal static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "BSAM-Experience-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            byte[] png;
            using (var image = new Bitmap(8, 8)) { using var memory = new MemoryStream(); image.Save(memory, ImageFormat.Png); png = memory.ToArray(); }
            int fetches = 0, active = 0, peak = 0;
            var cache = new ImageCache(folder, async url =>
            {
                Interlocked.Increment(ref fetches);
                int count = Interlocked.Increment(ref active);
                int previous;
                do { previous = peak; if (count <= previous) break; } while (Interlocked.CompareExchange(ref peak, count, previous) != previous);
                await Task.Delay(40).ConfigureAwait(false);
                Interlocked.Decrement(ref active); return png;
            });
            Task.WhenAll(Enumerable.Range(0, 12).Select(i => cache.Get("https://example.test/" + i))).GetAwaiter().GetResult();
            check(fetches == 12 && peak > 1 && peak <= 4, "artwork loads concurrently with a strict limit of four requests");
            int before = fetches;
            Task.WhenAll(Enumerable.Range(0, 8).Select(i => cache.Get("https://example.test/same"))).GetAwaiter().GetResult();
            check(fetches == before + 1, "simultaneous requests for one image share a single download");
            var reopened = new ImageCache(folder, _ => throw new InvalidOperationException("network must not be used"));
            check(reopened.Get("https://example.test/same").GetAwaiter().GetResult().Length == png.Length, "image cache survives app restart without another download");
            File.WriteAllText(cache.FilePath("https://example.test/same"), "corrupted");
            cache.Get("https://example.test/same").GetAwaiter().GetResult();
            check(fetches == before + 2, "corrupt cached image is replaced by a fresh download");
            var invalid = new ImageCache(folder, _ => Task.FromResult(new byte[] { 1, 2, 3 }));
            check(invalid.Get("https://example.test/bad").GetAwaiter().GetResult() == null && !File.Exists(invalid.FilePath("https://example.test/bad")), "invalid image data is not cached");
            var fixedTime = new PlaytimeSettings { Duration = new DelayRange { Minimum = 2, Maximum = 5, Unit = TimeUnit.Hours } };
            check(fixedTime.Sample(1) == TimeSpan.FromHours(2), "fixed GameTime ignores the random upper bound");
            fixedTime.Randomized = true;
            check(fixedTime.Sample(0.5) == TimeSpan.FromHours(3.5), "random GameTime samples inside the requested range");
            bool zero = false;
            try { new PlaytimeSettings { Duration = new DelayRange { Minimum = 0, Maximum = 0 } }.Sample(0); } catch (InvalidOperationException) { zero = true; }
            check(zero, "GameTime rejects zero duration before starting sessions");
            using var owner = Process.GetCurrentProcess();
            string statusPath = Path.Combine(folder, "time.json");
            string arguments = "620 --demo --playtime --seconds 1 --owner " + owner.Id + " --owner-start " + owner.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture) + " --status \"" + statusPath + "\"";
            using var worker = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"), arguments) { UseShellExecute = false, CreateNoWindow = true });
            if (!worker.WaitForExit(15000)) { worker.Kill(); throw new InvalidOperationException("GameTime worker failed to stop"); }
            var status = new JsonStore<PlaytimeStatus>(statusPath).Read();
            check(worker.ExitCode == 0 && status.State == "completed" && status.ElapsedSeconds >= 1 && status.ElapsedSeconds < 4, "GameTime simulation stops itself at its deadline without a visible game window");
            using var invalidOwner = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SAM.Game.exe"), arguments.Replace(owner.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), "1")) { UseShellExecute = false, CreateNoWindow = true });
            if (!invalidOwner.WaitForExit(15000)) { invalidOwner.Kill(); throw new InvalidOperationException("invalid owner worker did not exit"); }
            check(invalidOwner.ExitCode != 0 && new JsonStore<PlaytimeStatus>(statusPath).Read().State == "failed", "GameTime refuses a reused or mismatched owner process");
        }
        finally { Directory.Delete(folder, true); }
    }
}
