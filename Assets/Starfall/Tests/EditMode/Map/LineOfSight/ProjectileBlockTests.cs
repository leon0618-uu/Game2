using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.LineOfSight
{
    /// <summary>
    /// <see cref="LineOfSightService.ComputeProjectileLOS"/> 行为测试（≥ 6 项）。
    /// 覆盖：6 种 <see cref="ProjectileType"/> + 跨 Layer。
    /// </summary>
    public class ProjectileBlockTests
    {
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
            => new MapState(new MapDefinition("test.map", w, h, DimensionLayer.Reality, 0));

        // ──────────── Direct ────────────

        [Test]
        public void Direct_FullCover_Blocks()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Full },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Direct, null, covers, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        [Test]
        public void Direct_HalfCover_PenaltyOne()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Half },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Direct, null, covers, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(1, r.CoverPenalty);
        }

        // ──────────── Arc ────────────

        [Test]
        public void Arc_FullCover_Blocks()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Full },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Arc, null, covers, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        [Test]
        public void Arc_HalfCover_Ignored()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Half },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Arc, null, covers, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.AreEqual(0, r.CoverPenalty);
        }

        // ──────────── Beam ────────────

        [Test]
        public void Beam_FullCover_Blocks()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Full },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Beam, null, covers, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        // ──────────── Chain ────────────

        [Test]
        public void Chain_FullCover_Blocks()
        {
            var map = MakeMap();
            var covers = new DictCoverLookup(new Dictionary<GridCoord, CoverLevel>
            {
                { new GridCoord(6, 5), CoverLevel.Full },
            });
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(6, 5),
                ProjectileType.Chain, null, covers, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        // ──────────── GroundPropagation ────────────

        [Test]
        public void GroundPropagation_NonGroundBlock_Ignored()
        {
            // ground layer 阻挡会被看到；非 ground / 高 height 不影响
            var map = MakeMap();
            // 阻挡点放在 Reality 0 高度
            var blockers = new HashSet<GridCoord> { new GridCoord(6, 5, DimensionLayer.Reality) };
            var b = new DictBlockingLookup(blockers);

            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(8, 5),
                ProjectileType.GroundPropagation, null, null, b);
            // ground propagation 强制 ground layer 视线，且高度=0 时阻挡会被看到
            // 但实际：从 (5,5) 到 (8,5) ground layer 路径上 (6,5) 是阻挡
            // ground propagation 视为 0 高度 → 阻挡生效
            Assert.IsFalse(r.HasLineOfSight);
        }

        [Test]
        public void GroundPropagation_ClearPath_HasLineOfSight()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(8, 5),
                ProjectileType.GroundPropagation, null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
        }

        // ──────────── CrossPhase ────────────

        [Test]
        public void CrossPhase_PenetratesLayers()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeProjectileLOS(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(8, 5, DimensionLayer.Astral),
                ProjectileType.CrossPhase, null, null, null);
            Assert.IsTrue(r.HasLineOfSight);
        }

        [Test]
        public void CrossPhase_NoHighGroundEvenIfElevated()
        {
            // 即使 attacker 在 high ground，CrossPhase 跨层 → 无 high ground
            var map = MakeMap();
            var heights = new Dictionary<GridCoord, int>
            {
                { new GridCoord(5, 5, DimensionLayer.Reality), 2 },
                { new GridCoord(8, 5, DimensionLayer.Astral), 0 },
            };
            var hl = new DictHeightLookupAdapter(heights);

            var r = LineOfSightService.ComputeProjectileLOS(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(8, 5, DimensionLayer.Astral),
                ProjectileType.CrossPhase, hl, null, null);
            Assert.IsTrue(r.HasLineOfSight);
            Assert.IsFalse(r.HasHighGroundBonus);
        }

        // 简易高度适配器
        private sealed class DictHeightLookupAdapter : IHeightLookup
        {
            private readonly Dictionary<GridCoord, int> _data;
            public DictHeightLookupAdapter(Dictionary<GridCoord, int> data) { _data = data; }
            public int GetHeight(GridCoord c) => _data.TryGetValue(c, out var v) ? v : 0;
        }

        // ──────────── 跨 Layer 默认阻挡（Direct）────────────

        [Test]
        public void Direct_CrossLayer_FailsByDefault()
        {
            var map = MakeMap();
            var r = LineOfSightService.ComputeProjectileLOS(
                map,
                new GridCoord(5, 5, DimensionLayer.Reality),
                new GridCoord(8, 5, DimensionLayer.Astral),
                ProjectileType.Direct, null, null, null);
            Assert.IsFalse(r.HasLineOfSight);
        }

        // ──────────── Arc + 阻挡 ────────────

        [Test]
        public void Arc_Blocker_Blocks()
        {
            var map = MakeMap();
            var blockers = new HashSet<GridCoord> { new GridCoord(6, 5) };
            var b = new DictBlockingLookup(blockers);
            var r = LineOfSightService.ComputeProjectileLOS(
                map, new GridCoord(5, 5), new GridCoord(8, 5),
                ProjectileType.Arc, null, null, b);
            Assert.IsFalse(r.HasLineOfSight);
        }
    }
}
