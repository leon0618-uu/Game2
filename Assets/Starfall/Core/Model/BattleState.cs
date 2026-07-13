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

        public BattleState(int turnNumber, Owner activePlayer, BoardState board, IEnumerable<UnitState> units)
        {
            if (turnNumber < 0)
                throw new ArgumentException("TurnNumber must be >= 0", nameof(turnNumber));
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer;
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _units = new List<UnitState>(units ?? Array.Empty<UnitState>());
        }

        public void AddUnit(UnitState u)
        {
            if (u == null) throw new ArgumentNullException(nameof(u));
            _units.Add(u);
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

                // 7. statuses: 本任务暂未实现，预留空位（byte count=0）
                h = MixByte(h, 0);

                // 8. pendingDecrees: 本任务暂未实现，预留空位（byte count=0）
                h = MixByte(h, 0);

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
