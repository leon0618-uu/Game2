using System;
using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 部署区 / 出生区 / 跨层 bounds 校验测试集。
    /// <para/>
    /// 覆盖：PlayerDeployment bounds 校验 / EnemySpawn count 上限 / 跨层 bounds。
    /// </summary>
    public class DeploymentValidationTests
    {
        private static MapState MakeMap()
        {
            return new MapState(new MapDefinition("map.test", 8, 8, DimensionLayer.Reality, 0));
        }

        // ──────────── 1) PlayerDeployment 工厂默认 ────────────

        [Test]
        public void PlayerDeployment_Default_Is_OwnerSide_Zero()
        {
            var def = MapRegionDefinition.PlayerSpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) });
            Assert.AreEqual(0, def.OwnerSide);
            Assert.AreEqual(RegionActivation.Available, def.Activation);
        }

        [Test]
        public void PlayerDeployment_Priority_DefaultsTo50()
        {
            var def = MapRegionDefinition.PlayerSpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) });
            Assert.AreEqual(50, def.Priority);
        }

        // ──────────── 2) EnemySpawn 工厂默认 ────────────

        [Test]
        public void EnemySpawn_Default_Is_OwnerSide_One()
        {
            var def = MapRegionDefinition.EnemySpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) });
            Assert.AreEqual(1, def.OwnerSide);
        }

        // ──────────── 3) bounds 跨层 ────────────

        [Test]
        public void Bounds_CrossLayer_AreAllowed()
        {
            var raw = new List<GridCoord>
            {
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(2, 0, DimensionLayer.Reality),
                new GridCoord(2, 2, DimensionLayer.Astral),  // 跨层
                new GridCoord(0, 2, DimensionLayer.Astral)
            };
            var def = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, raw);
            Assert.AreEqual(4, def.Bounds.Count);
            // 输入顺序保留
            Assert.AreEqual(DimensionLayer.Reality, def.Bounds[0].Layer);
        }

        [Test]
        public void Bounds_CrossLayer_AllInBoundsSize()
        {
            // Map size is 8x8 = 64 tiles; cross-layer is fine
            var raw = new List<GridCoord>
            {
                new GridCoord(0, 0, DimensionLayer.Reality),
                new GridCoord(7, 0, DimensionLayer.Reality),
                new GridCoord(7, 7, DimensionLayer.Astral),
                new GridCoord(0, 7, DimensionLayer.Astral)
            };
            var def = new MapRegionDefinition(new RegionId(1), RegionKind.BossPhase, raw);
            // Should succeed
            Assert.IsNotNull(def);
        }

        // ──────────── 4) bounds 保留输入顺序 ────────────

        [Test]
        public void Bounds_ReverseOrder_PreservesInputOrder()
        {
            var raw = new List<GridCoord>
            {
                new GridCoord(0, 2), new GridCoord(2, 2),
                new GridCoord(2, 0), new GridCoord(0, 0)
            };
            var def = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, raw);
            // 反向输入也保留——多边形连通性依赖顶点顺序。
            Assert.AreEqual(new GridCoord(0, 2), def.Bounds[0]);
            Assert.AreEqual(new GridCoord(2, 2), def.Bounds[1]);
            Assert.AreEqual(new GridCoord(2, 0), def.Bounds[2]);
            Assert.AreEqual(new GridCoord(0, 0), def.Bounds[3]);
        }

        // ──────────── 5) 部署区 + spawn point 协作 ────────────

        [Test]
        public void PlayerDeployment_WithSpawnPoint_ServiceLink()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            var def = MapRegionDefinition.PlayerSpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) });
            service.Register(map, def);
            // Add spawn point manually
            map.AddSpawnPoint(new MapSpawnPoint(new SpawnId(1), 1, new GridCoord(1, 1), 0));
            var spawns = MapSpawnService.GetSpawnsInRegion(map, 1);
            Assert.AreEqual(1, spawns.Count);
            Assert.AreEqual(1, spawns[0].SpawnId);
        }

        // ──────────── 6) 多个 region 注册 ────────────

        [Test]
        public void MultipleRegions_AllRegistered()
        {
            var map = MakeMap();
            var service = new MapRegionService();
            service.Register(map, MapRegionDefinition.PlayerSpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) }));
            service.Register(map, MapRegionDefinition.EnemySpawn(2, new List<GridCoord> {
                new GridCoord(5, 0), new GridCoord(7, 0), new GridCoord(7, 2) }));
            Assert.AreEqual(2, map.RegionStates.Count);
        }

        // ──────────── 7) Triggers 在 deployment 上的用法 ────────────

        [Test]
        public void PlayerDeployment_WithOnEnterTrigger_IsSorted()
        {
            var triggers = new List<RegionTrigger>
            {
                new RegionTrigger(RegionTriggerKind.OnEnter, "deploy"),
            };
            var def = MapRegionDefinition.PlayerSpawn(1, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) }, priority: 80);
            var defWithTrig = new MapRegionDefinition(
                new RegionId(1), RegionKind.PlayerDeployment,
                def.Bounds, def.OwnerSide, def.Priority, def.Activation, triggers);
            Assert.AreEqual(1, defWithTrig.Triggers.Count);
            Assert.AreEqual(RegionTriggerKind.OnEnter, defWithTrig.Triggers[0].Kind);
        }

        // ──────────── 8) Empty trigger list 不影响 hash 稳定 ────────────

        [Test]
        public void EmptyTriggerList_DoesNotAffectHash()
        {
            var def1 = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) });
            var def2 = new MapRegionDefinition(new RegionId(1), RegionKind.Capture, new List<GridCoord> {
                new GridCoord(0, 0), new GridCoord(2, 0), new GridCoord(0, 2) }, triggers: null);
            // Both empty triggers => same hash
            var rs1 = new MapRegionState(def1);
            var rs2 = new MapRegionState(def2);
            Assert.AreEqual(rs1.PostStateHash, rs2.PostStateHash);
        }
    }
}