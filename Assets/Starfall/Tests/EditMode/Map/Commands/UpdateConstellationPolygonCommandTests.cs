using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 <see cref="UpdateConstellationPolygonCommand"/> 测试集。
    /// <para/>
    /// 覆盖：Validator 拒绝 / Undo 恢复旧 Polygon。
    /// </summary>
    public class UpdateConstellationPolygonCommandTests
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

        private static AnchorLink MakeLink(string linkId, string polyId)
        {
            return new AnchorLink(
                new AnchorLinkId(linkId),
                new ConstellationPolygon(
                    new ConstellationPolygonId(polyId),
                    new List<ConstellationVertex>
                    {
                        new ConstellationVertex(0, 0, DimensionLayer.Reality),
                        new ConstellationVertex(4, 0, DimensionLayer.Reality),
                        new ConstellationVertex(0, 4, DimensionLayer.Reality),
                    }));
        }

        private static ConstellationPolygon MakePolygon(string polyId)
        {
            return new ConstellationPolygon(
                new ConstellationPolygonId(polyId),
                new List<ConstellationVertex>
                {
                    new ConstellationVertex(2, 2, DimensionLayer.Reality),
                    new ConstellationVertex(6, 2, DimensionLayer.Reality),
                    new ConstellationVertex(2, 6, DimensionLayer.Reality),
                });
        }

        [Test]
        public void Execute_ReplacesPolygon()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-old")), _map);
            var newPoly = MakePolygon("poly-new");
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), newPoly);
            var r = cmd.Execute(_map);
            Assert.IsTrue(r.Success);
            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out var link);
            Assert.AreEqual("poly-new", link.Polygon.Id.Value);
        }

        [Test]
        public void Execute_NonexistentLink_Fails()
        {
            var newPoly = MakePolygon("poly-new");
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("ghost"), newPoly);
            var r = cmd.Execute(_map);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("not found", r.FailureReason);
        }

        [Test]
        public void Undo_RestoresOldPolygon()
        {
            _exec.Run(new RegisterAnchorLinkCommand(MakeLink("L1", "poly-old")), _map);
            var newPoly = MakePolygon("poly-new");
            _exec.Run(new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), newPoly), _map);
            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out var link);
            Assert.AreEqual("poly-new", link.Polygon.Id.Value);
            _exec.UndoLast(_map);
            _map.TryGetAnchorLink(new AnchorLinkId("L1"), out link);
            Assert.AreEqual("poly-old", link.Polygon.Id.Value);
        }

        [Test]
        public void CommandId_FormatIsCorrect()
        {
            var poly = MakePolygon("poly-x");
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), poly);
            Assert.AreEqual("update-constellation-polygon:L1:poly-x", cmd.CommandId);
        }

        [Test]
        public void Version_IsOne()
        {
            var poly = MakePolygon("poly-x");
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), poly);
            Assert.AreEqual(1, cmd.Version);
        }

        [Test]
        public void Dependencies_IncludesRegister()
        {
            var poly = MakePolygon("poly-x");
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), poly);
            var deps = cmd.Dependencies;
            Assert.AreEqual(1, deps.Count);
            Assert.AreEqual("register-anchor-link:L1", deps[0]);
        }

        [Test]
        public void Construct_InvalidPolygon_Throws()
        {
            // < 3 vertices → 构造期抛
            var badPoly = new ConstellationPolygon(
                new ConstellationPolygonId("poly-bad"),
                new List<ConstellationVertex>
                {
                    new ConstellationVertex(0, 0, DimensionLayer.Reality),
                    new ConstellationVertex(1, 0, DimensionLayer.Reality),
                });
            Assert.Throws<System.ArgumentException>(() =>
                new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), badPoly));
        }
    }
}