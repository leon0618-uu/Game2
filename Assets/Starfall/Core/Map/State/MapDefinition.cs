using System;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 §19.1 地图不可变定义（immutable）。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><c>MapId</c>：唯一标识（与 Data 层 <c>map_definitions.map_id</c> 对齐）；空串视为无效。</item>
    /// <item><c>Width</c>：地图 X 维宽度（>=1，与 <see cref="MapSize.MinWidth"/> 一致）。</item>
    /// <item><c>Height</c>：地图 Y 维高度（>=1）。</item>
    /// <item><c>InitialActiveLayer</c>：开局激活维度（Reality / Astral）。</item>
    /// <item><c>InitialGlobalCollapseValue</c>：开局全局坍塌值（doc1 §13.1 范围 0..100）。</item>
    /// <item><c>TilesetId</c>：可选地表美术 ID，留空表示使用默认 tileset。</item>
    /// <item><c>EnvironmentScheduleId</c>：可选环境时间表 ID（doc1 §15.1），留空表示无 schedule。</item>
    /// </list>
    ///
    /// <para/>
    /// 本结构是 <c>readonly struct</c>：字段全部 <c>readonly</c>，不持有任何可变集合引用，
    /// 满足 doc2 MAP-02 "immutable readonly struct" 要求。任何运行时修改都必须通过
    /// <see cref="MapState"/> 与对应 Command（MAP-03 引入）。
    ///
    /// <para/>
    /// 序列化约定：字段顺序固定，UTF-8 + LE 字节写入由 <see cref="MapStateHasher"/> 实现。
    /// </summary>
    public readonly struct MapDefinition : IEquatable<MapDefinition>
    {
        public const int MinWidth = MapSize.MinWidth;
        public const int MaxWidth = MapSize.MaxWidth;
        public const int MinHeight = MapSize.MinHeight;
        public const int MaxHeight = MapSize.MaxHeight;

        /// <summary>doc1 §13.1 全局 CV 范围下界（含）。</summary>
        public const int MinGlobalCollapseValue = 0;

        /// <summary>doc1 §13.1 全局 CV 范围上界（含）。</summary>
        public const int MaxGlobalCollapseValue = 100;

        public readonly string MapId;
        public readonly int Width;
        public readonly int Height;
        public readonly DimensionLayer InitialActiveLayer;
        public readonly int InitialGlobalCollapseValue;
        public readonly string TilesetId;
        public readonly string EnvironmentScheduleId;

        public MapDefinition(
            string mapId,
            int width,
            int height,
            DimensionLayer initialActiveLayer,
            int initialGlobalCollapseValue,
            string tilesetId = null,
            string environmentScheduleId = null)
        {
            if (mapId == null)
                throw new ArgumentNullException(nameof(mapId));
            // MapId 允许空串作为"未指定"，但显式 null 视为构造错误。
            if (width < MinWidth || width > MaxWidth)
                throw new ArgumentOutOfRangeException(nameof(width), width,
                    $"Width must be in [{MinWidth}, {MaxWidth}] (doc2 MAP-01 §4.2).");
            if (height < MinHeight || height > MaxHeight)
                throw new ArgumentOutOfRangeException(nameof(height), height,
                    $"Height must be in [{MinHeight}, {MaxHeight}] (doc2 MAP-01 §4.2).");
            if (initialGlobalCollapseValue < MinGlobalCollapseValue ||
                initialGlobalCollapseValue > MaxGlobalCollapseValue)
                throw new ArgumentOutOfRangeException(nameof(initialGlobalCollapseValue),
                    initialGlobalCollapseValue,
                    $"InitialGlobalCollapseValue must be in [{MinGlobalCollapseValue}, {MaxGlobalCollapseValue}] (doc1 §13.1).");

            MapId = mapId;
            Width = width;
            Height = height;
            InitialActiveLayer = initialActiveLayer;
            InitialGlobalCollapseValue = initialGlobalCollapseValue;
            // 可选 ID：null 归一化为空串以保证哈希稳定（避免 null/empty 抖动）。
            TilesetId = tilesetId ?? string.Empty;
            EnvironmentScheduleId = environmentScheduleId ?? string.Empty;
        }

        /// <summary>派生尺寸（MapSize 同样承担越界检查，这里只是投影）。</summary>
        public MapSize Size => new MapSize(Width, Height);

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(MapDefinition other)
            => string.Equals(MapId, other.MapId)
               && Width == other.Width
               && Height == other.Height
               && InitialActiveLayer == other.InitialActiveLayer
               && InitialGlobalCollapseValue == other.InitialGlobalCollapseValue
               && string.Equals(TilesetId, other.TilesetId)
               && string.Equals(EnvironmentScheduleId, other.EnvironmentScheduleId);

        public override bool Equals(object obj) => obj is MapDefinition other && Equals(other);

        public override int GetHashCode()
        {
            // 与 GetHashCode 行为一致：避开 object/string.GetHashCode 的不稳定实现，
            // 这里只用 int 字段组合 + MapId 串的稳定 hash。
            unchecked
            {
                int h = (MapId?.GetHashCode() ?? 0);
                h = (h * 397) ^ Width;
                h = (h * 397) ^ Height;
                h = (h * 397) ^ (int)InitialActiveLayer;
                h = (h * 397) ^ InitialGlobalCollapseValue;
                h = (h * 397) ^ (TilesetId?.GetHashCode() ?? 0);
                h = (h * 397) ^ (EnvironmentScheduleId?.GetHashCode() ?? 0);
                return h;
            }
        }

        public static bool operator ==(MapDefinition a, MapDefinition b) => a.Equals(b);

        public static bool operator !=(MapDefinition a, MapDefinition b) => !a.Equals(b);

        public override string ToString()
            => $"MapDefinition(Id={MapId}, {Width}x{Height}, Layer={InitialActiveLayer}, CV={InitialGlobalCollapseValue}, Tileset={TilesetId}, Schedule={EnvironmentScheduleId})";
    }
}
