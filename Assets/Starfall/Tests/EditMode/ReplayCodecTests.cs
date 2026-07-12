using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Replay;
using Starfall.Core.Status;

namespace Starfall.Tests.EditMode
{
    public class ReplayCodecTests
    {
        private static BoardState MakeBoard(int w = 4, int h = 4)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tiles[new GridPos(x, y)] = TileState.Normal;
            return new BoardState(w, h, tiles);
        }

        private static BattleState MakeState()
        {
            var s = new BattleState(0, Owner.Player, MakeBoard(), null);
            s.AddUnit(new UnitState(1, new GridPos(0, 0), 10, 10, Phase.Light, Owner.Player));
            return s;
        }

        [Test]
        public void Capture_ProducesCorrectHashAndEntry()
        {
            var s = MakeState();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            CommandExecutor.Run(s, move, out var events);
            ulong hash = s.PostStateHash;

            var rec = new CommandRecorder();
            rec.Record(move, events);
            var file = ReplayCodec.Capture(s, rec.Records);
            Assert.AreEqual(hash, file.FinalHash);
            Assert.AreEqual(1, file.Commands.Count);
            Assert.AreEqual("Move", file.Commands[0].Type);
            Assert.AreEqual(1, file.Commands[0].CommandId);
        }

        [Test]
        public void WriteRead_RoundtripsFile()
        {
            var s = MakeState();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            CommandExecutor.Run(s, move, out _);
            var rec = new CommandRecorder();
            rec.Record(move, new List<BattleEvent>());
            var file = ReplayCodec.Capture(s, rec.Records);

            var tmp = Path.Combine(Path.GetTempPath(), $"replay_{System.Guid.NewGuid():N}.json");
            ReplayCodec.WriteFile(file, tmp);
            var loaded = ReplayCodec.ReadFile(tmp);
            Assert.AreEqual(file.FinalHash, loaded.FinalHash);
            Assert.AreEqual(1, loaded.Commands.Count);
            File.Delete(tmp);
        }

        [Test]
        public void ReconstructCommands_MoveCommandRoundTrip()
        {
            var s = MakeState();
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            CommandExecutor.Run(s, move, out _);
            var rec = new CommandRecorder();
            rec.Record(move, new List<BattleEvent>());
            var file = ReplayCodec.Capture(s, rec.Records);

            var cmds = ReplayCodec.ReconstructCommands(file);
            Assert.AreEqual(1, cmds.Count);
            var m = (MoveCommand)cmds[0];
            Assert.AreEqual(1, m.UnitId);
            Assert.AreEqual(new GridPos(0, 0), m.From);
            Assert.AreEqual(new GridPos(1, 0), m.To);
        }

        [Test]
        public void ReconstructCommands_ApplyStatusRoundTrip()
        {
            var s = MakeState();
            var apply = new ApplyStatusCommand(1, 1, StatusKind.Burn, 2, 1);
            CommandExecutor.Run(s, apply, out _);
            var rec = new CommandRecorder();
            rec.Record(apply, new List<BattleEvent>());
            var file = ReplayCodec.Capture(s, rec.Records);

            var cmds = ReplayCodec.ReconstructCommands(file);
            Assert.AreEqual(1, cmds.Count);
            var a = (ApplyStatusCommand)cmds[0];
            Assert.AreEqual(StatusKind.Burn, a.Kind);
            Assert.AreEqual(2, a.RemainingTurns);
            Assert.AreEqual(1, a.SourceUnitId);
        }

        [Test]
        public void FullRoundTrip_HashMatchesOriginal()
        {
            var s = MakeState();
            var runner = new BattleRunner(s);
            var path = new List<GridPos> { new GridPos(0, 0), new GridPos(1, 0) };
            var move = new MoveCommand(1, 1, new GridPos(0, 0), new GridPos(1, 0), path);
            runner.Submit(move);
            var apply = new ApplyStatusCommand(2, 1, StatusKind.Burn, 2, 1);
            runner.Submit(apply);

            // 捕获
            var rec = new CommandRecorder();
            rec.Record(move, new List<BattleEvent>());
            rec.Record(apply, new List<BattleEvent>());
            var file = ReplayCodec.Capture(s, rec.Records);
            ulong expected = s.PostStateHash;

            // 写盘 + 读回
            var tmp = Path.Combine(Path.GetTempPath(), $"replay_{System.Guid.NewGuid():N}.json");
            ReplayCodec.WriteFile(file, tmp);
            var loaded = ReplayCodec.ReadFile(tmp);

            // 重放
            var freshState = MakeState();
            var cmds = ReplayCodec.ReconstructCommands(loaded);
            foreach (var c in cmds) CommandExecutor.Run(freshState, c, out _);
            Assert.AreEqual(expected, freshState.PostStateHash);
            File.Delete(tmp);
        }

        [Test]
        public void ReadFile_MissingThrows()
        {
            Assert.Throws<DefinitionException>(() =>
                ReplayCodec.ReadFile(Path.Combine(Path.GetTempPath(), "nonexistent_" + System.Guid.NewGuid().ToString("N") + ".json")));
        }
    }
}
