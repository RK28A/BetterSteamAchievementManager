using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace SAM.API.Orders
{
    internal sealed class JsonStore<T> where T : new()
    {
        internal readonly string Path;
        private readonly string _Mutex;
        public JsonStore(string path)
        {
            Path = System.IO.Path.GetFullPath(path);
            using var sha = SHA256.Create();
            _Mutex = "Local\\BSAM-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.ToUpperInvariant()))).Replace("-", "").Substring(0, 24);
        }
        internal T Read() => Update(null);
        internal T Update(Action<T> change)
        {
            using var gate = new Mutex(false, _Mutex);
            bool acquired;
            try { acquired = gate.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new IOException("Le panier est occupé. Réessayez.");
            try
            {
                var json = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
                T data;
                try { data = File.Exists(Path) ? json.Deserialize<T>(File.ReadAllText(Path, Encoding.UTF8)) : new T(); }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                { throw new InvalidDataException("Le fichier de panier est illisible. Une copie intacte est conservée sur disque.", e); }
                if (data == null) throw new InvalidDataException("Le fichier de panier est vide.");
                if (data is BackgroundState background) BackgroundPlanning.Validate(background);
                if (data is OrderCart cart)
                {
                    if (cart.Version != 1 || cart.Games == null || cart.GameDelay == null || cart.Games.Any(g => g == null || g.GameId == 0 || g.Delay == null || g.Achievements == null || g.Achievements.Any(a => a == null || string.IsNullOrWhiteSpace(a.Id))))
                        throw new InvalidDataException("Le format du panier est invalide ou incompatible.");
                    cart.History ??= new();
                    if (cart.Checkpoint != null && (double.IsNaN(cart.Checkpoint.RemainingSeconds) || double.IsInfinity(cart.Checkpoint.RemainingSeconds) || cart.Checkpoint.RemainingSeconds < 0 || cart.Checkpoint.RemainingSeconds > TimeSpan.FromDays(30).TotalSeconds))
                        throw new InvalidDataException("Le point de reprise est invalide.");
                    try { cart.GameDelay.Sample(0); foreach (var game in cart.Games) game.Delay.Sample(0); }
                    catch (InvalidOperationException e) { throw new InvalidDataException("Les délais du panier sont invalides.", e); }
                }
                if (change != null)
                {
                    change(data);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    string temp = Path + ".tmp";
                    File.WriteAllText(temp, json.Serialize(data), new UTF8Encoding(false));
                    if (File.Exists(Path)) File.Replace(temp, Path, Path + ".bak"); else File.Move(temp, Path);
                }
                return data;
            }
            finally { gate.ReleaseMutex(); }
        }
    }
    internal static class OrderPaths
    {
        public static string Cart => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterSteamAchievementManager", "achievement-order.json");
        public static void EnsureEditable(OrderCart cart)
        {
            if (cart.IsRunning) throw new InvalidOperationException("Arrêtez Achievement Order avant de modifier le panier.");
        }
        public static int Add(JsonStore<OrderCart> store, OrderGroup incoming)
        {
            int added = 0;
            store.Update(cart =>
            {
                EnsureEditable(cart);
                cart.Checkpoint = null;
                var group = cart.Games.FirstOrDefault(g => g.GameId == incoming.GameId);
                if (group == null) { group = new OrderGroup { GameId = incoming.GameId, Name = incoming.Name, ImageUrl = incoming.ImageUrl }; cart.Games.Add(group); }
                foreach (var achievement in incoming.Achievements)
                {
                    if (string.IsNullOrWhiteSpace(achievement.Id) || group.Achievements.Any(a => a.Id == achievement.Id)) continue;
                    group.Achievements.Add(achievement); added++;
                }
                cart.Games.RemoveAll(g => g.Achievements.Count == 0);
            });
            return added;
        }
    }
}
