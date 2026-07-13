using System.Collections.Generic;

namespace Starfall.Core.Model
{
    public sealed class GridPosComparer : IComparer<GridPos>
    {
        public static readonly GridPosComparer Instance = new GridPosComparer();
        public int Compare(GridPos a, GridPos b) => a.CompareTo(b);
    }
}
