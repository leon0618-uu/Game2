using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 <see cref="RegisterAnchorLinkCommand"/> 测试集。
    /// <para/>
    /// 覆盖：Execute / Undo / Version / Dependencies / 重复 Id 拒绝。
    /// </summary>
    public class RegisterAnchorLinkCommandTests
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

        private static AnchorLink MakeLink(string linkId, string polyId = null)
        {
            polyId = polyId ?? ("poly-" + linkId);
            var poly = new ConstellationPolygon(
                new ConstellationPolygonId(polyId),
                new List<ConstellationVertex>
                {
                    new ConstellationVertex(0, 0, DimensionLayer.Reality),
                    new ConstellationVertex(4, 0, DimensionLayer.Reality),
                    new ConstellationVertex(0, 4, DimensionLayer.Reality),
                });
            return new AnchorLink(new AnchorLinkId(linkId), poly);
        }

        [Test]
        public void Execute_AddsLinkToMapState()
        {
            var link = MakeLink("L1");
            var cmd = new RegisterAnchorLinkCommand(link);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, _map.AnchorLinks.Count);
            Assert.AreEqual("L1", _map.AnchorLinks[0].Id.Value);
        }

        [Test]
        public void Execute_IncrementsVersion()
        {
            int v0 = _map.Version;
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            // 通过 executor 让 MapState.Version 自增
            var r = _exec.Run(cmd, _map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(v0 + 1, _map.Version);
            Assert.AreEqual(v0 + 1, r.NewVersion);
        }

        [Test]
        public void Execute_EmitsEvent()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.GreaterOrEqual(r.Events.Count, 1);
        }

        [Test]
        public void Undo_RemovesLink()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            _exec.Run(cmd, _map);
            Assert.AreEqual(1, _map.AnchorLinks.Count);
            _exec.UndoLast(_map);
            Assert.AreEqual(0, _map.AnchorLinks.Count);
        }

        [Test]
        public void Undo_WithoutExecute_Throws()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            Assert.Throws<System.InvalidOperationException>(() => cmd.Undo(_map));
        }

        [Test]
        public void Execute_DuplicateId_Fails()
        {
            var cmd1 = new RegisterAnchorLinkCommand(MakeLink("L1"));
            var cmd2 = new RegisterAnchorLinkCommand(MakeLink("L1"));
            Assert.IsTrue(_exec.Run(cmd1, _map).Success);
            var r = _exec.Run(cmd2, _map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("duplicate", r.FailureReason);
        }

        [Test]
        public void CommandId_FormatIsCorrect()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            Assert.AreEqual("register-anchor-link:L1", cmd.CommandId);
        }

        [Test]
        public void Version_IsOne()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            Assert.AreEqual(1, cmd.Version);
        }

        [Test]
        public void Dependencies_IsEmpty()
        {
            var cmd = new RegisterAnchorLinkCommand(MakeLink("L1"));
            Assert.AreEqual(0, cmd.Dependencies.Count);
        }
    }
}