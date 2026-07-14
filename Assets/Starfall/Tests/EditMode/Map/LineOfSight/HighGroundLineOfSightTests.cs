using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.LineOfSight
{
    /// <summary>
    /// <see cref="LineOfSightService"/> 高地优势（High Ground）测试（≥ 6 项）。
    /// doc2 MAP-06 §4.6 + §4.7：attacker 比 defender 高 ≥ 1 时享 HighGroundBonus，
    /// 同 Layer 内 Half Cover 忽略；跨 Layer 不算 high ground。
    /// </summary>
    public class HighGroundLineOfSightTests
    {
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

        private static MapState MakeMap(int w = 16, int h = 16)
            => new MapState(new MapDefinition("test.map", w, h));

        [Test]
        public void AttackerHigher_ByOne_HasHighGroundBonus()
        {
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 2 },
                { new GridCoord(6, 5), 1 },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsTrue(r.HasHighGroundBonus);
        }

        [Test]
        public void AttackerHigher_HalfCover_Ignored()
        {
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 2 },
                { new GridCoord(6, 5), 0 },
            });
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Half },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, covers, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsTrue(r.HasHighGroundBonus);
            Assert.AreEqual(0, r.CoverPenalty); // Half 被忽略
        }

        [Test]
        public void AttackerLower_NoHighGround_HalfCoverPenalty()
        {
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 0 },
                { new GridCoord(6, 5), 1 },
            });
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Half },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, covers, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsFalse(r.HasHighGroundBonus);
            Assert.AreEqual(1, r.CoverPenalty);
        }

        [Test]
        public void SameHeight_NoHighGround()
        {
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 2 },
                { new GridCoord(6, 5), 2 },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        [Test]
        public void CrossLayer_NotCountedAsHighGround()
        {
            // 即使 attacker 在 Reality Height=2，defender 在 Astral Height=0，
            // 跨 Layer 不算 high ground。
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), 2 },
                { new GridCoord(8, 5, DimensionLayer.Astral), 0 },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(8, 5, DimensionLayer.Astral),
                heights, null, null);
            // 跨 Layer 默认失败，但即便能成功也不算 high ground
            // 直接看 Result.HasHighGroundBonus 一定为 false（不论视线是否成功）
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        [Test]
        public void HeightDifference_TwoOrMore_StillHighGround()
        {
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 4 },
                { new GridCoord(6, 5), 1 },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsTrue(r.HasHighGroundBonus);
        }

        [Test]
        public void AttackerLower_ByOne_NoHighGround_NoBonus()
        {
            // 边界：attacker 比 defender 低 1 → 不算 high ground
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 1 },
                { new GridCoord(6, 5), 2 },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, null, null);
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        [Test]
        public void HighGround_FullCover_StillPenalty()
        {
            // High Ground 不豁免 Full Cover（设计取舍：Full Cover 仍给 penalty=2，
            // 由调用方决定是否在 high ground 时仍将其视为挡视线）
            var map = MakeMap();
            var heights = new DictHeightLookup(new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5), 3 },
                { new GridCoord(6, 5), 0 },
            });
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Full },
            });
            var r = LineOfSightService.ComputeLineOfSight(
                map, new GridCoord(5, 5), new GridCoord(6, 5), heights, covers, null);
            Assert.IsTrue(r.HasHighGroundBonus);
            Assert.AreEqual(2, r.CoverPenalty); // Full 仍给 2
        }
    }
}
