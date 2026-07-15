using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 <see cref="MapCommandExecutor"/> 测试集。
    /// <para/>
    /// 覆盖：Run 成功 / 失败 / Version 自增 / Dependencies 通过 / 不通过 /
    /// UndoLast 成功 / UndoLast 空 / CommandId 移除 / History 深度 / 多 Run 链式。
    /// </summary>
    public class MapCommandExecutorTests
    {
        private MapState _map;

        [SetUp]
        public void SetUp()
        {
            _map = MapTestHarness.MakeMap();
            MapTestHarness.Attach(_map);
        }

        [TearDown]
        public void TearDown()
        {
            MapTestHarness.DetachAll();
        }

        // ──────────── 1) Run 成功路径 ────────────

        [Test]
        public void Run_FlipTilePhase_Success_IncrementsVersion()
        {
            int v0 = _map.Version;
            var executor = new MapCommandExecutor();
            var result = executor.Run(
                new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral),
                _map);
            Assert.IsTrue(result.Success, result.FailureReason);
            Assert.AreEqual(v0 + 1, _map.Version);
            Assert.AreEqual(v0 + 1, result.NewVersion);
        }

        [Test]
        public void Run_ModifyGlobalCV_Success_IncrementsVersion()
        {
            int v0 = _map.Version;
            var executor = new MapCommandExecutor();
            var result = executor.Run(new ModifyGlobalCVCommand(50), _map);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(v0 + 1, _map.Version);
            Assert.AreEqual(50, _map.GlobalCollapseValue);
        }

        // ──────────── 2) Run 失败路径 ────────────

        [Test]
        public void Run_NullCommand_ReturnsFail_NoVersionChange()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            var result = executor.Run(null, _map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("cmd is null", result.FailureReason);
            Assert.AreEqual(v0, _map.Version);
        }

        [Test]
        public void Run_CommandReturnsFail_MapStateUntouched()
        {
            // FlipPhaseLocked returns "phase locked"
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            int globalCVBefore = _map.GlobalCollapseValue;
            var result = executor.Run(
                new FlipTilePhaseCommand(MapTestHarness.PhaseLockedTileId, DimensionLayer.Astral),
                _map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("phase locked", result.FailureReason);
            Assert.AreEqual(v0, _map.Version);
            Assert.AreEqual(globalCVBefore, _map.GlobalCollapseValue);
        }

        // ──────────── 3) Dependencies 校验 ────────────

        private sealed class DepCmd : IMapCommand
        {
            public int Version => 1;
            public string CommandId => "dep-test-cmd";
            public IReadOnlyList<string> Dependencies { get; }
            public DepCmd(params string[] deps) { Dependencies = deps; }
            public MapCommandResult Execute(MapState mapState)
                => MapCommandResult.Ok(_events, mapState.Version + 1);
            public void Undo(MapState mapState) { }
            private static readonly IReadOnlyList<MapEvent> _events = new List<MapEvent>();
        }

        [Test]
        public void Run_DependencyNotMet_FailsBeforeExecute()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            var result = executor.Run(new DepCmd("nonexistent-dep-id"), _map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("missing dependency: nonexistent-dep-id", result.FailureReason);
            Assert.AreEqual(v0, _map.Version);
        }

        [Test]
        public void Run_DependencyMet_Succeeds()
        {
            var executor = new MapCommandExecutor();
            // 先 Run 一个会加入 CommandId "modify-global-cv" 的命令
            executor.Run(new ModifyGlobalCVCommand(20), _map);
            // 然后 Run 一个声明依赖 "modify-global-cv" 的命令
            var result = executor.Run(new DepCmd("modify-global-cv"), _map);
            Assert.IsTrue(result.Success);
        }

        // ──────────── 4) UndoLast 路径 ────────────

        [Test]
        public void UndoLast_Success_DecrementsVersion_AndCallsCommandUndo()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            executor.Run(new ModifyGlobalCVCommand(75), _map);
            Assert.AreEqual(v0 + 1, _map.Version);
            Assert.AreEqual(75, _map.GlobalCollapseValue);

            bool undoOk = executor.UndoLast(_map);
            Assert.IsTrue(undoOk);
            Assert.AreEqual(v0, _map.Version);
            // ModifyGlobalCV undo restores prevCV (defaults to initialCV = 0).
            Assert.AreEqual(0, _map.GlobalCollapseValue);
        }

        [Test]
        public void UndoLast_EmptyHistory_ReturnsFalse_NoVersionChange()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            Assert.IsFalse(executor.UndoLast(_map));
            Assert.AreEqual(v0, _map.Version);
        }

        [Test]
        public void UndoLast_RemovesCommandIdFromExecuted()
        {
            var executor = new MapCommandExecutor();
            executor.Run(new ModifyGlobalCVCommand(10), _map);
            Assert.IsTrue(executor.ExecutedCommandIds.Contains("modify-global-cv"));
            executor.UndoLast(_map);
            Assert.IsFalse(executor.ExecutedCommandIds.Contains("modify-global-cv"));
        }

        // ──────────── 5) History depth 限制 ────────────

        [Test]
        public void MaxHistoryDepth_EvictsOldestCommandId()
        {
            var executor = new MapCommandExecutor(maxHistoryDepth: 2);
            executor.Run(new ModifyGlobalCVCommand(1), _map); // history depth = 1
            // 修改 1 → 100 后再 1 次（同一命令）以创建不同 NewVersion。
            // 为制造不同 CommandId，使用 cv 0/1 等不同值无效（同一 CommandId "modify-global-cv"）。
            // 改用 InvalidatePathGraph 不同 origin 制造不同 CommandId。
            executor.Run(new InvalidatePathGraphCommand(new GridCoord(0, 0)), _map);
            Assert.AreEqual(2, executor.HistoryCount);
            // 第 3 次 push → 弹掉最早（modify-global-cv）
            executor.Run(new InvalidatePathGraphCommand(new GridCoord(1, 0)), _map);
            Assert.AreEqual(2, executor.HistoryCount);
            Assert.IsFalse(executor.ExecutedCommandIds.Contains("modify-global-cv"));
            Assert.IsTrue(executor.ExecutedCommandIds.Contains("invalidate-path-graph:(0, 0, Reality)"));
            Assert.IsTrue(executor.ExecutedCommandIds.Contains("invalidate-path-graph:(1, 0, Reality)"));
        }

        // ──────────── 6) 多 Run 链式 Version 自增 ────────────

        [Test]
        public void Run_MultipleCommands_VersionAccumulates()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            executor.Run(new ModifyGlobalCVCommand(10), _map);
            executor.Run(new ModifyGlobalCVCommand(20), _map);
            executor.Run(new ModifyGlobalCVCommand(30), _map);
            Assert.AreEqual(v0 + 3, _map.Version);
            Assert.AreEqual(30, _map.GlobalCollapseValue);
            Assert.AreEqual(3, executor.HistoryCount);
        }

        // ──────────── 7) Clear ────────────

        [Test]
        public void Clear_EmptiesHistoryAndCommandIds()
        {
            var executor = new MapCommandExecutor();
            executor.Run(new ModifyGlobalCVCommand(10), _map);
            executor.Run(new InvalidatePathGraphCommand(), _map);
            Assert.AreEqual(2, executor.HistoryCount);
            executor.Clear();
            Assert.AreEqual(0, executor.HistoryCount);
            Assert.AreEqual(0, executor.ExecutedCommandIds.Count);
        }
    }
}
