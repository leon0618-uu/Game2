using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Map.Tile.PhasePair;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 / MAP-08 §6.1 单 tile 相位翻转命令。
    /// <para/>
    /// **粒度**（公开签名不变，MAP-03 升级内部使用 <see cref="MapTileState.ActiveDimension"/> +
    /// <see cref="PhasePairLookup"/>）：
    /// <list type="bullet">
    /// <item>目标：在 <see cref="MapState"/> 中找到 <see cref="TileDefinition"/>，
    ///       校验 PhaseFlippable / !PhaseLocked 标签，把该 tile 的"激活层"
    ///       通过 <see cref="PhaseFlipStateService.SetActiveDimension"/>
    ///       写到 <see cref="MapTileState.ActiveDimension"/> 字段。</item>
    /// <item>**双层配对 cascade**：若该 tile 通过 <see cref="PhasePairLookup.TryGetPair"/>
    ///       找到配对 tile 且配对 tile 也通过 PhaseFlipStateService，cascade 调用
    ///       <see cref="PhaseFlipStateService.SetActiveDimension"/> 同步翻转配对。</item>
    /// </list>
    /// <para/>
    /// **MAP-03 完整化**：
    /// <list type="bullet">
    /// <item>实现 <see cref="Undo(MapState)"/>：记录原 layer，Undo 时反向 flip。</item>
    /// <li><see cref="Version"/> = 2（MAP-03 升级一档）。</li>
    /// <li><see cref="CommandId"/> = <c>"flip-tile-phase:{TileId}"</c>。</item>
    /// <li><see cref="Dependencies"/> = 空（独立命令）。</item>
    /// <li>Emit <see cref="MapEventKind.OnTileChanged"/> 事件（含 cascade 配对 tile）。</item>
    /// </list>
    /// <para/>
    /// **失败条件**（任一则返回 <see cref="MapCommandResult.Fail"/>，mapState 完全不变）：
    /// <list type="bullet">
    /// <item><see cref="TileId"/> 未在 <see cref="TileDefinitionRegistry"/> 注册。</item>
    /// <item>该 tile 含 <c>PhaseLocked</c> 标签 → <c>"phase locked"</c>。</item>
    /// <item>该 tile 上无 <c>PhaseFlippable</c> 标签 → <c>"not phase flippable"</c>。</item>
    /// <item>该 tile 当前层已是 <see cref="TargetLayer"/> → <c>"already at target layer"</c>。</item>
    /// <item>未 attach runtime states（MAP-07 写入前提条件）→ <c>"no runtime states attached"</c>。</item>
    /// </list>
    /// </summary>
    public sealed class FlipTilePhaseCommand : IMapCommand
    {
        /// <summary><see cref="TileDefinition.TileId"/>（>= 1）。</summary>
        public int TileId { get; }

        /// <summary>切换到的目标维度（<see cref="DimensionLayer.Reality"/> 或 <see cref="DimensionLayer.Astral"/>）。</summary>
        public DimensionLayer TargetLayer { get; }

        // ──────────── Undo 跟踪（MPRecords for forward + pair tiles）────────────

        private bool _executed;
        private int _executedNewVersion;
        private readonly List<(int tileId, DimensionLayer prevLayer)> _undoRecords
            = new List<(int, DimensionLayer)>(2);

        public FlipTilePhaseCommand(int tileId, DimensionLayer targetLayer)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1 (0 reserved for 'no tile').");
            TileId = tileId;
            TargetLayer = targetLayer;
        }

        /// <summary>
        /// 在 <paramref name="mapState"/> 上执行相位翻转。
        /// </summary>
        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));

            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 一次性 attach 检查（MAP-07 写入前提条件）。
            var runtimeStates = PhaseFlipStateService.GetRuntimeStates(mapState);
            if (runtimeStates == null)
            {
                return MapCommandResult.Fail("no runtime states attached");
            }

            var registry = PhaseFlipStateService.GetAttachedRegistry(mapState);
            if (registry == null)
            {
                return MapCommandResult.Fail("no tile registry attached");
            }

            if (!registry.TryGetById(TileId, out var def))
            {
                return MapCommandResult.Fail("tile not found");
            }

            // 单一闸门条件：先校验再写。
            if ((def.Tags & TileTags.PhaseLocked) != 0)
            {
                return MapCommandResult.Fail("phase locked");
            }

            if ((def.Tags & TileTags.PhaseFlippable) == 0)
            {
                return MapCommandResult.Fail("not phase flippable");
            }

            // MAP-07：用 per-tile 字段读取当前层（fallback 字典 — 兼容 MAP-08 测试）。
            PhaseFlipStateService.TryGetActiveDimension(mapState, TileId, out var currentLayer);

            if (currentLayer == TargetLayer)
            {
                return MapCommandResult.Fail("already at target layer");
            }

            // 重置 undo records（保证重复 Run / Undo 后状态可恢复）。
            _undoRecords.Clear();
            _undoRecords.Add((TileId, currentLayer));

            // 写入 ActivationDimension 字段（fallback 字典）。
            PhaseFlipStateService.SetActiveDimension(mapState, TileId, TargetLayer);

            var events = new List<MapEvent>(2) { MapEvent.TileChanged(def.Coord) };

            // MAP-07 cascade：若存在配对 tile 且配对 tile 也通过 PhaseFlipStateService，
            // 同步 flip 配对 tile 到 TargetLayer。
            if (PhasePairLookup.TryGetPair(mapState, TileId, out var pairTileId)
                && runtimeStates.TryGetValue(pairTileId, out _))
            {
                if (registry.TryGetById(pairTileId, out var pairDef))
                {
                    PhaseFlipStateService.TryGetActiveDimension(mapState, pairTileId, out var pairCurrentLayer);
                    if (pairCurrentLayer != TargetLayer)
                    {
                        _undoRecords.Add((pairTileId, pairCurrentLayer));
                        PhaseFlipStateService.SetActiveDimension(mapState, pairTileId, TargetLayer);
                        events.Add(MapEvent.TileChanged(pairDef.Coord));
                    }
                }
            }

            // 稳定排序（按 MapEvent.CompareTo：Kind / Coord）。
            events.Sort();

            _executed = true;
            _executedNewVersion = newVersion;

            return MapCommandResult.Ok(events, newVersion);
        }

        /// <summary>
        /// 单步撤销：把上次 <see cref="Execute"/> 影响的所有 tile（含 cascade 配对）
        /// 翻回原 layer。
        /// </summary>
        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "FlipTilePhaseCommand.Undo called without prior successful Execute; " +
                    "history stack violation (executor should not invoke Undo in this state).");

            // 反向应用：从后往前，确保 cascade 不影响主 tile。
            for (int i = _undoRecords.Count - 1; i >= 0; i--)
            {
                var (tileId, prevLayer) = _undoRecords[i];
                PhaseFlipStateService.SetActiveDimension(mapState, tileId, prevLayer);
            }
            // MapState.Version 回退一档（由 executor 在外层负责减去 1；本命令仅做 field-level 还原）。
            _executed = false;
            _undoRecords.Clear();
        }

        /// <inheritdoc />
        public int Version => 2;

        /// <inheritdoc />
        public string CommandId => $"flip-tile-phase:{TileId}";

        /// <inheritdoc />
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public override string ToString()
            => $"FlipTilePhaseCommand(TileId={TileId}, Target={TargetLayer})";
    }
}
