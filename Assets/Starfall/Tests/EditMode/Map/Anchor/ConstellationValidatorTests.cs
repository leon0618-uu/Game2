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
        public void IsSelfIntersecting_SixVertexFigure8_True()
        {
            // 6 顶点自相交多边形：边 (0,0)-(4,4) 与 (4,0)-(0,4) 在 (2,2) 相交
            // 其余 4 条边仅作为循环连贯存在。
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
                new ConstellationVertex(4, 4, DimensionLayer.Reality),
                new ConstellationVertex(8, 0, DimensionLayer.Reality),
                new ConstellationVertex(8, 4, DimensionLayer.Reality),
                new ConstellationVertex(4, 0, DimensionLayer.Reality),
                new ConstellationVertex(0, 4, DimensionLayer.Reality),
            };
            Assert.IsTrue(ConstellationValidator.IsSelfIntersecting(v));
        }

        // ──────────── NormalizeVertices ────────────

        [Test]
        public void NormalizeVertices_YFirst()
        {
            // 输入顺序：(5,5), (1,1), (3,1)
            // Y→X 最小的顶点 = (1,1)，以其为起点旋转（保持 cyclic）。
            // 原始 cyclic: (5,5) → (1,1) → (3,1)
            // 旋转后起点为 (1,1): (1,1) → (3,1) → (5,5)
            var v = new[]
            {
                new ConstellationVertex(5, 5, DimensionLayer.Reality),
                new ConstellationVertex(1, 1, DimensionLayer.Reality),
                new ConstellationVertex(3, 1, DimensionLayer.Reality),
            };
            var normalized = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(1, normalized[0].Coord.Y);
            Assert.AreEqual(1, normalized[0].Coord.X);
            // 后两个保持 cyclic 顺序
            Assert.AreEqual(1, normalized[1].Coord.Y);
            Assert.AreEqual(3, normalized[1].Coord.X);
            Assert.AreEqual(5, normalized[2].Coord.Y);
            Assert.AreEqual(5, normalized[2].Coord.X);
        }

        [Test]
        public void NormalizeVertices_XSecond()
        {
            // 同 Y，按 X 找最小起点
            // 输入 (5,0), (1,0), (3,0) cyclic = (5,0) → (1,0) → (3,0)
            // 旋转后：(1,0) → (3,0) → (5,0)
            var v = new[]
            {
                new ConstellationVertex(5, 0, DimensionLayer.Reality),
                new ConstellationVertex(1, 0, DimensionLayer.Reality),
                new ConstellationVertex(3, 0, DimensionLayer.Reality),
            };
            var normalized = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(1, normalized[0].Coord.X);
            Assert.AreEqual(3, normalized[1].Coord.X);
            Assert.AreEqual(5, normalized[2].Coord.X);
        }

        [Test]
        public void NormalizeVertices_LayerThird()
        {
            // (0,0,Reality) 和 (0,0,Astral)，Y/X 同，Layer 决胜。
            // 输入 cyclic: (Astral) → (Reality)
            // 旋转后：(Reality) → (Astral)
            var v = new[]
            {
                new ConstellationVertex(0, 0, DimensionLayer.Astral),
                new ConstellationVertex(0, 0, DimensionLayer.Reality),
            };
            var normalized = ConstellationValidator.NormalizeVertices(v);
            Assert.AreEqual(DimensionLayer.Reality, normalized[0].Coord.Layer);
            Assert.AreEqual(DimensionLayer.Astral, normalized[1].Coord.Layer);
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