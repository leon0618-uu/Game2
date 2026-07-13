using System;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// MapSize 行为测试。覆盖构造越界 / Min/Max / TileCount（含双层）/ 等值 / ToString。
    /// </summary>
    public class MapSizeTests
    {
        // ──────────── 构造越界 ────────────

        [Test]
        public void Constructor_ValidRange_DoesNotThrow()
        {
            // 边界值合法：1×1 与 48×64。
            Assert.DoesNotThrow(() => new MapSize(1, 1));
            Assert.DoesNotThrow(() => new MapSize(MapSize.MaxWidth, MapSize.MaxHeight));
            Assert.DoesNotThrow(() => new MapSize(8, 10)); // 标准 MVP 尺寸
        }

        [Test]
        public void Constructor_WidthZero_Throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MapSize(0, 10));
            Assert.That(ex.ParamName, Is.EqualTo("width"));
        }

        [Test]
        public void Constructor_WidthTooLarge_Throws()
        {
            // MaxWidth = 49 越界。
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MapSize(49, 10));
            Assert.That(ex.ParamName, Is.EqualTo("width"));
        }

        [Test]
        public void Constructor_HeightZero_Throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MapSize(8, 0));
            Assert.That(ex.ParamName, Is.EqualTo("height"));
        }

        [Test]
        public void Constructor_HeightTooLarge_Throws()
        {
            // MaxHeight = 65 越界。
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MapSize(8, 65));
            Assert.That(ex.ParamName, Is.EqualTo("height"));
        }

        // ──────────── Min / Max ────────────

        [Test]
        public void Min_Returns1x1()
        {
            Assert.AreEqual(1, MapSize.Min.Width);
            Assert.AreEqual(1, MapSize.Min.Height);
        }

        [Test]
        public void Max_Returns48x64()
        {
            Assert.AreEqual(48, MapSize.Max.Width);
            Assert.AreEqual(64, MapSize.Max.Height);
        }

        // ──────────── TileCount（含双层 = ×2）────────────

        [Test]
        public void TileCount_8x10_Is160()
        {
            // 8 × 10 × 2 = 160。
            Assert.AreEqual(160, new MapSize(8, 10).TileCount);
        }

        [Test]
        public void TileCount_Max_Is6144()
        {
            // 48 × 64 × 2 = 6144。
            Assert.AreEqual(6144, MapSize.Max.TileCount);
        }

        [Test]
        public void TileCount_Min_Is2()
        {
            // 1 × 1 × 2 = 2（双层最小）。
            Assert.AreEqual(2, MapSize.Min.TileCount);
        }

        // ──────────── 等值 / 哈希 ────────────

        [Test]
        public void Equals_SameDimensions_ReturnsTrue()
        {
            var a = new MapSize(8, 10);
            var b = new MapSize(8, 10);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
        }

        [Test]
        public void GetHashCode_SameDimensions_AreEqual()
        {
            var a = new MapSize(8, 10);
            var b = new MapSize(8, 10);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        // ──────────── ToString ────────────

        [Test]
        public void ToString_FormatsWxH()
        {
            Assert.AreEqual("8x10", new MapSize(8, 10).ToString());
            Assert.AreEqual("1x1", MapSize.Min.ToString());
            Assert.AreEqual("48x64", MapSize.Max.ToString());
        }
    }
}
