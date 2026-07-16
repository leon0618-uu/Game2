using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 <see cref="BatchTransitionAnchorLinksCommand"/> 测试集。
    /// <para/>
    /// 覆盖：全合法成功 / 任一非法 fail + 零修改 / Undo 恢复全部。
    /// </summary>
    public class BatchTransitionAnchorLinksCommandTests
    {
        private MapState _map;
        private MapCommandExecutor _exec;

        [SetUp]
        public void SetUp()
        {
            _map = MapTestHarness.MakeMap();
            MapTestHarness.Attach(_map);
            _exec = new MapCommandExecutor();
        }

        [TearDown]
        public void TearDown()
        {
            MapTestHarness.DetachAll();
        }

        private static AnchorLink MakeLink(string linkId)
        {
            return new AnchorLink(
                new AnchorLinkId(linkId),
                new ConstellationPolygon(
                    new ConstellationPolygonId("poly-" + linkId),
                    new List<ConstellationVertex>
                    {
                        new ConstellationVertex(0, 0, DimensionLayer.Reality),
                        new ConstellationVertex(4, 0, DimensionLayer.Reality),
                        new ConstellationVertex(0, 4, DimensionLayer.Reality),
                    }));
        }

        private void RegisterLink(string linkId)
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink(linkId)), _map);
        }

        [Test]
        public void Execute_AllLegal_UpdatesAllLinks()
        {
            RegisterLink("L1");
            RegisterLink("L2");
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 5),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L2"), AnchorZoneState.EnemyControlled, 6),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);

            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out var l1);
            _map.TryGetAnchorLink(new AnchorLinkId("L2"), out var l2);
            Assert.AreEqual(AnchorZoneState.PlayerControlled, l1.CurrentState);
            Assert.AreEqual(AnchorZoneState.EnemyControlled, l2.CurrentState);
        }

        [Test]
        public void Execute_OneIllegal_ZeroModification()
        {
            RegisterLink("L1");
            RegisterLink("L2");
            // L1: Inactive → PlayerControlled 合法
            // L2: Inactive → Destroyed 不合法
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 5),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L2"), AnchorZoneState.Destroyed, 5),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("illegal", r.FailureReason);

            // 零修改：L1 仍是 Inactive
            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out var l1);
            _map.TryGetAnchorLink(new AnchorLinkId("L2"), out var l2);
            Assert.AreEqual(AnchorZoneState.Inactive, l1.CurrentState);
            Assert.AreEqual(AnchorZoneState.Inactive, l2.CurrentState);
        }

        [Test]
        public void Execute_NonexistentLink_Fails()
        {
            RegisterLink("L1");
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("ghost"), AnchorZoneState.PlayerControlled, 1),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.FailureReason);
        }

        [Test]
        public void Undo_AllLegal_AllRestored()
        {
            RegisterLink("L1");
            RegisterLink("L2");
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 5),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L2"), AnchorZoneState.EnemyControlled, 6),
            };
            _exec.Run(new BatchTransitionAnchorLinksCommand(entries), _map);
            _exec.UndoLast(_map);

            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out var l1);
            _map.TryGetAnchorLink(new AnchorLinkId("L2"), out var l2);
            Assert.AreEqual(AnchorZoneState.Inactive, l1.CurrentState);
            Assert.AreEqual(0, l1.StateTick);
            Assert.AreEqual(AnchorZoneState.Inactive, l2.CurrentState);
            Assert.AreEqual(0, l2.StateTick);
        }

        [Test]
        public void CommandId_Stable_AcrossRuns()
        {
            RegisterLink("L1");
            RegisterLink("L2");
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L2"), AnchorZoneState.PlayerControlled, 1),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            // CommandId 应当按 LinkId 升序：L1, L2
            Assert.AreEqual("batch-transition-anchor-links:L1,L2", cmd.CommandId);
        }

        [Test]
        public void Version_IsOne()
        {
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            Assert.AreEqual(1, cmd.Version);
        }

        [Test]
        public void Dependencies_AllRegisterIds_Sorted()
        {
            RegisterLink("L1");
            RegisterLink("L2");
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L2"), AnchorZoneState.PlayerControlled, 1),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1),
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.EnemyControlled, 2),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            var deps = cmd.Dependencies;
            // L1, L1, L2 → 去重 + 升序：[L1, L2]
            Assert.AreEqual(2, deps.Count);
            Assert.AreEqual("register-anchor-link:L1", deps[0]);
            Assert.AreEqual("register-anchor-link:L2", deps[1]);
        }

        [Test]
        public void Construct_EmptyEntries_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new BatchTransitionAnchorLinksCommand(new List<BatchTransitionAnchorLinksCommand.TransitionEntry>()));
        }

        [Test]
        public void Execute_DependencyNotMet_FailsViaExecutor()
        {
            // 直接 Run（未先 Register）：依赖校验失败
            var entries = new List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            var r = _exec.Run(cmd, _map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("missing dependency", r.FailureReason);
        }
    }
}