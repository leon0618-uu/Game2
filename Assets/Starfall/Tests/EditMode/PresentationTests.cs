using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Model;
using Starfall.Unity.Presentation;

namespace Starfall.Tests.EditMode
{
    public class PresentationTests
    {
        private static BoardState MakeBoard(int w = 3, int h = 3)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        [Test]
        public void BoardSnapshot_FromState_OrdersByYX()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            var snap = BoardSnapshot.FromState(s);
            Assert.AreEqual(3, snap.Width);
            Assert.AreEqual(9, snap.Tiles.Count);
            Assert.AreEqual(new GridPos(0, 0), snap.Tiles[0].Pos);
        }

        [Test]
        public void HudSnapshot_FromState_ContainsTurnAndPlayer()
        {
            var s = new BattleState(5, Owner.Enemy, MakeBoard(), null);
            var snap = HudSnapshot.FromState(s, Starfall.Core.Combat.BattleOutcome.Ongoing);
            Assert.AreEqual(5, snap.TurnNumber);
            Assert.AreEqual(Owner.Enemy, snap.ActivePlayer);
            Assert.AreEqual("Ongoing", snap.Outcome);
        }

        [Test]
        public void UnitSnapshot_RoundTrips()
        {
            var snap = new UnitSnapshot(1, new GridPos(2, 3), 10, Phase.Light, Owner.Player);
            Assert.AreEqual(1, snap.UnitId);
            Assert.AreEqual(new GridPos(2, 3), snap.Pos);
            Assert.AreEqual(10, snap.Hp);
        }

        [Test]
        public void UnitIdKey_EqualityByValue()
        {
            var k1 = new UnitIdKey(42);
            var k2 = new UnitIdKey(42);
            Assert.AreEqual(k1, k2);
            Assert.AreEqual(k1.GetHashCode(), k2.GetHashCode());
        }

        [Test]
        public void PresentationEvent_StoresFields()
        {
            var pe = new PresentationEvent(PresentationEventKind.UnitMoveAnimated, 7, new GridPos(0, 0), new GridPos(1, 0));
            Assert.AreEqual(PresentationEventKind.UnitMoveAnimated, pe.Kind);
            Assert.AreEqual(7, pe.PrimaryUnitId);
            Assert.AreEqual(new GridPos(1, 0), pe.To);
        }

        [Test]
        public void IBoardPresenter_InterfaceUsableWithMock()
        {
            // Mock 测试：验证接口可注入与多态
            var mock = new MockBoardPresenter();
            var snap = new BoardSnapshot(2, 2, new List<TileSnapshot>());
            mock.Render(snap, System.Array.Empty<PresentationEvent>());
            Assert.IsTrue(mock.Called);
        }

        private class MockBoardPresenter : IBoardPresenter
        {
            public bool Called { get; private set; }
            public void Render(in BoardSnapshot snapshot, in IReadOnlyList<PresentationEvent> events)
            {
                Called = true;
            }
        }
    }
}