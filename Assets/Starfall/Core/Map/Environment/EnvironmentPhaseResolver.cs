using System;
using System.Collections.Generic;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b 环境时间表解析器（ADR-0008）。
    ///
    /// <para/>
    /// **核心职责**：执行 <see cref="MapEnvironmentSchedule"/> 的 10 步固定顺序
    /// （<see cref="EnvironmentPhaseIndex"/> 0..9），把 schedule 中的每条
    /// <see cref="MapEnvironmentEvent"/> 分派到对应 phase 的具体副作用。
    ///
    /// <para/>
    /// **10 步语义**（不可换序；执行时严格按 0→9 顺序）：
    /// <list type="number">
    /// <item><b>Phase 0 DeferredTriggers</b>：发射到期延迟事件（<see cref="EnvironmentEventKind.DeferredTrigger"/>）。
    ///       延迟到期的 event 被发到"高优先级"队列，等待下一轮 Resolver 处理。
    ///       本阶段本身不修改 map state。</item>
    /// <item><b>Phase 1 ContinuousEffects</b>：对 <see cref="EnvironmentEventKind.LocalDamageAmount"/>
    ///       在每回合自然累积的"持续伤害"调用 <see cref="CollapseValueService.Tick"/>，
    ///       推进 <see cref="MapState.GlobalCV"/> + emit <see cref="MapEventKind.OnGlobalCVChanged"/>。</item>
    /// <item><b>Phase 2 LocalCollapseValue</b>：对每个
    ///       <see cref="EnvironmentEventKind.LocalDamageAmount"/> 事件，在每个 AffectedCoord
    ///       上调用 <see cref="CollapseValueService.ApplyLocalDamage"/>；
    ///       如果 stability 跨越临界（<see cref="TileStability.Fractured"/> /
    ///       <see cref="TileStability.Collapsing"/> / <see cref="TileStability.Collapsed"/>），
    ///       emit <see cref="MapEventKind.OnTileFractured"/> 事件。</item>
    /// <item><b>Phase 3 GlobalCollapseValue</b>：对每个
    ///       <see cref="EnvironmentEventKind.GlobalCVDelta"/> 事件，运行
    ///       <see cref="ModifyGlobalCollapseValueCommand"/>（delta = <see cref="MapEnvironmentEvent.Magnitude"/>）。</item>
    /// <item><b>Phase 4 TileStability</b>：处理
    ///       <see cref="EnvironmentEventKind.TileStabilityChange"/>（执行
    ///       <see cref="CollapseTileCommand"/> 拆分逻辑）和
    ///       <see cref="EnvironmentEventKind.TileReconstruct"/>（执行
    ///       <see cref="ReconstructTileCommand"/> 拆分逻辑）。</item>
    /// <item><b>Phase 5 Falling</b>：处理 <see cref="EnvironmentEventKind.FallTrigger"/>。
    ///       当前 MVP 通过 tile stability 降级（<see cref="CollapseTileCommand"/> Collapsing）
    ///       + emit <see cref="MapEventKind.OnTileFractured"/> 实现；MAP-02 完整
    ///       FallingCommand 接入留待未来。</item>
    /// <item><b>Phase 6 RegionActivation</b>：处理
    ///       <see cref="EnvironmentEventKind.RegionActivation"/>。第一个 tag = regionId，
    ///       通过 <see cref="TransitionRegionStateCommand"/> 将 region 转入
    ///       <see cref="RegionState.Active"/>。</item>
    /// <item><b>Phase 7 ReinforcementSpawn</b>：处理
    ///       <see cref="EnvironmentEventKind.ReinforcementSpawn"/>。通过
    ///       <see cref="PlaceMapObjectCommand"/> stub（MAP-10 完整版后续）。</item>
    /// <item><b>Phase 8 MapEvent</b>：处理 <see cref="EnvironmentEventKind.MapEventRecord"/>。
    ///       通过 description 文本构造 <see cref="MapEventKind.OnRegionChanged"/>
    ///       或 <see cref="MapEventKind.OnGlobalCVChanged"/> 事件（按 tag 路由）。</item>
    /// <item><b>Phase 9 WarningEmitted</b>：处理 <see cref="EnvironmentEventKind.WarningEmitted"/>。
    ///       通过 <see cref="CollapseWarningService"/> 评估当前 GlobalCV 是否越阈值，
    ///       Emit <see cref="MapEventKind.OnAnomalyDetected"/> 事件。</item>
    /// </list>
    ///
    /// <para/>
    /// **集成**：本 Resolver 不构造新 <see cref="IMapCommand"/>；它将"已计算好的事件副作用"
    /// 直接修改 <see cref="MapState"/>（如 <see cref="MapState.GlobalCV"/> /
    /// <see cref="MapState.LocalCVs"/>），并返回事件列表让命令 / 服务编排。
    /// 真正的版本号自增由 <see cref="ScheduleEnvironmentCommand"/> 负责。
    ///
    /// <para/>
    /// **顺序保证**（ADR-0008 §D1）：<see cref="ExecuteAll"/> 强制按 phase 0→9 顺序执行；
    /// 即使 phase 5 失败（无 tile 可改），phase 6 仍继续。
    /// </summary>
    public sealed class EnvironmentPhaseResolver
    {
        // ──────────── 依赖（MAP-11a/09/04/02 联动）────────────

        /// <summary>注入的 <see cref="CollapseValueService"/>（处理 phase 1/2/3）。</summary>
        public CollapseValueService CollapseValueService { get; set; }

        /// <summary>注入的 <see cref="CollapseWarningService"/>（处理 phase 9）。</summary>
        public CollapseWarningService WarningService { get; set; }

        /// <summary>注入的 <see cref="MapRegionService"/>（可选，处理 phase 6）。</summary>
        public MapRegionService RegionService { get; set; }

        /// <summary>注入的 command 执行器（用于 phase 3/4/5/6/7 嵌套命令）。</summary>
        public MapCommandExecutor CommandExecutor { get; set; }

        // ──────────── 状态（仅统计 / 测试断言）────────────

        /// <summary>累计 phase 执行次数（<see cref="ExecutePhase"/> 每调一次 +1）。</summary>
        public int TotalPhaseExecutions { get; private set; }

        /// <summary>累计 schedule 执行次数（<see cref="ExecuteAll"/> 每调一次 +1）。</summary>
        public int TotalScheduleExecutions { get; private set; }

        // ──────────── 构造 ────────────

        public EnvironmentPhaseResolver()
        {
            CollapseValueService = new CollapseValueService();
            WarningService = new CollapseWarningService();
            // RegionService + CommandExecutor 由调用方注入（避免循环依赖）。
        }

        // ──────────── 公开入口 ────────────

        /// <summary>执行单步 phase（<paramref name="phaseIndex"/> 必须在 [0, 9] 内）。</summary>
        /// <returns>该 phase 内产生的所有 <see cref="MapEvent"/>。</returns>
        public IReadOnlyList<MapEvent> ExecutePhase(
            MapState mapState,
            int phaseIndex,
            MapEnvironmentSchedule schedule)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (phaseIndex < 0 || phaseIndex > 9)
                throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex,
                    "PhaseIndex must be in [0, 9] (ADR-0008 D1).");

            TotalPhaseExecutions++;
            var events = new List<MapEvent>();
            var phaseEvents = schedule.GetEventsForPhase((EnvironmentPhaseIndex)(byte)phaseIndex);

            switch ((EnvironmentPhaseIndex)(byte)phaseIndex)
            {
                case EnvironmentPhaseIndex.DeferredTriggers:
                    ExecutePhase0_DeferredTriggers(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.ContinuousEffects:
                    ExecutePhase1_ContinuousEffects(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.LocalCollapseValue:
                    ExecutePhase2_LocalCollapseValue(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.GlobalCollapseValue:
                    ExecutePhase3_GlobalCollapseValue(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.TileStability:
                    ExecutePhase4_TileStability(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.Falling:
                    ExecutePhase5_Falling(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.RegionActivation:
                    ExecutePhase6_RegionActivation(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.ReinforcementSpawn:
                    ExecutePhase7_ReinforcementSpawn(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.MapEvent:
                    ExecutePhase8_MapEvent(mapState, phaseEvents, events);
                    break;
                case EnvironmentPhaseIndex.WarningEmitted:
                    ExecutePhase9_WarningEmitted(mapState, phaseEvents, events);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex,
                        $"Unknown phase index: {phaseIndex}");
            }
            events.Sort();
            return events;
        }

        /// <summary>顺序执行 phase 0..9；返回合并后的事件列表（按 phase 顺序追加，内层 Sort）。</summary>
        public IReadOnlyList<MapEvent> ExecuteAll(
            MapState mapState,
            MapEnvironmentSchedule schedule)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            TotalScheduleExecutions++;
            var all = new List<MapEvent>();
            for (int i = 0; i < 10; i++)
            {
                var events = ExecutePhase(mapState, i, schedule);
                all.AddRange(events);
            }
            all.Sort();
            return all;
        }

        /// <summary>
        /// 校验 schedule 顺序：每个 event 的 phase index ∈ [0, 9]，
        /// 且 schedule.Events 按 phase 顺序 0..9 单调排列（稳定顺序）。
        /// <para/>
        /// 返回错误码：<c>0</c> = OK（无错）；非 0 = 第一个错误位置 / 错误类型编码。
        /// </summary>
        public int ValidateSchedule(MapEnvironmentSchedule schedule)
        {
            if (!schedule.IsEmpty)
            {
                int prevPhase = -1;
                for (int i = 0; i < schedule.Count; i++)
                {
                    var ev = schedule.Events[i];
                    int p = MapEnvironmentEventToPhaseMap.GetPhaseIndex(ev.Kind);
                    if (p < 0 || p > 9)
                        return 1; // invalid phase index
                    if (p < prevPhase)
                        return 2; // out-of-order
                    prevPhase = p;
                }
            }
            return 0;
        }

        // ──────────── Phase 实现（10 步）────────────

        /// <summary>Phase 0：延迟机关。本阶段发射"待触发列表"中到期的延迟事件。</summary>
        private void ExecutePhase0_DeferredTriggers(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            // 当前 MVP：直接将 DeferredTrigger event 转写为 MapEvent（描述性事件）
            // 完整实现：维护一个 pending list（mapState.PendingEvents），到期时把 event 移出。
            // 本阶段不动 map state（Phase 0 在 MVP 中仅记录）。
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                // 简化：直接把 Delayed event Append 到 PendingEvents 列表（保持稳定）
                mapState.AddPendingEvent(ev);
            }
        }

        /// <summary>Phase 1：持续效果（每回合 tick）。调 CollapseValueService.Tick 推进 GlobalCV。</summary>
        private void ExecutePhase1_ContinuousEffects(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            // 如果没有持续事件则什么也不做（每回合 tick 是 Resolver 调用方负责的）；
            // 此处仅处理 LocalDamageAmount 在 phase 1 的自然累积（按 schedule 表达）。
            // 调用方可通过 IMapCommand.Run 显式 CollapseValueService.Tick。
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.LocalDamageAmount) continue;
                // 在每个 AffectedCoord 上应用 damage（与 phase 2 同语义；保持一致性）
                for (int c = 0; c < ev.AffectedCoords.Count; c++)
                {
                    var coord = ev.AffectedCoords[c];
                    var prevStab = CollapseValueService.GetLocalValue(mapState, coord).Stability;
                    var (lcv, _) = CollapseValueService.ApplyLocalDamageWithEvent(
                        mapState, coord, ev.Magnitude, prevStab);
                    if (lcv.Stability != prevStab && !lcv.Stability.IsPassable())
                    {
                        output.Add(MapEvent.TileFractured(coord, (int)lcv.Stability, "env-phase1"));
                    }
                }
            }
            // 同时 tick GlobalCV（持续效果 = +1 默认 delta）
            var kinds = CollapseValueService.Tick(mapState, RegionService);
            // kind 结果如果非空，表示 GlobalCV 变化（emit OnGlobalCVChanged 事件）
            if (kinds != null && kinds.Count > 0)
            {
                // 仅在阶段切换或数值实际变化时再 emit（避免 delta=0 抖动）
                // 在 Resolve 时机我们直接 emit（force）。
                output.Add(MapEvent.GlobalCVChanged(
                    oldValue: -1,
                    newValue: mapState.GlobalCV.Value,
                    description: "env-phase1-tick"));
            }
        }

        /// <summary>Phase 2：局部 CV 应用（每个 event 在 AffectedCoord 上累积）。</summary>
        private void ExecutePhase2_LocalCollapseValue(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.LocalDamageAmount) continue;
                for (int c = 0; c < ev.AffectedCoords.Count; c++)
                {
                    var coord = ev.AffectedCoords[c];
                    var prevStab = CollapseValueService.GetLocalValue(mapState, coord).Stability;
                    var lcv = CollapseValueService.ApplyLocalDamage(mapState, coord, ev.Magnitude);
                    if (lcv.Stability != prevStab && !lcv.Stability.IsPassable())
                    {
                        output.Add(MapEvent.TileFractured(coord, (int)lcv.Stability, "env-phase2"));
                    }
                }
            }
        }

        /// <summary>Phase 3：全局 CV 应用（每个 GlobalCVDelta event 修改 GlobalCV）。</summary>
        private void ExecutePhase3_GlobalCollapseValue(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            int accumulatedDelta = 0;
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.GlobalCVDelta) continue;
                accumulatedDelta += ev.Magnitude;
            }
            if (accumulatedDelta != 0)
            {
                int oldValue = mapState.GlobalCV.Value;
                mapState.GlobalCV = mapState.GlobalCV.WithValue(oldValue + accumulatedDelta);
                int newValue = mapState.GlobalCV.Value;
                if (newValue != oldValue)
                    output.Add(MapEvent.GlobalCVChanged(oldValue, newValue, "env-phase3"));
            }
        }

        /// <summary>Phase 4：地块状态变更（TileStabilityChange / TileReconstruct）。</summary>
        private void ExecutePhase4_TileStability(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind == EnvironmentEventKind.TileStabilityChange)
                {
                    for (int c = 0; c < ev.AffectedCoords.Count; c++)
                    {
                        var coord = ev.AffectedCoords[c];
                        int newStabilityByte = ev.Magnitude;
                        // 校验坐标在 map 内
                        bool inMap = false;
                        for (int t = 0; t < mapState.TilesInternal.Count; t++)
                        {
                            if (mapState.TilesInternal[t].Equals(coord)) { inMap = true; break; }
                        }
                        if (!inMap) continue;
                        // 应用新 stability（直接修改 LocalCVs 字典，避免循环跑命令）
                        var existingLcv = mapState.TryGetLocalCV(coord)
                            ?? LocalCollapseValue.Zero(coord);
                        int newValue = LocalCollapseValue.DeriveStability(newStabilityByte) switch
                        {
                            TileStability.Stable => 0,
                            TileStability.Unstable => 30,
                            TileStability.Fractured => 60,
                            TileStability.Collapsing => 80,
                            TileStability.Collapsed => 100,
                            TileStability.Reconstructed => 0,
                            _ => existingLcv.Value,
                        };
                        var newLcv = new LocalCollapseValue(coord, newValue, existingLcv.TickAccumulated);
                        mapState.AddLocalCV(newLcv);
                        // emit OnTileStabilityChanged + OnTileFractured（如果进入 fragmented）
                        output.Add(MapEvent.TileStabilityChanged(coord, (int)existingLcv.Stability, newStabilityByte, "env-phase4"));
                        if (newStabilityByte >= (int)TileStability.Fractured)
                        {
                            output.Add(MapEvent.TileFractured(coord, newStabilityByte, "env-phase4"));
                        }
                    }
                }
                else if (ev.Kind == EnvironmentEventKind.TileReconstruct)
                {
                    for (int c = 0; c < ev.AffectedCoords.Count; c++)
                    {
                        var coord = ev.AffectedCoords[c];
                        // 校验坐标在 map 内
                        bool inMap = false;
                        for (int t = 0; t < mapState.TilesInternal.Count; t++)
                        {
                            if (mapState.TilesInternal[t].Equals(coord)) { inMap = true; break; }
                        }
                        if (!inMap) continue;
                        // 重置为 Reconstructed（Value=0 but Stability=Reconstructed semantic 由事件承载）
                        mapState.AddLocalCV(LocalCollapseValue.Zero(coord));
                        output.Add(MapEvent.TileReconstructed(coord, (int)TileStability.Reconstructed, "env-phase4"));
                    }
                }
            }
        }

        /// <summary>Phase 5：坠落（在本 MVP 中通过 tile stability 降级实现 + emit OnTileFractured）。</summary>
        private void ExecutePhase5_Falling(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.FallTrigger) continue;
                for (int c = 0; c < ev.AffectedCoords.Count; c++)
                {
                    var coord = ev.AffectedCoords[c];
                    bool inMap = false;
                    for (int t = 0; t < mapState.TilesInternal.Count; t++)
                    {
                        if (mapState.TilesInternal[t].Equals(coord)) { inMap = true; break; }
                    }
                    if (!inMap) continue;
                    // 触发 tile 坍塌（Collapsing 80）
                    mapState.AddLocalCV(new LocalCollapseValue(coord, 80, 0));
                    output.Add(MapEvent.TileFractured(coord, (int)TileStability.Collapsing, "env-phase5-fall"));
                }
            }
        }

        /// <summary>Phase 6：区域激活（每个 event 的 tag[0] = regionId）。</summary>
        private void ExecutePhase6_RegionActivation(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            if (RegionService == null) return; // 无 RegionService 则跳过
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.RegionActivation) continue;
                if (ev.Tags.Count == 0) continue;
                if (!int.TryParse(ev.Tags[0], out int regionId) || regionId < 0) continue;
                if (!RegionService.TryTransitionState(
                    mapState, new RegionId(regionId), RegionState.Active, out var oldState))
                    continue;
                output.Add(MapRegionService.MakeStateChangedEvent(
                    regionId, oldState, RegionState.Active, "env-phase6"));
            }
        }

        /// <summary>Phase 7：增援点（stub，调用 PlaceMapObjectCommand 时若无 Executor 则跳过）。</summary>
        private void ExecutePhase7_ReinforcementSpawn(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            // 在 MVP 中不强行执行 PlaceMapObjectCommand；保留接口位置（MAP-10 后续接入）。
            // 行为记录为 MapEventRegion activation 事件（用 OnRegionChanged 作为占位 event kind）。
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.ReinforcementSpawn) continue;
                if (ev.Tags.Count == 0 || ev.AffectedCoords.Count == 0) continue;
                int spawnId;
                if (!int.TryParse(ev.Tags[0], out spawnId) || spawnId < 0) continue;
                // stub: 仅 emit OnMapObjectPlaced 事件（值标签），不在此阶段实际放置 object
                // 由调用方通过 IMapCommand 显式 Run PlaceMapObjectCommand 时再实际生效。
                output.Add(MapEvent.MapObjectPlaced(spawnId, "env-phase7-stub"));
            }
        }

        /// <summary>Phase 8：地图事件记录（构造通用 MapEvent；按 tag 分发到对应 kind）。</summary>
        private void ExecutePhase8_MapEvent(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.MapEventRecord) continue;
                if (ev.Tags.Count == 0) continue;
                // 简单分发：tag[0] 形如 "region:1" / "cv:delta" / "raw:..."
                string raw = ev.Tags[0];
                if (raw.StartsWith("region:"))
                {
                    string regionPart = raw.Substring("region:".Length);
                    if (int.TryParse(regionPart, out int regionId))
                    {
                        output.Add(MapEvent.RegionChanged(regionId, "env-phase8"));
                    }
                }
                else if (raw.StartsWith("cv:"))
                {
                    string cvPart = raw.Substring("cv:".Length);
                    if (int.TryParse(cvPart, out int cvVal))
                    {
                        output.Add(MapEvent.GlobalCVChanged(mapState.GlobalCV.Value, cvVal, "env-phase8"));
                    }
                }
                else
                {
                    // 通用：emit OnRegionChanged event（携带 description）
                    output.Add(new MapEvent(
                        MapEventKind.OnRegionChanged,
                        description: $"env-phase8:{raw}"));
                }
            }
        }

        /// <summary>Phase 9：预警（按 WarningService 评估当前 GlobalCV，Emit AnomalyDetected）。</summary>
        private void ExecutePhase9_WarningEmitted(
            MapState mapState,
            IReadOnlyList<MapEnvironmentEvent> phaseEvents,
            List<MapEvent> output)
        {
            for (int i = 0; i < phaseEvents.Count; i++)
            {
                var ev = phaseEvents[i];
                if (ev.Kind != EnvironmentEventKind.WarningEmitted) continue;
                int level = ev.Magnitude;
                if (level > 0)
                {
                    // 直接按 level emit warning 事件
                    for (int c = 0; c < ev.AffectedCoords.Count; c++)
                    {
                        output.Add(MapEvent.AnomalyDetected(ev.AffectedCoords[c], $"env-phase9:level{level}"));
                    }
                }
                else
                {
                    // level == 0 → 自动通过 WarningService 评估
                    if (WarningService == null) continue;
                    if (!WarningService.ShouldWarn(mapState, CollapseWarningService.CautionThreshold)) continue;
                    // emit OnAnomalyDetected per top hotspot coord
                    var hotspots = WarningService.GetHotspots(mapState, 1);
                    for (int h = 0; h < hotspots.Count; h++)
                    {
                        output.Add(MapEvent.AnomalyDetected(hotspots[h].Coord, "env-phase9"));
                    }
                }
            }
        }
    }
}
