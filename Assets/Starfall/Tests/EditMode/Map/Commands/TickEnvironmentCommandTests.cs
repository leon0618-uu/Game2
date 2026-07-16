using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-11b <see cref="TickEnvironmentCommand"/> 测试集（≥ 6 测试）。
    /// 覆盖：单步 happy / 越界 phaseIndex / tick overflow / Undo / version。
    /// </summary>
    public class TickEnvironmentCommandTests
    {
        private static MapState MakeMap(int tickAccumulator = 0)
        {
            var map = new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, 0));
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    map.AddTile(new GridCoord(x, y));
            map.EnvironmentTickAccumulator = tickAccumulator;
            return map;
        }

        // ──────────── 1) Happy path ────────────

        [Test]
        public void Execute_NormalPhase_IncrementsTick_ExecutesPhase()
        {
            var map = MakeMap();
            var cmd = new TickEnvironmentCommand(phaseIndex: 0, tickDelta: 1);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, map.EnvironmentTickAccumulator);
        }

        // ──────────── 2) 不同 phase index 都接受 ────────────

        [Test]
        public void Execute_Phase9_IsAllowed()
        {
            var map = MakeMap();
            var cmd = new TickEnvironmentCommand(phaseIndex: 9);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Execute_Phase0_IsAllowed()
        {
            var map = MakeMap();
            var cmd = new TickEnvironmentCommand(phaseIndex: 0);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
        }

        // ──────────── 3) 越界 phaseIndex 失败 ────────────

        [Test]
        public void Execute_PhaseIndexOutOfRange_Fails()
        {
            var map = MakeMap();
            var cmd = new TickEnvironmentCommand(phaseIndex: 10);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.That(result.FailureReason, Does.Contain("phase index"));
        }

        // ──────────── 4) 构造时负 phaseIndex 抛异常 ────────────

        [Test]
        public void Constructor_NegativePhaseIndex_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new TickEnvironmentCommand(-1));
        }

        [Test]
        public void Constructor_PhaseOver9_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new TickEnvironmentCommand(10));
        }

        // ──────────── 5) Tick overflow 失败 ────────────

        [Test]
        public void Execute_TickOverflow_Fails()
        {
            var map = MakeMap(tickAccumulator: int.MaxValue - 5);
            var cmd = new TickEnvironmentCommand(phaseIndex: 0, tickDelta: 10);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.That(result.FailureReason, Does.Contain("tick overflow"));
        }

        // ──────────── 6) Undo 恢复原 tick ────────────

        [Test]
        public void Undo_RestoresOldTickAccumulator()
        {
            var map = MakeMap(tickAccumulator: 5);
            int oldTick = map.EnvironmentTickAccumulator;
            var cmd = new TickEnvironmentCommand(phaseIndex: 0, tickDelta: 3);
            cmd.Execute(map);
            Assert.AreEqual(8, map.EnvironmentTickAccumulator);
            cmd.Undo(map);
            Assert.AreEqual(oldTick, map.EnvironmentTickAccumulator);
        }

        // ──────────── 7) Undo without Execute 抛 ────────────

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap();
            var cmd = new TickEnvironmentCommand(phaseIndex: 0);
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 8) CommandId format ────────────

        [Test]
        public void CommandId_Format()
        {
            var cmd = new TickEnvironmentCommand(phaseIndex: 5);
            Assert.AreEqual("tick-environment:5", cmd.CommandId);
            Assert.AreEqual(1, cmd.Version);
        }
    }
}
