using System;

namespace Starfall.Core.Map.Coordinates
{
    /// <summary>
    /// 地图尺寸（doc2 MAP-01 §4.2）。
    /// 约束：Width 1..48，Height 1..64。最大值 48×64×2 = 6144 格（双层）。
    ///
    /// 与 MVP 的 <see cref="Starfall.Core.Model.BoardState"/>（Width/Height ≤ 255）共存，
    /// 但 doc2 的地图系统采用更严格的上限，便于确定性哈希和序列化。
    ///
    /// TileCount 计算包含双层（Reality + Astral），用于预算网格分配的内存峰值。
    /// </summary>
    public readonly struct MapSize : IEquatable<MapSize>
    {
        public const int MinWidth = 1;
        public const int MaxWidth = 48;
        public const int MinHeight = 1;
        public const int MaxHeight = 64;

        public readonly int Width;
        public readonly int Height;

        public MapSize(int width, int height)
        {
            if (width < MinWidth || width > MaxWidth)
                throw new ArgumentOutOfRangeException(nameof(width), width,
                    $"Width must be in [{MinWidth}, {MaxWidth}] (doc2 MAP-01 §4.2).");
            if (height < MinHeight || height > MaxHeight)
                throw new ArgumentOutOfRangeException(nameof(height), height,
                    $"Height must be in [{MinHeight}, {MaxHeight}] (doc2 MAP-01 §4.2).");
            Width = width;
            Height = height;
        }

        /// <summary>最小地图：1×1（双层共 2 格）。</summary>
        public static MapSize Min => new MapSize(MinWidth, MinHeight);

        /// <summary>最大地图：48×64（双层共 6144 格）。</summary>
        public static MapSize Max => new MapSize(MaxWidth, MaxHeight);

        /// <summary>双层总格数 = Width × Height × 2。</summary>
        public int TileCount
        {
            get
            {
                // 用 long 中间值避免 (48 * 64 * 2) 虽然不会溢出 int，但表达更安全。
                long count = (long)Width * Height * 2;
                return (int)count;
            }
        }

        public bool Equals(MapSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is MapSize other && Equals(other);

        public override int GetHashCode() => unchecked((Width * 397) ^ Height);

        public static bool operator ==(MapSize a, MapSize b) => a.Equals(b);

        public static bool operator !=(MapSize a, MapSize b) => !a.Equals(b);

        public override string ToString() => $"{Width}x{Height}";
    }
}