using NUnit.Framework;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.2 TileTags 测试集。
    /// 覆盖：22 个标签位唯一性、Flags 操作、组合、数值（bit 0..21）。
    /// </summary>
    public class TileTagsTests
    {
        // ──────────── 1. 22 个标签总位数 ────────────

        [Test]
        public void Count_ExactlyTwentyTwoTags()
        {
            int bitCount = 0;
            for (int bit = 0; bit < 32; bit++)
            {
                int value = 1 << bit;
                if (System.Enum.IsDefined(typeof(TileTags), value))
                    bitCount++;
            }
            Assert.AreEqual(22, bitCount, "doc2 MAP-04 supports exactly 22 TileTags (bit 0..21).");
        }

        [Test]
        public void BitValues_ZeroThroughTwentyOne_AreUnique()
        {
            // 验证每个 bit 都对应一个 enum 值。
            for (int bit = 0; bit < 22; bit++)
            {
                int value = 1 << bit;
                bool defined = System.Enum.IsDefined(typeof(TileTags), value);
                Assert.IsTrue(defined, $"TileTags bit {bit} (value {value}) is not defined.");
            }
        }

        // ──────────── 2. [Flags] 操作：Has / Add / Remove ────────────

        [Test]
        public void Has_TrueIfBitSet()
        {
            var tags = TileTags.Walkable | TileTags.Spawnable;
            Assert.IsTrue((tags & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsTrue((tags & TileTags.Spawnable) == TileTags.Spawnable);
            Assert.IsFalse((tags & TileTags.Hazardous) == TileTags.Hazardous);
        }

        [Test]
        public void Add_CombinesTags()
        {
            var tags = TileTags.None;
            tags |= TileTags.Walkable;
            tags |= TileTags.Hazardous;
            Assert.IsTrue((tags & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsTrue((tags & TileTags.Hazardous) == TileTags.Hazardous);
        }

        [Test]
        public void Remove_ClearsBit()
        {
            var tags = TileTags.Walkable | TileTags.Hazardous;
            tags &= ~TileTags.Hazardous;
            Assert.IsTrue((tags & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsFalse((tags & TileTags.Hazardous) == TileTags.Hazardous);
        }

        // ──────────── 3. 组合 ────────────

        [Test]
        public void Combined_WalkablePlusBridge_NotEqualToImpassable()
        {
            var combined = TileTags.Walkable | TileTags.Bridge;
            Assert.AreNotEqual(combined, TileTags.Impassable);
            Assert.IsTrue((combined & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsTrue((combined & TileTags.Bridge) == TileTags.Bridge);
            Assert.IsFalse((combined & TileTags.Impassable) == TileTags.Impassable);
        }

        [Test]
        public void Combined_AllTwentyTwo_HasAllBits()
        {
            int all = 0;
            for (int bit = 0; bit < 22; bit++) all |= (1 << bit);
            var combined = (TileTags)all;
            for (int bit = 0; bit < 22; bit++)
            {
                int value = 1 << bit;
                Assert.IsTrue(((int)combined & value) == value,
                    $"TileTags combined value missing bit {bit}.");
            }
        }

        // ──────────── 4. 具体标签值契约 ────────────

        [Test]
        public void SpecificTagValues_AsExpected()
        {
            Assert.AreEqual(1 << 0, (int)TileTags.Walkable);
            Assert.AreEqual(1 << 1, (int)TileTags.Impassable);
            Assert.AreEqual(1 << 2, (int)TileTags.PhaseFlippable);
            Assert.AreEqual(1 << 3, (int)TileTags.PhaseLocked);
            Assert.AreEqual(1 << 6, (int)TileTags.Hazardous);
            Assert.AreEqual(1 << 10, (int)TileTags.AnchorNode);
            Assert.AreEqual(1 << 14, (int)TileTags.Extraction);
            Assert.AreEqual(1 << 15, (int)TileTags.GuardObjective);
            Assert.AreEqual(1 << 21, (int)TileTags.AudioSource);
        }

        // ──────────── 5. None = 0 ────────────

        [Test]
        public void None_IsZero()
        {
            Assert.AreEqual(0, (int)TileTags.None);
            // 无任何 bit 设置。
            for (int bit = 0; bit < 22; bit++)
                Assert.IsFalse(((int)TileTags.None & (1 << bit)) == (1 << bit));
        }

        // ──────────── 6. 互斥语义 ────────────

        [Test]
        public void WalkableAndImpassable_AreDistinctBits()
        {
            // Walkable (bit 0) ≠ Impassable (bit 1)；并存时不会自动抵消。
            var combined = TileTags.Walkable | TileTags.Impassable;
            Assert.IsTrue((combined & TileTags.Walkable) == TileTags.Walkable);
            Assert.IsTrue((combined & TileTags.Impassable) == TileTags.Impassable);
            // 但游戏语义上 Walkable + Impassable 是矛盾组合；本测试只验证位运算正确。
        }

        // ──────────── 7. Equality / ToString ────────────

        [Test]
        public void EnumEquality_WorksAsExpected()
        {
            var a = TileTags.Walkable | TileTags.Hazardous;
            var b = TileTags.Walkable | TileTags.Hazardous;
            Assert.AreEqual(a, b);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }
    }
}