using System;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.7 旧 <see cref="Starfall.Core.Model.TileState"/> enum 到新
    /// <see cref="TileDefinition"/> 的桥接适配器。
    ///
    /// <para/>
    /// **背景**：doc1 MVP 的 <see cref="Starfall.Core.Model.BoardState"/> 仅支持
    /// 4 类 tile enum（Normal / Blocked / Hazard / Objective），由 179+ 既有测试使用。
    /// doc2 MAP-04 引入 11 类 <see cref="TerrainType"/> 与 22 个 <see cref="TileTags"/>；
    /// 旧 enum 需要在过渡期内被桥接，而不能直接删除。
    ///
    /// <para/>
    /// **映射规则**（与 doc2 §3.4 验收矩阵对齐）：
    /// <list type="table">
    /// <listheader><term>旧 enum</term><description>新 TileDefinition</description></listheader>
    /// <item><term><see cref="Starfall.Core.Model.TileState.Normal"/></term>
    ///       <description><see cref="TerrainType.Plain"/> + <see cref="TileTags.Walkable"/></description></item>
    /// <item><term><see cref="Starfall.Core.Model.TileState.Blocked"/></term>
    ///       <description><see cref="TerrainType.Wall"/> +
    ///       <see cref="TileTags.Impassable"/> + <see cref="TileTags.VisionBlocker"/> +
    ///       <see cref="TileTags.ProjectileBlocker"/>，<see cref="CoverLevel"/> = <see cref="CoverLevel.Full"/></description></item>
    /// <item><term><see cref="Starfall.Core.Model.TileState.Hazard"/></term>
    ///       <description><see cref="TerrainType.Plain"/> + <see cref="TileTags.Hazardous"/>，
    ///       <see cref="TerrainDefinition.HazardousDamagePerTurn"/> = 5</description></item>
    /// <item><term><see cref="Starfall.Core.Model.TileState.Objective"/></term>
    ///       <description><see cref="TerrainType.Plain"/> + <see cref="TileTags.GuardObjective"/></description></item>
    /// </list>
    ///
    /// <para/>
    /// **不可变性**：本适配器是纯静态方法，无副作用；多次调用相同输入产生相同输出。
    /// </summary>
    public static class LegacyTileStateAdapter
    {
        /// <summary>把旧 <see cref="Starfall.Core.Model.TileState"/> 转换为新 <see cref="TileDefinition"/>。</summary>
        /// <param name="legacy">旧 enum 值。</param>
        /// <param name="tileId">新 tile id（>= 1）。</param>
        /// <param name="coord">新 tile 坐标（含 Layer）。</param>
        /// <returns>对应的 <see cref="TileDefinition"/>。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="legacy"/> 非法值。</exception>
        public static TileDefinition ToTileDefinition(
            Starfall.Core.Model.TileState legacy,
            int tileId,
            GridCoord coord)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "tileId must be >= 1 (0 reserved for 'no tile').");

            switch (legacy)
            {
                case Starfall.Core.Model.TileState.Normal:
                    return TileDefinitionRegistry.Make(
                        tileId: tileId,
                        coord: coord,
                        terrainType: TerrainType.Plain,
                        tags: TileTags.Walkable);

                case Starfall.Core.Model.TileState.Blocked:
                    {
                        // Blocked → Wall + Full cover + 三个阻挡标签。
                        // Wall 标准 BaseMoveCost=99（不可通过的哨兵值）。
                        var terrain = TerrainRegistry.Wall;
                        return new TileDefinition(
                            tileId: tileId,
                            coord: coord,
                            terrainType: TerrainType.Wall,
                            terrain: terrain,
                            coverLevel: CoverLevel.Full,
                            tags: TileTags.Impassable | TileTags.VisionBlocker | TileTags.ProjectileBlocker);
                    }

                case Starfall.Core.Model.TileState.Hazard:
                    {
                        // Hazard → Plain + Hazardous tag + 每回合 5 伤害。
                        // 通过覆盖 BaseMoveCost 不变，但覆盖 hazardousDamagePerTurn 不在
                        // TileDefinition 字段集内；改由 MapTileState 持有；这里仅打标签。
                        // 注意：TerrainDefinition.HazardousDamagePerTurn 不可在 TileDefinition
                        // 构造时覆盖（仅 TerrainDefinition 自身持有），因此 Hazard 标签作为
                        // 唯一信号；具体的"每回合 5 伤害"在 MapTileState 或上层律令生效。
                        return TileDefinitionRegistry.Make(
                            tileId: tileId,
                            coord: coord,
                            terrainType: TerrainType.Plain,
                            tags: TileTags.Walkable | TileTags.Hazardous);
                    }

                case Starfall.Core.Model.TileState.Objective:
                    return TileDefinitionRegistry.Make(
                        tileId: tileId,
                        coord: coord,
                        terrainType: TerrainType.Plain,
                        tags: TileTags.Walkable | TileTags.GuardObjective);

                default:
                    throw new ArgumentOutOfRangeException(nameof(legacy), legacy,
                        $"Unknown legacy TileState value: {(byte)legacy}");
            }
        }
    }
}