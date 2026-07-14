using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 占位类型 + MAP-09 区域定义雏形。
    ///
    /// <para/>
    /// MAP-02 仅要求 MapState 持有 <c>Regions</c> 集合并能正确排序（按 RegionId），
    /// 完整 14 类区域语义在 MAP-09 实现。这里给出最小可用字段集，使集合克隆 / 哈希
    /// / 序列化路径在 MAP-02 阶段即可对齐 doc2 §3.4 验收标准。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><c>RegionId</c>：唯一 ID（>=0），用于确定性排序与哈希。</item>
    /// <item><c>RegionType</c>：doc2 §21.3 14 类区域之一（字符串占位；MAP-09 引入枚举）。</item>
    /// <item><c>Owner</c>：归属方（"Player" / "Enemy" / "Neutral"）；MVP 与 AnchorZone 对齐。</item>
    /// <item><c>TileCoords</c>：区域包含的格子（按 GridCoord.CompareTo 排序）。</item>
    /// </list>
    /// </summary>
    public sealed class MapRegion
    {
        public int RegionId { get; }
        public string RegionType { get; }
        public string Owner { get; }
        public IReadOnlyList<GridCoord> TileCoords { get; }

        public MapRegion(int regionId, string regionType, string owner, IEnumerable<GridCoord> tileCoords)
        {
            if (regionId < 0)
                throw new ArgumentException("RegionId must be >= 0", nameof(regionId));
            if (regionType == null)
                throw new ArgumentNullException(nameof(regionType));
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (tileCoords == null)
                throw new ArgumentNullException(nameof(tileCoords));

            RegionId = regionId;
            RegionType = regionType;
            Owner = owner;
            var list = new List<GridCoord>(tileCoords);
            list.Sort(); // GridCoord.CompareTo: Y → X → Layer
            TileCoords = list;
        }

        public override string ToString()
            => $"MapRegion(Id={RegionId}, Type={RegionType}, Owner={Owner}, Tiles={TileCoords.Count})";
    }
}
