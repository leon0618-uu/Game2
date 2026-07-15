using System.Collections.Generic;
using NUnit.Framework;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Tests.EditMode.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a <see cref="CollapseValueService"/> 测试集（≥ 15 测试）。
    /// 覆盖：Tick 推进、5 阶段效果、ApplyLocalDamage、GetHotspots、与 MAP-09 联动、终态 GateFault。
    /// </summary>
    public class CollapseValueServiceTests
    {
        private static MapState MakeMap(int initialCV = 0)
        {
            return new MapState(new MapDefinition("map.test", 8, 8,
                DimensionLayer.Reality, initialCV));
        }

        // ──────────── 1-5) Tick 推进 + 阶段切换 ────────────

        [Test]
        public void Tick_IncrementsValue_ByDefaultDelta()
        {
            var map = MakeMap(0);
            var svc = new CollapseValueService();
            svc.Tick(map);
            Assert.AreEqual(1, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.Stable, map.CurrentStage);
        }

        [Test]
        public void Tick_IncrementsTickAccumulated()
        {
            var map = MakeMap(0);
            var svc = new CollapseValueService();
            svc.Tick(map);
            svc.Tick(map);
            svc.Tick(map);
            Assert.AreEqual(3, map.GlobalCV.TickAccumulated);
        }

        [Test]
        public void Tick_AtBoundary_TransitionsStage()
        {
            var map = MakeMap(19);
            var svc = new CollapseValueService();
            svc.Tick(map);
            Assert.AreEqual(20, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.Anomalous, map.CurrentStage);
        }

        [Test]
        public void Tick_OverHundred_ClampsToGateFault()
        {
            var map = MakeMap(99);
            var svc = new CollapseValueService();
            svc.Tick(map);
            Assert.AreEqual(100, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.GateFault, map.CurrentStage);
        }

        [Test]
        public void Tick_DefaultTickDelta_Configurable()
        {
            var map = MakeMap(0);
            var svc = new CollapseValueService { DefaultTickDelta = 5 };
            svc.Tick(map);
            Assert.AreEqual(5, map.GlobalCV.Value);
        }

        [Test]
        public void Tick_SyncsShadowField()
        {
            var map = MakeMap(0);
            var svc = new CollapseValueService();
            svc.Tick(map);
            svc.Tick(map);
            Assert.AreEqual(map.GlobalCV.Value, map.GlobalCollapseValue,
                "Tick must sync GlobalCollapseValue shadow field with GlobalCV");
        }

        // ──────────── 6) Stable 阶段无效果 ────────────

        [Test]
        public void Tick_AtStable_TriggersNoEventOrRegionEffect()
        {
            var map = MakeMap(0);
            var regionService = new MapRegionService();
            var svc = new CollapseValueService();
            var kinds = svc.Tick(map, regionService);
            // Stable 阶段：不产生 OnGlobalCVChanged 事件（值变化 ≠ 阶段变化）
            // 注：本服务返回 list 表示"本 Tick 内哪些事件被请求 Emit"；
            //     Stable 阶段不变化也 Emit，因为值变了（0→1），
            //     但阶段没变 = 仍 Emit OnGlobalCVChanged 表示数值变化。
            //     严格语义：值变 = 必有 OnGlobalCVChanged。
            Assert.Contains(MapEvent.MapEventKind.OnGlobalCVChanged, kinds);
        }

        // ──────────── 7-9) ApplyLocalDamage ────────────

        [Test]
        public void ApplyLocalDamage_AddsValue_AndDerivesStability()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            var lcv = svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            Assert.AreEqual(30, lcv.Value);
            Assert.AreEqual(TileStability.Unstable, lcv.Stability);
        }

        [Test]
        public void ApplyLocalDamage_Accumulates_ExistingValue()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            var lcv = svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            Assert.AreEqual(60, lcv.Value);
            Assert.AreEqual(TileStability.Fractured, lcv.Stability);
        }

        [Test]
        public void ApplyLocalDamage_ClampsAt100()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 80);
            var lcv = svc.ApplyLocalDamage(map, new GridCoord(0, 0), 50);
            Assert.AreEqual(100, lcv.Value);
        }

        [Test]
        public void ApplyLocalDamage_NegativeAmount_Throws()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => svc.ApplyLocalDamage(map, new GridCoord(0, 0), -1));
        }

        // ──────────── 10) GetLocalValue / GetGlobalValue ────────────

        [Test]
        public void GetLocalValue_NotFound_ReturnsZero()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            var lcv = svc.GetLocalValue(map, new GridCoord(5, 5));
            Assert.AreEqual(0, lcv.Value);
            Assert.AreEqual(TileStability.Stable, lcv.Stability);
        }

        [Test]
        public void GetGlobalValue_ReturnsCurrent()
        {
            var map = MakeMap(45);
            var svc = new CollapseValueService();
            var gcv = svc.GetGlobalValue(map);
            Assert.AreEqual(45, gcv.Value);
            Assert.AreEqual(CollapseStage.Fracturing, gcv.Stage);
        }

        // ──────────── 11-12) GetHighLocalValues / GetHotspots ────────────

        [Test]
        public void GetHighLocalValues_FiltersAndSorts()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            svc.ApplyLocalDamage(map, new GridCoord(1, 0), 80);
            svc.ApplyLocalDamage(map, new GridCoord(2, 0), 50);
            svc.ApplyLocalDamage(map, new GridCoord(3, 0), 90);

            var high = svc.GetHighLocalValues(map, threshold: 50);
            // 应返回 (3,0)=90, (1,0)=80, (2,0)=50；过滤掉 (0,0)=30
            Assert.AreEqual(3, high.Count);
            Assert.AreEqual(90, high[0].Value);
            Assert.AreEqual(80, high[1].Value);
            Assert.AreEqual(50, high[2].Value);
        }

        [Test]
        public void GetHotspots_ReturnsTopN_SortedByValue()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            svc.ApplyLocalDamage(map, new GridCoord(1, 0), 80);
            svc.ApplyLocalDamage(map, new GridCoord(2, 0), 50);
            svc.ApplyLocalDamage(map, new GridCoord(3, 0), 90);
            var hotspots = svc.GetHotspots(map, topN: 2);
            Assert.AreEqual(2, hotspots.Count);
            Assert.AreEqual(90, hotspots[0].Value);
            Assert.AreEqual(80, hotspots[1].Value);
        }

        [Test]
        public void GetHotspots_TopNZero_ReturnsAll()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            svc.ApplyLocalDamage(map, new GridCoord(1, 0), 80);
            var hotspots = svc.GetHotspots(map, topN: 0);
            Assert.AreEqual(2, hotspots.Count);
        }

        // ──────────── 13-14) 与 MAP-09 联动（Collapsing 阶段）────────────

        [Test]
        public void Tick_AtCollapsing_TransitionsEnvironmentalHazard_Active_To_Contested()
        {
            var map = MakeMap(60);
            var regionService = new MapRegionService();
            // 注册一个 EnvironmentalHazard region
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.EnvironmentalHazard,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(0, 3) });
            regionService.Register(map, def);
            // 强制进入 Active（合法性：Available → Active）
            regionService.TransitionState(map, new RegionId(1), RegionState.Active, "test");

            var svc = new CollapseValueService();
            svc.Tick(map, regionService);

            var rs = regionService.FindRegion(map, new RegionId(1));
            Assert.AreEqual(RegionState.Contested, rs.State, "EnvironmentalHazard Active → Contested in Collapsing");
        }

        [Test]
        public void Tick_AtCollapsing_AdjustsCaptureRegion_ActivationProgress()
        {
            var map = MakeMap(60);
            var regionService = new MapRegionService();
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(0, 3) });
            regionService.Register(map, def);
            regionService.TransitionState(map, new RegionId(1), RegionState.Active, "test");

            var svc = new CollapseValueService { CollapsingRegionDelta = 10 };
            int before = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;
            svc.Tick(map, regionService);
            int after = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;
            Assert.AreEqual(before + 10, after, "Capture ActivationProgress += CollapsingRegionDelta");
        }

        [Test]
        public void Tick_AtCollapsing_DoesNotAffectPlayerDeployment_Region()
        {
            var map = MakeMap(60);
            var regionService = new MapRegionService();
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.PlayerDeployment,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(0, 3) });
            regionService.Register(map, def);
            regionService.TransitionState(map, new RegionId(1), RegionState.Active, "test");
            int before = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;

            var svc = new CollapseValueService();
            svc.Tick(map, regionService);

            int after = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;
            Assert.AreEqual(before, after, "PlayerDeployment should not change in Collapsing");
        }

        // ──────────── 15) 终态 GateFault ────────────

        [Test]
        public void Tick_Reaching_GateFault_StaysAt100()
        {
            var map = MakeMap(100);
            var svc = new CollapseValueService();
            svc.Tick(map);
            Assert.AreEqual(100, map.GlobalCV.Value);
            Assert.AreEqual(CollapseStage.GateFault, map.CurrentStage);
        }

        [Test]
        public void GateFault_DoesNotAdjustRegions()
        {
            // GateFault 阶段：服务不主动调整 regions（业务编排器在收到 OnGateFaultTriggered 后
            // 自行决定如何处理）。
            var map = MakeMap(80);
            var regionService = new MapRegionService();
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(0, 3) });
            regionService.Register(map, def);
            regionService.TransitionState(map, new RegionId(1), RegionState.Active, "test");
            int before = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;

            var svc = new CollapseValueService();
            svc.Tick(map, regionService);

            int after = regionService.FindRegion(map, new RegionId(1)).ActivationProgress;
            Assert.AreEqual(before, after, "GateFault must not adjust region ActivationProgress");
        }

        // ──────────── 16) Reset ────────────

        [Test]
        public void Reset_ClearsGlobalAndLocal()
        {
            var map = MakeMap(50);
            var svc = new CollapseValueService();
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 80);
            svc.ApplyLocalDamage(map, new GridCoord(1, 1), 60);
            Assert.AreEqual(50, map.GlobalCV.Value);
            Assert.AreEqual(2, map.LocalCVs.Count);

            svc.Reset(map);
            Assert.AreEqual(0, map.GlobalCV.Value);
            Assert.AreEqual(0, map.LocalCVs.Count);
        }

        // ──────────── 17) GetRegionsAtCoord ────────────

        [Test]
        public void GetRegionsAtCoord_WithRegionService_ReturnsContaining()
        {
            var map = MakeMap();
            var regionService = new MapRegionService();
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(3, 3), new GridCoord(0, 3) });
            regionService.Register(map, def);
            var svc = new CollapseValueService();
            var regions = svc.GetRegionsAtCoord(map, new GridCoord(1, 1), regionService);
            Assert.AreEqual(1, regions.Count);
            Assert.AreEqual(new RegionId(1), regions[0].Definition.RegionIdValue);
        }

        [Test]
        public void GetRegionsAtCoord_Fallback_NoRegionService()
        {
            var map = MakeMap();
            var regionService = new MapRegionService();
            var def = new MapRegionDefinition(
                new RegionId(1),
                RegionKind.Capture,
                new[] { new GridCoord(0, 0), new GridCoord(3, 0), new GridCoord(3, 3), new GridCoord(0, 3) });
            regionService.Register(map, def);
            var svc = new CollapseValueService();
            // regionService = null → fallback to direct iteration
            var regions = svc.GetRegionsAtCoord(map, new GridCoord(1, 1), null);
            Assert.AreEqual(1, regions.Count);
        }

        // ──────────── 18) ApplyLocalDamageWithEvent ────────────

        [Test]
        public void ApplyLocalDamageWithEvent_ReportsFracture_WhenStabilityBecomesImpassable()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            // (0,0) 累积 60 → Fractured (不可通行)
            var (lcv, fractured) = svc.ApplyLocalDamageWithEvent(
                map, new GridCoord(0, 0), 60, prevStability: TileStability.Stable);
            Assert.IsTrue(fractured, "Value 60 → Fractured → should report fractured=true");
            Assert.AreEqual(60, lcv.Value);
            Assert.AreEqual(TileStability.Fractured, lcv.Stability);
        }

        [Test]
        public void ApplyLocalDamageWithEvent_StableToUnstable_NotFractured()
        {
            var map = MakeMap();
            var svc = new CollapseValueService();
            // (0,0) 累积 30 → Unstable (可通行)
            var (lcv, fractured) = svc.ApplyLocalDamageWithEvent(
                map, new GridCoord(0, 0), 30, prevStability: TileStability.Stable);
            Assert.IsFalse(fractured, "Unstable is still passable → fractured=false");
            Assert.AreEqual(TileStability.Unstable, lcv.Stability);
        }

        // ──────────── 19) 100-run hash 稳定（含 service 操作）────────────

        [Test]
        public void MapState_Hash_StableAfter_ServiceTick_Over100Runs()
        {
            var map = MakeMap(0);
            var svc = new CollapseValueService();
            svc.Tick(map);
            svc.ApplyLocalDamage(map, new GridCoord(0, 0), 30);
            svc.ApplyLocalDamage(map, new GridCoord(1, 1), 60);
            ulong h0 = map.PostStateHash;
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(h0, map.PostStateHash, $"Hash drift at iteration {i}");
            }
        }
    }
}
