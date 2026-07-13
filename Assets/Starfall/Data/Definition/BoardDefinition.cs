using System.Collections.Generic;

namespace Starfall.Data.Definition
{
    public sealed class BoardDefinition
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<TileEntry> Tiles { get; set; } = new List<TileEntry>();
    }

    public sealed class TileEntry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string State { get; set; } = "Normal";  // Normal/Blocked/Hazard/Objective
    }
}