using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode
{
    public class FoundationStateTests
    {
        private static BoardState MakeBoard(int w = 3, int h = 3)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeEmpty(int turn = 0, Owner who = Owner.Player)
            => new BattleState(turn, who, MakeBoard(), null);

        private static BattleState MakeSingleUnit(int unitId = 1, GridPos? pos = null, int hp = 10)
        {
            var s = MakeEmpty();
            s.AddUnit(new UnitState(unitId, pos ?? new GridPos(0, 0), hp, hp, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void GridPos_CompareTo_OrdersByYThenX()
        {
            var list = new List<GridPos> {
                new GridPos(0, 2), new GridPos(1, 0), new GridPos(0, 1), new GridPos(2, 0)
            };
            list.Sort();
            Assert.AreEqual(new GridPos(1, 0), list[0]);
            Assert.AreEqual(new GridPos(2, 0), list[1]);
            Assert.AreEqual(new GridPos(0, 1), list[2]);
            Assert.AreEqual(new GridPos(0, 2), list[3]);
        }

        [Test]
        public void GridPos_RecordStruct_Equality()
        {
            Assert.AreEqual(new GridPos(1, 2), new GridPos(1, 2));
            Assert.AreNotEqual(new GridPos(1, 2), new GridPos(2, 1));
        }

        [Test]
        public void BattleState_Empty_HashIsDeterministic()
        {
            ulong h1 = MakeEmpty().PostStateHash;
            ulong h2 = MakeEmpty().PostStateHash;
            Assert.AreEqual(h1, h2);
            Assert.AreNotEqual(0UL, h1);
        }

        [Test]
        public void BattleState_DifferentTurnNumber_DifferentHash()
        {
            ulong h0 = MakeEmpty(0).PostStateHash;
            ulong h1 = MakeEmpty(1).PostStateHash;
            Assert.AreNotEqual(h0, h1);
        }

        [Test]
        public void BattleState_DifferentActivePlayer_DifferentHash()
        {
            ulong hP = MakeEmpty(0, Owner.Player).PostStateHash;
            ulong hE = MakeEmpty(0, Owner.Enemy).PostStateHash;
            Assert.AreNotEqual(hP, hE);
        }

        [Test]
        public void BattleState_UnitsReordered_SameHash()
        {
            var s1 = MakeEmpty();
            s1.AddUnit(new UnitState(1, new GridPos(0, 0), 5, 5, Phase.Light, Owner.Player));
            s1.AddUnit(new UnitState(2, new GridPos(1, 0), 5, 5, Phase.Light, Owner.Player));

            var s2 = MakeEmpty();
            s2.AddUnit(new UnitState(2, new GridPos(1, 0), 5, 5, Phase.Light, Owner.Player));
            s2.AddUnit(new UnitState(1, new GridPos(0, 0), 5, 5, Phase.Light, Owner.Player));

            Assert.AreEqual(s1.PostStateHash, s2.PostStateHash);
        }

        [Test]
        public void BattleState_TilesReordered_SameHash()
        {
            var tilesA = new Dictionary<GridPos, TileState>();
            tilesA[new GridPos(0, 0)] = TileState.Normal;
            tilesA[new GridPos(1, 0)] = TileState.Hazard;
            tilesA[new GridPos(0, 1)] = TileState.Objective;

            var tilesB = new Dictionary<GridPos, TileState>();
            tilesB[new GridPos(0, 1)] = TileState.Objective;
            tilesB[new GridPos(1, 0)] = TileState.Hazard;
            tilesB[new GridPos(0, 0)] = TileState.Normal;

            var bA = new BoardState(2, 2, tilesA);
            var bB = new BoardState(2, 2, tilesB);
            var sA = new BattleState(0, Owner.Player, bA, null);
            var sB = new BattleState(0, Owner.Player, bB, null);
            Assert.AreEqual(sA.PostStateHash, sB.PostStateHash);
        }

        [Test]
        public void Cloner_DeepCopy_IndependentOfSource()
        {
            var src = MakeSingleUnit(hp: 10);
            var clone = BattleStateCloner.Clone(src);
            Assert.IsNotNull(clone);
            clone.Units[0].Hp = 1;
            Assert.AreEqual(10, src.Units[0].Hp);
            Assert.AreEqual(1, clone.Units[0].Hp);
        }

        [Test]
        public void Cloner_DoesNotShareUnitReferences()
        {
            var src = MakeSingleUnit();
            var clone = BattleStateCloner.Clone(src);
            Assert.AreNotSame(src.Units[0], clone.Units[0]);
        }

        [Test]
        public void Comparer_Equals_TrueForClones()
        {
            var src = MakeSingleUnit();
            var clone = BattleStateCloner.Clone(src);
            Assert.IsTrue(BattleStateComparer.Equals(src, clone));
            Assert.AreEqual(src.PostStateHash, clone.PostStateHash);
        }

        [Test]
        public void Comparer_Equals_FalseForDifferentTurn()
        {
            var a = MakeEmpty(0);
            var b = MakeEmpty(1);
            Assert.IsFalse(BattleStateComparer.Equals(a, b));
        }

        [Test]
        public void Comparer_NullSafety()
        {
            Assert.IsFalse(BattleStateComparer.Equals(null, MakeEmpty()));
            Assert.IsFalse(BattleStateComparer.Equals(MakeEmpty(), null));
            Assert.IsTrue(BattleStateComparer.Equals(null, null));
        }
    }
}
