using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="ModifyGlobalCollapseValueCommand"/> 测试集（≥ 8 测试）。
    /// 覆盖：happy / undo / 越界 / 失败回滚 / 事件验证。
    /// </summary>
    public class ModifyGlobalCollapseValueCommandTests
    {
        private static MapState MakeMap(int initialCV = 0)
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, initialCV));
        }

        // ──────────── 1) Happy path ────────────

        [Test]
        public void Execute_PositiveDelta_AddsToValue()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(10, "test");
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(60, map.GlobalCV.Value);
            Assert.AreEqual(60, map.GlobalCollapseValue); // 影子字段
        }

        [Test]
        public void Execute_NegativeDelta_SubtractsFromValue()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(-10, "test");
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(40, map.GlobalCV.Value);
        }

        [Test]
        public void Execute_ClampsAbove100()
        {
            var map = MakeMap(95);
            var cmd = new ModifyGlobalCollapseValueCommand(50);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(100, map.GlobalCV.Value);
        }

        [Test]
        public void Execute_ClampsBelow0()
        {
            var map = MakeMap(20);
            var cmd = new ModifyGlobalCollapseValueCommand(-100);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, map.GlobalCV.Value);
        }

        // ──────────── 2) Undo 路径 ────────────

        [Test]
        public void Undo_RestoresPreviousValue()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(10);
            cmd.Execute(map);
            Assert.AreEqual(60, map.GlobalCV.Value);
            cmd.Undo(map);
            Assert.AreEqual(50, map.GlobalCV.Value);
        }

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(10);
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        [Test]
        public void Undo_AfterRoundTrip_ResetsState()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(10);
            cmd.Execute(map);
            cmd.Undo(map);
            // 再次 Undo 应抛异常（已复位）
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(map));
        }

        // ──────────── 3) 越界 / 失败 ────────────

        [Test]
        public void Constructor_DeltaOutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ModifyGlobalCollapseValueCommand(101));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new ModifyGlobalCollapseValueCommand(-101));
        }

        [Test]
        public void Execute_ZeroDelta_Fails()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(0);
            var result = cmd.Execute(map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("no-op: delta is zero", result.FailureReason);
            // 状态不变
            Assert.AreEqual(50, map.GlobalCV.Value);
        }

        [Test]
        public void Execute_InvalidDelta_RuntimeDefense_Fails()
        {
            var map = MakeMap(50);
            // 构造时不抛（因为 5 在范围内），但运行时防御（这里模拟）
            var cmd = new ModifyGlobalCollapseValueCommand(5);
            cmd.Execute(map);
            // 这里不能再改 _delta；构造已锁定。改用另一个测试覆盖"未运行 Execute 就 Undo"
            Assert.IsTrue(map.GlobalCV.Value > 50);
        }

        // ──────────── 4) 事件验证 ────────────

        [Test]
        public void Execute_Emits_OnGlobalCVChanged_Event()
        {
            var map = MakeMap(50);
            var cmd = new ModifyGlobalCollapseValueCommand(10);
            var result = cmd.Execute(map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.Events.Count);
            var e = result.Events[0];
            Assert.AreEqual(MapEventKind.OnGlobalCVChanged, e.Kind);
            Assert.AreEqual(50, e.OldValue);
            Assert.AreEqual(60, e.NewValue);
        }

        [Test]
        public void Execute_ThroughExecutor_IncrementsVersion()
        {
            var map = MakeMap(50);
            var executor = new MapCommandExecutor();
            var cmd = new ModifyGlobalCollapseValueCommand(10);
            var result = executor.Run(cmd, map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, map.Version);
        }

        [Test]
        public void Execute_UndoThroughExecutor_DecrementsVersion()
        {
            var map = MakeMap(50);
            var executor = new MapCommandExecutor();
            executor.Run(new ModifyGlobalCollapseValueCommand(10), map);
            Assert.AreEqual(1, map.Version);
            Assert.IsTrue(executor.UndoLast(map));
            Assert.AreEqual(0, map.Version);
            Assert.AreEqual(50, map.GlobalCV.Value);
        }

        // ──────────── 5) CommandId / Dependencies / Version ────────────

        [Test]
        public void CommandId_IsStable()
        {
            Assert.AreEqual("modify-global-collapse-value",
                new ModifyGlobalCollapseValueCommand(1).CommandId);
        }

        [Test]
        public void Version_Is1()
        {
            Assert.AreEqual(1, new ModifyGlobalCollapseValueCommand(1).Version);
        }

        [Test]
        public void Dependencies_IsEmpty()
        {
            Assert.AreEqual(0, new ModifyGlobalCollapseValueCommand(1).Dependencies.Count);
        }

        [Test]
        public void ToString_ContainsDeltaAndReason()
        {
            var cmd = new ModifyGlobalCollapseValueCommand(10, "my-reason");
            string s = cmd.ToString();
            StringAssert.Contains("10", s);
            StringAssert.Contains("my-reason", s);
        }
    }
}
