namespace Starfall.Core.Combat
{
    /// <summary>
    /// 战斗胜负状态。由 <see cref="WinConditionChecker"/> 在每回合结束（含 AI 回合后）计算，
    /// 由 <see cref="BattleRunner"/> 持有并随 BattleState 变化更新。
    /// </summary>
    public enum BattleOutcome : byte
    {
        Ongoing = 0,
        PlayerWins = 1,
        EnemyWins = 2,
        Draw = 3,
    }
}