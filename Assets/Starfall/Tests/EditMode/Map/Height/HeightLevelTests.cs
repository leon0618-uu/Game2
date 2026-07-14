using NUnit.Framework;
using Starfall.Core.Map.Height;

namespace Starfall.Tests.EditMode.Map.Height
{
    /// <summary>
    /// <see cref="HeightLevel"/> readonly struct 行为测试。
    /// 覆盖：构造范围 / clamp 行为 / 等值 / 哈希 / 比较 / 算术 / Ground 常量。
    /// AGENTS.md §11 强制要求排序键稳定。
    /// </summary>
    public class HeightLevelTests
    {
        // ──────────── 构造 + clamp ────────────

        [Test]
        public void Constructor_InRange_KeepsValue()
        {
            var h = new HeightLevel(3);
            Assert.AreEqual(3, h.Value);
        }

        [Test]
        public void Constructor_BelowMin_ClampsToZero()
        {
            var h = new HeightLevel(-5);
            Assert.AreEqual(0, h.Value);
        }

        [Test]
        public void Constructor_AboveMax_ClampsToMax()
        {
            var h = new HeightLevel(99);
            Assert.AreEqual(HeightLevel.MaxValue, h.Value);
            Assert.AreEqual(4, h.Value);
        }

        [Test]
        public void Ground_IsZero()
        {
            Assert.AreEqual(0, HeightLevel.Ground.Value);
        }

        // ──────────── 等值 / 哈希 ────────────

        [Test]
        public void Equals_SameValue_True()
        {
            Assert.IsTrue(new HeightLevel(2).Equals(new HeightLevel(2)));
            Assert.IsTrue(new HeightLevel(2) == new HeightLevel(2));
            Assert.IsFalse(new HeightLevel(2) != new HeightLevel(2));
        }

        [Test]
        public void Equals_DifferentValue_False()
        {
            Assert.IsFalse(new HeightLevel(2).Equals(new HeightLevel(3)));
            Assert.IsTrue(new HeightLevel(2) != new HeightLevel(3));
        }

        [Test]
        public void GetHashCode_SameValue_SameHash()
        {
            Assert.AreEqual(new HeightLevel(3).GetHashCode(), new HeightLevel(3).GetHashCode());
        }

        // ──────────── 排序 ────────────

        [Test]
        public void CompareTo_LowerFirst()
        {
            Assert.AreEqual(-1, new HeightLevel(1).CompareTo(new HeightLevel(2)));
            Assert.AreEqual(1, new HeightLevel(3).CompareTo(new HeightLevel(1)));
            Assert.AreEqual(0, new HeightLevel(2).CompareTo(new HeightLevel(2)));
        }

        [Test]
        public void Operators_ComparisonWork()
        {
            var a = new HeightLevel(1);
            var b = new HeightLevel(2);
            Assert.IsTrue(a < b);
            Assert.IsTrue(b > a);
            Assert.IsTrue(a <= b);
            Assert.IsTrue(b >= a);
            Assert.IsTrue(a <= new HeightLevel(1));
            Assert.IsTrue(a >= new HeightLevel(1));
        }

        // ──────────── 算术 ────────────

        [Test]
        public void Subtract_PositiveWhenToHigher()
        {
            int delta = new HeightLevel(3) - new HeightLevel(1);
            Assert.AreEqual(2, delta);
        }

        [Test]
        public void Subtract_NegativeWhenToLower()
        {
            int delta = new HeightLevel(1) - new HeightLevel(3);
            Assert.AreEqual(-2, delta);
        }

        [Test]
        public void Add_ClampsToMax()
        {
            var sum = new HeightLevel(3) + new HeightLevel(4);
            Assert.AreEqual(HeightLevel.MaxValue, sum.Value);
            Assert.AreEqual(4, sum.Value);
        }

        [Test]
        public void ToString_FormatsWithPrefix()
        {
            Assert.AreEqual("H0", new HeightLevel(0).ToString());
            Assert.AreEqual("H3", new HeightLevel(3).ToString());
        }

        [Test]
        public void MinMax_Static_ReturnsBounds()
        {
            Assert.AreEqual(0, HeightLevel.Min.Value);
            Assert.AreEqual(4, HeightLevel.Max.Value);
        }
    }
}
