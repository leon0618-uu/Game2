using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 <see cref="UnregisterAnchorLinkCommand"/> 测试集。
    /// <para/>
    /// 覆盖：Execute / Undo / 不存在 Id 拒绝。
    /// </summary>
    public class UnregisterAnchorLinkCommandTests
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
        public void Execute_RemovesExistingLink()
        {
            RegisterLink("L1");
            Assert.AreEqual(1, _map.AnchorLinks.Count);

            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(0, _map.AnchorLinks.Count);
        }

        [Test]
        public void Execute_NonexistentId_Fails()
        {
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("ghost"));
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.FailureReason);
        }

        [Test]
        public void Undo_RestoresLink()
        {
            RegisterLink("L1");
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            _exec.Run(cmd, _map);
            Assert.AreEqual(0, _map.AnchorLinks.Count);
            _exec.UndoLast(_map);
            Assert.AreEqual(1, _map.AnchorLinks.Count);
            Assert.AreEqual("L1", _map.AnchorLinks[0].Id.Value);
        }

        [Test]
        public void CommandId_FormatIsCorrect()
        {
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            Assert.AreEqual("unregister-anchor-link:L1", cmd.CommandId);
        }

        [Test]
        public void Version_IsOne()
        {
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            Assert.AreEqual(1, cmd.Version);
        }

        [Test]
        public void Execute_IncrementsVersion()
        {
            RegisterLink("L1");
            int v0 = _map.Version;
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            var r = _exec.Run(cmd, _map);
            Assert.IsTrue(r.Success);
            Assert.AreEqual(v0 + 1, _map.Version);
        }
    }
}