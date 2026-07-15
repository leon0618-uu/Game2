using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Map.Tile.PhasePair;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 单 tile 相位翻转命令（MAP-08 核心玩法）。
    /// <para/>
    /// **契约**（公开签名不变；MAP-07 重写内部使用 <see cref="MapTileState.ActiveDimension"/> +
    /// <see cref="PhasePairLookup"/>）：
    /// <list type="bullet">
    /// <item>目标：在 <see cref="MapState"/> 上找到 <see cref="TileDefinition"/>，
    ///       校验 PhaseFlippable / !PhaseLocked 标签，把该 tile 的"激活层"
    ///       通过 <see cref="PhaseFlipStateService.SetActiveDimension"/>
    ///       写到 <see cref="MapTileState.ActiveDimension"/> 字段。</item>
    /// <item>**双层配对 cascade**：若该 tile 通过 <see cref="PhasePairLookup.TryGetPair"/>
    ///       找到配对 tile 且配对 tile 也通过 PhaseFlipStateService，cascade 调用
    ///       <see cref="PhaseFlipStateService.SetActiveDimension"/> 同步翻转配对。</item>
    /// </list>
    /// <para/>
    /// **MAP-07 升级**：命令内部从旧的 `PhaseFlipState.SetFlippedLayer` 字典
    /// 改为 <see cref="PhaseFlipStateService.SetActiveDimension"/>（直接修改
    /// <see cref="MapTileState.ActiveDimension"/> 字段）。这使 flip 状态由
    /// tile 自带 — 配合 <see cref="MapStateCloner"/> 深拷贝可完美 rehydrate。
    /// <para/>
    /// **失败条件**（任一即返回 <see cref="MapCommandResult.Fail"/>）：
    /// <list type="bullet">
    /// <item><paramref name="tileId"/> 未在 <see cref="MapTileState"/> 注册。</item>
    /// <item>该 tile 上 <c>PhaseLocked</c> 标签生效 → <c>"phase locked"</c>。</item>
    /// <item>该 tile 上无 <c>PhaseFlippable</c> 标签 → <c>"not phase flippable"</c>。</item>
    /// <item>该 tile 当前层已是 <see cref="TargetLayer"/> → <c>"already at target layer"</c>。</item>
    /// <item>未 attach runtime states（MAP-07 写前置条件）→ <c>"no runtime states attached"</c>。</item>
    /// </list>
    /// <para/>
    /// **影响面**：成功 → AffectedTiles = [tile.Coord, ...配对 tile Coord]（按 CompareTo 升序）。
    /// <para/>
    /// **不发业务事件**：map commands 不发 <see cref="Starfall.Core.Command.BattleEvent"/>。
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
        public MapCommandResult Execute(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

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

            // MAP-07：用 per-tile 字段读取当前层（fallback 字典 — 兼容 MAP-08 测试）。
            DimensionLayer currentLayer = map.ActiveLayer;
            PhaseFlipStateService.TryGetActiveDimension(map, TileId, out currentLayer);

            if (currentLayer == TargetLayer)
            {
                return MapCommandResult.Fail("already at target layer");
            }

            // 通过校验 → 写 ActivationDimension 字段（fallback 字典）。
            PhaseFlipStateService.SetActiveDimension(map, TileId, TargetLayer);

            var affected = new List<GridCoord>(1) { def.Coord };

            // MAP-07 cascade：若存在配对 tile 且配对 tile 也通过 PhaseFlipStateService，
            // 同步 flip 配对 tile 到 TargetLayer。
            if (PhasePairLookup.TryGetPair(map, TileId, out var pairTileId))
            {
                if (registry.TryGetById(pairTileId, out var pairDef))
                {
                    DimensionLayer pairLayer = map.ActiveLayer;
                    PhaseFlipStateService.TryGetActiveDimension(map, pairTileId, out pairLayer);
                    if (pairLayer != TargetLayer)
                    {
                        PhaseFlipStateService.SetActiveDimension(map, pairTileId, TargetLayer);
                        affected.Add(pairDef.Coord);
                    }
                }
            }

            affected.Sort();
            return MapCommandResult.Ok(affected);
        }

        public override string ToString()
            => $"FlipTilePhaseCommand(TileId={TileId}, Target={TargetLayer})";
    }
}
