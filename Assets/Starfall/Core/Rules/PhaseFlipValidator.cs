using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Core.Rules
{
    /// <summary>
    /// 相位翻转校验：同一单位同一回合仅允许翻转一次（避免 PhaseInvert 状态叠加）。
    /// MVP：通过 StatusInstance 数量判断；若已有 PhaseInvert 状态未过期则禁止再次施加。
    /// </summary>
    public static class PhaseFlipValidator
    {
        public static bool CanFlipPhase(BattleState state, int unitId)
        {
            foreach (var s in state.Statuses)
            {
                if (s.SourceUnitId == unitId && s.Kind == StatusKind.PhaseInvert && s.RemainingTurns > 0)
                    return false;
            }
            return true;
        }
    }
}