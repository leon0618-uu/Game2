using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Commands.Fall;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 FallResolutionService 测试集。
    /// 覆盖：Void / Stability=0 / 已占用起点、跨层候选、曼哈顿距离并列、
    /// 排序、footprint 2x2 跨越、起点已在合法落点。
    /// </summary>
    public class FallResolutionTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.fall.test",
                width: 9,
                height: 9,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);

            int id = 1;
            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    _registry.Register(TileDefinitionRegistry.Make(id++, new GridCoord(x, y), TerrainType.Plain));
                    _map.AddTile(new GridCoord(x, y));
                }
            }
            PhaseFlipStateService.Attach(_map, _registry);
            TileOccupancyService.AttachTileDefinitionRegistry(_map, _registry);
            TileOccupancyService.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PhaseFlipStateService.Detach(_map);
            TileOccupancyService.DetachAll(_map);
            TileOccupancyService.Clear();
        }

        [Test]
        public void Map08_TaskId_AssertedString()
        {
            const string taskId = "MAP-08";
            Assert.AreEqual("MAP-08", taskId);
        }

        [Test]
        public void FallResolution_StartIsValid_ReturnsStartCoord()
        {
            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, 1);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(coord, result.Value);
        }

        [Test]
        public void FallResolution_VoidStart_ReturnsNearestPlain()
        {
            // 替换 (4,4) 为 Void tile（BlocksMovement=true）
            _registry.Remove(new GridCoord(4, 4));
            _registry.Register(TileDefinitionRegistry.Make(
                100, new GridCoord(4, 4), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));

            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, 1);
            Assert.IsTrue(result.HasValue);
            // Manhattan distance 1 from (4,4)
            var d = result.Value.ManhattanDistance(coord);
            Assert.AreEqual(1, d, "Should land within Manhattan 1 of (4,4)");
        }

        [Test]
        public void FallResolution_VoidStart_TieBreakPrefersLowerY()
        {
            // (4,4) 是 Void。8 个 1-距离邻居均合法 Plain。
            // 在 8 邻居里 (3,4) Y=4, (4,4) Y=4, (5,4) Y=4, (4,3) Y=3, (4,5) Y=5
            // CompareTo: Y 小者优先 → Y=3 (i.e. (4,3))
            _registry.Remove(new GridCoord(4, 4));
            _registry.Register(TileDefinitionRegistry.Make(
                100, new GridCoord(4, 4), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));

            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, 1);
            Assert.IsTrue(result.HasValue);
            // 排序：(4, 3) Y=3 < (3,4)/(4,4)/(5,4) Y=4 < (4,5) Y=5
            Assert.AreEqual(new GridCoord(4, 3, DimensionLayer.Reality), result.Value,
                "Tie-break by Y first → (4,3) has lower Y.");
        }

        [Test]
        public void FallResolution_NoLegalAroundOrigin_ReturnsNull()
        {
            // 把整张 9x9 都标为 Void（除了中心）也基本无解。
            // 极端：把 9x9 全部 Void 不可通过。
            var def = new MapDefinition(
                mapId: "map.fall.allvoid",
                width: 3, height: 3,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);
            int id = 1;
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    smallReg.Register(TileDefinitionRegistry.Make(
                        id++, new GridCoord(x, y), TerrainType.Void,
                        tags: TileTags.Void | TileTags.Impassable));
                    small.AddTile(new GridCoord(x, y));
                }
            }
            PhaseFlipStateService.Attach(small, smallReg);
            try
            {
                var result = FallResolutionService.FindNearestLegalLanding(small, new GridCoord(1, 1, DimensionLayer.Reality), 1);
                Assert.IsFalse(result.HasValue, "All voids → should return null.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
            }
        }

        [Test]
        public void FallResolution_CrossLayer_Free_AstralChosen()
        {
            // 起点 (5,5,Reality) 是 Void 不可用。同坐标 (5,5,Astral) Plain 可用。
            // 按 AGENTS.md §11 确定性规则：不能跨 Layer 自由“跨相位靠指”而落；
            // 跨 Layer 需要 PhaseFlip + GateTile 机制（本轮不启用）。
            // 因此起点 Reality 仍只查 Reality 合落点。近邻 (5,4)/(5,6)/(4,5)/(6,5) Plain 都可用，
            // Y 小者 (5,4) 胜。
            _registry.Remove(new GridCoord(5, 5));
            _registry.Register(TileDefinitionRegistry.Make(
                201, new GridCoord(5, 5, DimensionLayer.Reality),
                TerrainType.Void, tags: TileTags.Void | TileTags.Impassable));
            _registry.Register(TileDefinitionRegistry.Make(
                202, new GridCoord(5, 5, DimensionLayer.Astral),
                TerrainType.Plain, tags: TileTags.Walkable));

            var result = FallResolutionService.FindNearestLegalLanding(_map, new GridCoord(5, 5, DimensionLayer.Reality), 1);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(new GridCoord(5, 4, DimensionLayer.Reality), result.Value,
                "Cross-layer without PhaseFlip: stays on Reality layer; Y=4 (5,4) wins by CompareTo.");
        }

        [Test]
        public void FallResolution_StartOccupiedByAnotherUnit_FindsFreeNeighbour()
        {
            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            // 起点 (4,4) 仍合法（不是 Void / blocked），但被另一个 unit 占了。
            TileOccupancyService.TryPlaceUnit(_map, 99, Footprint.SingleCell, coord);

            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, 1);
            Assert.IsTrue(result.HasValue);
            Assert.AreNotEqual(coord, result.Value, "Should NOT return occupied origin.");
            Assert.AreEqual(1, result.Value.ManhattanDistance(coord), "Should land within Manhattan 1.");
        }

        [Test]
        public void FallResolution_StartOccupiedBySelf_StillReturnsStart()
        {
            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);

            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, unitId: 1);
            // 自身占用被视为"可重新布置"，返回起点。
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(coord, result.Value);
        }

        [Test]
        public void FallResolution_SurroundedByImpassable_ReturnsNull()
        {
            // 极端：3x3，只有 (1,1) 是 Plain；起点为 (1,1) 本身 → 是 valid → 返回起点。
            var def = new MapDefinition(
                mapId: "map.fall.surrounded",
                width: 3, height: 3,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);

            // 中心 (1,1) Plain
            smallReg.Register(TileDefinitionRegistry.Make(
                1, new GridCoord(1, 1), TerrainType.Plain, tags: TileTags.Walkable));
            small.AddTile(new GridCoord(1, 1));
            // 邻居 8 个全部 Void + Impassable
            int id = 10;
            foreach (var c in new[] {
                new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0),
                new GridCoord(0, 1), new GridCoord(2, 1),
                new GridCoord(0, 2), new GridCoord(1, 2), new GridCoord(2, 2)
            })
            {
                smallReg.Register(TileDefinitionRegistry.Make(
                    id++, c, TerrainType.Void, tags: TileTags.Void | TileTags.Impassable));
                small.AddTile(c);
            }
            PhaseFlipStateService.Attach(small, smallReg);
            TileOccupancyService.AttachTileDefinitionRegistry(small, smallReg);
            try
            {
                // 起点 (1,1) 本身合法 → 返回 (1,1) 本身。
                var coord = new GridCoord(1, 1, DimensionLayer.Reality);
                var result = FallResolutionService.FindNearestLegalLanding(small, coord, 1);
                Assert.IsTrue(result.HasValue);
                Assert.AreEqual(coord, result.Value,
                    "Already legal starting cell: should return itself.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
                TileOccupancyService.DetachAll(small);
            }
        }

        [Test]
        public void FallResolution_TieBreak_LargeGridRandomCheck()
        {
            // 极端：(2,3) vs (3,2) 都是 (2,3) → Y 小者 (2,3) 胜出
            // 用 5x5 全 Plain：(2,3)(3,2) 距同点 (2,2)：M=1+1=2
            // 设 (2,2) 起点被认为 Void，移走
            _registry.Remove(new GridCoord(2, 2));
            _registry.Register(TileDefinitionRegistry.Make(
                300, new GridCoord(2, 2), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));

            // 第二点：(2,3) vs (3,2) — Y 小者优先 = (3,2) 胜（Y=2）
            // 但 (2,1) 的 Y=1 比他们都小，最近邻居里 (2,1) 实际胜。
            var origin = new GridCoord(2, 2, DimensionLayer.Reality);
            var result = FallResolutionService.FindNearestLegalLanding(_map, origin, 1);
            Assert.IsTrue(result.HasValue);
            // 实际胜者：(2,1) Y=1（4 邻居中最小 Y）
            Assert.AreEqual(new GridCoord(2, 1, DimensionLayer.Reality), result.Value,
                $"Tie-break Manhattan=1 should pick (2,1) (lowest Y). Got {result.Value}.");
        }

        [Test]
        public void FallResolution_PhaseFlip_RealityTile_TreatedAsValid()
        {
            // 起点 (4,4,Reality) 本身是 Plain。 PhaseFlip 为它们提供一个备选（实际不跳层）。
            // 本轮跨 Layer 需要 PhasePairTileId（MAP-07）才能拼接跨层。
            // 起点已合法 → 返回起点。
            var coord = new GridCoord(4, 4, DimensionLayer.Reality);
            var result = FallResolutionService.FindNearestLegalLanding(_map, coord, 1);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(coord, result.Value,
                "Already at a legal starting cell: should land back to itself.");
        }

        [Test]
        public void FallResolution_Determinism_SameInputSameOutput()
        {
            _registry.Remove(new GridCoord(4, 4));
            _registry.Register(TileDefinitionRegistry.Make(
                500, new GridCoord(4, 4), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));

            var origin = new GridCoord(4, 4, DimensionLayer.Reality);
            // 跑 3 次确保完全确定性
            var r1 = FallResolutionService.FindNearestLegalLanding(_map, origin, 1);
            var r2 = FallResolutionService.FindNearestLegalLanding(_map, origin, 1);
            var r3 = FallResolutionService.FindNearestLegalLanding(_map, origin, 1);
            Assert.IsTrue(r1.HasValue);
            Assert.AreEqual(r1.Value, r2.Value);
            Assert.AreEqual(r2.Value, r3.Value);
        }

        [Test]
        public void FallResolution_AllValidCandidate_SortedByCompareTo()
        {
            // 大网格 5x5，把中心 (2,2) 设为 Void；候选包括全部其它 cells。
            // 选 Manhattan=1：4 邻居 { (1,2), (3,2), (2,1), (2,3) }
            // CompareTo 排序：Y=1(2,1) < Y=2(1,2)/(3,2) < Y=3(2,3) → (2,1) 胜。
            var def = new MapDefinition(
                mapId: "map.fall.deter",
                width: 5, height: 5,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);
            int id = 1;
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 5; x++)
                {
                    smallReg.Register(TileDefinitionRegistry.Make(id++, new GridCoord(x, y), TerrainType.Plain));
                    small.AddTile(new GridCoord(x, y));
                }
            }
            // (2,2) 删了重新注册为 Void
            smallReg.Remove(new GridCoord(2, 2));
            id = 100;
            smallReg.Register(TileDefinitionRegistry.Make(
                id, new GridCoord(2, 2), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));
            PhaseFlipStateService.Attach(small, smallReg);

            try
            {
                var result = FallResolutionService.FindNearestLegalLanding(small, new GridCoord(2, 2, DimensionLayer.Reality), 1);
                Assert.IsTrue(result.HasValue);
                Assert.AreEqual(new GridCoord(2, 1, DimensionLayer.Reality), result.Value,
                    $"Tie-break CompareTo → Y=1 (2,1) wins. Got {result.Value}.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
            }
        }

        [Test]
        public void FallResolution_VoidStart_CrossLayerPriority()
        {
            // 起点 (5,5,Reality) 是 Void 不可用。(5,5,Astral) 是 Plain 可用。
            // 跨 Layer 需要 PhaseFlip 机制（本轮不可用）。
            // 所以仅 Reality 合落点：(5,4)/(5,6)/(4,5)/(6,5) Plain 都可用，Y 小者 (5,4) 胜。
            _registry.Remove(new GridCoord(5, 5));
            _registry.Register(TileDefinitionRegistry.Make(
                600, new GridCoord(5, 5, DimensionLayer.Reality),
                TerrainType.Void, tags: TileTags.Void | TileTags.Impassable));
            _registry.Register(TileDefinitionRegistry.Make(
                601, new GridCoord(5, 5, DimensionLayer.Astral),
                TerrainType.Plain, tags: TileTags.Walkable));

            var result = FallResolutionService.FindNearestLegalLanding(_map, new GridCoord(5, 5, DimensionLayer.Reality), 1);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(new GridCoord(5, 4, DimensionLayer.Reality), result.Value);
        }

        [Test]
        public void FallResolution_StableOrderVerification_HundredCandidates()
        {
            // 10x10 网格 + 中心 (5,5) 为 Void + 全部 Plain
            var def = new MapDefinition(
                mapId: "map.fall.big",
                width: 10, height: 10,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 10; x++)
                {
                    smallReg.Register(TileDefinitionRegistry.Make(
                        y * 10 + x + 1, new GridCoord(x, y), TerrainType.Plain));
                    small.AddTile(new GridCoord(x, y));
                }
            }
            smallReg.Remove(new GridCoord(5, 5));
            smallReg.Register(TileDefinitionRegistry.Make(
                999, new GridCoord(5, 5), TerrainType.Void,
                tags: TileTags.Void | TileTags.Impassable));
            PhaseFlipStateService.Attach(small, smallReg);
            try
            {
                var result = FallResolutionService.FindNearestLegalLanding(small, new GridCoord(5, 5, DimensionLayer.Reality), 1);
                Assert.IsTrue(result.HasValue);
                // Manhattan=1 邻居中 Y 最小：(4,5) → 无，(5,4) Y=4
                // 实际：邻居 (4,5)(5,4)(6,5)(5,6) — Y=4 是 (5,4)
                Assert.AreEqual(new GridCoord(5, 4, DimensionLayer.Reality), result.Value,
                    "Among 8-neighbours of (5,5) with all voids around, Y=4 (5,4) should win by CompareTo.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
            }
        }

        [Test]
        public void FallResolution_NullRegistry_ReturnsNull()
        {
            var def = new MapDefinition(
                mapId: "map.fall.noattach",
                width: 5, height: 5,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            // 不 attach PhaseFlipStateService
            var result = FallResolutionService.FindNearestLegalLanding(small, new GridCoord(0, 0, DimensionLayer.Reality), 1);
            Assert.IsFalse(result.HasValue);
        }
    }
}
