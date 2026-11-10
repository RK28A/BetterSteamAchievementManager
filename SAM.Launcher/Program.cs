// Single-file distributor. The Windows application and dependencies are extracted locally.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

namespace SAM.Launcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                string fingerprint;
                using (var hash = SHA256.Create())
                using (var file = File.OpenRead(assembly.Location))
                    fingerprint = BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").Substring(0, 24);
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterSteamAchievementManager", "app-" + fingerprint);
                using (var mutex = new Mutex(false, "Local\\BSAM-Extract-" + fingerprint))
                {
                    bool acquired;
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(30)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Une autre instance prépare l’application. Réessayez.");
                    try
                    {
                        Directory.CreateDirectory(directory);
                        foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Payload.", StringComparison.Ordinal)))
                        {
                            string name = resource.Substring("Payload.".Length);
                            if (Path.GetFileName(name) != name) throw new IOException("Nom de ressource invalide.");
                            string destination = Path.Combine(directory, name);
                            using var content = assembly.GetManifestResourceStream(resource);
                            // Verify the content on every launch; don't execute a modified cache file.
                            bool matches = false;
                            if (File.Exists(destination))
                            {
                                using var sha = SHA256.Create();
                                using var existing = File.OpenRead(destination);
                                matches = sha.ComputeHash(content).SequenceEqual(sha.ComputeHash(existing));
                                content.Position = 0;
                            }
                            if (matches) continue;
                            string temporary = destination + ".tmp";
                            using (var output = File.Create(temporary)) content.CopyTo(output);
                            if (File.Exists(destination)) File.Delete(destination);
                            File.Move(temporary, destination);
                        }
                    }
                    finally { mutex.ReleaseMutex(); }
                }
                if (Array.IndexOf(args, "--extract-only") >= 0) return 0;
                var child = new ProcessStartInfo(Path.Combine(directory, "SAM.Picker.exe")) { UseShellExecute = false, WorkingDirectory = directory };
                if (Array.IndexOf(args, "--demo") >= 0) child.Arguments = "--demo";
                Process.Start(child);
                return 0;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Impossible de démarrer BetterSteamAchievementManager.\n" + e.Message, "Démarrage", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
