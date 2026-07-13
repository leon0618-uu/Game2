using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// GridCoord 4 邻居几何语义测试。
    /// 独立于 GridCoordTests 中的顺序测试，专门验证每个方向的位移语义
    /// （doc2 MAP-01 §4.5）。
    /// </summary>
    public class GridNeighbourTests
    {
        [Test]
        public void North_IsYPlusOne()
        {
            var c = new GridCoord(5, 5, DimensionLayer.Reality);
            var n = c.Neighbour(GridDirection.North);
            Assert.AreEqual(5, n.X);
            Assert.AreEqual(6, n.Y);
            Assert.AreEqual(DimensionLayer.Reality, n.Layer);
        }

        [Test]
        public void East_IsXPlusOne()
        {
            var c = new GridCoord(5, 5, DimensionLayer.Reality);
            var e = c.Neighbour(GridDirection.East);
            Assert.AreEqual(6, e.X);
            Assert.AreEqual(5, e.Y);
            Assert.AreEqual(DimensionLayer.Reality, e.Layer);
        }

        [Test]
        public void South_IsYMinusOne()
        {
            var c = new GridCoord(5, 5, DimensionLayer.Reality);
            var s = c.Neighbour(GridDirection.South);
            Assert.AreEqual(5, s.X);
            Assert.AreEqual(4, s.Y);
            Assert.AreEqual(DimensionLayer.Reality, s.Layer);
        }

        [Test]
        public void West_IsXMinusOne()
        {
            var c = new GridCoord(5, 5, DimensionLayer.Reality);
            var w = c.Neighbour(GridDirection.West);
            Assert.AreEqual(4, w.X);
            Assert.AreEqual(5, w.Y);
            Assert.AreEqual(DimensionLayer.Reality, w.Layer);
        }

        [Test]
        public void Neighbours_PreservesLayer()
        {
            // 4 邻居必须在同一 Layer 上，不跨层（跨层需显式 Flip）。
            var c = new GridCoord(3, 3, DimensionLayer.Astral);
            foreach (var n in c.Neighbours())
            {
                Assert.AreEqual(DimensionLayer.Astral, n.Layer);
            }
        }

        [Test]
        public void Neighbours_Roundtrip_ReturnsToOrigin()
        {
            // N + S = 原点，E + W = 原点。
            var c = new GridCoord(4, 4, DimensionLayer.Reality);
            var afterNS = c.Neighbour(GridDirection.North).Neighbour(GridDirection.South);
            var afterEW = c.Neighbour(GridDirection.East).Neighbour(GridDirection.West);
            Assert.AreEqual(c, afterNS);
            Assert.AreEqual(c, afterEW);
        }
    }
}
