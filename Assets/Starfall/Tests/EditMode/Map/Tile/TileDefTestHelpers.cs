using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 测试 helper：构造 <see cref="TileDefinition"/> 的便捷工厂。
    /// 之所以需要：因为 MAP-04 冻结的 <see cref="TileDefinitionRegistry.Make"/>
    /// 不接受 <c>phasePairTileId</c> 参数（不在 MAP-04 范围内）；本 helper 提供
    /// 一个 "Make" + pair 参数的等价物，专供 MAP-07 测试 fixture 使用。
    /// </summary>
    public static class TileDefTestHelpers
    {
        /// <summary>等同 <see cref="TileDefinitionRegistry.Make"/>，但带 <c>phasePairTileId</c>。</summary>
        public static TileDefinition MakePair(
            int tileId,
            GridCoord coord,
            TerrainType terrainType,
            HeightLevel height = default,
            int? baseMoveCost = null,
            bool? blocksMovement = null,
            bool? blocksVision = null,
            bool? blocksProjectile = null,
            TileTags tags = TileTags.None,
            int? phasePairTileId = null)
        {
            var terrain = TerrainRegistry.GetStandard(terrainType);
            return new TileDefinition(
                tileId: tileId,
                coord: coord,
                terrainType: terrainType,
                terrain: terrain,
                height: height,
                baseMoveCost: baseMoveCost,
                blocksMovement: blocksMovement,
                blocksVision: blocksVision,
                blocksProjectile: blocksProjectile,
                coverLevel: null,
                coverDirections: null,
                phasePairTileId: phasePairTileId,
                tags: tags);
        }
    }
}
