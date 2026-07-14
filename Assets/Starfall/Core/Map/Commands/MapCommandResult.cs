using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 <see cref="IMapCommand"/> 执行结果。
    /// <para/>
    /// **字段语义**：
    /// <list type="bullet">
    /// <item><see cref="Success"/>：true = 成功；false = 失败（任何非 OK 都视作失败）。</item>
    /// <item><see cref="FailureReason"/>：成功时 null；失败时返回机器可读字符串
    ///       （如 <c>"phase locked"</c>、<c>"already at target layer"</c>、<c>"not phase flippable"</c>）。
    ///       永远不抛异常（断言失败 / 状态非法等情形由调用方捕获）。</item>
    /// <item><see cref="AffectedTiles"/>：本次操作影响的 cells，按 <see cref="GridCoord.CompareTo"/>
    ///       升序（Y → X → Layer），与 AGENTS.md §11 一致。仅在成功时包含非空列表；
    ///       失败时为空。</item>
    /// </list>
    /// </summary>
    public readonly struct MapCommandResult
    {
        /// <summary>true = 成功。</summary>
        public readonly bool Success;

        /// <summary>失败原因（null = 成功）。</summary>
        public readonly string FailureReason;

        /// <summary>影响的 cells（Y → X → Layer 升序）。</summary>
        public readonly IReadOnlyList<GridCoord> AffectedTiles;

        public MapCommandResult(
            bool success,
            string failureReason,
            IReadOnlyList<GridCoord> affectedTiles)
        {
            Success = success;
            FailureReason = failureReason;
            // 失败时归一化为空 list，避免下游遍历 null。
            AffectedTiles = success
                ? (affectedTiles ?? System.Array.Empty<GridCoord>())
                : System.Array.Empty<GridCoord>();
        }

        /// <summary>构造成功结果（影响 cells 可为 null，内部归一化为空 list）。</summary>
        public static MapCommandResult Ok(IReadOnlyList<GridCoord> affectedTiles)
            => new MapCommandResult(true, null, affectedTiles);

        /// <summary>构造失败结果（影响 cells 强制为空）。</summary>
        public static MapCommandResult Fail(string reason)
            => new MapCommandResult(false, reason, null);

        public override string ToString()
            => Success
                ? $"MapCommandResult(OK, cells={AffectedTiles.Count})"
                : $"MapCommandResult(Fail, reason={FailureReason})";
    }
}
