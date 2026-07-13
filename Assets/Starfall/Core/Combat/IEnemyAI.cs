using System.Collections.Generic;
using Starfall.Core.Command;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 敌方 AI 接口。每当 <see cref="BattleRunner.EndTurn"/> 切换到
    /// <see cref="Model.Owner.Enemy"/> 时，由 BattleRunner 调用一次。
    ///
    /// 返回的命令序列将按顺序经 <see cref="BattleRunner.Submit"/> 派发；
    /// AI 必须保持确定性：相同 (commandIdSeed, state) 必须返回相同序列。
    /// </summary>
    public interface IEnemyAI
    {
        IEnumerable<ICommand> PlanTurn(int commandIdSeed, Model.BattleState state);
    }
}