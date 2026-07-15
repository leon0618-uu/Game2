using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 路径图失效命令（"invalidate" no-op）。
    /// <para/>
    /// **角色**：发出 <see cref="MapEventKind.OnPathGraphInvalidated"/> 事件让
    /// path graph 缓存系统知道"重新计算"。本命令**不修改任何 mapState 字段**。
    /// <para/>
    /// **用途**：
    /// <list type="bullet">
    /// <item>装饰性：被 <see cref="TransformTileCommand"/> / <see cref="SetTileStabilityCommand"/> /
    ///       <see cref="CreateAnchorLinkCommand"/> 等底层拓扑改动的命令在 parent 上
    ///       显式调用（可有可无；下游订阅路径失效 event 即可）。</item>
    /// <item>外部：port mapping / teleport / teleport 律令结束后手动 invalidate。</item>
    /// </list>
    /// <para/>
    /// **MVP 注**：MVP 阶段 path graph 是按需 BFS 计算（<c>BFSPathfinder</c>），本命令
    /// 不维护实际缓存。仅作为**信号**存在。
    /// <para/>
    /// **可选 origin**：构造时 <paramref name="origin"/> 可选；为 null 时事件
    /// <c>Coord</c> = null（全局失效）。
    /// </summary>
    public sealed class InvalidatePathGraphCommand : IMapCommand
    {
        public GridCoord? Origin { get; }

        public InvalidatePathGraphCommand(GridCoord? origin = null)
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
                MapEvent.PathGraphInvalidated(Origin, "path-graph-invalidated")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            // "invalidate" 类型命令无可逆状态变化；保留 Undo 以满足 IMapCommand
            // 契约，但语义上抛 NotSupportedException。
            throw new NotSupportedException(
                "InvalidatePathGraphCommand does not support Undo (no state change to revert).");
        }

        public int Version => 1;
        public string CommandId => Origin.HasValue
            ? $"invalidate-path-graph:{Origin.Value}"
            : $"invalidate-path-graph";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        public override string ToString()
            => $"InvalidatePathGraphCommand(Origin={Origin?.ToString() ?? "-"})";
    }
}
