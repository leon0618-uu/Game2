using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Decree;
using Starfall.Core.Model;
using Starfall.Core.Pathfinding;
using Starfall.Core.Status;

namespace Starfall.Unity.Input
{
    /// <summary>
    /// 把 <see cref="ICommandPlan"/> 翻译为具体 <see cref="ICommand"/> 实例，并分配 CommandId。
    /// CommandId 起始于 1000，避开 BattleRunner 内部计数器（1..N），
    /// 保证 Replay / Debug 打印时外部命令和内部命令可区分。
    /// </summary>
    /// <remarks>
    /// 不持有 BattleState 真值；构造 Command 时读 BoardState / UnitState 只读视图。
    /// </remarks>
    public static class CommandBuilder
    {
        /// <summary>外部 CommandId 起始值；必须 > BattleRunner._nextCommandId 的可达范围（1..数百）。</summary>
        public const int ExternalCommandIdBase = 1000;

        // 简单单调递增计数器；同一进程多次 PlayMode 会续号，但 Replay 不依赖此值（ReplayCodec 自己重排）
        private static int _nextExternalId = ExternalCommandIdBase;

        public static int NextCommandId() => _nextExternalId++;

        public static void Reset() => _nextExternalId = ExternalCommandIdBase;

        /// <summary>
        /// 批量构建。
        /// </summary>
        public static List<ICommand> BuildAll(IReadOnlyList<ICommandPlan> plans, BattleState s)
        {
            var list = new List<ICommand>(plans.Count);
            foreach (var p in plans)
            {
                var cmd = Build(p, s);
                if (cmd != null) list.Add(cmd);
            }
            return list;
        }

        public static ICommand Build(ICommandPlan plan, BattleState s)
        {
            if (plan == null) return null;
            switch (plan)
            {
                case MovePlan m:      return BuildMove(m, s);
                case AttackPlan a:    return BuildAttack(a);
                case PhaseFlipPlan p: return BuildPhaseFlip(p);
                case DecreeHoldPlan d:return BuildDecreeHold(d);
                default: return null;
            }
        }

        // ===== Move =====

        private static ICommand BuildMove(MovePlan plan, BattleState s)
        {
            var unit = FindUnit(s, plan.UnitId);
            if (unit == null) return null;
            // 用 BFSPathfinder 算最短路径；不可达则降级为单步路径（from → to）
            var path = ComputePath(s, unit.Pos, plan.To);
            return new MoveCommand(NextCommandId(), plan.UnitId, unit.Pos, plan.To, path);
        }

        private static IReadOnlyList<GridPos> ComputePath(BattleState s, GridPos from, GridPos to)
        {
            IPathfinder pf = new BFSPathfinder();
            var path = pf.FindPath(s.Board, from, to);
            if (path != null && path.Count >= 2) return path;
            // 不可达（被阻挡 / 距离过远）→ 单步路径，仍能让 MoveCommand 校验失败
            return new[] { from, to };
        }

        // ===== Attack =====

        private static ICommand BuildAttack(AttackPlan plan)
            => new AttackCommand(NextCommandId(), plan.AttackerId, plan.TargetId, baseDamage: 3);

        // ===== PhaseFlip =====

        private static ICommand BuildPhaseFlip(PhaseFlipPlan plan)
            => new ApplyStatusCommand(
                NextCommandId(),
                targetUnitId: plan.TargetUnitId,
                kind: StatusKind.PhaseInvert,
                remainingTurns: 1,
                sourceUnitId: plan.SourceUnitId);

        // ===== Decree Hold =====

        private static ICommand BuildDecreeHold(DecreeHoldPlan plan)
        {
            // DecreeId 由 caller 提供（确定性）。
            // MVP: 用一个相对稳定的 hash（zoneId + 序号）作为 ID 占位；
            // 实际部署时 Lead / architect 应提供 DecreeId 分配器（Task 19 接入）。
            const uint Tag = 0xD0000000u;  // 避免与 BattleRunner 内部 id（1..N）和外部 id（1000+）冲突
            uint ownerTag = plan.IssuingPlayer == Owner.Player ? 0x100u : 0x200u;
            uint zoneTag = (uint)(plan.ZoneId & 0x0FFF);
            int decreeId = unchecked((int)(Tag | ownerTag | zoneTag));
            var decree = new Decree(decreeId, DecreeKind.Hold, plan.ZoneId, remainingTurns: 3, plan.IssuingPlayer);
            // ApplyDecreeCommand 的 Execute 只发 DecreeApplied 事件；真正的 Decree 注册需 caller 在 Submit 前完成。
            return new ApplyDecreeCommand(NextCommandId(), decree);
        }

        private static UnitState FindUnit(BattleState s, int unitId)
        {
            foreach (var u in s.Units)
                if (u.UnitId == unitId) return u;
            return null;
        }
    }
}