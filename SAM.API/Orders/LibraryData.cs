using System;
using System.Collections.Generic;

namespace SAM.API.Orders
{
    internal sealed class LibraryPreferences
    {
        public List<uint> Favorites { get; set; } = new();
        public bool FavoritesOnly { get; set; }
        public bool InstalledOnly { get; set; }
    }
    internal sealed class LibraryProgress
    {
        public List<GameProgress> Games { get; set; } = new();
    }
    internal sealed class GameProgress
    {
        public uint GameId { get; set; }
        public int Unlocked { get; set; }
        public int Total { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
