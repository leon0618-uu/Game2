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
