using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Starfall.Core.Anchor;
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
        public void BoardSnapshot_FromState_IncludesUnitsSortedById()
        {
            // Task 16: Units 现在属于 BoardSnapshot
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(2, new GridPos(1, 0), 10, 10, Phase.Light, Owner.Enemy));
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            s.AddUnit(new UnitState(3, new GridPos(2, 0), 10, 10, Phase.Dark, Owner.Enemy));
            var snap = BoardSnapshot.FromState(s);
            Assert.AreEqual(3, snap.Units.Count);
            Assert.AreEqual(1, snap.Units[0].UnitId);
            Assert.AreEqual(2, snap.Units[1].UnitId);
            Assert.AreEqual(3, snap.Units[2].UnitId);
        }

        [Test]
        public void BoardSnapshot_FromState_IncludesAnchorsSortedByZoneId()
        {
            // Task 16: Anchors 现在属于 BoardSnapshot
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.Anchors.Register(new AnchorZone(3, "Enemy", new[]
            {
                new GridPos(5, 5), new GridPos(6, 5), new GridPos(6, 6)
            }));
            s.Anchors.Register(new AnchorZone(1, "Player", new[]
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(1, 1)
            }));
            var snap = BoardSnapshot.FromState(s);
            Assert.AreEqual(2, snap.Anchors.Count);
            Assert.AreEqual(1, snap.Anchors[0].ZoneId);
            Assert.AreEqual(3, snap.Anchors[1].ZoneId);
            Assert.AreEqual("Player", snap.Anchors[0].Owner);
        }

        [Test]
        public void BoardSnapshot_BackwardCompatible_3ArgCtor_DefaultsUnitsAndAnchorsToEmpty()
        {
            var tiles = new List<TileSnapshot> { new TileSnapshot(new GridPos(0, 0), TileState.Normal) };
            var snap = new BoardSnapshot(1, 1, tiles);
            Assert.AreEqual(0, snap.Units.Count);
            Assert.AreEqual(0, snap.Anchors.Count);
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

        // ====== Task 16: BoardPalette 纯函数测试（确定性，颜色固定）======

        [Test]
        public void BoardPalette_TileColor_MapsByState()
        {
            Assert.AreEqual(BoardPalette.TileNormal, BoardPalette.TileColor(TileState.Normal));
            Assert.AreEqual(BoardPalette.TileBlocked, BoardPalette.TileColor(TileState.Blocked));
            Assert.AreEqual(BoardPalette.TileHazard, BoardPalette.TileColor(TileState.Hazard));
            Assert.AreEqual(BoardPalette.TileObjective, BoardPalette.TileColor(TileState.Objective));
        }

        [Test]
        public void BoardPalette_UnitColor_DistinguishesPhaseAndOwner()
        {
            var pLight = BoardPalette.UnitColor(Phase.Light, Owner.Player);
            var pDark  = BoardPalette.UnitColor(Phase.Dark,  Owner.Player);
            var eLight = BoardPalette.UnitColor(Phase.Light, Owner.Enemy);
            var eDark  = BoardPalette.UnitColor(Phase.Dark,  Owner.Enemy);
            // Light ≠ Dark in 蓝色通道
            Assert.AreNotEqual(pLight, pDark);
            Assert.AreNotEqual(eLight, eDark);
            // Player ≠ Enemy
            Assert.AreNotEqual(pLight, eLight);
            Assert.AreNotEqual(pDark,  eDark);
        }

        [Test]
        public void BoardPalette_OutcomeColor_MapsByOutcomeString()
        {
            Assert.AreEqual(BoardPalette.HudOutcomeOngoing,     BoardPalette.OutcomeColor("Ongoing"));
            Assert.AreEqual(BoardPalette.HudOutcomePlayerWins, BoardPalette.OutcomeColor("PlayerWins"));
            Assert.AreEqual(BoardPalette.HudOutcomeEnemyWins,  BoardPalette.OutcomeColor("EnemyWins"));
            Assert.AreEqual(BoardPalette.HudOutcomeDraw,       BoardPalette.OutcomeColor("Draw"));
        }

        [Test]
        public void BoardPalette_AnchorColor_DefaultsNeutral()
        {
            Assert.AreEqual(BoardPalette.AnchorPlayer,  BoardPalette.AnchorColor("Player"));
            Assert.AreEqual(BoardPalette.AnchorEnemy,   BoardPalette.AnchorColor("Enemy"));
            Assert.AreEqual(BoardPalette.AnchorNeutral, BoardPalette.AnchorColor("Other"));
            Assert.AreEqual(BoardPalette.AnchorNeutral, BoardPalette.AnchorColor(null));
        }

        // ====== Task 16: AnchorSnapshot 基础测试 ======

        [Test]
        public void AnchorSnapshot_StoresFields()
        {
            var verts = new List<GridPos>
            {
                new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1)
            };
            var snap = new AnchorSnapshot(42, "Player", verts);
            Assert.AreEqual(42, snap.ZoneId);
            Assert.AreEqual("Player", snap.Owner);
            Assert.AreEqual(3, snap.Vertices.Count);
        }

        [Test]
        public void AnchorSnapshot_NullOwner_DefaultsToNeutral()
        {
            var snap = new AnchorSnapshot(1, null, new List<GridPos>());
            Assert.AreEqual("Neutral", snap.Owner);
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