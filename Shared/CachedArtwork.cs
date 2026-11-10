using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SAM.UI
{
    // Only visible rows request images. Keep a bounded decoded cache and batch repaints.
    internal sealed class CachedArtwork : IDisposable
    {
        private readonly Dictionary<string, (Image Image, long Used)> _Images = new(StringComparer.Ordinal);
        private readonly HashSet<string> _Pending = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _Retry = new(StringComparer.Ordinal);
        private readonly Timer _Repaint = new() { Interval = 40 };
        private long _Clock;
        private bool _Disposed;
        internal event EventHandler Loaded;
        internal CachedArtwork() { _Repaint.Tick += (_, _) => { _Repaint.Stop(); if (!_Disposed) Loaded?.Invoke(this, EventArgs.Empty); }; }
        internal Image Get(string url)
        {
            if (_Disposed || string.IsNullOrEmpty(url)) return null;
            if (_Images.TryGetValue(url, out var item)) { _Images[url] = (item.Image, ++_Clock); return item.Image; }
            if (_Retry.TryGetValue(url, out var until) && until > DateTime.UtcNow) return null;
            if (_Pending.Add(url)) Load(url);
            return null;
        }
        private async void Load(string url)
        {
            try
            {
                var bytes = await API.ImageCache.Shared.Get(url);
                if (_Disposed) return;
                if (bytes == null) { _Retry[url] = DateTime.UtcNow.AddMinutes(1); return; }
                using var stream = new MemoryStream(bytes);
                using var decoded = Image.FromStream(stream);
                _Images[url] = (new Bitmap(decoded), ++_Clock);
                if (_Images.Count > 256)
                {
                    var oldest = _Images.OrderBy(p => p.Value.Used).First();
                    oldest.Value.Image.Dispose(); _Images.Remove(oldest.Key);
                }
                _Repaint.Start();
            }
            catch (Exception e) when (e is ArgumentException || e is OutOfMemoryException || e is IOException || e is UnauthorizedAccessException)
            { if (!_Disposed) _Retry[url] = DateTime.UtcNow.AddMinutes(1); }
            finally { _Pending.Remove(url); }
        }
        public void Dispose()
        {
            _Disposed = true; _Repaint.Dispose();
            foreach (var item in _Images.Values) item.Image.Dispose();
            _Images.Clear();
        }
    }
}
