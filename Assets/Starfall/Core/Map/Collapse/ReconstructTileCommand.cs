using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 重建单 tile 命令（IMapCommand；ADR-0007）。
    ///
    /// <para/>
    /// **作用**：把指定 tile 的 <see cref="LocalCollapseValue"/> 恢复到
    /// <see cref="TileStability.Reconstructed"/>（默认）或 <see cref="TileStability.Stable"/>，
    /// Emit <see cref="MapEventKind.OnTileReconstructed"/> 事件。
    ///
    /// <para/>
    /// **设计说明**：
    /// <see cref="LocalCollapseValue"/> 构造时根据 Value 自动派生 Stability；
    /// 因此命令的运行时效果 = 把 <c>Value</c> 重置为 0，对应 Stability = Stable。
    /// "Reconstructed" 语义通过 <see cref="MapEventKind.OnTileReconstructed"/> 事件承载
    /// （description 标识"重建动作"），同时 <see cref="TargetStability"/> 参数用于
    /// 校验输入合法性 + 测试断言。
    ///
    /// <para/>
    /// **非法状态转换**（拒绝执行）：
    /// <list type="bullet">
    /// <item>当前 Stability = <see cref="TileStability.Reconstructed"/>（已重建）→ 拒绝
    ///       <c>"already reconstructed"</c>。</item>
    /// </list>
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>tile 不在 <see cref="MapState.Tiles"/> → <c>"tile not in map"</c>。</item>
    /// <item>当前已 Reconstructed → <c>"already reconstructed"</c>。</item>
    /// <item>目标 Stability 非法（仅 Reconstructed / Stable 允许）→ 构造时拒绝。</item>
    /// </list>
    /// </summary>
    public sealed class ReconstructTileCommand : IMapCommand
    {
        public GridCoord Coord { get; }
        public TileStability TargetStability { get; }

        public ReconstructTileCommand(GridCoord coord, TileStability target = TileStability.Reconstructed)
        {
            if (target != TileStability.Reconstructed && target != TileStability.Stable)
                throw new ArgumentOutOfRangeException(nameof(target), target,
                    "ReconstructTileCommand target must be Reconstructed or Stable.");
            Coord = coord;
            TargetStability = target;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 校验 tile 在 map 内
            bool found = false;
            for (int i = 0; i < mapState.TilesInternal.Count; i++)
            {
                if (mapState.TilesInternal[i].Equals(Coord)) { found = true; break; }
            }
            if (!found)
                return MapCommandResult.Fail("tile not in map");

            // 读取当前 LCV
            LocalCollapseValue prevLcv;
            TileStability prevStability;
            if (mapState.LocalCVsInternal.TryGetValue(Coord, out var existing))
            {
                prevLcv = existing;
                prevStability = existing.Stability;
            }
            else
            {
                // 没 LCV 记录（之前未累积），设为 Zero；Stability 默认 Stable
                prevLcv = LocalCollapseValue.Zero(Coord);
                prevStability = TileStability.Stable;
            }

            if (prevStability == TileStability.Reconstructed)
                return MapCommandResult.Fail("already reconstructed");

            // 重建效果：Value 重置为 0（Stability 自动派生 = Stable）。
            // "Reconstructed" 语义由 OnTileReconstructed 事件承载。
            int newValue = 0;
            var newLcv = new LocalCollapseValue(Coord, newValue, prevLcv.TickAccumulated);
            _previousLcv = prevLcv;
            _previousStability = prevStability;
            mapState.LocalCVsInternal[Coord] = newLcv;
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.TileReconstructed(Coord, (int)newLcv.Stability, $"reconstruct-tile")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("ReconstructTileCommand.Undo called without prior Execute.");
            if (_previousLcv.HasValue)
            {
                mapState.LocalCVsInternal[Coord] = _previousLcv.Value;
            }
            else
            {
                mapState.LocalCVsInternal.Remove(Coord);
            }
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"reconstruct-tile:{Coord.X},{Coord.Y},{(byte)Coord.Layer}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private LocalCollapseValue? _previousLcv;
        private TileStability _previousStability;

        public override string ToString()
            => $"ReconstructTileCommand(Coord={Coord}, Target={TargetStability})";
    }
}
