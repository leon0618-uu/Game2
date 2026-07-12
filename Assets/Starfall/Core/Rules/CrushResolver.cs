using System.Collections.Generic;
using Starfall.Core.Model;

namespace Starfall.Core.Rules
{
    /// <summary>
    /// 挤压检测：检测同格内 ≥2 单位，触发相互伤害。
    /// MVP：静态 ApplyCrush(BattleState, damage) → 移除最弱单位或全员扣 1 HP。
    /// </summary>
    public static class CrushResolver
    {
        public class CrushOutcome
        {
            public bool CrushDetected { get; set; }
            public List<int> AffectedUnitIds { get; set; } = new List<int>();
        }

        public static CrushOutcome DetectAndApply(BattleState state, int damagePerUnit = 1)
        {
            var outcome = new CrushOutcome();
            // 按位置分组
            var groups = new Dictionary<GridPos, List<UnitState>>();
            foreach (var u in state.Units)
            {
                if (u.Hp <= 0) continue;
                if (!groups.TryGetValue(u.Pos, out var list))
                {
                    list = new List<UnitState>();
                    groups[u.Pos] = list;
                }
                list.Add(u);
            }
            foreach (var kv in groups)
            {
                if (kv.Value.Count >= 2)
                {
                    outcome.CrushDetected = true;
                    foreach (var u in kv.Value)
                    {
                        u.Hp = System.Math.Max(0, u.Hp - damagePerUnit);
                        outcome.AffectedUnitIds.Add(u.UnitId);
                    }
                }
            }
            return outcome;
        }
    }
}