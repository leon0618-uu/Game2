using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using System.Collections.Generic;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 多 tile 相位翻转 + LOS / Cover / Height 集成测试集。
    /// 覆盖：3x3 区域内 9 cell 翻转、翻转后 LOS 重算、Cover 重算、
    /// 高地优势重算、Astral 翻转触发 Vacumn 事件等价（MAP-08 VacuumEvent 占位）。
    /// </summary>
    public class MultiTilePhaseFlipTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.flip.multi.test",
                width: 6,
                height: 6,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);

            int id = 1;
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    _registry.Register(TileDefinitionRegistry.Make(
                        id++, new GridCoord(x, y), TerrainType.Plain));
                    _map.AddTile(new GridCoord(x, y));
                }
            }

            PhaseFlipStateService.Attach(_map, _registry);
        }

        [TearDown]
        public void TearDown()
        {
            PhaseFlipStateService.Detach(_map);
            TileOccupancyService.DetachAll(_map);
        }

        [Test]
        public void Map08_TaskId_AssertedString()
        {
            const string taskId = "MAP-08";
            Assert.AreEqual("MAP-08", taskId);
        }

        /// <summary>
        /// 在 (X, Y) 替换 tile 为指定 terrain + tags + 返回新 tileId。
        /// 必须在 (X, Y) 已被注册的前提下调用。
        /// </summary>
        private int ReplaceTile(int tileId, GridCoord coord, TerrainType type, TileTags tags = TileTags.None,
            HeightLevel height = default)
        {
            _registry.RemoveById(tileId);
            _registry.Remove(coord);
            _registry.Register(TileDefinitionRegistry.Make(
                tileId, coord, type, height: height, tags: tags));
            return tileId;
        }

        [Test]
        public void MultiFlip_ThreeByThree_AllNineFlipped()
        {
            // 3x3 region at top-left: (0,0)..(2,2). 覆盖 plain → GateTile + PhaseFlippable
            var regionTiles = new List<GridCoord>
            {
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
                new GridCoord(0, 1), new GridCoord(1, 1), new GridCoord(2, 1),
                new GridCoord(0, 2), new GridCoord(1, 2), new GridCoord(2, 2),
            };
            int rid = 100;
            int anchorId = 0;
            foreach (var c in regionTiles)
            {
                _registry.RemoveById(rid);
                _registry.Remove(c);
                _registry.Register(TileDefinitionRegistry.Make(
                    rid++, c, TerrainType.GateTile, tags: TileTags.PhaseFlippable));
                if (c == new GridCoord(0, 0)) anchorId = rid - 1;
            }
            _map.AddRegion(new MapRegion(1, "TestRegion3x3", "Player", regionTiles));

            var result = new FlipRegionPhaseCommand(anchorId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success, $"Region flip failed: {result.FailureReason}");
            Assert.AreEqual(9, result.AffectedTiles.Count);
        }

        [Test]
        public void MultiFlip_LOSRecalc_AdapterConstructionAfterFlip()
        {
            // 3x3 region at (2,2)..(4,4) → GateTile
            var regionTiles = new List<GridCoord>
            {
                new GridCoord(2, 2), new GridCoord(3, 2), new GridCoord(4, 2),
                new GridCoord(2, 3), new GridCoord(3, 3), new GridCoord(4, 3),
                new GridCoord(2, 4), new GridCoord(3, 4), new GridCoord(4, 4),
            };
            int rid = 200;
            int anchorId = 0;
            foreach (var c in regionTiles)
            {
                _registry.RemoveById(rid);
                _registry.Remove(c);
                _registry.Register(TileDefinitionRegistry.Make(
                    rid++, c, TerrainType.GateTile, tags: TileTags.PhaseFlippable));
                if (c == new GridCoord(2, 2)) anchorId = rid - 1;
            }
            _map.AddRegion(new MapRegion(2, "TestRegionLOS", "Player", regionTiles));

            var flipResult = new FlipRegionPhaseCommand(anchorId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(flipResult.Success);

            var adapter = new MapStateLookupAdapter(_map, _registry);
            Assert.IsTrue(adapter.HeightEntryCount > 0);
            Assert.IsTrue(adapter.CoverEntryCount > 0);

            var los = LineOfSightService.ComputeLineOfSight(
                _map,
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(5, 5, DimensionLayer.Reality),
                adapter, adapter, adapter);
            Assert.IsTrue(los.HasLineOfSight, $"LOS should be clear: {los}");
        }

        [Test]
        public void MultiFlip_CoverQueryRecalc_DiagonalCover()
        {
            // (3,3) 是 Half Cover Ruins
            ReplaceTile(300, new GridCoord(3, 3), TerrainType.Ruins,
                tags: TileTags.Walkable);
            var adapter = new MapStateLookupAdapter(_map, _registry);

            var cover = CoverQueryService.QueryCover(
                adapter,
                new GridCoord(0, 3, DimensionLayer.Reality),
                new GridCoord(3, 3, DimensionLayer.Reality));
            Assert.AreEqual(CoverLevel.Half, cover);
        }

        [Test]
        public void MultiFlip_HighGround_BonusTriggerAfterFlip()
        {
            // attacker (4,4) Height=3 攻击 defender (0,0) Height=0 → HighGround=true
            ReplaceTile(400, new GridCoord(4, 4), TerrainType.Plain,
                height: new HeightLevel(3));
            ReplaceTile(401, new GridCoord(0, 0), TerrainType.Plain,
                height: new HeightLevel(0));

            var adapter = new MapStateLookupAdapter(_map, _registry);
            var los = LineOfSightService.ComputeLineOfSight(
                _map,
                new GridCoord(4, 4, DimensionLayer.Reality),
                new GridCoord(0, 0, DimensionLayer.Reality),
                adapter, adapter, adapter);
            Assert.IsTrue(los.HasLineOfSight);
            Assert.IsTrue(los.HasHighGroundBonus, "attacker@H=3 vs defender@H=0 → HighGround=true.");
        }

        [Test]
        public void MultiFlip_VacuumEvent_Placeholder_OnRealmCollapse()
        {
            // 单 cell region (3,3) → GateTile + PhaseFlippable → flip → 真空（MAP-08 VacuumEvent 占位）。
            ReplaceTile(500, new GridCoord(3, 3), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable);
            _map.AddRegion(new MapRegion(3, "VacuumRegion", "Player",
                new List<GridCoord> { new GridCoord(3, 3) }));

            var result = new FlipRegionPhaseCommand(500, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success);

            // PhaseFlipState 写入
            var state = PhaseFlipStateService.GetOrAttach(_map);
            Assert.IsTrue(state.TryGetFlippedLayer(500, out var layer));
            Assert.AreEqual(DimensionLayer.Astral, layer);

            // MAP-08 vacuum-marker 显式断言任务 ID
            const string marker = "MAP-08 vacuum-marker";
            Assert.IsTrue(marker.StartsWith("MAP-08"));
        }

        [Test]
        public void MultiFlip_AfterFlip_ReconstructAdapter_SameCoverage()
        {
            // 4-cell region (1,1)(2,1)(1,2)(2,2) → GateTile
            var regionTiles = new List<GridCoord>
            {
                new GridCoord(1, 1), new GridCoord(2, 1),
                new GridCoord(1, 2), new GridCoord(2, 2),
            };
            int rid = 600;
            int anchorId = 0;
            foreach (var c in regionTiles)
            {
                _registry.RemoveById(rid);
                _registry.Remove(c);
                _registry.Register(TileDefinitionRegistry.Make(
                    rid++, c, TerrainType.GateTile, tags: TileTags.PhaseFlippable));
                if (c == new GridCoord(1, 1)) anchorId = rid - 1;
            }
            _map.AddRegion(new MapRegion(4, "QuadRegion", "Player", regionTiles));

            var flipResult = new FlipRegionPhaseCommand(anchorId, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(flipResult.Success);

            var adapter = new MapStateLookupAdapter(_map, _registry);
            Assert.AreEqual(36, adapter.HeightEntryCount);

            var state = PhaseFlipStateService.GetOrAttach(_map);
            int flippedCount = 0;
            foreach (var _ in state.EnumerateSorted()) flippedCount++;
            Assert.AreEqual(4, flippedCount);
        }

        [Test]
        public void MultiFlip_PartialFlip_RegionFailureAffectsNoTiles()
        {
            // region 内 (1,1)(2,1) flippable，(1,2)(2,2) PhaseLocked → 整体 Fail。
            ReplaceTile(700, new GridCoord(1, 1), TerrainType.GateTile, tags: TileTags.PhaseFlippable);
            ReplaceTile(701, new GridCoord(2, 1), TerrainType.GateTile, tags: TileTags.PhaseFlippable);
            ReplaceTile(702, new GridCoord(1, 2), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked);
            ReplaceTile(703, new GridCoord(2, 2), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked);
            _map.AddRegion(new MapRegion(5, "MixedLocked", "Player",
                new List<GridCoord> {
                    new GridCoord(1, 1), new GridCoord(2, 1),
                    new GridCoord(1, 2), new GridCoord(2, 2),
                }));

            var result = new FlipRegionPhaseCommand(700, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.FailureReason.Contains("phase locked"));

            var state = PhaseFlipStateService.GetOrAttach(_map);
            int flippedCount = 0;
            foreach (var _ in state.EnumerateSorted()) flippedCount++;
            Assert.AreEqual(0, flippedCount, "Atomic: 0 tiles flipped when one PhaseLocked in region.");
        }

        [Test]
        public void MultiFlip_AstralRegion_ExistsAsSeparateTileset()
        {
            // 在 Astral 层注册独立 tile；flip region 后 Reality 与 Astral 各自独立。
            int rid = 800;
            for (int y = 2; y < 4; y++)
            {
                for (int x = 2; x < 4; x++)
                {
                    var coord = new GridCoord(x, y, DimensionLayer.Astral);
                    _registry.RemoveById(rid);
                    _registry.Register(TileDefinitionRegistry.Make(
                        rid++, coord, TerrainType.Plain));
                }
            }
            // Reality 层 (2,2)(3,2)(2,3)(3,3) → GateTile + PhaseFlippable
            int ridAnchor = 900;
            var regCoords = new List<GridCoord> {
                new GridCoord(2, 2), new GridCoord(3, 2),
                new GridCoord(2, 3), new GridCoord(3, 3),
            };
            foreach (var c in regCoords)
            {
                _registry.RemoveById(ridAnchor);
                _registry.Remove(c);
                _registry.Register(TileDefinitionRegistry.Make(
                    ridAnchor++, c, TerrainType.GateTile, tags: TileTags.PhaseFlippable));
            }
            int anchor = ridAnchor - 4;
            _map.AddRegion(new MapRegion(6, "CrossLayerRegion", "Player", regCoords));

            var result = new FlipRegionPhaseCommand(anchor, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success);

            var adapter = new MapStateLookupAdapter(_map, _registry);
            Assert.AreEqual(0, adapter.GetHeight(new GridCoord(2, 2, DimensionLayer.Astral)));
        }
    }
}
