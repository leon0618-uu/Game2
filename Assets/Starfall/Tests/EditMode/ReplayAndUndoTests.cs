using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Replay;
using Starfall.Core.Undo;

namespace Starfall.Tests.EditMode
{
    public class ReplayAndUndoTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeState(int unitId = 1, GridPos? pos = null)
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(unitId, pos ?? new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void CommandRecorder_AddsRecords()
        {
            var s = MakeState();
            var rec = new CommandRecorder();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            CommandExecutor.Run(s, move, out var events);
            rec.Record(move, events);
            Assert.AreEqual(1, rec.Records.Count);
            Assert.AreEqual(1, rec.Records[0].Sequence);
        }

        [Test]
        public void CommandRecorder_SequenceIncrements()
        {
            var s = MakeState();
            var rec = new CommandRecorder();
            for (int i = 0; i < 3; i++)
            {
                rec.Record(new EndTurnCommand(i + 1, s.ActivePlayer), System.Array.Empty<BattleEvent>());
            }
            Assert.AreEqual(3, rec.Records.Count);
            Assert.AreEqual(1, rec.Records[0].Sequence);
            Assert.AreEqual(2, rec.Records[1].Sequence);
            Assert.AreEqual(3, rec.Records[2].Sequence);
        }

        [Test]
        public void ReplayPlayer_ProducesIdenticalHash()
        {
            var s = MakeState();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            CommandExecutor.Run(s, move, out var events);
            ulong expectedHash = s.PostStateHash;

            var rec = new CommandRecorder();
            rec.Record(move, events);

            var freshState = MakeState();
            var result = ReplayPlayer.Replay(freshState, rec.Records, expectedHash);
            Assert.IsTrue(result.HashMatches);
            Assert.AreEqual(1, result.ReplayedCount);  // 1 record replayed once
        }

        [Test]
        public void ReplayPlayer_DetectsHashMismatch()
        {
            var s = MakeState();
            var rec = new CommandRecorder();
            ulong fakeHash = 0xDEADBEEFUL;
            var freshState = MakeState();
            var result = ReplayPlayer.Replay(freshState, rec.Records, fakeHash);
            Assert.IsFalse(result.HashMatches);
            Assert.AreEqual(fakeHash, result.ExpectedHash);
        }

        [Test]
        public void UndoStack_PushAndPop()
        {
            var s = MakeState();
            var undo = new UndoStack();
            undo.Push(s);
            Assert.AreEqual(1, undo.Count);
            Assert.IsTrue(undo.TryUndo(out var restored));
            Assert.IsNotNull(restored);
            Assert.AreEqual(0, undo.Count);
        }

        [Test]
        public void UndoStack_DeepCopyPreventsMutation()
        {
            var s = MakeState();
            var undo = new UndoStack();
            undo.Push(s);
            s.Units[0].Hp = 1;
            undo.TryUndo(out var restored);
            Assert.AreEqual(10, restored.Units[0].Hp);
            Assert.AreEqual(1, s.Units[0].Hp);
        }

        [Test]
        public void UndoStack_RespectsMaxDepth()
        {
            var s = MakeState();
            var undo = new UndoStack(maxDepth: 3);
            for (int i = 0; i < 5; i++) undo.Push(s);
            Assert.AreEqual(3, undo.Count);
        }

        [Test]
        public void UndoStack_TryUndoOnEmptyReturnsFalse()
        {
            var undo = new UndoStack();
            Assert.IsFalse(undo.TryUndo(out _));
        }
    }
}