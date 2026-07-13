namespace Starfall.Core.Combat
{
    /// <summary>
    /// 关卡目标阶段（Task 19 关卡闭环）。
    /// <para/>
    /// <list type="bullet">
    /// <item><see cref="Guard"/>：默认阶段。玩家守住目标。完成 <see cref="Core.Model.BattleState.GuardsRequired"/> 个完整回合后进入 <see cref="Retreat"/>。</item>
    /// <item><see cref="Retreat"/>：进入撤离。活着的 <see cref="Core.Model.Owner.Player"/> 单位全部站在撤离格邻接格时判定 PlayerWins。</item>
    /// <item><see cref="Ended"/>：胜负已定（终端态，与 <see cref="BattleOutcome"/> 锁定一致，不可回退）。</item>
    /// </list>
    /// <para/>
    /// 由 <c>ObjectivePhaseUpdater</c> 在每回合结束（玩家 + AI 各一次）后调用推进；
    /// 阶段切换与胜负判定解耦（先看胜负，再看阶段，再看推进条件）。
    /// </summary>
    public enum ObjectivePhase : byte
    {
        Guard = 0,
        Retreat = 1,
        Ended = 2,
    }
}
