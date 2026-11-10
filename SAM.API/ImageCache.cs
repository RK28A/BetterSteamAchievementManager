using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAM.API
{
    // Shared disk cache; in-flight requests are deduplicated within each process.
    internal sealed class ImageCache
    {
        internal static readonly ImageCache Shared = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetterSteamAchievementManager", "images-v1"));
        private static readonly HttpClient Http = new(new HttpClientHandler { MaxConnectionsPerServer = 4 }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 6 * 1024 * 1024 };
        private readonly string _Folder;
        private readonly SemaphoreSlim _Slots;
        private readonly Func<string, Task<byte[]>> _Fetch;
        private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _Pending = new(StringComparer.Ordinal);
        internal ImageCache(string folder, Func<string, Task<byte[]>> fetch = null, int concurrency = 4)
        {
            _Folder = folder; _Slots = new SemaphoreSlim(concurrency); _Fetch = fetch ?? (url => Http.GetByteArrayAsync(url));
        }
        internal string FilePath(string url)
        {
            using var hash = SHA256.Create();
            return Path.Combine(_Folder, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "") + ".img");
        }
        internal async Task<byte[]> Get(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https") return null;
            var pending = _Pending.GetOrAdd(url, key => new Lazy<Task<byte[]>>(() => Load(key), LazyThreadSafetyMode.ExecutionAndPublication));
            try { return await pending.Value.ConfigureAwait(false); }
            finally { if (_Pending.TryGetValue(url, out var current) && ReferenceEquals(current, pending)) _Pending.TryRemove(url, out _); }
        }
        private static bool Valid(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 6 * 1024 * 1024) return false;
            try { using var stream = new MemoryStream(bytes); using var image = Image.FromStream(stream, false, true); return image.Width <= 4096 && image.Height <= 4096; }
            catch (Exception e) when (e is ArgumentException || e is OutOfMemoryException) { return false; }
        }
        private async Task<byte[]> Load(string url)
        {
            return await Task.Run(async () =>
            {
                string path = FilePath(url);
                try
                {
                    if (File.Exists(path))
                    {
                        var cached = File.ReadAllBytes(path);
                        if (Valid(cached))
                        {
                            try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                            return cached;
                        }
                        File.Delete(path);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                await _Slots.WaitAsync().ConfigureAwait(false);
                try
                {
                    var bytes = await _Fetch(url).ConfigureAwait(false);
                    if (!Valid(bytes)) return null;
                    string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        Directory.CreateDirectory(_Folder); File.WriteAllBytes(temp, bytes);
                        if (!File.Exists(path)) File.Move(temp, path);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                    finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                    return bytes;
                }
                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is IOException) { return null; }
                finally { _Slots.Release(); }
            }).ConfigureAwait(false);
        }
        internal void Trim()
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_Folder)) return;
                    long total = 0;
                    foreach (var file in new DirectoryInfo(_Folder).EnumerateFiles("*.img").OrderByDescending(f => f.LastAccessTimeUtc))
                    { total += file.Length; if (total > 256L * 1024 * 1024) try { file.Delete(); } catch (IOException) { } }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            });
        }
    }
}
