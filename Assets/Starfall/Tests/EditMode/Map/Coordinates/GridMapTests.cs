using System;
using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Tests.EditMode.Map.Coordinates
{
    /// <summary>
    /// GridMap&lt;T&gt; 行为测试。覆盖 Set/Get / Contains / 越界异常 / DeepClone 独立性 /
    /// Clear / Count / 确定性遍历（按 Y → X → Layer 排序）。
    /// </summary>
    public class GridMapTests
    {
        private static GridMap<int> Make8x10Map()
        {
            return new GridMap<int>(new MapSize(8, 10));
        }

        // ──────────── Set / Get / Contains ────────────

        [Test]
        public void Set_Get_RoundtripsValue()
        {
            var map = Make8x10Map();
            var c = new GridCoord(3, 5, DimensionLayer.Reality);
            map.Set(c, 42);
            Assert.AreEqual(42, map[c]);
            Assert.IsTrue(map.Contains(c));
        }

        [Test]
        public void TryGet_Missing_ReturnsFalse()
        {
            var map = Make8x10Map();
            var c = new GridCoord(0, 0, DimensionLayer.Reality);
            Assert.IsFalse(map.Contains(c));
            Assert.IsFalse(map.TryGet(c, out var v));
            Assert.AreEqual(default(int), v);
        }

        [Test]
        public void TryGet_Present_ReturnsTrueAndValue()
        {
            var map = Make8x10Map();
            var c = new GridCoord(2, 4, DimensionLayer.Astral);
            map.Set(c, 99);
            Assert.IsTrue(map.TryGet(c, out var v));
            Assert.AreEqual(99, v);
        }

        [Test]
        public void IndexerSet_OverwritesExistingValue()
        {
            var map = Make8x10Map();
            var c = new GridCoord(1, 1, DimensionLayer.Reality);
            map[c] = 1;
            map[c] = 2;
            Assert.AreEqual(2, map[c]);
            Assert.AreEqual(1, map.Count); // 同一坐标不重复计数
        }

        // ──────────── 越界异常 ────────────

        [Test]
        public void Set_OutOfBounds_Throws()
        {
            var map = Make8x10Map();
            var oob = new GridCoord(8, 5, DimensionLayer.Reality); // X == Width
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => map.Set(oob, 1));
            Assert.That(ex.Message, Does.Contain("out of bounds"));
        }

        [Test]
        public void IndexerGet_OutOfBounds_Throws()
        {
            var map = Make8x10Map();
            var oob = new GridCoord(0, -1, DimensionLayer.Reality);
            Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = map[oob]; });
        }

        [Test]
        public void IndexerSet_NegativeCoords_Throws()
        {
            var map = Make8x10Map();
            var oob = new GridCoord(-1, 5, DimensionLayer.Reality);
            Assert.Throws<ArgumentOutOfRangeException>(() => { map[oob] = 1; });
        }

        // ──────────── DeepClone 独立性 ────────────

        [Test]
        public void DeepClone_NewContainer_DoesNotAffectOriginal()
        {
            var map = Make8x10Map();
            map.Set(new GridCoord(2, 3, DimensionLayer.Reality), 10);
            map.Set(new GridCoord(5, 1, DimensionLayer.Astral), 20);

            var clone = map.DeepClone();
            Assert.AreEqual(map.Count, clone.Count);
            Assert.AreEqual(map.Size, clone.Size);

            // 修改克隆不应影响原 map。
            clone.Set(new GridCoord(0, 0, DimensionLayer.Reality), 999);
            Assert.IsFalse(map.Contains(new GridCoord(0, 0, DimensionLayer.Reality)));
            Assert.IsTrue(clone.Contains(new GridCoord(0, 0, DimensionLayer.Reality)));
        }

        [Test]
        public void DeepClone_MutatingOriginal_DoesNotAffectClone()
        {
            var map = Make8x10Map();
            map.Set(new GridCoord(1, 1, DimensionLayer.Reality), 5);

            var clone = map.DeepClone();
            map.Set(new GridCoord(1, 1, DimensionLayer.Reality), 6); // 覆盖
            map.Set(new GridCoord(2, 2, DimensionLayer.Reality), 7); // 新增

            Assert.AreEqual(5, clone[new GridCoord(1, 1, DimensionLayer.Reality)]);
            Assert.AreEqual(1, clone.Count);
        }

        // ──────────── Clear / Count ────────────

        [Test]
        public void Clear_RemovesAllEntries()
        {
            var map = Make8x10Map();
            map.Set(new GridCoord(0, 0), 1);
            map.Set(new GridCoord(3, 3), 2);
            map.Set(new GridCoord(5, 7, DimensionLayer.Astral), 3);
            Assert.AreEqual(3, map.Count);

            map.Clear();
            Assert.AreEqual(0, map.Count);
            Assert.IsFalse(map.Contains(new GridCoord(0, 0)));
        }

        [Test]
        public void Count_NewMap_IsZero()
        {
            var map = new GridMap<string>(new MapSize(8, 10));
            Assert.AreEqual(0, map.Count);
        }

        [Test]
        public void Count_TracksDistinctCoords()
        {
            var map = Make8x10Map();
            var c = new GridCoord(4, 4, DimensionLayer.Reality);
            map.Set(c, 1);
            map.Set(c, 2);
            map.Set(c, 3);
            Assert.AreEqual(1, map.Count);
        }

        // ──────────── 确定性遍历（Y → X → Layer）────────────

        [Test]
        public void AllEntries_OrderedByYThenXThenLayer()
        {
            // 故意按"乱序"插入多个点（双层）。
            var map = Make8x10Map();
            map.Set(new GridCoord(3, 2, DimensionLayer.Reality), 32);
            map.Set(new GridCoord(1, 0, DimensionLayer.Astral), 10);
            map.Set(new GridCoord(0, 0, DimensionLayer.Reality), 0);
            map.Set(new GridCoord(2, 0, DimensionLayer.Reality), 20);
            map.Set(new GridCoord(0, 1, DimensionLayer.Reality), 1);
            map.Set(new GridCoord(0, 0, DimensionLayer.Astral), 100); // (0,0,Astral) > (0,0,Reality)

            var entries = new List<KeyValuePair<GridCoord, int>>(map.AllEntries());
            Assert.AreEqual(6, entries.Count);

            // 期望顺序：(0,0,Reality)=0 < (0,0,Astral)=100 < (1,0,Astral)=10
            //            < (2,0,Reality)=20 < (0,1,Reality)=1 < (3,2,Reality)=32
            Assert.AreEqual(new GridCoord(0, 0, DimensionLayer.Reality), entries[0].Key);
            Assert.AreEqual(0, entries[0].Value);
            Assert.AreEqual(new GridCoord(0, 0, DimensionLayer.Astral), entries[1].Key);
            Assert.AreEqual(100, entries[1].Value);
            Assert.AreEqual(new GridCoord(1, 0, DimensionLayer.Astral), entries[2].Key);
            Assert.AreEqual(10, entries[2].Value);
            Assert.AreEqual(new GridCoord(2, 0, DimensionLayer.Reality), entries[3].Key);
            Assert.AreEqual(20, entries[3].Value);
            Assert.AreEqual(new GridCoord(0, 1, DimensionLayer.Reality), entries[4].Key);
            Assert.AreEqual(1, entries[4].Value);
            Assert.AreEqual(new GridCoord(3, 2, DimensionLayer.Reality), entries[5].Key);
            Assert.AreEqual(32, entries[5].Value);
        }

        [Test]
        public void AllCoords_MatchesDistinctKeyCount()
        {
            var map = Make8x10Map();
            map.Set(new GridCoord(0, 0), 1);
            map.Set(new GridCoord(1, 1), 2);
            map.Set(new GridCoord(2, 2, DimensionLayer.Astral), 3);

            var coords = new List<GridCoord>(map.AllCoords());
            Assert.AreEqual(3, coords.Count);
            // Y → X → Layer 顺序：(0,0,R) < (1,1,R) < (2,2,A)
            Assert.AreEqual(new GridCoord(0, 0, DimensionLayer.Reality), coords[0]);
            Assert.AreEqual(new GridCoord(1, 1, DimensionLayer.Reality), coords[1]);
            Assert.AreEqual(new GridCoord(2, 2, DimensionLayer.Astral), coords[2]);
        }

        [Test]
        public void ToString_ContainsSizeAndCount()
        {
            var map = new GridMap<int>(new MapSize(8, 10));
            map.Set(new GridCoord(0, 0), 1);
            map.Set(new GridCoord(1, 0), 2);

            var s = map.ToString();
            Assert.That(s, Does.Contain("Int32"));      // T 的类型名
            Assert.That(s, Does.Contain("8x10"));       // Size
            Assert.That(s, Does.Contain("count=2"));
        }

        [Test]
        public void Size_Property_IsImmutable()
        {
            var size = new MapSize(8, 10);
            var map = new GridMap<int>(size);
            Assert.AreEqual(size, map.Size);
            // MapSize 是 struct + readonly，不能通过 map.Size 修改。
        }
    }
}
