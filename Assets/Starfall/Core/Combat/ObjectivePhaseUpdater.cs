using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 关卡阶段推进器（Task 19 关卡闭环）。
    /// <para/>
    /// 在每次 <c>EndTurn()</c> 末尾由 <c>BattleRunner</c> 调用一次，
    /// 决定：
    /// <list type="number">
    /// <item>是否产生新的 <see cref="BattleOutcome"/>（胜负已定 → <see cref="ObjectivePhase.Ended"/>）。</item>
    /// <item>是否从 <see cref="ObjectivePhase.Guard"/> 推进到 <see cref="ObjectivePhase.Retreat"/>。</item>
    /// <item>是否在 <see cref="ObjectivePhase.Retreat"/> 中完成撤离（所有活 Player 单位站在 ExitTile 邻接格）。</item>
    /// </list>
    /// <para/>
    /// 推进规则（确定性）：
    /// <list type="bullet">
    /// <item>任意阶段：Enemy 全灭 → <see cref="BattleOutcome.PlayerWins"/> + 锁定 Ended。</item>
    /// <item>任意阶段：Player 全灭 → <see cref="BattleOutcome.EnemyWins"/> + 锁定 Ended。</item>
    /// <item>Guard 阶段：双方都仍有活单位且胜利条件未触发 → <c>GuardsCompleted++</c>；
    ///       达到 <c>GuardsRequired</c> → 切 Retreat。</item>
    /// <item>Retreat 阶段：<c>ExitTile != null</c> 且所有活 Player 单位均位于 ExitTile 的 4 邻居之一 → PlayerWins + Ended。</item>
    /// <item>Retreat 阶段：无 ExitTile 但 Player 全灭 → EnemyWins + Ended。</item>
    /// </list>
    /// <para/>
    /// 单位遍历顺序：先 <c>Y</c> 后 <c>X</c>（与 <see cref="GridPosComparer"/> 一致）；不依赖外部顺序。
    /// </summary>
    public static class ObjectivePhaseUpdater
    {
        /// <summary>
        /// 推进关卡阶段并返回 (新 Outcome, 是否切到 Retreat, 是否完成撤离)。
        /// 返回元组的三个元素可独立判断。
        /// </summary>
        public static (BattleOutcome outcome, bool advancedToRetreat, bool retreated) Update(
            BattleState state)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));

            bool advancedToRetreat = false;
            bool retreated = false;

            // 1. 胜负判定（任意阶段都重新判定，与 WinConditionChecker 同步）
            var outcome = WinConditionChecker.Check(state);

            // 任何胜负已定：进入 Ended 阶段并返回
            if (outcome != BattleOutcome.Ongoing)
            {
                if (state.CurrentPhase != ObjectivePhase.Ended)
                {
                    state.CurrentPhase = ObjectivePhase.Ended;
                }
                return (outcome, false, false);
            }

            // 2. 双方仍有活单位时，按阶段推进
            if (state.CurrentPhase == ObjectivePhase.Guard)
            {
                // 累加防守次数。仅在"双方都仍有活单位"时递增；
                // （上面已检查 Ongoing 等价于这个条件）
                state.GuardsCompleted++;

                if (state.GuardsCompleted >= state.GuardsRequired)
                {
                    state.CurrentPhase = ObjectivePhase.Retreat;
                    advancedToRetreat = true;
                }
            }
            else if (state.CurrentPhase == ObjectivePhase.Retreat)
            {
                // 撤离推进：所有活 Player 单位都站在 ExitTile 的 4 邻居之一
                if (state.ExitTile.HasValue && AllLivePlayersAdjacentToExit(state))
                {
                    retreated = true;
                    state.CurrentPhase = ObjectivePhase.Ended;
                    outcome = BattleOutcome.PlayerWins;
                }
                // 无 ExitTile：维持 Retreat 阶段（玩家未提供撤离目标时只能继续防守或失败）
            }

            // Outcome 维持 Ongoing（若推进但未结束）
            return (outcome, advancedToRetreat, retreated);
        }

        /// <summary>
        /// 检查所有活 Player 单位是否都在 ExitTile 的 4 邻居之一（AGENTS.md §11 顺序：下、左、右、上）。
        /// </summary>
        private static bool AllLivePlayersAdjacentToExit(BattleState state)
        {
            var exit = state.ExitTile.Value;
            var adj = new HashSet<GridPos>
            {
                new GridPos(exit.X, exit.Y + 1),     // 下
                new GridPos(exit.X - 1, exit.Y),     // 左
                new GridPos(exit.X + 1, exit.Y),     // 右
                new GridPos(exit.X, exit.Y - 1),     // 上
            };

            bool foundAny = false;
            foreach (var u in state.Units)
            {
                if (u == null || u.Hp <= 0) continue;
                if (u.Owner != Owner.Player) continue;
                foundAny = true;
                if (!adj.Contains(u.Pos)) return false;
            }
            // 没有活 Player 单位时不算撤离完成（这种情况已被 WinConditionChecker 截获）
            return foundAny;
        }

        /// <summary>
        /// 收集所有存活的 <see cref="Owner.Player"/> 单位的位置（按 UnitId 升序，用于测试与 Replay）。
        /// </summary>
        public static List<GridPos> GetLivePlayerPositions(BattleState state)
        {
            var list = new List<GridPos>();
            if (state == null) return list;
            foreach (var u in state.Units)
            {
                if (u == null || u.Hp <= 0) continue;
                if (u.Owner != Owner.Player) continue;
                list.Add(u.Pos);
            }
            list.Sort(GridPosComparer.Instance);
            return list;
        }
    }
}
