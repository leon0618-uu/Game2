using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLinkHasher"/> 测试集。
    /// <para/>
    /// 覆盖：100 次一致性 / 字段差异 / 规范化顺序 / 跨 AnchorLink 区分 / null 安全。
    /// <para/>
    /// **per ADR-0009 §9**：tag 字节 0x43-0x47 + PostStateHash（由 (state, tick) 派生）参与哈希。
    /// </summary>
    public class AnchorLinkHasherTests
    {
        private static ConstellationPolygon MakePolygon(string polyId, int x1, int y1, int x2, int y2, int x3, int y3)
        {
            var verts = new List<ConstellationVertex>
            {
                new ConstellationVertex(x1, y1, DimensionLayer.Reality),
                new ConstellationVertex(x2, y2, DimensionLayer.Reality),
                new ConstellationVertex(x3, y3, DimensionLayer.Reality),
            };
            return new ConstellationPolygon(new ConstellationPolygonId(polyId), verts);
        }

        private static AnchorLink MakeLink(string linkId, string polyId,
            AnchorZoneState state = AnchorZoneState.Inactive,
            int tick = 0)
        {
            var poly = MakePolygon(polyId, 0, 0, 4, 0, 0, 4);
            return new AnchorLink(
                new AnchorLinkId(linkId),
                poly,
                initialState: state,
                initialTick: tick);
        }

        [Test]
        public void Hash_IsStableAcross100Calls()
        {
            var link = MakeLink("L1", "P1", AnchorZoneState.PlayerControlled, 7);
            ulong first = AnchorLinkHasher.CalculateDeterministicHash(link);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(first, AnchorLinkHasher.CalculateDeterministicHash(link));
            }
        }

        [Test]
        public void Hash_DifferentLinkId_Different()
        {
            var l1 = MakeLink("L1", "P1");
            var l2 = MakeLink("L2", "P1");
            Assert.AreNotEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_DifferentPolygonId_SameBecausePolygonIdNotInByteStream()
        {
            // Per ADR-0009 §9，PolygonId 不显式写入 hash 字节流；
            // 只有 Vertex 序列决定几何部分的 hash。
            // 这里验证：两个 PolygonId 不同但顶点相同的 link，hash 相同。
            var l1 = MakeLink("L1", "P1");
            var l2 = MakeLink("L1", "P2");
            Assert.AreEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_DifferentVertexPositions_Different()
        {
            // 验证 Vertices 不同 → hash 不同（per ADR-0009 §9 顶点序列参与 hash）
            var poly1 = MakePolygon("P", 0, 0, 4, 0, 0, 4);
            var poly2 = MakePolygon("P", 0, 0, 6, 0, 0, 6); // 不同顶点
            var l1 = new AnchorLink(new AnchorLinkId("L"), poly1);
            var l2 = new AnchorLink(new AnchorLinkId("L"), poly2);
            Assert.AreNotEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_DifferentState_Different()
        {
            var l1 = MakeLink("L1", "P1", AnchorZoneState.Inactive);
            var l2 = MakeLink("L1", "P1", AnchorZoneState.PlayerControlled);
            Assert.AreNotEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_DifferentTick_Different()
        {
            var l1 = MakeLink("L1", "P1", AnchorZoneState.Inactive, 0);
            var l2 = MakeLink("L1", "P1", AnchorZoneState.Inactive, 1);
            Assert.AreNotEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_DifferentPostStateHash_Different()
        {
            // PostStateHash 参与 hash（per ADR-0009 §9 tag 0x47）。
            // 两个不同 (state, tick) 组合 → PostStateHash 不同 → 总 hash 不同
            var l1 = MakeLink("L1", "P1", AnchorZoneState.Inactive, 0);
            var l2 = MakeLink("L1", "P1", AnchorZoneState.PlayerControlled, 0);
            Assert.AreNotEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
            Assert.AreNotEqual(l1.PostStateHash, l2.PostStateHash);
        }

        [Test]
        public void Hash_VertexInputOrderIndependent()
        {
            // 不同输入顺序 → ConstellationPolygon 构造期规范化 → 相同 hash
            var poly1 = MakePolygon("P", 0, 0, 4, 0, 0, 4);
            var poly2 = MakePolygon("P", 4, 0, 0, 4, 0, 0);
            var l1 = new AnchorLink(new AnchorLinkId("L"), poly1);
            var l2 = new AnchorLink(new AnchorLinkId("L"), poly2);
            Assert.AreEqual(
                AnchorLinkHasher.CalculateDeterministicHash(l1),
                AnchorLinkHasher.CalculateDeterministicHash(l2));
        }

        [Test]
        public void Hash_NullInput_ReturnsOffsetBasis()
        {
            ulong h = AnchorLinkHasher.CalculateDeterministicHash(null);
            Assert.AreEqual(AnchorLinkHasher.Fnv1aOffsetBasis, h);
        }

        [Test]
        public void ComputeStateHash_SameStateSameTick_Same()
        {
            var l1 = MakeLink("L1", "P1", AnchorZoneState.PlayerControlled, 5);
            var l2 = MakeLink("L2", "P2", AnchorZoneState.PlayerControlled, 5);
            Assert.AreEqual(
                AnchorLinkHasher.ComputeStateHash(l1),
                AnchorLinkHasher.ComputeStateHash(l2));
        }

        [Test]
        public void ComputeStateHash_DifferentState_Different()
        {
            var l1 = MakeLink("L1", "P1", AnchorZoneState.Inactive, 5);
            var l2 = MakeLink("L1", "P1", AnchorZoneState.PlayerControlled, 5);
            Assert.AreNotEqual(
                AnchorLinkHasher.ComputeStateHash(l1),
                AnchorLinkHasher.ComputeStateHash(l2));
        }

        [Test]
        public void ComputeStateHash_NullInput_ReturnsOffsetBasis()
        {
            Assert.AreEqual(AnchorLinkHasher.Fnv1aOffsetBasis, AnchorLinkHasher.ComputeStateHash(null));
        }
    }
}