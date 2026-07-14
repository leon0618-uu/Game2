using System;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.4 单一地块的不可变定义（immutable readonly struct）。
    ///
    /// <para/>
    /// 每个 tile 唯一对应一个 <see cref="TileDefinition"/>（由 <c>TileId</c> 标识）。
    /// 字段语义：
    /// <list type="bullet">
    /// <item><see cref="TileId"/>：>= 1 的正整数，唯一标识。</item>
    /// <item><see cref="Coord"/>：地块的三维坐标（含 <see cref="DimensionLayer"/>）。</item>
    /// <item><see cref="TerrainType"/>：<see cref="TerrainType"/> 枚举值。</item>
    /// <item><see cref="Terrain"/>：对应的 <see cref="TerrainDefinition"/> 实例（由
    ///       <see cref="TerrainRegistry.GetStandard"/> 提供）。</item>
    /// <item><see cref="Height"/>：高度等级（来自 MAP-06 <see cref="HeightLevel"/>）。</item>
    /// <item><see cref="BaseMoveCost"/>：移动成本（默认 = <see cref="TerrainDefinition.BaseMoveCost"/>，
    ///       可被覆盖以表达"此地特殊地形"的玩法差异）。</item>
    /// <item><see cref="BlocksMovement"/>：阻挡移动（默认 = <see cref="TerrainDefinition.BlocksMovement"/>）。</item>
    /// <item><see cref="BlocksVision"/>：阻挡视线（默认 = <see cref="TerrainDefinition.BlocksVision"/>）。</item>
    /// <item><see cref="BlocksProjectile"/>：阻挡弹道（默认 = <see cref="TerrainDefinition.BlocksProjectile"/>）。</item>
    /// <item><see cref="CoverLevel"/>：掩体等级（默认 = <see cref="TerrainDefinition.CoverLevel"/>）。</item>
    /// <item><see cref="CoverDirections"/>：掩体方向（默认 = <see cref="TerrainDefinition.CoverDirections"/>）。</item>
    /// <item><see cref="PhasePairTileId"/>：跨层配对 tile id（MAP-07 使用；本轮所有 tile 都为 null）。</item>
    /// <item><see cref="Tags"/>：附加 <see cref="TileTags"/> 位掩码标签。</item>
    /// </list>
    ///
    /// <para/>
    /// **不变量**（构造时强制）：
    /// <list type="number">
    /// <item><see cref="TileId"/> >= 1（0 视为无效；与 doc2 MAP-02 MapObjectId 区间对齐）。</item>
    /// <item><see cref="BaseMoveCost"/> ∈ [<see cref="TerrainDefinition.MinMoveCost"/>,
    ///       <see cref="TerrainDefinition.MaxMoveCost"/>]。</item>
    /// <item><see cref="PhasePairTileId"/> 若非 null，必须 >= 1。</item>
    /// </list>
    ///
    /// <para/>
    /// **不可变性**：本类型是 <c>readonly struct</c>，字段全部 <c>readonly</c>；
    /// 运行时状态（稳定性、占用信息）由 <see cref="MapTileState"/> 持有。
    /// </summary>
    public readonly struct TileDefinition : IEquatable<TileDefinition>
    {
        public readonly int TileId;
        public readonly GridCoord Coord;
        public readonly TerrainType TerrainType;
        public readonly TerrainDefinition Terrain;
        public readonly HeightLevel Height;
        public readonly int BaseMoveCost;
        public readonly bool BlocksMovement;
        public readonly bool BlocksVision;
        public readonly bool BlocksProjectile;
        public readonly CoverLevel CoverLevel;
        public readonly CoverDirection CoverDirections;
        public readonly int? PhasePairTileId;
        public readonly TileTags Tags;

        public TileDefinition(
            int tileId,
            GridCoord coord,
            TerrainType terrainType,
            TerrainDefinition terrain,
            HeightLevel height = default,
            int? baseMoveCost = null,
            bool? blocksMovement = null,
            bool? blocksVision = null,
            bool? blocksProjectile = null,
            CoverLevel? coverLevel = null,
            CoverDirection? coverDirections = null,
            int? phasePairTileId = null,
            TileTags tags = TileTags.None)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1 (0 reserved for 'no tile').");
            int moveCost = baseMoveCost ?? terrain.BaseMoveCost;
            if (moveCost < TerrainDefinition.MinMoveCost || moveCost > TerrainDefinition.MaxMoveCost)
                throw new ArgumentOutOfRangeException(nameof(baseMoveCost), moveCost,
                    $"BaseMoveCost must be in [{TerrainDefinition.MinMoveCost}, {TerrainDefinition.MaxMoveCost}] (doc2 MAP-04 §4.4).");
            if (phasePairTileId.HasValue && phasePairTileId.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(phasePairTileId), phasePairTileId.Value,
                    "PhasePairTileId must be >= 1 when provided (or null).");

            TileId = tileId;
            Coord = coord;
            TerrainType = terrainType;
            Terrain = terrain;
            Height = height;
            BaseMoveCost = moveCost;
            BlocksMovement = blocksMovement ?? terrain.BlocksMovement;
            BlocksVision = blocksVision ?? terrain.BlocksVision;
            BlocksProjectile = blocksProjectile ?? terrain.BlocksProjectile;
            CoverLevel = coverLevel ?? terrain.CoverLevel;
            CoverDirections = coverDirections ?? terrain.CoverDirections;
            PhasePairTileId = phasePairTileId;
            Tags = tags;
        }

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(TileDefinition other)
            => TileId == other.TileId
               && Coord == other.Coord
               && TerrainType == other.TerrainType
               && Terrain == other.Terrain
               && Height == other.Height
               && BaseMoveCost == other.BaseMoveCost
               && BlocksMovement == other.BlocksMovement
               && BlocksVision == other.BlocksVision
               && BlocksProjectile == other.BlocksProjectile
               && CoverLevel == other.CoverLevel
               && CoverDirections == other.CoverDirections
 && PhasePairTileId == other.PhasePairTileId
               && Tags == other.Tags;

        public override bool Equals(object obj) => obj is TileDefinition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = TileId;
                h = (h * 397) ^ Coord.GetHashCode();
                h = (h * 397) ^ (int)TerrainType;
                h = (h * 397) ^ Terrain.GetHashCode();
                h = (h * 397) ^ Height.GetHashCode();
                h = (h * 397) ^ BaseMoveCost;
                h = (h * 397) ^ (BlocksMovement ? 1 : 0);
                h = (h * 397) ^ (BlocksVision ? 1 : 0);
                h = (h * 397) ^ (BlocksProjectile ? 1 : 0);
                h = (h * 397) ^ (int)CoverLevel;
                h = (h * 397) ^ (int)CoverDirections;
                h = (h * 397) ^ (PhasePairTileId ?? 0);
                h = (h * 397) ^ (int)Tags;
                return h;
            }
        }

        public static bool operator ==(TileDefinition a, TileDefinition b) => a.Equals(b);

        public static bool operator !=(TileDefinition a, TileDefinition b) => !a.Equals(b);

        public override string ToString()
            => $"TileDef(Id={TileId}, Coord={Coord}, Terrain={TerrainType}, H={Height.Value}, Move={BaseMoveCost}, BlockMv={BlocksMovement}, BlockVis={BlocksVision}, BlockPrj={BlocksProjectile}, Cover={CoverLevel}, Tags={Tags})";
    }
}