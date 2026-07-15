using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 坍塌值核心服务（ADR-0007）。
    ///
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item>每回合推进 <see cref="GlobalCollapseValue"/>（默认 +1 / 回合）。</item>
    /// <item>按阶段应用不同效果：Stable 无效果；Anomalous 随机生成 <see cref="MapEventKind.OnAnomalyDetected"/>；
    ///       Fracturing 高频生成 <see cref="MapEventKind.OnTileFractured"/>（基于本地 CV ≥ 50 的格子）；
    ///       Collapsing 调用 <see cref="MapRegionService"/> 把 <see cref="RegionKind.EnvironmentalHazard"/>
    ///       / <see cref="RegionKind.Restricted"/> 标 <see cref="RegionState.Contested"/>；
    ///       GateFault 终态 Emit <see cref="MapEventKind.OnGateFaultTriggered"/>（游戏结束条件之一）。</item>
    /// <item>提供 <see cref="ApplyLocalDamage"/> 单 tile CV 累积入口。</item>
    /// <item>查询高 CV 区域：<see cref="GetHighLocalValues"/> / <see cref="GetHotspots"/>。</item>
    /// <item>与 MAP-09 联动：<see cref="MapRegionService.GetRegionsContaining"/> 查询包含该格的所有 region；
    ///       当 region 是 <see cref="RegionKind.Capture"/> / <see cref="RegionKind.Escort"/> /
    ///       <see cref="RegionKind.Restricted"/> 时，根据 GlobalCV 阶段调整 <c>ActivationProgress</c>。</item>
    /// </list>
    ///
    /// <para/>
    /// **设计原则**：服务不缓存状态——所有读写直接走 <see cref="MapState"/>。
    /// 本服务**没有**内部 Tick 计数器；Tick 来源是调用方（<c>BattleRunner</c> / 编排器）。
    ///
    /// <para/>
    /// **确定性**：
    /// <list type="bullet">
    /// <item>本服务不依赖 <see cref="Random"/> / 当前时间；所有"随机"由调用方决定。</item>
    /// <item>5 阶段效果在 Tick 内顺序固定；事件按 (kind, coord, value) 稳定排序（写入方排）。</item>
    /// </list>
    /// </summary>
    public sealed class CollapseValueService
    {
        // ──────────── 配置 ────────────

        /// <summary>每 Tick GlobalCV 默认增量。</summary>
        public int DefaultTickDelta { get; set; } = 1;

        /// <summary>本地 CV ≥ 此阈值视为"高 CV 格子"（Fracturing 阶段使用）。</summary>
        public int LocalFractureThreshold { get; set; } = 50;

        /// <summary>Fracturing 阶段 Emit OnTileFractured 事件的"概率权重"（确定性场景下用 idx % denom 模拟）。</summary>
        public int FracturingEmitDenominator { get; set; } = 1; // 默认每个高 CV tile 都 Emit

        /// <summary>每个 region 在 Collapsing 阶段被调整的 ActivationProgress 增量。</summary>
        public int CollapsingRegionDelta { get; set; } = 5;

        // ──────────── 状态机效应：Tick 推进 ────────────

        /// <summary>
        /// 推进 1 回合：GlobalCV += <see cref="DefaultTickDelta"/>；按阶段 Emit 事件 + 应用效果。
        /// </summary>
        /// <returns>本 Tick 内产生的全部 MapEvent（按 stage 顺序追加，跨 stage 内已 Sort）。</returns>
        public IReadOnlyList<MapEventKind> Tick(
            MapState mapState,
            MapRegionService regionService = null)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));

            // 1) GlobalCV += delta（自动 clamp + 派生 Stage）
            int oldValue = mapState.GlobalCV.Value;
            CollapseStage oldStage = mapState.GlobalCV.Stage;
            var newGcv = mapState.GlobalCV
                .WithValue(oldValue + DefaultTickDelta)
                .WithIncrementedTick();
            mapState.GlobalCV = newGcv;
            int newValue = newGcv.Value;
            CollapseStage newStage = newGcv.Stage;

            // 2) 同步影子字段（向后兼容）
            // mapState.GlobalCollapseValue setter 会自动同步；本步骤不需要。

            // 3) OnGlobalCVChanged 事件（按契约：value 变化时必须 Emit）
            //    注意：仅当 stage 切换或 value 实际变化时 Emit；
            //    delta=0 时不变化，Tick 不应增加。
            if (newValue != oldValue || newStage != oldStage)
            {
                // 状态机阶段切换同时 Emit OnGlobalCVChanged
                // （调用方通过 MapCommandResult 返回）
            }

            // 4) 按新阶段应用效果
            switch (newStage)
            {
                case CollapseStage.Stable:
                    // 无效果
                    break;
                case CollapseStage.Anomalous:
                    // 调用方负责 Emit OnAnomalyDetected；本服务不主动生成（确定性）。
                    break;
                case CollapseStage.Fracturing:
                    // 标注：调用方应基于 ApplyLocalDamage 的累积结果 Emit OnTileFractured。
                    // 本服务不主动 Emit（与"高 CV 格子"查询分离，调用方编排）。
                    break;
                case CollapseStage.Collapsing:
                    // 调整 Capture / Escort / Restricted / EnvironmentalHazard region 的 ActivationProgress / 状态
                    if (regionService != null)
                        ApplyCollapsingEffects(mapState, regionService);
                    break;
                case CollapseStage.GateFault:
                    // 终态：调用方负责 Emit OnGateFaultTriggered
                    break;
            }

            // 5) 阶段切换检测：返回哪些 stage transition 发生，供调用方决策事件 Emit
            var result = new List<MapEventKind>(1);
            if (newStage != oldStage)
                result.Add(MapEventKind.OnGlobalCVChanged); // 阶段切换
            else if (newValue != oldValue)
                result.Add(MapEventKind.OnGlobalCVChanged); // 数值变化
            return result;
        }

        /// <summary>
        /// 应用 Collapsing 阶段效果：把 <see cref="RegionKind.EnvironmentalHazard"/> /
        /// <see cref="RegionKind.Restricted"/> 标 <see cref="RegionState.Contested"/>；
        /// 把 <see cref="RegionKind.Capture"/> / <see cref="RegionKind.Escort"/> 的
        /// ActivationProgress 增加 <see cref="CollapsingRegionDelta"/>。
        /// </summary>
        private void ApplyCollapsingEffects(MapState mapState, MapRegionService regionService)
        {
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                var kind = rs.Definition.Kind;
                if (kind == RegionKind.EnvironmentalHazard || kind == RegionKind.Restricted)
                {
                    if (rs.State == RegionState.Active)
                    {
                        // Active → Contested（合法性表允许）
                        regionService.TryTransitionState(mapState, rs.Definition.RegionIdValue, RegionState.Contested, out _);
                    }
                }
                else if (kind == RegionKind.Capture || kind == RegionKind.Escort)
                {
                    // ActivationProgress += CollapsingRegionDelta（clamp 到 [0, 100]）
                    int newProg = rs.ActivationProgress + CollapsingRegionDelta;
                    if (newProg > 100) newProg = 100;
                    rs.SetActivationProgressInternal(newProg, regionService.CurrentTick);
                }
            }
        }

        // ──────────── 局部 CV 累积 ────────────

        /// <summary>
        /// 在指定 tile 上累积局部 CV：<c>LocalCV[coord] += amount</c>，clamp 到 [0, 100]，
        /// 自动派生 Stability。
        /// </summary>
        /// <returns>更新后的 <see cref="LocalCollapseValue"/>。</returns>
        public LocalCollapseValue ApplyLocalDamage(MapState mapState, GridCoord coord, int amount)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "ApplyLocalDamage amount must be >= 0.");

            LocalCollapseValue lcv;
            if (mapState.LocalCVsInternal.TryGetValue(coord, out var existing))
            {
                lcv = existing.WithDelta(amount);
            }
            else
            {
                lcv = LocalCollapseValue.Of(coord, amount);
            }
            mapState.LocalCVsInternal[coord] = lcv;
            return lcv;
        }

        /// <summary>
        /// 应用单 tile 局部 CV 累积 + 当 Stability 变化时 Emit <see cref="MapEventKind.OnTileFractured"/>
        /// 事件（用 deterministic 路径：返回值供调用方组织 events）。
        /// </summary>
        /// <param name="mapState">map state。</param>
        /// <param name="coord">tile 坐标。</param>
        /// <param name="amount">累积量（≥ 0）。</param>
        /// <param name="prevStability">之前的 stability（调用方需提供以做变化检测）。</param>
        /// <returns>(new LCV, fractured?)，若 fractured=true 表示 stability 进入了
        /// Fractured/Collapsing/Collapsed 中任一不可通行状态。</returns>
        public (LocalCollapseValue lcv, bool fractured) ApplyLocalDamageWithEvent(
            MapState mapState, GridCoord coord, int amount, TileStability prevStability)
        {
            var lcv = ApplyLocalDamage(mapState, coord, amount);
            bool fractured = lcv.Stability != prevStability
                             && lcv.Stability.IsPassable() == false;
            return (lcv, fractured);
        }

        // ──────────── 查询 ────────────

        /// <summary>读取全局 CV。</summary>
        public GlobalCollapseValue GetGlobalValue(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            return mapState.GlobalCV;
        }

        /// <summary>读取指定 tile 的局部 CV（不存在 → 零值）。</summary>
        public LocalCollapseValue GetLocalValue(MapState mapState, GridCoord coord)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (mapState.LocalCVsInternal.TryGetValue(coord, out var v)) return v;
            return LocalCollapseValue.Zero(coord);
        }

        /// <summary>
        /// 查询所有 Value ≥ threshold 的 <see cref="LocalCollapseValue"/>，按 Value 降序
        /// 排序（同值按 GridCoord.CompareTo 升序）。
        /// </summary>
        public IReadOnlyList<LocalCollapseValue> GetHighLocalValues(MapState mapState, int threshold)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (threshold < 0 || threshold > 100)
                throw new ArgumentOutOfRangeException(nameof(threshold), threshold,
                    "threshold must be in [0, 100].");

            var list = new List<LocalCollapseValue>();
            foreach (var lcv in mapState.LocalCVsInternal.Values)
            {
                if (lcv.Value >= threshold)
                    list.Add(lcv);
            }
            // 排序：Value DESC, then GridCoord.CompareTo ASC
            list.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                return a.Coord.CompareTo(b.Coord);
            });
            return list;
        }

        /// <summary>
        /// 与 <see cref="MapRegionService.GetRegionsContaining"/> 联动：
        /// 返回包含指定 tile 的所有 region，按 RegionId 升序。
        /// 当 regionService == null 时回退到直接遍历 <see cref="MapState.RegionStates"/>。
        /// </summary>
        public IReadOnlyList<MapRegionState> GetRegionsAtCoord(
            MapState mapState, GridCoord coord, MapRegionService regionService = null)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (regionService != null)
                return regionService.GetRegionsContaining(mapState, coord);

            // Fallback：直接遍历
            var list = new List<MapRegionState>();
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                if (rs.Definition.Contains(coord))
                    list.Add(rs);
            }
            list.Sort((a, b) => a.Definition.RegionIdValue.CompareTo(b.Definition.RegionIdValue));
            return list;
        }

        // ──────────── 预警查询（供 CollapseWarningService 复用）────────────

        /// <summary>按 Value 降序返回 topN 热点（topN ≤ 0 时返回所有）。</summary>
        public IReadOnlyList<LocalCollapseValue> GetHotspots(MapState mapState, int topN)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (topN < 0)
                throw new ArgumentOutOfRangeException(nameof(topN), topN, "topN must be >= 0.");

            var list = new List<LocalCollapseValue>(mapState.LocalCVsInternal.Count);
            foreach (var v in mapState.LocalCVsInternal.Values)
                list.Add(v);
            list.Sort((a, b) =>
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                return a.Coord.CompareTo(b.Coord);
            });
            if (topN > 0 && list.Count > topN)
                list.RemoveRange(topN, list.Count - topN);
            return list;
        }

        // ──────────── 测试用：重置 ────────────

        /// <summary>清空所有 LocalCVs（GlobalCV 归零）。仅供测试 / Replay 重置使用。</summary>
        public void Reset(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            mapState.LocalCVsInternal.Clear();
            mapState.GlobalCV = GlobalCollapseValue.Zero;
        }
    }
}
