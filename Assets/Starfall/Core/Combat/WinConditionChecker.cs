using Starfall.Core.Model;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// MVP 胜负判定（M-14=A 最小化）：
    /// - <see cref="BattleOutcome.PlayerWins"/>：所有 <see cref="Owner.Enemy"/> 单位 Hp==0
    /// - <see cref="BattleOutcome.EnemyWins"/>：所有 <see cref="Owner.Player"/> 单位 Hp==0
    /// - <see cref="BattleOutcome.Draw"/>：双方同时全灭
    /// - <see cref="BattleOutcome.Ongoing"/>：任何一方仍有存活
    ///
    /// 单位遍历顺序沿用 <see cref="BattleState.Units"/> 顺序（确定性 = 同 seed 必同结果）。
    /// 锚点围区、引力律令等扩展胜负条件由后续 Task 引入，本类保持纯函数。
    /// </summary>
    public static class WinConditionChecker
    {
        public static BattleOutcome Check(BattleState state)
        {
            if (state == null) throw new System.ArgumentNullException(nameof(state));

            bool playerAlive = false;
            bool enemyAlive = false;
            foreach (var u in state.Units)
            {
                if (u == null) continue;
                if (u.Hp <= 0) continue;
                if (u.Owner == Owner.Player) playerAlive = true;
                else if (u.Owner == Owner.Enemy) enemyAlive = true;
            }

            if (!playerAlive && !enemyAlive) return BattleOutcome.Draw;
            if (!enemyAlive) return BattleOutcome.PlayerWins;
            if (!playerAlive) return BattleOutcome.EnemyWins;
            return BattleOutcome.Ongoing;
        }
    }
}