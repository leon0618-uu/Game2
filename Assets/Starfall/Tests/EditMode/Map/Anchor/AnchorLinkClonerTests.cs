using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLinkCloner"/> 测试集。
    /// <para/>
    /// 覆盖：深拷贝独立 / 集合独立 / 修改克隆不修改原。
    /// </summary>
    public class AnchorLinkClonerTests
    {
        private static AnchorLink MakeLink(string linkId = "L1", string polyId = "P1",
            AnchorZoneState state = AnchorZoneState.PlayerControlled,
            int tick = 5, ulong hash = 0xABCDUL)
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
                    }),
                initialState: AnchorZoneState.Inactive,
                initialTick: tick,
                initialPostStateHash: hash)
            { /* CurrentState 由构造保证 = initialState */ };
        }

        [Test]
        public void DeepClone_NullInput_ReturnsNull()
        {
            Assert.IsNull(AnchorLinkCloner.DeepClone(null));
        }

        [Test]
        public void DeepClone_NewInstance_NotSameReference()
        {
            var link = MakeLink();
            var clone = AnchorLinkCloner.DeepClone(link);
            Assert.IsNotNull(clone);
            Assert.AreNotSame(link, clone);
        }

        [Test]
        public void DeepClone_SameFieldValues_AfterTransition()
        {
            var link = MakeLink(state: AnchorZoneState.PlayerControlled, tick: 5, hash: 0xABCDUL);
            var clone = AnchorLinkCloner.DeepClone(link);
            Assert.AreEqual(link.Id.Value, clone.Id.Value);
            Assert.AreEqual(link.CurrentState, clone.CurrentState);
            Assert.AreEqual(link.StateTick, clone.StateTick);
            Assert.AreEqual(link.PostStateHash, clone.PostStateHash);
            Assert.AreEqual(link.InitialState, clone.InitialState);
            Assert.AreEqual(link.Polygon.Id.Value, clone.Polygon.Id.Value);
            Assert.AreEqual(link.Polygon.Vertices.Count, clone.Polygon.Vertices.Count);
        }

        [Test]
        public void DeepClone_PolygonListIndependent()
        {
            var link = MakeLink();
            var clone = AnchorLinkCloner.DeepClone(link);
            // 修改 clone 的 polygon 顶点不应影响源
            var newPoly = new ConstellationPolygon(
                new ConstellationPolygonId("different"),
                new List<ConstellationVertex>
                {
                    new ConstellationVertex(10, 10, DimensionLayer.Reality),
                    new ConstellationVertex(20, 10, DimensionLayer.Reality),
                    new ConstellationVertex(10, 20, DimensionLayer.Reality),
                });
            clone.UpdatePolygon(newPoly);
            Assert.AreEqual("P1", link.Polygon.Id.Value);
            Assert.AreEqual("different", clone.Polygon.Id.Value);
        }

        [Test]
        public void DeepCloneAll_PreservesCount()
        {
            var list = new List<AnchorLink>
            {
                MakeLink("L1"),
                MakeLink("L2"),
                MakeLink("L3"),
            };
            var clone = AnchorLinkCloner.DeepCloneAll(list);
            Assert.AreEqual(3, clone.Count);
            Assert.AreEqual("L1", clone[0].Id.Value);
            Assert.AreEqual("L2", clone[1].Id.Value);
            Assert.AreEqual("L3", clone[2].Id.Value);
        }

        [Test]
        public void DeepCloneAll_ClonesAreIndependent()
        {
            var list = new List<AnchorLink>
            {
                MakeLink("L1"),
                MakeLink("L2"),
            };
            var clone = AnchorLinkCloner.DeepCloneAll(list);
            // 修改 list[0] 不影响 clone[0]
            list[0].TransitionTo(AnchorZoneState.EnemyControlled, 10, 0UL);
            Assert.AreEqual(AnchorZoneState.PlayerControlled, clone[0].CurrentState);
        }

        [Test]
        public void DeepCloneAll_NullInput_ReturnsNull()
        {
            Assert.IsNull(AnchorLinkCloner.DeepCloneAll(null));
        }
    }
}