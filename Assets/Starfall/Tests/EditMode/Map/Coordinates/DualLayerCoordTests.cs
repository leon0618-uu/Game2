using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// 双层坐标独立性测试。验证 (X, Y) 相同但 Layer 不同的坐标被视为不同地块
    /// （doc2 MAP-01 §4.3 / §4.4）。这是 Phase Flip、跨层锚点、跨层律令的基础语义。
    /// </summary>
    public class DualLayerCoordTests
    {
        [Test]
        public void SameXY_DifferentLayer_NotEqual()
        {
            var reality = new GridCoord(5, 7, DimensionLayer.Reality);
            var astral = new GridCoord(5, 7, DimensionLayer.Astral);
            Assert.IsFalse(reality.Equals(astral));
            Assert.IsTrue(reality != astral);
        }

        [Test]
        public void SameXY_DifferentLayer_DifferentHash()
        {
            // 哈希不同：Dictionary / HashSet / Set<T> 才能把两者当独立 key。
            var reality = new GridCoord(5, 7, DimensionLayer.Reality);
            var astral = new GridCoord(5, 7, DimensionLayer.Astral);
            Assert.AreNotEqual(reality.GetHashCode(), astral.GetHashCode());
        }

        [Test]
        public void SameXY_DifferentLayer_StoredAsDistinctKeysInGridMap()
        {
            // GridMap 的 Dictionary 必须把双层当两个不同 key。
            var map = new GridMap<int>(new MapSize(8, 10));
            map.Set(new GridCoord(3, 4, DimensionLayer.Reality), 1);
            map.Set(new GridCoord(3, 4, DimensionLayer.Astral), 2);

            Assert.AreEqual(2, map.Count);
            Assert.AreEqual(1, map[new GridCoord(3, 4, DimensionLayer.Reality)]);
            Assert.AreEqual(2, map[new GridCoord(3, 4, DimensionLayer.Astral)]);
        }

        [Test]
        public void DualLayerEntries_OrderedRealityBeforeAstral()
        {
            // 同 X, Y 时 Reality(0) < Astral(1)，CompareTo 第三键生效。
            var map = new GridMap<int>(new MapSize(8, 10));
            map.Set(new GridCoord(2, 2, DimensionLayer.Astral), 100);
            map.Set(new GridCoord(2, 2, DimensionLayer.Reality), 50);

            var entries = new List<KeyValuePair<GridCoord, int>>(map.AllEntries());
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(DimensionLayer.Reality, entries[0].Key.Layer);
            Assert.AreEqual(50, entries[0].Value);
            Assert.AreEqual(DimensionLayer.Astral, entries[1].Key.Layer);
            Assert.AreEqual(100, entries[1].Value);
        }
    }
}
