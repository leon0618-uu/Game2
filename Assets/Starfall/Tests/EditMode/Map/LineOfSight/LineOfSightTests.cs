using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.LineOfSight
{
    /// <summary>
    /// <see cref="LineOfSightService"/> 基础视线测试（≥ 12 项）。
    /// 覆盖：同 tile / 相邻 / 直线 / 对角 / 阻挡 / 跨 Layer / 越界。
    /// </summary>
    public class LineOfSightTests
    {
        // 简单字典适配器
        private sealed class DictHeightLookup : IHeightLookup
        {
            private readonly Dictionary<GridCoord, int> _data;
            public DictHeightLookup(Dictionary<GridCoord, int> data) { _data = data; }
            public int GetHeight(GridCoord c) => _data.TryGetValue(c, out var v) ? v : 0;
        }

        private sealed class DictCoverLookup : ICoverLookup
        {
            private readonly Dictionary<GridCoord, CoverLevel> _data;
            public DictCoverLookup(Dictionary<GridCoord, CoverLevel> data) { _data = data; }
            public CoverLevel? GetCover(GridCoord c)
                => _data.TryGetValue(c, out var v) ? (CoverLevel?)v : null;
        }

        private sealed class DictBlockingLookup : IBlockingLookup
        {
            private readonly HashSet<GridCoord> _data;
            public DictBlockingLookup(HashSet<GridCoord> data) { _data = data; }
            public bool BlocksLineOfSight(GridCoord c) => _data.Contains(c);
        }

        private static MapState MakeMap(int w = 16, int h = 16)
        {
            var def = new MapDefinition("test.map", w, h, DimensionLayer.Reality, 0);
            return new MapState(def);
        }

        // ──────────── 同 tile ────────────

        [Test]
        public void SameTile_HasLineOfSight_NoPenalty()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(5, 5), null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsFalse(r.HasHighGroundBonus);
            Assert.AreEqual(0, r.CoverPenalty);
            Assert.AreEqual(0, r.BlockingTiles.Count);
        }

        // ──────────── 相邻无阻挡 ────────────

        [Test]
        public void Adjacent_NoBlock_HasLineOfSight()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(0, r.CoverPenalty);
            Assert.AreEqual(0, r.BlockingTiles.Count);
        }

        // ──────────── 直线无阻挡 ────────────

        [Test]
        public void StraightLine8Cells_NoBlock_HasLineOfSight()
        {
            var map = MakeMap(16, 16);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(8, 0), null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(0, r.BlockingTiles.Count);
        }

        // ──────────── 直线有阻挡 ────────────

        [Test]
        public void StraightLine_MiddleBlock_Fails()
        {
            var map = MakeMap(16, 16);
            var blockers = new HashSet<GridCoord> { new GridCoord(4, 0) };
            var b = new DictBlockingLookup(blockers);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(8, 0), null, null, b);
            Assert.IsFalse(r.HasLineOfSight);
            Assert.AreEqual(1, r.BlockingTiles.Count);
            Assert.AreEqual(new GridCoord(4, 0), r.BlockingTiles[0]);
        }

        [Test]
        public void StraightLine_BlockAtStart_Fails()
        {
            var map = MakeMap(16, 16);
            var blockers = new HashSet<GridCoord> { new GridCoord(0, 0) };
            var b = new DictBlockingLookup(blockers);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(8, 0), null, null, b);
            Assert.IsFalse(r.HasLineOfSight);
            Assert.AreEqual(1, r.BlockingTiles.Count);
        }

        // ──────────── 对角线 ────────────

        [Test]
        public void Diagonal8x8_NoBlock_HasLineOfSight()
        {
            var map = MakeMap(16, 16);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(8, 8), null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(0, r.BlockingTiles.Count);
        }

        [Test]
        public void Diagonal8x8_CenterBlock_Fails()
        {
            var map = MakeMap(16, 16);
            // Supercover 路径中心约 (4, 4)，置阻挡
            var blockers = new HashSet<GridCoord> { new GridCoord(4, 4) };
            var b = new DictBlockingLookup(blockers);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(8, 8), null, null, b);
            Assert.IsFalse(r.HasLineOfSight);
            Assert.IsTrue(r.BlockingTiles.Count >= 1);
        }

        // ──────────── 跨 Layer ────────────

        [Test]
        public void CrossLayer_Direct_FailsByDefault()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeLineOfSight(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Astral),
                null, null, null);
            // 同 (X, Y) 不同 Layer 但坐标不同 → CrossLayer → 失败
            Assert.IsFalse(r.HasLineOfSight);
        }

        // ──────────── 越界 ────────────

        [Test]
        public void OutOfBounds_Fails()
        {
            var map = MakeMap(8, 8);
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(0, 0), new GridCoord(20, 20), null, null, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        // ──────────── Half Cover 惩罚（无 high ground）────────────

        [Test]
        public void HalfCover_SameHeight_PenaltyOne()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Half },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), null, covers, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(1, r.CoverPenalty);
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        // ──────────── Supercover 路径唯一性 ────────────

        [Test]
        public void Supercover_DeterministicOverManyRuns()
        {
            // 同一 (from, to) 多次调用 → 完全相同路径
            var from = new GridCoord(0, 0);
            var to = new GridCoord(7, 3);
            var first = LineOfSightService.TraceSupercoverPath(from, to);
            for (int i = 0; i < 10; i++)
            {
                var next = LineOfSightService.TraceSupercoverPath(from, to);
                Assert.AreEqual(first.Count, next.Count);
                for (int j = 0; j < first.Count; j++)
                {
                    Assert.AreEqual(first[j], next[j]);
                }
            }
        }

        [Test]
        public void Supercover_HorizontalPath_AllOnSameY()
        {
            var path = LineOfSightService.TraceSupercoverPath(new GridCoord(0, 5), new GridCoord(7, 5));
            Assert.AreEqual(8, path.Count);
            foreach (var p in path)
            {
                Assert.AreEqual(5, p.Y);
            }
            // 起点 + 终点包含
            Assert.AreEqual(new GridCoord(0, 5), path[0]);
            Assert.AreEqual(new GridCoord(7, 5), path[path.Count - 1]);
        }

        [Test]
        public void Supercover_VerticalPath_AllOnSameX()
        {
            var path = LineOfSightService.TraceSupercoverPath(new GridCoord(3, 0), new GridCoord(3, 7));
            Assert.AreEqual(8, path.Count);
            foreach (var p in path)
            {
                Assert.AreEqual(3, p.X);
            }
        }

        [Test]
        public void Supercover_SingleStep_LengthIsOne()
        {
            var path = LineOfSightService.TraceSupercoverPath(new GridCoord(5, 5), new GridCoord(5, 5));
            Assert.AreEqual(1, path.Count);
            Assert.AreEqual(new GridCoord(5, 5), path[0]);
        }

        [Test]
        public void Supercover_LayerPreserved()
        {
            var path = LineOfSightService.TraceSupercoverPath(
                new GridCoord(0, 0, DimensionLayer.Astral),
                new GridCoord(3, 3, DimensionLayer.Astral));
            foreach (var p in path)
            {
                Assert.AreEqual(DimensionLayer.Astral, p.Layer);
            }
        }
    }
}
