using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 单 tile 相位翻转命令（MAP-08 核心玩法）。
    /// <para/>
    /// **契约**：
    /// <list type="bullet">
    /// <item>目标：在 <see cref="MapState"/> 上找到 <see cref="TileDefinition"/>，
    ///       校验 PhaseFlippable / !PhaseLocked 标签，把该 tile 的"激活层"
    ///       在 <see cref="MapState.ActiveLayer"/> 之外移到 <see cref="TargetLayer"/>。
    ///       本轮（MAP-08）"激活层"以一个 MAP-07 引入的 per-tile 字段表达：
    ///       借助 <see cref="MapTileState"/> 的额外字段 <c>ActiveDimension</c>。但
    ///       <see cref="MapTileState"/> 在 MAP-04 已冻结，故本轮引入最小副作用：
    ///       通过 <see cref="MapState.AddTile"/> 路径嵌入 <see cref="MapState"/> 的
    ///       <c>PhaseFlippedTiles</c> 字典（MAP-03 已冻结的容器不可能改）。
    ///       因此本命令采用「MAP-07 stub」语义：
    ///       **成功 = 校验通过并把 tileId 记录到 <see cref="MapState.ActiveLayer"/>
    ///       的临时 side-effect**。
    ///       完整 per-tile ActiveDimension 状态留待 MAP-07 接入后整体刷新。</item>
    /// </list>
    /// <para/>
    /// **MAP-08 妥协方案**：本轮使用 <see cref="MapState"/> 上一个临时 side-effect
    /// 字典（通过静态服务 + 字典 attach 模式，类似 <see cref="TileOccupancyService"/>），
    /// 完整 per-tile ActiveDimension 字段由 MAP-07 引入后再迁移。本命令的
    /// "ActiveDimension" 状态表达为：成功翻转后该 tile 可被其它命令识别为
    /// <see cref="TargetLayer"/> 层上的激活 tile —— 这对 <see cref="FallResolutionService"/>
    /// 决定 "能否落" 已经足够（不再依赖 <see cref="MapState.ActiveLayer"/> 单值）。
    /// <para/>
    /// **失败条件**（任一即返回 <see cref="MapCommandResult.Fail"/>）：
    /// <list type="bullet">
    /// <item><paramref name="tileId"/> 未在 <see cref="MapTileState"/> 注册。</item>
    /// <item>该 tile 上 <c>PhaseLocked</c> 标签生效 → <c>"phase locked"</c>。</item>
    /// <item>该 tile 上无 <c>PhaseFlippable</c> 标签 → <c>"not phase flippable"</c>。</item>
    /// <item>该 tile 当前层已是 <see cref="TargetLayer"/> → <c>"already at target layer"</c>。</item>
    /// </list>
    /// <para/>
    /// **影响面**：成功 → AffectedTiles = [tile.Coord]（按 CompareTo 升序，单元素已是序）。
    /// <para/>
    /// **不发业务事件**：map commands 不发 <see cref="Starfall.Core.Command.BattleEvent"/>，
    /// 上层 <c>BattleRunner</c> 在成功执行后注入事件流。
    /// </summary>
    public sealed class FlipTilePhaseCommand : IMapCommand
    {
        /// <summary><see cref="TileDefinition.TileId"/>（>= 1）。</summary>
        public int TileId { get; }

        /// <summary>切换到的目标维度（<see cref="DimensionLayer.Reality"/> 或 <see cref="DimensionLayer.Astral"/>）。</summary>
        public DimensionLayer TargetLayer { get; }

        public FlipTilePhaseCommand(int tileId, DimensionLayer targetLayer)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1 (0 reserved for 'no tile').");
            TileId = tileId;
            TargetLayer = targetLayer;
        }

        /// <summary>
        /// 在 <paramref name="map"/> 上执行相位翻转。
        /// </summary>
        /// <remarks>
        /// **实现细节**：
        /// <list type="bullet">
        /// <item>从 <see cref="PhaseFlipStateService"/>（attach 模式）取出该 map 的
        ///       per-tile 当前激活层。首次访问的 tile = <see cref="MapState.ActiveLayer"/>。</item>
        /// <item>校验 PhaseLocked / PhaseFlippable / 当前层。</item>
        /// <item>通过：把该 tile 写入 phase flip 状态字典 + 返回 <see cref="MapCommandResult.Ok"/>。</item>
        /// </list>
        /// </remarks>
        public MapCommandResult Execute(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            var phaseState = PhaseFlipStateService.GetOrAttach(map);
            var registry = PhaseFlipStateService.GetAttachedRegistry(map);

            if (registry == null)
            {
                return MapCommandResult.Fail("no tile registry attached");
            }

            if (!registry.TryGetById(TileId, out var def))
            {
                return MapCommandResult.Fail("tile not found");
            }

            // 任一阻挡条件：先校验再写。
            if ((def.Tags & TileTags.PhaseLocked) != 0)
            {
                return MapCommandResult.Fail("phase locked");
            }

            if ((def.Tags & TileTags.PhaseFlippable) == 0)
            {
                return MapCommandResult.Fail("not phase flippable");
            }

            DimensionLayer currentLayer = phaseState.TryGetFlippedLayer(TileId, out var cur)
                ? cur
                : map.ActiveLayer;

            if (currentLayer == TargetLayer)
            {
                return MapCommandResult.Fail("already at target layer");
            }

            // 通过校验 → 写翻转状态。
            phaseState.SetFlippedLayer(TileId, TargetLayer);

            var affected = new List<GridCoord>(1) { def.Coord };
            return MapCommandResult.Ok(affected);
        }

        public override string ToString()
            => $"FlipTilePhaseCommand(TileId={TileId}, Target={TargetLayer})";
    }
}
