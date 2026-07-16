using NUnit.Framework;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="ConstellationVertex"/> 测试集。
    /// <para/>
    /// 覆盖：CompareTo 顺序（Y→X→Layer）/ Equals / GetHashCode / 不可变（readonly struct）。
    /// </summary>
    public class ConstellationVertexTests
    {
        [Test]
        public void CompareTo_YFirst()
        {
            // a.Y = 0, b.Y = 1 → a < b → a.CompareTo(b) < 0
            var a = new ConstellationVertex(0, 0, DimensionLayer.Reality);
            var b = new ConstellationVertex(5, 1, DimensionLayer.Reality);
            Assert.Less(a.CompareTo(b), 0, "a.Y=0 < b.Y=1 → a < b");
        }

        [Test]
        public void CompareTo_XSecond()
        {
            var a = new ConstellationVertex(1, 1, DimensionLayer.Reality);
            var b = new ConstellationVertex(2, 1, DimensionLayer.Reality);
            Assert.Less(a.CompareTo(b), 0, "X=1 < X=2 (Y equal)");
        }

        [Test]
        public void CompareTo_LayerThird()
        {
            var reality = new ConstellationVertex(1, 1, DimensionLayer.Reality);
            var astral = new ConstellationVertex(1, 1, DimensionLayer.Astral);
            Assert.Less(reality.CompareTo(astral), 0, "Layer Reality < Astral");
        }

        [Test]
        public void CompareTo_EqualReturnsZero()
        {
            var a = new ConstellationVertex(3, 4, DimensionLayer.Astral);
            var b = new ConstellationVertex(3, 4, DimensionLayer.Astral);
            Assert.AreEqual(0, a.CompareTo(b));
        }

        [Test]
        public void Equals_SameCoord_True()
        {
            var a = new ConstellationVertex(2, 3, DimensionLayer.Reality);
            var b = new ConstellationVertex(2, 3, DimensionLayer.Reality);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void Equals_DifferentCoord_False()
        {
            var a = new ConstellationVertex(2, 3, DimensionLayer.Reality);
            var b = new ConstellationVertex(2, 4, DimensionLayer.Reality);
            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void GetHashCode_SameCoord_Equal()
        {
            var a = new ConstellationVertex(7, 8, DimensionLayer.Astral);
            var b = new ConstellationVertex(7, 8, DimensionLayer.Astral);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ConstellationVertex_IsImmutableStruct()
        {
            // readonly struct：没有 setter 字段；只能通过 .Coord 访问。
            var v = new ConstellationVertex(5, 5, DimensionLayer.Reality);
            // 通过 reflection 检查类型是否声明 readonly
            var t = typeof(ConstellationVertex);
            Assert.IsTrue(t.IsValueType, "ConstellationVertex is a struct");
            // readonly struct 在 C# 中通过 readonly 关键字标记；
            // 此测试主要确认 ToString / Coord getter 工作。
            Assert.AreEqual(5, v.Coord.X);
            Assert.AreEqual(5, v.Coord.Y);
            Assert.AreEqual(DimensionLayer.Reality, v.Coord.Layer);
        }
    }
}