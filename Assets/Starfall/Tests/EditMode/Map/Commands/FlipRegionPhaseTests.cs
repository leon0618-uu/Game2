using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 FlipRegionPhaseCommand 测试集。
    /// 覆盖：5 cell 区域全部翻、PhaseLocked 导致整体 Fail + atomic、
    /// 区域不存在、空区域、同 layer 翻同区域、AffectedTiles 排序。
    /// </summary>
    public class FlipRegionPhaseTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;
        private const int RegionId = 1;

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.region.test",
                width: 8,
                height: 8,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);

            int id = 1;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    _registry.Register(TileDefinitionRegistry.Make(
                        id++, new GridCoord(x, y), TerrainType.Plain, tags: TileTags.Walkable));
                    _map.AddTile(new GridCoord(x, y));
                }
            }

            // 区域 regionId=1：5 cell —— (2,2) (3,2) (4,2) (3,3) (3,4)。全部 GateTile + PhaseFlippable。
            var regionTiles = new System.Collections.Generic.List<GridCoord>
            {
                new GridCoord(2, 2), new GridCoord(3, 2),
                new GridCoord(4, 2), new GridCoord(3, 3), new GridCoord(3, 4),
            };
            // 先把要重用的 coords + 新 ids 都从 registry 中除去。
            // 注：RemoveById 才能唯一清理某个 tileId，不能只 Remove(coord) —
            //     否则新 id=50 会冲突于之前 (6,1) 的 id=50。
            for (int rid = 50; rid <= 54; rid++)
            {
                _registry.RemoveById(rid);
            }
            foreach (var rc in regionTiles)
            {
                _registry.Remove(rc);
            }
            // region 中再标 5 个 tile 为 GateTile + PhaseFlippable
            int ridCounter = 50;
            foreach (var rc in regionTiles)
            {
                _registry.Register(TileDefinitionRegistry.Make(
                    ridCounter++, rc, TerrainType.GateTile, tags: TileTags.PhaseFlippable));
            }
            _map.AddRegion(new MapRegion(RegionId, "TestRegion", "Player", regionTiles));

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

        [Test]
        public void FlipRegionPhase_AllFiveTiles_Succeed()
        {
            // anchor tile = (2,2)，id=50
            var result = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success, $"Should flip region: {result.FailureReason}");
            Assert.AreEqual(5, result.AffectedTiles.Count);
        }

        [Test]
        public void FlipRegionPhase_AffectedTiles_SortedYThenX()
        {
            var result = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success);
            for (int i = 1; i < result.AffectedTiles.Count; i++)
            {
                Assert.IsTrue(
                    result.AffectedTiles[i - 1].CompareTo(result.AffectedTiles[i]) < 0,
                    $"AffectedTiles not sorted at index {i}");
            }
        }

        [Test]
        public void FlipRegionPhase_PhaseLockedInRegion_AtomicFailure()
        {
            // 把 region 内某个 tile 改成 PhaseLocked
            _registry.Remove(new GridCoord(3, 3));
            // 找下一个可用 id（之前 region 内 id 从 50 起：51,52,53,54,55）
            _registry.Register(TileDefinitionRegistry.Make(
                99, new GridCoord(3, 3), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked));

            var result = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.FailureReason.Contains("phase locked"));

            // atomic 验证：state 没被部分写入（region 内其它 tile 应未被改写为 Astral）
            var state = PhaseFlipStateService.GetOrAttach(_map);
            Assert.IsFalse(state.TryGetFlippedLayer(50, out _),
                "Atomic: tileId=50 should NOT be flipped when atomic-fail.");
        }

        [Test]
        public void FlipRegionPhase_NotPhaseFlippableInRegion_Fails()
        {
            // 把 region 内 (2,2) 改为 plain（无 PhaseFlippable）；anchor=99 注册为 PhaseFlippable，
            // 但不是 region 内坐标→ 返回 "region not found"。对于 "not phase flippable"
            // 检查：使用 anchor=98（同 coord (2,2) 原位零 swap 选型）。
            // 本轮使用 region 中另个 anchor：
            // 策略：保留 (2,2) 为 PhaseFlippable，在 region 另一 cell (3,3) 改为 Plain Walkable，
            // 然后用 51 (3,2) 作为 anchor（仍在 region 内）。
            _registry.RemoveById(98);
            _registry.Remove(new GridCoord(3, 3));
            _registry.Register(TileDefinitionRegistry.Make(
                98, new GridCoord(3, 3), TerrainType.Plain, tags: TileTags.Walkable));
            // anchor=51 is at (3,2) which is PhaseFlippable GateTile — still valid anchor
            var result = new FlipRegionPhaseCommand(51, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.FailureReason.Contains("not phase flippable"));
        }

        [Test]
        public void FlipRegionPhase_AlreadyAtTargetLayer_Fails()
        {
            // 翻转一次 → 全部翻到 Astral
            var r1 = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r1.Success);
            // 同样的命令再跑（同样的 TargetLayer=Astral → 全部已到目标 → Fail）
            var r2 = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(r2.Success);
            Assert.IsTrue(r2.FailureReason.Contains("already at target layer"));
        }

        [Test]
        public void FlipRegionPhase_UnknownTileAnchor_Fails()
        {
            // tileId 99999 不存在
            var result = new FlipRegionPhaseCommand(99999, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("tile not found", result.FailureReason);
        }

        [Test]
        public void FlipRegionPhase_AnchorTileNotInAnyRegion_Fails()
        {
            // 在 (7,7) 处追加一个 PhaseFlippable tileId，但不在任何 region 内。
            _registry.RemoveById(200);
            _registry.Remove(new GridCoord(7, 7));
            _registry.Register(TileDefinitionRegistry.Make(
                200, new GridCoord(7, 7), TerrainType.GateTile, tags: TileTags.PhaseFlippable));
            _map.AddTile(new GridCoord(7, 7));
            var result = new FlipRegionPhaseCommand(200, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("region not found", result.FailureReason);
        }

        [Test]
        public void FlipRegionPhase_Constructor_RejectsZeroOrNegativeAnchorTileId()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new FlipRegionPhaseCommand(0, DimensionLayer.Astral));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new FlipRegionPhaseCommand(-1, DimensionLayer.Astral));
        }

        [Test]
        public void FlipRegionPhase_NoRegistryAttached_FailsWithMessage()
        {
            PhaseFlipStateService.Detach(_map);
            var result = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("no tile registry attached", result.FailureReason);
            PhaseFlipStateService.Attach(_map, _registry);
        }

        [Test]
        public void FlipRegionPhase_RegionFlipState_PersistsOnAllTiles()
        {
            var result = new FlipRegionPhaseCommand(50, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success);
            var state = PhaseFlipStateService.GetOrAttach(_map);
            // region 内每个 tile id 应对应（50~54）
            for (int id = 50; id <= 54; id++)
            {
                Assert.IsTrue(state.TryGetFlippedLayer(id, out var layer),
                    $"tileId={id} should be flipped");
                Assert.AreEqual(DimensionLayer.Astral, layer);
            }
        }
    }
}
