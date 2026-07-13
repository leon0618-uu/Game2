using System;
using System.Collections.Generic;
using Starfall.Core.Combat;
using Starfall.Core.Model;

namespace Starfall.Core.Command
{
    /// <summary>
    /// 攻击命令：attacker 对 target 造成伤害。
    /// MVP 限制：双方必须相邻（Chebyshev 距离 ≤ 1，即 8 邻居含自身）。
    /// 伤害由 <see cref="DamageFormula.ComputeWithStatuses"/> 计算。
    /// </summary>
    public sealed class AttackCommand : ICommand
    {
        public int CommandId { get; }
        public int AttackerId { get; }
        public int TargetId { get; }
        public int BaseDamage { get; }

        public AttackCommand(int commandId, int attackerId, int targetId, int baseDamage = 3)
        {
            CommandId = commandId;
            AttackerId = attackerId;
            TargetId = targetId;
            BaseDamage = baseDamage;
        }

        public bool CanExecute(BattleState state)
        {
            var attacker = FindUnit(state, AttackerId);
            var target = FindUnit(state, TargetId);
            if (attacker == null || target == null) return false;
            // 相邻（Chebyshev 距离 ≤ 1）
            int d = Math.Max(Math.Abs(attacker.Pos.X - target.Pos.X),
                             Math.Abs(attacker.Pos.Y - target.Pos.Y));
            return d <= 1;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            var attacker = FindUnit(state, AttackerId);
            var target = FindUnit(state, TargetId);
            int dmg = DamageFormula.ComputeWithStatuses(BaseDamage, attacker, target, state.Statuses);
            target.Hp = Math.Max(0, target.Hp - dmg);
            events = new[] { new BattleEvent(BattleEventKind.UnitDamaged, TargetId, null, null) };
            return CommandResult.Success;
        }

        private static UnitState FindUnit(BattleState s, int id)
        {
            foreach (var u in s.Units)
                if (u.UnitId == id) return u;
            return null;
        }
    }
}
