using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 占位敌方 AI：仅产生 <see cref="EndTurnCommand"/>。
    ///
    /// 设计目的：
    /// - MVP 让 BattleRunner.EndTurn → AI → 切回 Player 的完整闭环可在 EditMode 测试中跑通；
    /// - Task 14 替换为真实策略 AI（评分 + Tie-break + 锚点反应等）。
    ///
    /// 该实现无状态、不读取 UnityEngine.Random，可作为 Replay 基线。
    /// </summary>
    public sealed class SimpleEnemyAI : IEnemyAI
    {
        public IEnumerable<ICommand> PlanTurn(int commandIdSeed, BattleState state)
        {
            yield return new EndTurnCommand(commandIdSeed, state.ActivePlayer);
        }
    }
}