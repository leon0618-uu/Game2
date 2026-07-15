using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 视线图失效命令（"invalidate" no-op）。
    /// <para/>
    /// **角色**：发出 <see cref="MapEventKind.OnLineOfSightInvalidated"/> 事件让 LOS
    /// 缓存系统知道"重新计算"。本命令**不修改任何 mapState 字段**。
    /// <para/>
    /// **用途**：与 <see cref="InvalidatePathGraphCommand"/> 平行；
    /// 由 <see cref="TransformTileCommand"/> / <see cref="SetTileStabilityCommand"/> /
    /// 视线阻挡地形变化触发。本命令提供手动 invalidation 入口。
    /// </summary>
    public sealed class InvalidateLineOfSightCommand : IMapCommand
    {
        public GridCoord? Origin { get; }

        public InvalidateLineOfSightCommand(GridCoord? origin = null)
        {
            Origin = origin;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            var events = new List<MapEvent>(1)
            {
                MapEvent.LineOfSightInvalidated(Origin, "los-invalidated")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            throw new NotSupportedException(
                "InvalidateLineOfSightCommand does not support Undo (no state change to revert).");
        }

        public int Version => 1;
        public string CommandId => Origin.HasValue
            ? $"invalidate-line-of-sight:{Origin.Value}"
            : $"invalidate-line-of-sight";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public override string ToString()
            => $"InvalidateLineOfSightCommand(Origin={Origin?.ToString() ?? "-"})";
    }
}
