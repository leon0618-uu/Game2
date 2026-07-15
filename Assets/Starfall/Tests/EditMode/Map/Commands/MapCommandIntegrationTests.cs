using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Model;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 集成测试：多命令链式执行 + executor 跨命令依赖 + undo 链。
    /// <para/>
    /// 覆盖：链式 Run / 跨 Run 依赖 / 多次 Run 后 undo / Undo 后再 Run 失败依赖 /
    /// Version 恢复 / hash 跨 Run 一致。
    /// </summary>
    public class MapCommandIntegrationTests
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

        // ──────────── 1) 链式 Run 顺序与 Version ────────────

        [Test]
        public void Chain_FlipFlipModifyCV_VersionAccumulates()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            var r1 = executor.Run(new FlipTilePhaseCommand(MapTestHarness.FlippableTileId, DimensionLayer.Astral), _map);
            var r2 = executor.Run(new FlipTilePhaseCommand(MapTestHarness.GateTileId, DimensionLayer.Astral), _map);
            var r3 = executor.Run(new ModifyGlobalCVCommand(50), _map);
            Assert.IsTrue(r1.Success);
            Assert.IsTrue(r2.Success);
            Assert.IsTrue(r3.Success);
            Assert.AreEqual(v0 + 3, _map.Version);
            Assert.AreEqual(3, executor.HistoryCount);
        }

        // ──────────── 2) 跨 Run 依赖：ModifyAnchorState 必须在 CreateAnchorLink 之后跑 ────────────

        [Test]
        public void ModifyAnchorState_BeforeCreateAnchor_Fails_DependencyCheck()
        {
            var executor = new MapCommandExecutor();
            // 直接 Run 一个声明依赖 create-anchor-link:5 但未先跑的命令：
            var r = executor.Run(new ModifyAnchorStateCommand(5, AnchorZoneState.PlayerControlled), _map);
            Assert.IsFalse(r.Success);
            Assert.AreEqual("missing dependency: create-anchor-link:5", r.FailureReason);
        }

        [Test]
        public void ModifyAnchorState_AfterCreateAnchor_Succeeds()
        {
            var executor = new MapCommandExecutor();
            // step 1: create anchor
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2));
            var r1 = executor.Run(new CreateAnchorLinkCommand(5, "Player", verts), _map);
            Assert.IsTrue(r1.Success);
            // step 2: modify state on that zone（依赖满足）
            var r2 = executor.Run(new ModifyAnchorStateCommand(5, AnchorZoneState.PlayerControlled), _map);
            Assert.IsTrue(r2.Success, r2.FailureReason);
            Assert.AreEqual(AnchorZoneState.PlayerControlled, AnchorStateService.GetOrDefault(_map, 5));
        }

        // ──────────── 3) Undo 链恢复 Version ────────────

        [Test]
        public void Undo_Chain_RestoresInitialVersion_AndState()
        {
            var executor = new MapCommandExecutor();
            int v0 = _map.Version;
            executor.Run(new ModifyGlobalCVCommand(20), _map);
            executor.Run(new ModifyGlobalCVCommand(40), _map);
            executor.Run(new ModifyGlobalCVCommand(60), _map);
            Assert.AreEqual(v0 + 3, _map.Version);

            executor.UndoLast(_map);
            executor.UndoLast(_map);
            executor.UndoLast(_map);

            Assert.AreEqual(v0, _map.Version);
            Assert.AreEqual(0, _map.GlobalCollapseValue);
            Assert.AreEqual(0, executor.HistoryCount);
        }

        [Test]
        public void Undo_DependencyIsRechecked_OnNextRun()
        {
            var executor = new MapCommandExecutor();
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2));
            executor.Run(new CreateAnchorLinkCommand(7, "Player", verts), _map);
            // 此时 modify-anchor-state:7 应可 Run
            var r1 = executor.Run(new ModifyAnchorStateCommand(7, AnchorZoneState.PlayerControlled), _map);
            Assert.IsTrue(r1.Success);
            // Undo create anchor → ExecutedCommandIds 移除 create-anchor-link:7
            executor.UndoLast(_map);
            // 现在 modify-anchor-state:7 依赖 create-anchor-link:7 找不到了 → 失败
            // but the executor history at this point: ModifyAnchor (just undone in last call) was popped first,
            // so the next-to-pop is CreateAnchorLink. Need to clear all:
            while (executor.HistoryCount > 0) executor.UndoLast(_map);
            var r2 = executor.Run(new ModifyAnchorStateCommand(7, AnchorZoneState.PlayerControlled), _map);
            Assert.IsFalse(r2.Success);
            Assert.AreEqual("missing dependency: create-anchor-link:7", r2.FailureReason);
        }

        // ──────────── 4) Hash 跨 Run 一致 ────────────

        [Test]
        public void SameCommandSequence_SameFinalHash()
        {
            var mapA = MapTestHarness.MakeMap();
            MapTestHarness.Attach(mapA);
            var executorA = new MapCommandExecutor();
            executorA.Run(new ModifyGlobalCVCommand(40), mapA);
            var hashA = mapA.PostStateHash;

            var mapB = MapTestHarness.MakeMap();
            MapTestHarness.Attach(mapB);
            var executorB = new MapCommandExecutor();
            executorB.Run(new ModifyGlobalCVCommand(40), mapB);
            var hashB = mapB.PostStateHash;

            Assert.AreEqual(hashA, hashB, "Same command sequence should yield same MapState hash");

            MapTestHarness.DetachAll();
            // 重新 detach 让 _map 也清干净
        }

        // ──────────── 5) 复合场景：anchor create → state modify → LOS invalidate ────────────

        [Test]
        public void CompositeScenario_AnchorCreateStateModifyLOSInvalidate()
        {
            var executor = new MapCommandExecutor();
            // step 1
            var verts = MapTestHarness.Poly(new GridPos(0, 0), new GridPos(2, 0), new GridPos(0, 2));
            var r1 = executor.Run(new CreateAnchorLinkCommand(99, "Enemy", verts), _map);
            Assert.IsTrue(r1.Success);
            // step 2
            var r2 = executor.Run(new ModifyAnchorStateCommand(99, AnchorZoneState.EnemyControlled), _map);
            Assert.IsTrue(r2.Success);
            // step 3（无依赖）
            var r3 = executor.Run(new InvalidateLineOfSightCommand(), _map);
            Assert.IsTrue(r3.Success);
            Assert.AreEqual(3, executor.HistoryCount);
            Assert.AreEqual(AnchorZoneState.EnemyControlled, AnchorStateService.GetOrDefault(_map, 99));
        }

        // ──────────── 6) 多 Run 同一命令允许（CommandId 相同但不强制唯一）────────────

        [Test]
        public void Run_SameCommandId_Twice_ExecutorAllows()
        {
            var executor = new MapCommandExecutor();
            var r1 = executor.Run(new InvalidatePathGraphCommand(), _map);
            var r2 = executor.Run(new InvalidatePathGraphCommand(), _map);
            Assert.IsTrue(r1.Success);
            Assert.IsTrue(r2.Success);
            // CommandId "invalidate-path-graph" 在集合中只占 1 个槽位（SortedSet）
            Assert.AreEqual(1, executor.ExecutedCommandIds.Count);
            Assert.AreEqual(2, executor.HistoryCount);
        }

        // ──────────── 7) Failed command 不进入 history ────────────

        [Test]
        public void FailedCommand_DoesNotEnterHistory()
        {
            var executor = new MapCommandExecutor();
            executor.Run(new ModifyGlobalCVCommand(50), _map); // success
            executor.Run(new ModifyGlobalCVCommand(50), _map); // fail (same)
            // 50 == 50: actual current = 50；same value → fail；history 应只有 1 条
            Assert.AreEqual(1, executor.HistoryCount);
            Assert.AreEqual(1, executor.ExecutedCommandIds.Count);
        }

        // ──────────── 8) 清空后再 Run 同命令成功 ────────────

        [Test]
        public void Clear_AllowsReRun_OfAnyCommand()
        {
            var executor = new MapCommandExecutor();
            executor.Run(new ModifyGlobalCVCommand(50), _map);
            executor.Run(new ModifyGlobalCVCommand(60), _map);
            Assert.AreEqual(2, executor.HistoryCount);
            executor.Clear();
            Assert.AreEqual(0, executor.HistoryCount);
            // modify-global-cv 现在值 = 60，重新 Run 70 应该成功（值不同）
            var r = executor.Run(new ModifyGlobalCVCommand(70), _map);
            Assert.IsTrue(r.Success);
        }
    }
}
