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
    /// doc2 MAP-12 <see cref="TransitionAnchorLinkStateCommand"/> 测试集。
    /// <para/>
    /// 覆盖：合法迁移 / 非法迁移 fail / Execute/Undo / Dependencies。
    /// </summary>
    public class TransitionAnchorLinkStateCommandTests
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

        private static AnchorLink MakeLink(string linkId,
            AnchorZoneState initialState = AnchorZoneState.Inactive)
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
                    }),
                initialState: initialState);
        }

        private void RegisterLink(string linkId, AnchorZoneState initial = AnchorZoneState.Inactive)
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink(linkId, initial)), _map);
        }

        [Test]
        public void Execute_LegalTransition_UpdatesState()
        {
            RegisterLink("L1");
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 5);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(_map.TryGetAnchorLink(new AnchorLinkId("L1"), out var link));
            Assert.AreEqual(AnchorZoneState.PlayerControlled, link.CurrentState);
            Assert.AreEqual(5, link.StateTick);
        }

        [Test]
        public void Execute_IllegalTransition_Fails()
        {
            // Initial = Inactive → Destroyed 不在合法矩阵
            RegisterLink("L1");
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.Destroyed, 1);
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("illegal", r.FailureReason);
        }

        [Test]
        public void Execute_NonexistentLink_Fails()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("ghost"), AnchorZoneState.PlayerControlled, 1);
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.FailureReason);
        }

        [Test]
        public void Execute_NegativeTick_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TransitionAnchorLinkStateCommand(
                    new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, -1));
        }

        [Test]
        public void Undo_RestoresPreviousState()
        {
            RegisterLink("L1", AnchorZoneState.Inactive);
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 5);
            _exec.Run(cmd, _map);
            Assert.AreEqual(AnchorZoneState.PlayerControlled,
                _map.AnchorLinks[0].CurrentState);
            _exec.UndoLast(_map);
            Assert.AreEqual(AnchorZoneState.Inactive,
                _map.AnchorLinks[0].CurrentState);
        }

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1);
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(_map));
        }

        [Test]
        public void CommandId_FormatIsCorrect()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 0);
            Assert.AreEqual("transition-anchor-link-state:L1:PlayerControlled", cmd.CommandId);
        }

        [Test]
        public void Version_IsOne()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 0);
            Assert.AreEqual(1, cmd.Version);
        }

        [Test]
        public void Dependencies_IncludesRegisterCommand()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 0);
            var deps = cmd.Dependencies;
            Assert.AreEqual(1, deps.Count);
            Assert.AreEqual("register-anchor-link:L1", deps[0]);
        }

        [Test]
        public void Execute_DependencyNotMet_FailsViaExecutor()
        {
            // Transition 必须在 Register 之后跑。直接 Run 失败（依赖校验）。
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1);
            var r = _exec.Run(cmd, _map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("missing dependency", r.FailureReason);
        }

        [Test]
        public void Execute_IncrementsVersion()
        {
            RegisterLink("L1");
            int v0 = _map.Version;
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(v0 + 1, _map.Version);
        }

        [Test]
        public void Execute_EmitsEvent()
        {
            RegisterLink("L1");
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("L1"), AnchorZoneState.PlayerControlled, 1);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.GreaterOrEqual(r.Events.Count, 1);
        }
    }
}