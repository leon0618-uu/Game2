using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="ConstellationValidator"/> 测试集。
    /// <para/>
    /// 覆盖：IsDegenerate（3 顶点共线 / 2 顶点 / 1 顶点 / null）/
    /// IsSelfIntersecting（4 顶点蝴蝶 / 8 顶点星形）/ NormalizeVertices（Y→X→Layer）。
    /// </summary>
    public class ConstellationValidatorTests
    {
        // ──────────── IsDegenerate ────────────

        [Test]
        public void IsDegenerate_NullVertices_True()
        {
            Assert.IsTrue(ConstellationValidator.IsDegenerate(null));
        }

        [Test]
        public void IsDegenerate_SingleVertex_True()
        {
            var v = new[] { new ConstellationVertex(0, 0, DimensionLayer.Reality) };
            Assert.IsTrue(ConstellationValidator.IsDegenerate(v));
        }

        [Test]
        public void IsDegenerate_TwoVertices_True()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
            };
            Assert.IsTrue(ConstellationValidator.IsDegenerate(v));
        }

        [Test]
        public void IsDegenerate_ThreeCollinearVertices_True()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
            };
            Assert.IsTrue(ConstellationValidator.IsDegenerate(v));
        }

        [Test]
        public void IsDegenerate_ThreeNonCollinearVertices_False()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.IsFalse(ConstellationValidator.IsDegenerate(v));
        }

        // ──────────── IsSelfIntersecting ────────────

        [Test]
        public void IsSelfIntersecting_Triangle_False()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.IsFalse(ConstellationValidator.IsSelfIntersecting(v));
        }

        [Test]
        public void IsSelfIntersecting_QuadrilateralButterfly_True()
        {
            // 蝴蝶形：4 顶点 (0,0)(2,2)(2,0)(0,2) — 边 (0,0)-(2,2) 与 (2,0)-(0,2) 相交
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 2, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.IsTrue(ConstellationValidator.IsSelfIntersecting(v));
        }

        [Test]
        public void IsSelfIntersecting_QuadrilateralNonIntersecting_False()
        {
            // 凸四边形
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 2, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.IsFalse(ConstellationValidator.IsSelfIntersecting(v));
        }

        [Test]
        public void IsSelfIntersecting_StarShape_True()
        {
            // 8 顶点星形：每 2 个相邻顶点形成一条外凸边，但内部存在交叉
            var v = new[]
            {
                new ConstellationVertex(0, 4, DimensionLayer.Reality),    // top
                new ConstellationVertex(1, 1, DimensionLayer.Reality),    // inner-left
                new ConstellationVertex(4, 0, DimensionLayer.Reality),    // right
                new ConstellationVertex(1, -1, DimensionLayer.Reality),   // inner-right
                new ConstellationVertex(0, -4, DimensionLayer.Reality),   // bottom
                new ConstellationVertex(-1, -1, DimensionLayer.Reality),  // inner-bottom
                new ConstellationVertex(-4, 0, DimensionLayer.Reality),   // left
                new ConstellationVertex(-1, 1, DimensionLayer.Reality),   // inner-top
            };
            Assert.IsTrue(ConstellationValidator.IsSelfIntersecting(v));
        }

        // ──────────── NormalizeVertices ────────────

        [Test]
        public void NormalizeVertices_YFirst()
        {
            var v = new[]
            {
                new ConstellationVertex(5, 5, DimensionLayer.Reality),
                new ConstellationVertex(1, 1, DimensionLayer.Reality),
                new ConstellationVertex(3, 1, DimensionLayer.Reality),
            };
            var sorted = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(1, sorted[0].Coord.Y);
            Assert.AreEqual(1, sorted[1].Coord.Y); // Y 同 → 按 X
            Assert.AreEqual(1, sorted[1].Coord.X);
            Assert.AreEqual(3, sorted[1].Coord.X == 3 ? 3 : 0); // 此断言为冗余，下面补强
            // 严格断言：sorted = [(1,1), (3,1), (5,5)]
            Assert.AreEqual(1, sorted[0].Coord.X);
            Assert.AreEqual(3, sorted[1].Coord.X);
            Assert.AreEqual(5, sorted[2].Coord.X);
        }

        [Test]
        public void NormalizeVertices_XSecond()
        {
            var v = new[]
            {
                new ConstellationVertex(5, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
                new ConstellationVertex(3, 0, DimensionLayer.Reality),
            };
            var sorted = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(1, sorted[0].Coord.X);
            Assert.AreEqual(3, sorted[1].Coord.X);
            Assert.AreEqual(5, sorted[2].Coord.X);
        }

        [Test]
        public void NormalizeVertices_LayerThird()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Astral),
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
            };
            var sorted = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(DimensionLayer.Reality, sorted[0].Coord.Layer);
            Assert.AreEqual(DimensionLayer.Astral, sorted[1].Coord.Layer);
        }

        [Test]
        public void NormalizeVertices_EmptyInput_EmptyOutput()
        {
            var sorted = ConstellationValidator.NormalizeVertices(new ConstellationVertex[0]);
            Assert.AreEqual(0, sorted.Count);
        }

        [Test]
        public void Validate_Triangle_None()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.AreEqual(ConstellationValidator.ConstellationValidationError.None,
                ConstellationValidator.Validate(v));
        }

        [Test]
        public void Validate_TwoVertices_TooFewVertices()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
            };
            Assert.AreEqual(ConstellationValidator.ConstellationValidationError.TooFewVertices,
                ConstellationValidator.Validate(v));
        }

        [Test]
        public void Validate_Collinear_Collinear()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
            };
            Assert.AreEqual(ConstellationValidator.ConstellationValidationError.Collinear,
                ConstellationValidator.Validate(v));
        }

        [Test]
        public void Validate_Butterfly_SelfIntersecting()
        {
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(2, 2, DimensionLayer.Reality),
                new ConstellationVertex(2, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 2, DimensionLayer.Reality),
            };
            Assert.AreEqual(ConstellationValidator.ConstellationValidationError.SelfIntersecting,
                ConstellationValidator.Validate(v));
        }
    }
}