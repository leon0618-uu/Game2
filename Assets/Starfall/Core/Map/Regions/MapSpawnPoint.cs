using System;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 出生点（immutable readonly struct）。
    ///
    /// <para/>
    /// <b>语义</b>：一个具体的"出生格"，关联到某个 <see cref="MapRegionDefinition"/>；
    /// 通常由关卡数据生成（一个 PlayerDeployment 区域含 N 个 spawn 点）。
    ///
    /// <para/>
    /// 字段语义：
    /// <list type="bullet">
    /// <item><see cref="SpawnIdValue"/>：强类型 ID（>=0）。</item>
    /// <item><see cref="RegionIdValue"/>：所属 region 的 RegionId（>=0）。</item>
    /// <item><see cref="Coord"/>：出生格坐标（含 Layer）。</item>
    /// <item><see cref="OwnerSide"/>：归属方（-1 = 中立，0 = 玩家，1+ = 敌方）。</item>
    /// <item><see cref="Capacity"/>：可容纳单位数（默认 1；>1 = 多单位格）。</item>
    /// <item><see cref="Active"/>：是否可用（false = 已被占用 / 已禁用）。</item>
    /// </list>
    /// </summary>
    public readonly struct MapSpawnPoint : IEquatable<MapSpawnPoint>
    {
        public readonly SpawnId SpawnIdValue;
        public readonly int RegionIdValue;
        public readonly GridCoord Coord;
        public readonly int OwnerSide;
        public readonly int Capacity;
        public readonly bool Active;

        public MapSpawnPoint(
            SpawnId id,
            int regionId,
            GridCoord coord,
            int ownerSide,
            int capacity = 1,
            bool active = true)
        {
            if (id.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(id), id,
                    "MapSpawnPoint.id must be >= 0.");
            if (regionId < 0)
                throw new ArgumentOutOfRangeException(nameof(regionId), regionId,
                    "MapSpawnPoint.regionId must be >= 0.");
            if (ownerSide < -1)
                throw new ArgumentOutOfRangeException(nameof(ownerSide), ownerSide,
                    "OwnerSide must be >= -1 (-1 = Neutral).");
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                    "Capacity must be >= 1.");

            SpawnIdValue = id;
            RegionIdValue = regionId;
            Coord = coord;
            OwnerSide = ownerSide;
            Capacity = capacity;
            Active = active;
        }

        public int SpawnId => SpawnIdValue.Value;

        public bool Equals(MapSpawnPoint other)
            => SpawnIdValue == other.SpawnIdValue
               && RegionIdValue == other.RegionIdValue
               && Coord.Equals(other.Coord)
               && OwnerSide == other.OwnerSide
               && Capacity == other.Capacity
               && Active == other.Active;

        public override bool Equals(object obj) => obj is MapSpawnPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = SpawnIdValue.Value * 397;
                h = (h * 397) ^ RegionIdValue;
                h = (h * 397) ^ Coord.GetHashCode();
                h = (h * 397) ^ OwnerSide;
                h = (h * 397) ^ Capacity;
                h = (h * 397) ^ (Active ? 1 : 0);
                return h;
            }
        }

        public static bool operator ==(MapSpawnPoint a, MapSpawnPoint b) => a.Equals(b);
        public static bool operator !=(MapSpawnPoint a, MapSpawnPoint b) => !a.Equals(b);

        public override string ToString()
            => $"MapSpawnPoint(Id={SpawnIdValue}, Region={RegionIdValue}, {Coord}, Owner={OwnerSide}, Cap={Capacity}, Active={Active})";
    }
}