using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 强制坍塌单 tile 命令（IMapCommand；ADR-0007）。
    ///
    /// <para/>
    /// **作用**：把指定 tile 的 <see cref="LocalCollapseValue"/> 设为 <see cref="TileStability.Collapsing"/>
    /// （<see cref="LocalCollapseValue.Value"/> = 80）或 <see cref="TileStability.Collapsed"/>
    /// （<see cref="LocalCollapseValue.Value"/> = 100），Emit <see cref="MapEventKind.OnTileFractured"/> 事件。
    ///
    /// <para/>
    /// **非法状态转换**（拒绝执行）：
    /// <list type="bullet">
    /// <item>当前 Stability = <see cref="TileStability.Collapsed"/>（已是终态）→ 拒绝
    ///       <c>"already collapsed"</c>。必须先 <c>ReconstructTileCommand</c>。</item>
    /// </list>
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>tile 不在 <see cref="MapState.Tiles"/> → <c>"tile not in map"</c>。</item>
    /// <item>当前已 Collapsed → <c>"already collapsed"</c>。</item>
    /// <item>目标 Stability 非法（<see cref="TileStability.Stable"/> /
    ///       <see cref="TileStability.Unstable"/> / <see cref="TileStability.Reconstructed"/>）→
    ///       构造时拒绝。</item>
    /// </list>
    /// </summary>
    public sealed class CollapseTileCommand : IMapCommand
    {
        public GridCoord Coord { get; }
        public TileStability TargetStability { get; }

        public CollapseTileCommand(GridCoord coord, TileStability target = TileStability.Collapsing)
        {
            // 校验目标稳定性必须是 Collapsing / Collapsed（不可用 Stable 等）
            if (target != TileStability.Collapsing && target != TileStability.Collapsed)
                throw new ArgumentOutOfRangeException(nameof(target), target,
                    "CollapseTileCommand target must be Collapsing or Collapsed.");
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
            TileStability prevStability;
            LocalCollapseValue prevLcv;
            if (mapState.LocalCVsInternal.TryGetValue(Coord, out var existing))
            {
                prevLcv = existing;
                prevStability = existing.Stability;
            }
            else
            {
                prevLcv = LocalCollapseValue.Zero(Coord);
                prevStability = TileStability.Stable;
            }

            if (prevStability == TileStability.Collapsed)
                return MapCommandResult.Fail("already collapsed");

            // 设新值（按目标 stability 选 value）
            int newValue = TargetStability == TileStability.Collapsed ? 100 : 80;
            var newLcv = prevLcv.WithValue(newValue);
            _previousLcv = prevLcv;
            _previousStability = prevStability;
            mapState.LocalCVsInternal[Coord] = newLcv;
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.TileFractured(Coord, (int)newLcv.Stability, $"collapse-tile")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("CollapseTileCommand.Undo called without prior Execute.");
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
        public string CommandId => $"collapse-tile:{Coord.X},{Coord.Y},{(byte)Coord.Layer}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private LocalCollapseValue? _previousLcv;
        private TileStability _previousStability;

        public override string ToString()
            => $"CollapseTileCommand(Coord={Coord}, Target={TargetStability})";
    }
}
