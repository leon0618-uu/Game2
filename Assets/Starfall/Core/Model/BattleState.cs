using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Starfall.Core.Model
{
    public sealed class BattleState
    {
        public const ulong Fnv1aOffsetBasis = 0xCBF29CE484222325UL;
        public const ulong Fnv1aPrime = 0x100000001B3UL;

        public int TurnNumber { get; set; }
        public Owner ActivePlayer { get; set; }
        public BoardState Board { get; }
        private readonly List<UnitState> _units;
        public IReadOnlyList<UnitState> Units => _units;

        private readonly List<Starfall.Core.Status.StatusInstance> _statuses;
        public IReadOnlyList<Starfall.Core.Status.StatusInstance> Statuses => _statuses;

        private readonly Starfall.Core.Anchor.AnchorRegistry _anchors;
        public Starfall.Core.Anchor.AnchorRegistry Anchors => _anchors;

        private readonly Starfall.Core.Decree.DecreeRegistry _decrees;
        public Starfall.Core.Decree.DecreeRegistry Decrees => _decrees;

        /// <summary>
        /// 下一个 StatusInstance 的 ID（确定性递增，用于 Replay 重放）。
        /// </summary>
        public int NextStatusInstanceId { get; set; }

        /// <summary>
        /// 关卡阶段（Task 19 关卡闭环）。默认 <see cref="Starfall.Core.Combat.ObjectivePhase.Guard"/>；
        /// 完成 <see cref="GuardsRequired"/> 个完整回合后切 <see cref="Starfall.Core.Combat.ObjectivePhase.Retreat"/>；
        /// 胜负已定后切 <see cref="Starfall.Core.Combat.ObjectivePhase.Ended"/>。
        /// </summary>
        public Starfall.Core.Combat.ObjectivePhase CurrentPhase { get; set; } = Starfall.Core.Combat.ObjectivePhase.Guard;

        /// <summary>
        /// 已完成的防守次数。仅 <see cref="Starfall.Core.Combat.ObjectivePhase.Guard"/> 阶段累加。
        /// <see cref="GuardsCompleted"/> &gt;= <see cref="GuardsRequired"/> 即推进到 Retreat。
        /// </summary>
        public int GuardsCompleted { get; set; } = 0;

        /// <summary>
        /// 防守次数门槛。MVP 默认 3；BattleDefinition 可覆盖。
        /// </summary>
        public int GuardsRequired { get; set; } = 3;

        /// <summary>
        /// 撤离格（Retreat 目标）。<c>null</c> 表示无撤离目标；MVP 默认 <c>null</c>。
        /// 推进条件：所有存活 Player 单位均站在 ExitTile 的 4 邻居之一。
        /// </summary>
        public GridPos? ExitTile { get; set; } = null;

        public BattleState(int turnNumber, Owner activePlayer, BoardState board, IEnumerable<UnitState> units)
        {
            if (turnNumber < 0)
                throw new ArgumentException("TurnNumber must be >= 0", nameof(turnNumber));
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer;
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _units = new List<UnitState>(units ?? Array.Empty<UnitState>());
            _statuses = new List<Starfall.Core.Status.StatusInstance>();
            _anchors = new Starfall.Core.Anchor.AnchorRegistry();
            _decrees = new Starfall.Core.Decree.DecreeRegistry();
            NextStatusInstanceId = 0;
            CurrentPhase = Starfall.Core.Combat.ObjectivePhase.Guard;
            GuardsCompleted = 0;
            GuardsRequired = 3;
            ExitTile = null;
        }

        public void AddUnit(UnitState u)
        {
            if (u == null) throw new ArgumentNullException(nameof(u));
            _units.Add(u);
        }

        public void AddStatus(Starfall.Core.Status.StatusInstance s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            _statuses.Add(s);
        }

        public bool RemoveStatus(int instanceId)
        {
            for (int i = 0; i < _statuses.Count; i++)
            {
                if (_statuses[i].InstanceId == instanceId)
                {
                    _statuses.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// PostStateHash：FNV-1a 64 位链式哈希，字段顺序严格按 ADR-0001 §Decision 3-4。
        /// </summary>
        public ulong PostStateHash
        {
            get
            {
                ulong h = Fnv1aOffsetBasis;
                h = MixInt32(h, TurnNumber);
                h = MixByte(h, (byte)ActivePlayer);
                // 4.1 关卡阶段字段（Task 19）
                h = MixByte(h, (byte)CurrentPhase);
                h = MixInt32(h, GuardsCompleted);
                h = MixInt32(h, GuardsRequired);
                // ExitTile：是否为空 + (X, Y)；空则用 byte 0 占位
                h = MixByte(h, (byte)(ExitTile.HasValue ? 1 : 0));
                if (ExitTile.HasValue)
                {
                    h = MixByte(h, (byte)ExitTile.Value.X);
                    h = MixByte(h, (byte)ExitTile.Value.Y);
                }
                h = MixByte(h, (byte)Board.Width);
                h = MixByte(h, (byte)Board.Height);

                // 5. units: 按 UnitId 升序
                var sortedUnits = _units.OrderBy(u => u.UnitId);
                foreach (var u in sortedUnits)
                {
                    h = MixInt32(h, u.UnitId);
                    h = MixByte(h, (byte)u.Pos.X);
                    h = MixByte(h, (byte)u.Pos.Y);
                    h = MixInt16(h, (short)u.Hp);
                    h = MixInt16(h, (short)u.MaxHp);
                    h = MixByte(h, (byte)u.Phase);
                    h = MixByte(h, (byte)u.Owner);
                }

                // 6. tileStates: 按 (Y,X) 升序
                foreach (var kv in Board.TilesInDeterministicOrder())
                {
                    h = MixByte(h, (byte)kv.Key.Y);
                    h = MixByte(h, (byte)kv.Key.X);
                    h = MixByte(h, (byte)kv.Value);
                }

                // 6.5. anchors: 按 ZoneId 升序；每个 zone 的顶点按 (Y,X) 升序（GridPos.CompareTo 已在 AnchorZone 构造函数中规范化）
                h = MixByte(h, (byte)_anchors.ZonesInOrder.Count);
                foreach (var z in _anchors.ZonesInOrder)
                {
                    h = MixInt32(h, z.ZoneId);
                    h = MixByte(h, (byte)z.Vertices.Count);
                    foreach (var v in z.Vertices)
                    {
                        h = MixByte(h, (byte)v.X);
                        h = MixByte(h, (byte)v.Y);
                    }
                }

                // 7. statuses: 按 (Kind, RemainingTurns, InstanceId) 升序（ADR-0001 §Decision 4）
                h = MixByte(h, (byte)_statuses.Count);
                var sortedStatuses = _statuses.OrderBy(s => s, Starfall.Core.Status.StatusInstanceComparer.Instance);
                foreach (var s in sortedStatuses)
                {
                    h = MixByte(h, (byte)s.Kind);
                    h = MixInt32(h, s.RemainingTurns);
                    h = MixInt32(h, s.InstanceId);
                }

                // 8. decrees: 按 DecreeId 升序
                h = MixByte(h, (byte)_decrees.DecreesInOrder.Count);
                foreach (var d in _decrees.DecreesInOrder)
                {
                    h = MixByte(h, (byte)d.Kind);
                    h = MixInt32(h, d.TargetZoneId);
                    h = MixInt32(h, d.RemainingTurns);
                    h = MixByte(h, (byte)d.IssuingPlayer);
                }

                return h;
            }
        }

        private static ulong MixByte(ulong h, byte b)
        {
            h ^= b;
            h *= Fnv1aPrime;
            return h;
        }

        private static ulong MixInt16(ulong h, short v)
        {
            h = MixByte(h, (byte)(v & 0xFF));
            h = MixByte(h, (byte)((v >> 8) & 0xFF));
            return h;
        }

        private static ulong MixInt32(ulong h, int v)
        {
            h = MixByte(h, (byte)(v & 0xFF));
            h = MixByte(h, (byte)((v >> 8) & 0xFF));
            h = MixByte(h, (byte)((v >> 16) & 0xFF));
            h = MixByte(h, (byte)((v >> 24) & 0xFF));
            return h;
        }
    }
}
