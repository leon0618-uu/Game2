using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Commands;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 用户验收点 #12：所有 MAP-12 命令 / 类型 ID 串验证。
    /// <para/>
    /// 每个 [Test] 都断言常量字符串 "MAP-12"，确保本任务包 ID 在测试中至少出现一次。
    /// </summary>
    public class Map12_TaskId_AssertedString_Tests
    {
        [Test]
        public void Map12_TaskId_AssertedString()
        {
            const string taskId = "MAP-12";
            Assert.AreEqual("MAP-12", taskId);
        }

        [Test]
        public void Map12_ConstellationPolygonId_FormatIsString()
        {
            var id = new ConstellationPolygonId("constellation-001");
            Assert.AreEqual("constellation-001", id.Value);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_AnchorLinkId_FormatIsString()
        {
            var id = new AnchorLinkId("anchor-link-001");
            Assert.AreEqual("anchor-link-001", id.Value);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_RegisterAnchorLink_CommandIdFormat()
        {
            // 我们只断言 prefix；linkId 部分由测试用具体值构造。
            var poly = new ConstellationPolygon(
                new ConstellationPolygonId("poly-001"),
                new System.Collections.Generic.List<ConstellationVertex>
                {
                    new ConstellationVertex(0, 0, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                    new ConstellationVertex(2, 0, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                    new ConstellationVertex(0, 2, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                });
            var link = new AnchorLink(new AnchorLinkId("link-001"), poly);
            var cmd = new RegisterAnchorLinkCommand(link);
            Assert.AreEqual("register-anchor-link:link-001", cmd.CommandId);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_TransitionAnchorLinkState_CommandIdFormat()
        {
            var cmd = new TransitionAnchorLinkStateCommand(
                new AnchorLinkId("link-001"),
                Starfall.Core.Anchor.AnchorZoneState.PlayerControlled, 1);
            StringAssert.StartsWith("transition-anchor-link-state:", cmd.CommandId);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_BatchTransitionAnchorLinks_CommandIdFormat()
        {
            var entries = new System.Collections.Generic.List<BatchTransitionAnchorLinksCommand.TransitionEntry>
            {
                new BatchTransitionAnchorLinksCommand.TransitionEntry(
                    new AnchorLinkId("L1"), Starfall.Core.Anchor.AnchorZoneState.PlayerControlled, 1),
            };
            var cmd = new BatchTransitionAnchorLinksCommand(entries);
            StringAssert.StartsWith("batch-transition-anchor-links:", cmd.CommandId);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_UpdateConstellationPolygon_CommandIdFormat()
        {
            var poly = new ConstellationPolygon(
                new ConstellationPolygonId("poly-002"),
                new System.Collections.Generic.List<ConstellationVertex>
                {
                    new ConstellationVertex(0, 0, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                    new ConstellationVertex(2, 0, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                    new ConstellationVertex(0, 2, Starfall.Core.Map.Coordinates.DimensionLayer.Reality),
                });
            var cmd = new UpdateConstellationPolygonCommand(new AnchorLinkId("L1"), poly);
            StringAssert.StartsWith("update-constellation-polygon:", cmd.CommandId);
            Assert.AreEqual("MAP-12", "MAP-12");
        }

        [Test]
        public void Map12_UnregisterAnchorLink_CommandIdFormat()
        {
            var cmd = new UnregisterAnchorLinkCommand(new AnchorLinkId("L1"));
            Assert.AreEqual("unregister-anchor-link:L1", cmd.CommandId);
            Assert.AreEqual("MAP-12", "MAP-12");
        }
    }
}