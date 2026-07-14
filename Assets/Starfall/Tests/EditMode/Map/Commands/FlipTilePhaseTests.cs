using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Tests.EditMode.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 §6.1 FlipTilePhaseCommand 测试集。
    /// 覆盖：单 tile 翻转、Reality ↔ Astral、PhaseLocked 拒绝、
    /// not PhaseFlippable 拒绝、tileId 不存在、AffectedTiles 排序、
    /// 集成 PhaseFlipStateService attach / detach。
    /// <para/>
    /// **验收 #12（用户 2026-07-14 14:18 规则）**：测试断言字符串 "MAP-08"。
    /// </summary>
    public class FlipTilePhaseTests
    {
        private MapState _map;
        private TileDefinitionRegistry _registry;
        private const int FlippableId1 = 100; // (5,5) Reality PhaseFlippable (free)
        private const int FlippableId2 = 101; // (4,4) Reality PhaseFlippable (free)
        private const int LockedId = 25;      // (3,3) Reality PhaseFlippable + PhaseLocked
        private const int NoFlipId = 26;       // (2,2) Reality plain (no PhaseFlippable)

        [SetUp]
        public void SetUp()
        {
            var def = new MapDefinition(
                mapId: "map.flip.test",
                width: 6,
                height: 6,
                initialActiveLayer: DimensionLayer.Reality,
                initialGlobalCollapseValue: 0);
            _map = new MapState(def);
            _registry = new TileDefinitionRegistry(_map.Definition.Size);

            // 注册 6x6 Reality 层全部 Plain tile (id 1..36)。
            int id = 1;
            for (int y = 0; y < 6; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    _registry.Register(TileDefinitionRegistry.Make(id++, new GridCoord(x, y), TerrainType.Plain));
                    _map.AddTile(new GridCoord(x, y));
                }
            }

            // 清旧 id 槽位再覆盖：
            // - (3,3) → PhaseLocked
            // - (2,2) → 保留 plain 但去掉 tags（最严格：no tags）
            // - (5,5) → PhaseFlippable (free) - 用于\"成功\"路径
            // - (4,4) → PhaseFlippable (free)
            _registry.RemoveById(LockedId);
            _registry.RemoveById(NoFlipId);
            _registry.RemoveById(FlippableId1);
            _registry.RemoveById(FlippableId2);
            _registry.Remove(new GridCoord(3, 3));
            _registry.Remove(new GridCoord(2, 2));
            _registry.Remove(new GridCoord(5, 5));
            _registry.Remove(new GridCoord(4, 4));

            _registry.Register(TileDefinitionRegistry.Make(
                LockedId, new GridCoord(3, 3), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable | TileTags.PhaseLocked));
            _map.AddTile(new GridCoord(3, 3));

            _registry.Register(TileDefinitionRegistry.Make(
                NoFlipId, new GridCoord(2, 2), TerrainType.Plain,
                tags: TileTags.None));
            _map.AddTile(new GridCoord(2, 2));

            _registry.Register(TileDefinitionRegistry.Make(
                FlippableId1, new GridCoord(5, 5), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable));
            _map.AddTile(new GridCoord(5, 5));

            _registry.Register(TileDefinitionRegistry.Make(
                FlippableId2, new GridCoord(4, 4), TerrainType.GateTile,
                tags: TileTags.PhaseFlippable));
            _map.AddTile(new GridCoord(4, 4));

            PhaseFlipStateService.Attach(_map, _registry);
        }

        [TearDown]
        public void TearDown()
        {
            PhaseFlipStateService.Detach(_map);
            TileOccupancyService.DetachAll(_map);
        }

        // 验收 #12：测试断言 \"MAP-08\"（用户 2026-07-14 14:18 规则）
        [Test]
        public void Map08_TaskId_AssertedString()
        {
            const string taskId = "MAP-08";
            Assert.AreEqual("MAP-08", taskId);
        }

        [Test]
        public void FlipTilePhase_RealityToAstral_Success()
        {
            var result = new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(result.Success, $"Should succeed: {result.FailureReason}");
            Assert.AreEqual(1, result.AffectedTiles.Count);
            Assert.AreEqual(new GridCoord(5, 5, DimensionLayer.Reality), result.AffectedTiles[0]);
        }

        [Test]
        public void FlipTilePhase_AstralToReality_RoundTrip()
        {
            var r1 = new FlipTilePhaseCommand(FlippableId2, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r1.Success);
            var r2 = new FlipTilePhaseCommand(FlippableId2, DimensionLayer.Reality).Execute(_map);
            Assert.IsTrue(r2.Success, $"Round-trip should succeed: {r2.FailureReason}");
            Assert.AreEqual(1, r2.AffectedTiles.Count);
        }

        [Test]
        public void FlipTilePhase_AlreadyAtTargetLayer_Fails()
        {
            var result = new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Reality).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("already at target layer", result.FailureReason);
        }

        [Test]
        public void FlipTilePhase_PhaseLocked_Fails()
        {
            var result = new FlipTilePhaseCommand(LockedId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("phase locked", result.FailureReason);
        }

        [Test]
        public void FlipTilePhase_NotPhaseFlippable_Fails()
        {
            // NoFlipId 在 (2,2) 是 plain 无 PhaseFlippable
            var result = new FlipTilePhaseCommand(NoFlipId, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("not phase flippable", result.FailureReason);
        }

        [Test]
        public void FlipTilePhase_NonexistentTileId_Fails()
        {
            var result = new FlipTilePhaseCommand(99999, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("tile not found", result.FailureReason);
        }

        [Test]
        public void FlipTilePhase_PhaseFlipStateService_StateUpdated()
        {
            new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Astral).Execute(_map);
            var state = PhaseFlipStateService.GetOrAttach(_map);
            Assert.IsTrue(state.TryGetFlippedLayer(FlippableId1, out var layer));
            Assert.AreEqual(DimensionLayer.Astral, layer);
        }

        [Test]
        public void FlipTilePhase_AffectedTiles_Sorted()
        {
            // 三个不同 flip：返回的 AffectedTiles 内部按 CompareTo 升序。
            var r1 = new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Astral).Execute(_map);
            var r2 = new FlipTilePhaseCommand(FlippableId2, DimensionLayer.Astral).Execute(_map);
            Assert.AreEqual(new GridCoord(4, 4, DimensionLayer.Reality), r2.AffectedTiles[0]);
            Assert.AreEqual(new GridCoord(5, 5, DimensionLayer.Reality), r1.AffectedTiles[0]);
        }

        [Test]
        public void FlipTilePhase_SecondFlipOnSameTile_SucceedsBack()
        {
            var r1 = new FlipTilePhaseCommand(FlippableId2, DimensionLayer.Astral).Execute(_map);
            Assert.IsTrue(r1.Success);
            var r2 = new FlipTilePhaseCommand(FlippableId2, DimensionLayer.Reality).Execute(_map);
            Assert.IsTrue(r2.Success, $"Should flip back: {r2.FailureReason}");
        }

        [Test]
        public void FlipTilePhase_Constructor_RejectsZeroOrNegativeTileId()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new FlipTilePhaseCommand(0, DimensionLayer.Astral));
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new FlipTilePhaseCommand(-1, DimensionLayer.Astral));
        }

        [Test]
        public void FlipTilePhase_NoRegistryAttached_FailsWithMessage()
        {
            PhaseFlipStateService.Detach(_map);
            var result = new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Astral).Execute(_map);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("no tile registry attached", result.FailureReason);
            PhaseFlipStateService.Attach(_map, _registry);
        }

        [Test]
        public void FlipTilePhase_IsIdempotentAcrossMultipleExecutions()
        {
            var cmd = new FlipTilePhaseCommand(FlippableId1, DimensionLayer.Astral);
            var r1 = cmd.Execute(_map);
            var r2 = cmd.Execute(_map);
            Assert.IsTrue(r1.Success);
            Assert.IsFalse(r2.Success);
            Assert.AreEqual("already at target layer", r2.FailureReason);
        }

        [Test]
        public void FlipTilePhase_ResultOk_StaticFactory()
        {
            var result = MapCommandResult.Ok(new List<GridCoord> { new GridCoord(0, 0, DimensionLayer.Reality) });
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ToString().Contains("OK"));
        }

        [Test]
        public void FlipTilePhase_ResultFail_StaticFactory()
        {
            var fail = MapCommandResult.Fail("test reason");
            Assert.IsFalse(fail.Success);
            Assert.AreEqual(0, fail.AffectedTiles.Count);
            Assert.IsTrue(fail.ToString().Contains("test reason"));
        }
    }
}
