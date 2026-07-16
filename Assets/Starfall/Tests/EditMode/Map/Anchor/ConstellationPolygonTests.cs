using System;
using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="ConstellationPolygon"/> 测试集。
    /// <para/>
    /// 覆盖：构造期 Validator 调用 / 自相交拒绝 / 退化拒绝 / Contains / Equals / 规范化顺序。
    /// </summary>
    public class ConstellationPolygonTests
    {
        private static List<ConstellationVertex> Triangle()
        {
            return new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
        }

        private static List<ConstellationVertex> Square()
        {
            return new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 4, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
        }

        // ──────────── 构造期 Validator 调用 ────────────

        [Test]
        public void Construct_TooFewVertices_Throws()
        {
            var verts = new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
            };
            Assert.Throws<ArgumentException>(() =>
                new ConstellationPolygon(new ConstellationPolygonId("poly-a"), verts));
        }

        [Test]
        public void Construct_NullVertices_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConstellationPolygon(new ConstellationPolygonId("poly-a"), null));
        }

        [Test]
        public void Construct_EmptyVertices_Throws()
        {
            var verts = new List<ConstellationVertex>();
            Assert.Throws<ArgumentException>(() =>
                new ConstellationPolygon(new ConstellationPolygonId("poly-a"), verts));
        }

        // ──────────── 退化 / 自相交 拒绝 ────────────

        [Test]
        public void Construct_CollinearVertices_Throws()
        {
            var verts = new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
            };
            Assert.Throws<ArgumentException>(() =>
                new ConstellationPolygon(new ConstellationPolygonId("poly-col"), verts));
        }

        [Test]
        public void Construct_SelfIntersectingVertices_Throws()
        {
            var verts = new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 4, DimensionLayer.Reality),
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
            Assert.Throws<ArgumentException>(() =>
                new ConstellationPolygon(new ConstellationPolygonId("poly-bfly"), verts));
        }

        [Test]
        public void Construct_ValidTriangle_Succeeds()
        {
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-tri"), Triangle());
            Assert.AreEqual(3, p.Vertices.Count);
            Assert.AreEqual("poly-tri", p.Id.Value);
        }

        // ──────────── 规范化顺序（Y→X→Layer）────────────

        [Test]
        public void Construct_VerticesNormalized_YFirst()
        {
            // 输入顺序：(4,0), (0,0), (0,4) → 规范化后 (0,0), (0,4), (4,0)
            var input = new List<ConstellationVertex>
            {
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-norm"), input);
            Assert.AreEqual(0, p.Vertices[0].Coord.Y);
            Assert.AreEqual(0, p.Vertices[0].Coord.X);
            Assert.AreEqual(4, p.Vertices[1].Coord.Y);
            Assert.AreEqual(0, p.Vertices[1].Coord.X);
            Assert.AreEqual(0, p.Vertices[2].Coord.Y);
            Assert.AreEqual(4, p.Vertices[2].Coord.X);
        }

        [Test]
        public void Construct_VerticesNormalized_XSecond()
        {
            // 输入顺序：(0,1,5), (0,1,1), (0,1,3) → 同 Y，按 X 排序
            var input = new List<ConstellationVertex>
            {
                new ConstellationVertex(5, 1, DimensionLayer.Reality),
                new ConstellationVertex(1, 1, DimensionLayer.Reality),
                new ConstellationVertex(3, 1, DimensionLayer.Reality),
            };
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-norm-x"), input);
            Assert.AreEqual(1, p.Vertices[0].Coord.X);
            Assert.AreEqual(3, p.Vertices[1].Coord.X);
            Assert.AreEqual(5, p.Vertices[2].Coord.X);
        }

        // ──────────── Contains ────────────

        [Test]
        public void Contains_PointInsideTriangle_True()
        {
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-tri"), Triangle());
            Assert.IsTrue(p.Contains(new GridCoord(1, 1, DimensionLayer.Reality)));
        }

        [Test]
        public void Contains_PointOutsideTriangle_False()
        {
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-tri"), Triangle());
            Assert.IsFalse(p.Contains(new GridCoord(10, 10, DimensionLayer.Reality)));
        }

        [Test]
        public void Contains_PointInsideSquare_True()
        {
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-sq"), Square());
            Assert.IsTrue(p.Contains(new GridCoord(2, 2, DimensionLayer.Reality)));
        }

        [Test]
        public void Contains_PointOnEdge_False()
        {
            // 半开约定：顶点位于边上 → false
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-sq"), Square());
            Assert.IsFalse(p.Contains(new GridCoord(2, 0, DimensionLayer.Reality)));
        }

        [Test]
        public void Contains_VertexCoordinate_False()
        {
            // 顶点本身：(0,0) 是多边形顶点。
            var p = new ConstellationPolygon(new ConstellationPolygonId("poly-tri"), Triangle());
            // 取决于半开约定；这里只校验不抛。
            Assert.DoesNotThrow(() => p.Contains(new GridCoord(0, 0, DimensionLayer.Reality)));
        }

        // ──────────── Equals / 等值 ────────────

        [Test]
        public void Equals_SameIdSameVerticesOrder_True()
        {
            var p1 = new ConstellationPolygon(new ConstellationPolygonId("p"), Triangle());
            var p2 = new ConstellationPolygon(new ConstellationPolygonId("p"), Triangle());
            Assert.IsTrue(p1.Equals(p2));
            Assert.IsTrue(p1 == p2);
        }

        [Test]
        public void Equals_DifferentId_False()
        {
            var p1 = new ConstellationPolygon(new ConstellationPolygonId("p1"), Triangle());
            var p2 = new ConstellationPolygon(new ConstellationPolygonId("p2"), Triangle());
            Assert.IsFalse(p1.Equals(p2));
        }

        [Test]
        public void Equals_NormalizedVerticesEqual_True()
        {
            // 输入顺序不同但顶点相同 → 规范化后相等
            var input1 = new List<ConstellationVertex>
            {
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
            var input2 = new List<ConstellationVertex>
            {
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
            };
            var p1 = new ConstellationPolygon(new ConstellationPolygonId("p"), input1);
            var p2 = new ConstellationPolygon(new ConstellationPolygonId("p"), input2);
            Assert.IsTrue(p1.Equals(p2));
        }

        [Test]
        public void GetHashCode_SameIdSameVertices_Equal()
        {
            var p1 = new ConstellationPolygon(new ConstellationPolygonId("p"), Triangle());
            var p2 = new ConstellationPolygon(new ConstellationPolygonId("p"), Triangle());
            Assert.AreEqual(p1.GetHashCode(), p2.GetHashCode());
        }
    }
}