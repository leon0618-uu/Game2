using System;
using System.Collections.Generic;
using System.Linq;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 / §21.1 <see cref="IMapCommand"/> 执行结果。
    /// <para/>
    /// **与 MAP-08 stub 的差异**：
    /// MAP-08 stub 直接用 <c>AffectedTiles : IReadOnlyList&lt;GridCoord&gt;</c> 表达影响范围；
    /// MAP-03 完整化后改为 **<see cref="Events"/>**（统一的 <see cref="MapEvent"/> 列表），
    /// 涵盖所有命令的影响（tile / region / anchor / LOS / path graph 等）。
    /// 同时保留 <see cref="AffectedTiles"/> 作为**派生视图**（仅过滤
    /// <see cref="MapEventKind.OnTileChanged"/> 类型事件），让 MAP-08 测试继续有效，
    /// 不强制外部代码迁移。
    /// <para/>
    /// **字段语义**：
    /// <list type="bullet">
    /// <item><see cref="Success"/>：true = 成功；false = 失败。</item>
    /// <item><see cref="FailureReason"/>：成功时 null；失败时返回机器可读字符串
    ///       （如 <c>"tile not found"</c>、<c>"phase locked"</c>）。永远不抛异常。</item>
    /// <item><see cref="Events"/>：副作用事件（按 executor 调用顺序；同命令内由命令实现稳定排序）。
    ///       仅在成功时包含非空列表（多事件也可）；失败时为空 list。</item>
    /// <item><see cref="AffectedTiles"/>：派生视图 — 从 <see cref="Events"/> 过滤
    ///       <see cref="MapEventKind.OnTileChanged"/> 事件的 <see cref="GridCoord"/>。</item>
    /// <item><see cref="NewVersion"/>：成功后 mapState 应持有的 <c>MapState.Version</c> 值
    ///       （即 <c>mapState.Version + 1</c>）。失败时 = -1（无意义）。</item>
    /// </list>
    /// </summary>
    public readonly struct MapCommandResult
    {
        /// <summary>true = 成功。</summary>
        public readonly bool Success;

        /// <summary>失败原因（null = 成功）。</summary>
        public readonly string FailureReason;

        /// <summary>副作用事件列表（按命令实现顺序；executor 跨命令追加）。</summary>
        public readonly IReadOnlyList<MapEvent> Events;

        /// <summary>成功后的 mapState.Version 值（= 旧值 + 1）；失败 = -1。</summary>
        public readonly int NewVersion;

        public MapCommandResult(
            bool success,
            string failureReason,
            IReadOnlyList<MapEvent> events,
            int newVersion)
        {
            Success = success;
            FailureReason = failureReason;
            // 失败时归一化为空 list，避免下游遍历 null。
            Events = success
                ? (events ?? Array.Empty<MapEvent>())
                : Array.Empty<MapEvent>();
            NewVersion = success ? newVersion : -1;
        }

        /// <summary>
        /// 派生视图：仅包含 <see cref="MapEventKind.OnTileChanged"/> 事件的 <see cref="GridCoord"/>。
        /// <para/>
        /// **保留目的**：保持 MAP-08 stub 公开 API 向后兼容；新代码请使用 <see cref="Events"/>。
        /// </summary>
        public IReadOnlyList<GridCoord> AffectedTiles
        {
            get
            {
                if (Events == null || Events.Count == 0) return Array.Empty<GridCoord>();
                var list = new List<GridCoord>(Events.Count);
                foreach (var e in Events)
                {
                    if (e.Kind == MapEventKind.OnTileChanged && e.Coord.HasValue)
                        list.Add(e.Coord.Value);
                }
                if (list.Count <= 1) return list;
                list.Sort();
                return list;
            }
        }

        /// <summary>构造成功结果（事件列表可为 null，内部归一化为空 list）。</summary>
        public static MapCommandResult Ok(IReadOnlyList<MapEvent> events, int newVersion)
            => new MapCommandResult(true, null, events, newVersion);

        /// <summary>构造成功结果（无事件，empty list）。</summary>
        public static MapCommandResult Ok(int newVersion)
            => new MapCommandResult(true, null, Array.Empty<MapEvent>(), newVersion);

        /// <summary>
        /// MAP-08 stub 兼容：从 <see cref="GridCoord"/> 列表构造 <see cref="MapCommandResult"/>，
        /// 自动包装为 <see cref="MapEventKind.OnTileChanged"/> 事件。
        /// <para/>
        /// **保留目的**：让 MAP-08 stub 测试无需修改即可继续 valid；新代码请用
        /// <see cref="Ok(IReadOnlyList{MapEvent}, int)"/>。
        /// </summary>
        public static MapCommandResult Ok(IReadOnlyList<GridCoord> affectedTiles, int newVersion)
        {
            if (affectedTiles == null || affectedTiles.Count == 0)
                return Ok(newVersion);
            var events = new List<MapEvent>(affectedTiles.Count);
            foreach (var c in affectedTiles)
                events.Add(MapEvent.TileChanged(c));
            return Ok((IReadOnlyList<MapEvent>)events, newVersion);
        }

        /// <summary>构造失败结果（事件强制为空，NewVersion=-1）。</summary>
        public static MapCommandResult Fail(string reason)
            => new MapCommandResult(false, reason, null, -1);

        public override string ToString()
            => Success
                ? $"MapCommandResult(OK, events={Events.Count}, ver={NewVersion})"
                : $"MapCommandResult(Fail, reason={FailureReason})";
    }
}
