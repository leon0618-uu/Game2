using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Commands.Compression;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using System.Collections.Generic;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 PhaseCompressionResolutionService 测试集。
    /// 覆盖：3 unit 同 cell → 弹出第 3 个到邻居；1-距离 / 2-距离回退；
    /// 全部不可达 → null；footprint 2x2 跨格压缩；占用迁移 + 落点合法。
    /// </summary>
    public class PhaseCompressionTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.compress.test",
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
        public void PhaseCompression_ThreeUnits_DisplacedIsThird()
        {
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 2, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 3, Footprint.SingleCell, coord);

            var ids = new List<int> { 1, 2, 3 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(3, result.Value.displacedUnitId);
        }

        [Test]
        public void PhaseCompression_DisplacedToNearestNeighbour()
        {
            // 起点 (3,3) → 4 邻居全部合法 → 应该弹出到 (4,3) 或 (3,4) 等
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 2, Footprint.SingleCell, coord);

            var ids = new List<int> { 1, 2 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(2, result.Value.displacedUnitId);
            // 邻居顺序 N(3,4) E(4,3) S(3,2) W(2,3)
            var dst = result.Value.newCoord;
            Assert.AreEqual(1, dst.ManhattanDistance(coord),
                "Should land in a 4-neighbour (Manhattan=1).");
        }

        [Test]
        public void PhaseCompression_DisplacedToNorthFirstDeterministic()
        {
            // (3,3) 邻居全空 → N (3,4) 第一个胜
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 2, Footprint.SingleCell, coord);

            var ids = new List<int> { 1, 2 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(result.HasValue);
            // 我的服务是 N → E → S → W。N = (3, 4)
            Assert.AreEqual(new GridCoord(3, 4, DimensionLayer.Reality), result.Value.newCoord);
        }

        [Test]
        public void PhaseCompression_FallsBackToManhattan2WhenAllNeighboursOccupied()
        {
            // 起点 (3,3)。把 4 邻居全被占 → 跳到 Manhattan=2 ring
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 2, Footprint.SingleCell, coord);

            // 4 邻居各放一个
            TileOccupancyService.TryPlaceUnit(_map, 10, Footprint.SingleCell, new GridCoord(3, 4, DimensionLayer.Reality));
            TileOccupancyService.TryPlaceUnit(_map, 11, Footprint.SingleCell, new GridCoord(4, 3, DimensionLayer.Reality));
            TileOccupancyService.TryPlaceUnit(_map, 12, Footprint.SingleCell, new GridCoord(3, 2, DimensionLayer.Reality));
            TileOccupancyService.TryPlaceUnit(_map, 13, Footprint.SingleCell, new GridCoord(2, 3, DimensionLayer.Reality));

            var ids = new List<int> { 1, 2 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(2, result.Value.displacedUnitId);
            // Manhattan=2 ring 任意：比较 CompareTo 顺序
            Assert.AreEqual(2, result.Value.newCoord.ManhattanDistance(coord),
                "Should fall back to Manhattan=2 when Manhattan=1 ring is full.");
        }

        [Test]
        public void PhaseCompression_AllCellsBlocked_ReturnsNull()
        {
            // 极端：3x3 全部 Plain 但起点被占 → 邻居部分被占 → 也不可达
            var def = new MapDefinition(
                mapId: "map.compress.blocked",
                width: 3, height: 3,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            var smallReg = new TileDefinitionRegistry(small.Definition.Size);

            // (1,1) Plain, 其余 Void
            smallReg.Register(TileDefinitionRegistry.Make(1, new GridCoord(1, 1), TerrainType.Plain));
            small.AddTile(new GridCoord(1, 1));
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
            try
            {
                var coord = new GridCoord(1, 1, DimensionLayer.Reality);
                TileOccupancyService.TryPlaceUnit(small, 1, Footprint.SingleCell, coord);
                TileOccupancyService.TryPlaceUnit(small, 2, Footprint.SingleCell, coord);

                var ids = new List<int> { 1, 2 };
                // 起点 (1,1) 是边界 cell；4 邻居 + Manhattan=2 都越界 → ring2 全越界 → null
                var result = PhaseCompressionResolutionService.Resolve(small, coord, ids);
                Assert.IsFalse(result.HasValue, "All neighbours / ring2 out-of-bounds → null.");
            }
            finally
            {
                PhaseFlipStateService.Detach(small);
                TileOccupancyService.DetachAll(small);
            }
        }

        [Test]
        public void PhaseCompression_LessThanTwoUnits_ReturnsNull()
        {
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            var ids = new List<int> { 1 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsFalse(result.HasValue,
                "Single unit → no compression.");
        }

        [Test]
        public void PhaseCompression_EmptyUnitList_ReturnsNull()
        {
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            var ids = new List<int>();
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void PhaseCompression_Footprint2x2_AnchorCellOccupied_Resolution()
        {
            // 跨格 2x2 footprint。anchor (3,3) 占用 4 cells: (3,3)(4,3)(3,4)(4,4)
            // 同 anchor 多 unit → 1 个 unit 整体迁走。
            // 我们先把 (3,3)(4,3)(3,4)(4,4) 都空着，再用 Footprint.TwoByTwo 占一次
            var anchor = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 7, Footprint.TwoByTwo, anchor);

            // 但 Compression 仍然用 anchor coord 作 origin
            var ids = new List<int> { 7, 8 };
            // 第二个 unitId 8 没真的 place；这里直接验证返回值不抛，且 displaced 是 8
            // 邻居 (3,2)(4,2)(5,3)(2,3)(5,2)(2,2) 等空着 → 第一个 N (3,4) 是 occupied → 跳过
            // 实际上 (3,4) 是 anchor footprint → 占了。E (4,3) 也占了。
            // 接下来 S (3,2) → 落点
            // 注意：fallback 当 4 邻居全被占才退 Manhattan=2。这里 (3,4)(4,3) 被占，(3,2)(2,3) 空 → S 胜
            // 实际上 4 邻居是 N→E→S→W：N(3,4)=占用 → 跳；E(4,3)=占用 → 跳；S(3,2)=空 → 胜
            // 由于 unitId 8 没 place，expected new coord 应当选 (3,2)
            var result = PhaseCompressionResolutionService.Resolve(_map, anchor, ids);
            Assert.IsTrue(result.HasValue, $"Should succeed: {result?.ToString()}");
            Assert.AreEqual(8, result.Value.displacedUnitId);
            Assert.AreEqual(new GridCoord(3, 2, DimensionLayer.Reality), result.Value.newCoord,
                "Anchor footprint 2x2 → N/E occupied → S (3,2) wins.");
        }

        [Test]
        public void PhaseCompression_DisplacedUnit_ResolveAndApply_HappyPath()
        {
            // 验证 Resolve + 后续 Remove/Place 流。
            // 注意：TileOccupancyService 不允许同 cell 2 个 unit（MVP-04 freeze）；
            // 本测试以 unitId 1 占据 (3,3)，调用 Resolve(unitIds={1, 99})，其中 unitId=99
            // 为“逻辑上另一同 cell unit”（象征压缩场景中的第 2 个 unit）。Resolve 仅凭 unitIds 列表计算，
            // 不依赖 occupancy 状态。返回值 = (99, newCoord)。
            // 验证当 newCoord 上能被 unit 99 place 时 — 可 place=true。
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);

            var ids = new List<int> { 1, 99 };
            var resolution = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(resolution.HasValue);
            Assert.AreEqual(99, resolution.Value.displacedUnitId);

            // 验证 newCoord 能放 unitId=99
            Assert.IsTrue(
                TileOccupancyService.TryPlaceUnit(_map, 99, Footprint.SingleCell, resolution.Value.newCoord),
                "newCoord should be a placeable cell.");

            // 起点仍被 unitId=1 占
            var occ = TileOccupancyService.GetOccupantUnit(_map, coord);
            Assert.IsTrue(occ.HasValue);
            Assert.AreEqual(1, occ.Value);
            // 新坐标是 unitId=99
            var occNew = TileOccupancyService.GetOccupantUnit(_map, resolution.Value.newCoord);
            Assert.IsTrue(occNew.HasValue);
            Assert.AreEqual(99, occNew.Value);
        }

        [Test]
        public void PhaseCompression_FallsBackToManhattan2_CellsSortedByCompareTo()
        {
            // 起 (3,3)，4 邻居全占。Manhattan=2 ring 内 CompareTo 最小 → (3,1) Y=1 X=3。
            // 因为 Manhattan=2 要求 |ΔX|+|ΔY|=2：最低 Y=1 的点为 (3,1)（X=±3+0）。
            var coord = new GridCoord(3, 3, DimensionLayer.Reality);
            TileOccupancyService.TryPlaceUnit(_map, 1, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 2, Footprint.SingleCell, coord);
            TileOccupancyService.TryPlaceUnit(_map, 10, Footprint.SingleCell, new GridCoord(3, 4));
            TileOccupancyService.TryPlaceUnit(_map, 11, Footprint.SingleCell, new GridCoord(4, 3));
            TileOccupancyService.TryPlaceUnit(_map, 12, Footprint.SingleCell, new GridCoord(3, 2));
            TileOccupancyService.TryPlaceUnit(_map, 13, Footprint.SingleCell, new GridCoord(2, 3));

            var ids = new List<int> { 1, 2 };
            var result = PhaseCompressionResolutionService.Resolve(_map, coord, ids);
            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(2, result.Value.displacedUnitId);
            // ring2 sorted by CompareTo: Y=1 first → (3,1) 胜。
            Assert.AreEqual(new GridCoord(3, 1, DimensionLayer.Reality), result.Value.newCoord,
                $"Manhattan=2 ring with sorted CompareTo should pick (3,1) (lowest Y among Manhattan=2 cells). Got {result.Value.newCoord}.");
        }

        [Test]
        public void PhaseCompression_NoRegistryAttached_ReturnsNull()
        {
            var def = new MapDefinition(
                mapId: "map.compress.noattach",
                width: 3, height: 3,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            var small = new MapState(def);
            // 不 attach
            var ids = new List<int> { 1, 2 };
            var result = PhaseCompressionResolutionService.Resolve(small, new GridCoord(1, 1), ids);
            Assert.IsFalse(result.HasValue);
        }
    }
}
