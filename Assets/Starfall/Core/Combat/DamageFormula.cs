using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// MVP 伤害公式：baseDamage × phaseModifier。
    /// - 同相位（Light vs Light / Dark vs Dark）：1.0×
    /// - 异相位（Light vs Dark / Dark vs Light）：1.5×（克制，整数化为 baseDamage * 3 / 2）
    /// - Burn 状态（attacker 处于 Burn）：+1
    ///
    /// 确定性强：相同输入得到相同伤害；不依赖 UnityEngine.Random 或时间。
    /// </summary>
    public static class DamageFormula
    {
        public static int Compute(int baseDamage, UnitState attacker, UnitState defender)
        {
            if (attacker == null || defender == null) return 0;
            int dmg = baseDamage;
            if (attacker.Phase != defender.Phase) dmg = dmg * 3 / 2; // 1.5x 取整
            return dmg;
        }

        public static int ComputeWithStatuses(
            int baseDamage,
            UnitState attacker,
            UnitState defender,
            IReadOnlyList<StatusInstance> allStatuses)
        {
            int dmg = Compute(baseDamage, attacker, defender);
            if (allStatuses != null)
            {
                foreach (var s in allStatuses)
                {
                    if (s.Kind == StatusKind.Burn
                        && s.SourceUnitId == attacker.UnitId
                        && s.RemainingTurns > 0)
                    {
                        dmg += 1;
                        break;
                    }
                }
            }
            return dmg;
        }
    }
}
