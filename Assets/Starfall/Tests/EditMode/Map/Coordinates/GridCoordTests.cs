using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// GridCoord 行为测试。覆盖等值 / 哈希 / 4 邻居顺序 / 曼哈顿距离 / 越界 / 排序。
    /// AGENTS.md §11 强制要求 4 邻居顺序 North → East → South → West，排序键 Y → X → Layer。
    /// </summary>
    public class GridCoordTests
    {
        // ──────────── 等值 / 哈希 ────────────

        [Test]
        public void Equals_SameXYLayer_ReturnsTrue()
        {
            var a = new GridCoord(3, 5, DimensionLayer.Reality);
            var b = new GridCoord(3, 5, DimensionLayer.Reality);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Equals_DifferentX_ReturnsFalse()
        {
            var a = new GridCoord(3, 5, DimensionLayer.Reality);
            var b = new GridCoord(4, 5, DimensionLayer.Reality);
            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
        }

        [Test]
        public void Equals_DifferentY_ReturnsFalse()
        {
            var a = new GridCoord(3, 5, DimensionLayer.Reality);
            var b = new GridCoord(3, 6, DimensionLayer.Reality);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_SameCoords_AreEqual()
        {
            // 关键：同坐标 → 同哈希（Dictionary / HashSet 依赖此保证）。
            var a = new GridCoord(7, 11, DimensionLayer.Astral);
            var b = new GridCoord(7, 11, DimensionLayer.Astral);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void GetHashCode_StableAcrossCalls()
        {
            // GridCoord 是 struct + readonly，GetHashCode 应为纯函数。
            var c = new GridCoord(13, 17, DimensionLayer.Reality);
            int h1 = c.GetHashCode();
            int h2 = c.GetHashCode();
            int h3 = c.GetHashCode();
            Assert.AreEqual(h1, h2);
            Assert.AreEqual(h2, h3);
        }

        // ──────────── 4 邻居顺序（AGENTS.md §11）────────────

        [Test]
        public void Neighbours_FixedOrder_NorthEastSouthWest()
        {
            var c = new GridCoord(5, 5, DimensionLayer.Reality);
            var list = new List<GridCoord>(c.Neighbours());

            Assert.AreEqual(4, list.Count);
            // North = (5, 6)
            Assert.AreEqual(new GridCoord(5, 6, DimensionLayer.Reality), list[0]);
            // East  = (6, 5)
            Assert.AreEqual(new GridCoord(6, 5, DimensionLayer.Reality), list[1]);
            // South = (5, 4)
            Assert.AreEqual(new GridCoord(5, 4, DimensionLayer.Reality), list[2]);
            // West  = (4, 5)
            Assert.AreEqual(new GridCoord(4, 5, DimensionLayer.Reality), list[3]);
        }

        [Test]
        public void Neighbour_DirectionSwitch_MatchesExpected()
        {
            var c = new GridCoord(2, 3, DimensionLayer.Astral);
            Assert.AreEqual(new GridCoord(2, 4, DimensionLayer.Astral), c.Neighbour(GridDirection.North));
            Assert.AreEqual(new GridCoord(3, 3, DimensionLayer.Astral), c.Neighbour(GridDirection.East));
            Assert.AreEqual(new GridCoord(2, 2, DimensionLayer.Astral), c.Neighbour(GridDirection.South));
            Assert.AreEqual(new GridCoord(1, 3, DimensionLayer.Astral), c.Neighbour(GridDirection.West));
        }

        // ──────────── 曼哈顿距离 ────────────

        [Test]
        public void ManhattanDistance_IgnoresLayer()
        {
            // Layer 不参与距离（doc2 MAP-01 §4.1）。
            var a = new GridCoord(0, 0, DimensionLayer.Reality);
            var b = new GridCoord(3, 4, DimensionLayer.Astral);
            Assert.AreEqual(7, a.ManhattanDistance(b));
            Assert.AreEqual(7, b.ManhattanDistance(a));
        }

        [Test]
        public void ManhattanDistance_SameCoord_IsZero()
        {
            var a = new GridCoord(5, 5, DimensionLayer.Reality);
            Assert.AreEqual(0, a.ManhattanDistance(a));
        }

        // ──────────── 越界检查 ────────────

        [Test]
        public void IsInBounds_Inside_ReturnsTrue()
        {
            var size = new MapSize(8, 10);
            var c = new GridCoord(7, 9, DimensionLayer.Reality);
            Assert.IsTrue(c.IsInBounds(size));
        }

        [Test]
        public void IsInBounds_OnBorder_ReturnsTrue()
        {
            // 边界包含：(0,0) 和 (W-1, H-1) 均合法。
            var size = new MapSize(8, 10);
            Assert.IsTrue(new GridCoord(0, 0).IsInBounds(size));
            Assert.IsTrue(new GridCoord(7, 9).IsInBounds(size));
        }

        [Test]
        public void IsInBounds_OutsideXOrY_ReturnsFalse()
        {
            var size = new MapSize(8, 10);
            Assert.IsFalse(new GridCoord(-1, 0).IsInBounds(size));
            Assert.IsFalse(new GridCoord(8, 0).IsInBounds(size));  // X == Width 越界
            Assert.IsFalse(new GridCoord(0, -1).IsInBounds(size));
            Assert.IsFalse(new GridCoord(0, 10).IsInBounds(size)); // Y == Height 越界
        }

        // ──────────── 排序（Y → X → Layer）────────────

        [Test]
        public void CompareTo_OrdersByYThenXThenLayer()
        {
            // 同一 Layer 时按 Y → X。
            var a = new GridCoord(0, 0, DimensionLayer.Reality);
            var b = new GridCoord(1, 0, DimensionLayer.Reality);
            var c = new GridCoord(0, 1, DimensionLayer.Reality);
            var list = new List<GridCoord> { b, c, a };
            list.Sort();

            Assert.AreEqual(new GridCoord(0, 0, DimensionLayer.Reality), list[0]);
            Assert.AreEqual(new GridCoord(1, 0, DimensionLayer.Reality), list[1]);
            Assert.AreEqual(new GridCoord(0, 1, DimensionLayer.Reality), list[2]);
        }

        [Test]
        public void CompareTo_SameXY_LayerTiebreak()
        {
            // 同 X, Y 不同 Layer：Reality(0) < Astral(1)。
            var reality = new GridCoord(3, 3, DimensionLayer.Reality);
            var astral = new GridCoord(3, 3, DimensionLayer.Astral);
            var list = new List<GridCoord> { astral, reality };
            list.Sort();

            Assert.AreEqual(reality, list[0]);
            Assert.AreEqual(astral, list[1]);
        }

        [Test]
        public void ToString_FormatsXYLayer()
        {
            var c = new GridCoord(5, 7, DimensionLayer.Astral);
            Assert.AreEqual("(5, 7, Astral)", c.ToString());
        }

        [Test]
        public void Constructor_DefaultLayer_IsReality()
        {
            var c = new GridCoord(1, 2);
            Assert.AreEqual(1, c.X);
            Assert.AreEqual(2, c.Y);
            Assert.AreEqual(DimensionLayer.Reality, c.Layer);
        }
    }
}
