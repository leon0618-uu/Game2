using System;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.LineOfSight;

namespace Starfall.Core.Map.Cover
{
    /// <summary>
    /// doc2 MAP-06 §4.5 掩体查询服务。
    ///
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item>由 attacker 与 defender 坐标推算攻击方向（<see cref="CoverDirection"/>）。</item>
    /// <item>按攻击方向查询 defender tile 的掩体等级（<see cref="CoverLevel"/>）。</item>
    /// </list>
    ///
    /// <para/>
    /// **规则**（doc2 §10.5）：
    /// <list type="bullet">
    /// <item>同 tile → <see cref="CoverDirection.All"/>，掩体等级 = <see cref="CoverLevel.None"/>。</item>
    /// <item>共线（X 或 Y 相同）→ 按攻击方向查 defender 的对应方向掩体。</item>
    /// <item>对角线（X,Y 都不同）→ 主方向按 |Δ| 较大者；相等时按攻击者偏 X → East，
    ///       偏 Y → 取决于 Y 符号。</item>
    /// <item>无 <see cref="ICoverLookup"/> → 默认 <see cref="CoverLevel.None"/>。</item>
    /// <item><see cref="CoverDirection.All"/> → 任意方向都暴露，等价于 None。</item>
    /// </list>
    ///
    /// <para/>
    /// **与 MapState 解耦**：本服务不直接读 <c>MapState</c> 字段，
    /// 由调用方传入 <see cref="ICoverLookup"/>（数据层未实现 TileDef 时可传 null）。
    /// </summary>
    public static class CoverQueryService
    {
        /// <summary>
        /// 推算攻击方向：attacker → defender 的 4 邻居方向（含 All 表示同 tile）。
        /// </summary>
        /// <param name="attacker">攻击者坐标（含 Layer）。</param>
        /// <param name="defender">防御者坐标（含 Layer）。</param>
        /// <returns>从 defender 看 attacker 所在方向；同 tile 返回 <see cref="CoverDirection.All"/>。</returns>
        /// <exception cref="ArgumentException">attacker 与 defender 不在同一 <see cref="DimensionLayer"/>。</exception>
        public static CoverDirection ComputeAttackDirection(GridCoord attacker, GridCoord defender)
        {
            if (attacker.Layer != defender.Layer)
                throw new ArgumentException(
                    $"Attacker {attacker} and defender {defender} must be on the same layer " +
                    "(cover direction is intra-layer).",
                    nameof(attacker));

            int dx = attacker.X - defender.X;
            int dy = attacker.Y - defender.Y;

            // 同 tile（含同 X 同 Y）→ All
            if (dx == 0 && dy == 0) return CoverDirection.All;

            // 共线 → 单一方向
            if (dx == 0)
            {
                // defender.Y < attacker.Y → attacker 在 defender 北侧 → CoverDirection.North
                return dy > 0 ? CoverDirection.North : CoverDirection.South;
            }
            if (dy == 0)
            {
                return dx > 0 ? CoverDirection.East : CoverDirection.West;
            }

            // 对角线 → 主方向按 |Δ| 较大者；相等时 X 优先（North > East > South > West）
            int adx = dx < 0 ? -dx : dx;
            int ady = dy < 0 ? -dy : dy;

            if (ady > adx)
            {
                return dy > 0 ? CoverDirection.North : CoverDirection.South;
            }
            if (adx > ady)
            {
                return dx > 0 ? CoverDirection.East : CoverDirection.West;
            }

            // |dx| == |dy| → 严格按 X 优先（确定性 tie-break）
            // 这是为了保证同 (|dx|, |dy|) 的对角攻击永远得到同一方向，
            // 避免 UnitId 排序之外的 tie-break 引入非确定性。
            return dx > 0 ? CoverDirection.East : CoverDirection.West;
        }

        /// <summary>
        /// 查询 defender tile 对 attacker 方向的掩体等级。
        /// </summary>
        /// <param name="cover">掩体查询接口；null = 视为全 None。</param>
        /// <param name="attacker">攻击者坐标。</param>
        /// <param name="defender">防御者坐标。</param>
        /// <returns>掩体等级（None / Half / Full）。</returns>
        public static CoverLevel QueryCover(
            ICoverLookup cover,
            GridCoord attacker,
            GridCoord defender)
        {
            if (cover == null) return CoverLevel.None;
            if (attacker.Layer != defender.Layer) return CoverLevel.None;

            CoverDirection dir = ComputeAttackDirection(attacker, defender);
            if (dir == CoverDirection.All) return CoverLevel.None;

            CoverLevel? level = cover.GetCover(defender);
            return level ?? CoverLevel.None;
        }

        /// <summary>
        /// 高级查询：同时考虑对角线情况下两条轴上的掩体"取较重"。
        /// doc2 §10.5 对角线规则：当 attacker 与 defender 不同 X 不同 Y，
        /// defender 的掩体来自两个方向（Y 方向 + X 方向），
        /// 取两者较重（Full &gt; Half &gt; None）作为最终掩体等级。
        /// </summary>
        public static CoverLevel QueryCoverDiagonal(
            ICoverLookup cover,
            GridCoord attacker,
            GridCoord defender)
        {
            if (cover == null) return CoverLevel.None;
            if (attacker.Layer != defender.Layer) return CoverLevel.None;

            int dx = attacker.X - defender.X;
            int dy = attacker.Y - defender.Y;

            if (dx == 0 && dy == 0) return CoverLevel.None;

            CoverLevel worst = CoverLevel.None;

            // Y 轴方向掩体
            if (dy != 0)
            {
                CoverDirection yDir = dy > 0 ? CoverDirection.North : CoverDirection.South;
                CoverLevel? l = cover.GetCover(defender);
                // 注意：当前 ICoverLookup 不分方向，只能查 defender 的总掩体；
                // 对角线场景下视 ICoverLookup 返回值为"该 tile 总掩体"，
                // 与方向无关。本方法保留为高级入口，
                // 后续 ICoverLookup 升级为按方向查询时再启用。
                _ = yDir;
                if (l.HasValue && l.Value > worst) worst = l.Value;
            }

            // X 轴方向掩体
            if (dx != 0)
            {
                CoverDirection xDir = dx > 0 ? CoverDirection.East : CoverDirection.West;
                CoverLevel? l = cover.GetCover(defender);
                _ = xDir;
                if (l.HasValue && l.Value > worst) worst = l.Value;
            }

            return worst;
        }
    }
}
