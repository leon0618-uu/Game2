using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b 环境时间表事件类型（ADR-0008）。
    ///
    /// <para/>
    /// **职责**：作为 <see cref="MapEnvironmentSchedule"/> 的最小事件单元，
    /// 描述"哪一 tick / 哪种阶段 / 哪些坐标 / 多少量值 / 哪些标签"的环境影响。
    /// <see cref="EnvironmentPhaseResolver"/> 按 phase 0..9 顺序消费事件。
    ///
    /// <para/>
    /// **稳定性约束**（AGENTS.md §11）：
    /// <list type="bullet">
    /// <item>所有事件由 factory（<see cref="LocalDamage"/> / <see cref="GlobalCVShift"/> 等）构造；
    ///       构造后 <see cref="AffectedCoords"/> 已按 <see cref="GridCoord.CompareTo"/> 升序排序。</item>
    /// <item><see cref="Tags"/> 默认空 list；调用方可追加但不修改已存 events。</item>
    /// <item><see cref="Magnitude"/> 仅对"数值化事件"（<see cref="EnvironmentEventKind.LocalDamageAmount"/> /
    ///       <see cref="EnvironmentEventKind.GlobalCVDelta"/> 等）有语义；
    ///       对"状态型事件"（<see cref="EnvironmentEventKind.TileStabilityChange"/> /
    ///       <see cref="EnvironmentEventKind.RegionActivation"/> 等）作为"新值"或"强度"。</item>
    /// </list>
    /// </summary>
    public sealed class MapEnvironmentEvent : IEquatable<MapEnvironmentEvent>
    {
        // ──────────── 字段 ────────────

        /// <summary>事件类型（10 种；不可跳号、不可重排，AGENTS.md §11）。</summary>
        public EnvironmentEventKind Kind { get; }

        /// <summary>触发 tick（按 <see cref="MapState.EnvironmentTickAccumulator"/> 计算）。</summary>
        public int TriggerTick { get; }

        /// <summary>影响的 tile 坐标列表（按 <see cref="GridCoord.CompareTo"/> 升序，已冻结）。</summary>
        public IReadOnlyList<GridCoord> AffectedCoords { get; }

        /// <summary>
        /// 数值化负载：
        /// <list type="bullet">
        /// <item><see cref="EnvironmentEventKind.LocalDamageAmount"/>：damage 增量。</item>
        /// <item><see cref="EnvironmentEventKind.GlobalCVDelta"/>：delta（可正可负）。</item>
        /// <item><see cref="EnvironmentEventKind.TileStabilityChange"/>：新 stability byte 值。</item>
        /// <item><see cref="EnvironmentEventKind.WarningEmitted"/>：warning level byte。</item>
        /// </list>
        /// </summary>
        public int Magnitude { get; }

        /// <summary>外部标签列表（region id / spawn id / 阶段 byte 等；按字典序排序）。</summary>
        public IReadOnlyList<string> Tags { get; }

        // ──────────── 构造 ────────────

        public MapEnvironmentEvent(
            EnvironmentEventKind kind,
            int triggerTick,
            IReadOnlyList<GridCoord> affectedCoords = null,
            int magnitude = 0,
            IReadOnlyList<string> tags = null)
        {
            if (triggerTick < 0)
                throw new ArgumentOutOfRangeException(nameof(triggerTick), triggerTick,
                    "TriggerTick must be >= 0 (no future-tick replay acceptable).");
            Kind = kind;
            TriggerTick = triggerTick;
            // 排序复制：保证哈希 / Setter 都看到稳定顺序。
            if (affectedCoords != null && affectedCoords.Count > 0)
            {
                var sorted = new List<GridCoord>(affectedCoords);
                sorted.Sort();
                AffectedCoords = sorted;
            }
            else
            {
                AffectedCoords = Array.Empty<GridCoord>();
            }
            Magnitude = magnitude;
            if (tags != null && tags.Count > 0)
            {
                var sortedTags = new List<string>(tags);
                sortedTags.Sort(StringComparer.Ordinal);
                Tags = sortedTags;
            }
            else
            {
                Tags = Array.Empty<string>();
            }
        }

        // ──────────── 工厂（10 种）────────────

        /// <summary>Phase 1 持续效果 / Phase 2 局部 CV：在指定 tile 累积 damage。</summary>
        public static MapEnvironmentEvent LocalDamage(GridCoord coord, int amount, int triggerTick = 0)
        {
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "LocalDamage amount must be >= 0.");
            return new MapEnvironmentEvent(
                EnvironmentEventKind.LocalDamageAmount,
                triggerTick,
                new[] { coord },
                magnitude: amount);
        }

        /// <summary>Phase 3 全局 CV：整体 CV 偏移（可正可负）。</summary>
        public static MapEnvironmentEvent GlobalCVShift(int delta, int triggerTick = 0, string tag = null)
        {
            return new MapEnvironmentEvent(
                EnvironmentEventKind.GlobalCVDelta,
                triggerTick,
                affectedCoords: null,
                magnitude: delta,
                tags: tag == null ? null : new[] { tag });
        }

        /// <summary>Phase 4 地块状态：把指定 tile 的 stability 改为新值（按 byte 写入）。</summary>
        public static MapEnvironmentEvent TileStabilityChange(GridCoord coord, int newStabilityByte, int triggerTick = 0)
        {
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.TileStabilityChange,
                triggerTick,
                new[] { coord },
                magnitude: newStabilityByte);
        }

        /// <summary>Phase 5 坠落：触发指定 tile 的 FallingCommand。</summary>
        public static MapEnvironmentEvent FallTrigger(GridCoord coord, int triggerTick = 0)
        {
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.FallTrigger,
                triggerTick,
                new[] { coord },
                magnitude: 0);
        }

        /// <summary>Phase 6 区域激活：指定 regionId 进入新 state。</summary>
        public static MapEnvironmentEvent RegionActivation(string regionId, int triggerTick = 0)
        {
            if (string.IsNullOrEmpty(regionId))
                throw new ArgumentException("RegionId must be non-empty", nameof(regionId));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.RegionActivation,
                triggerTick,
                affectedCoords: null,
                magnitude: 0,
                tags: new[] { regionId });
        }

        /// <summary>Phase 7 增援点：在指定 spawn 位置放置（MAP-10 stub）。</summary>
        public static MapEnvironmentEvent ReinforcementSpawn(string spawnId, GridCoord coord, int triggerTick = 0)
        {
            if (string.IsNullOrEmpty(spawnId))
                throw new ArgumentException("SpawnId must be non-empty", nameof(spawnId));
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.ReinforcementSpawn,
                triggerTick,
                new[] { coord },
                magnitude: 0,
                tags: new[] { spawnId });
        }

        /// <summary>Phase 8 地图事件：记录任意标记（含 region / cv 变化注释）。</summary>
        public static MapEnvironmentEvent MapEvent(string description, int triggerTick = 0)
        {
            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("Description must be non-empty", nameof(description));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.MapEventRecord,
                triggerTick,
                affectedCoords: null,
                magnitude: 0,
                tags: new[] { description });
        }

        /// <summary>Phase 9 预警：在指定坐标 Emit Warning 事件（level = <see cref="Magnitude"/>）。</summary>
        public static MapEnvironmentEvent WarningEmitted(int levelByte, IReadOnlyList<GridCoord> coords, int triggerTick = 0)
        {
            if (coords == null)
                throw new ArgumentNullException(nameof(coords));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.WarningEmitted,
                triggerTick,
                coords,
                magnitude: levelByte);
        }

        /// <summary>Phase 0 延迟机关：定时延迟触发的事件（延迟 tick 数 = <see cref="Magnitude"/>）。</summary>
        public static MapEnvironmentEvent DeferredTrigger(GridCoord coord, int delayTicks, int triggerTick = 0)
        {
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            if (delayTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks,
                    "DelayTicks must be >= 0.");
            return new MapEnvironmentEvent(
                EnvironmentEventKind.DeferredTrigger,
                triggerTick,
                new[] { coord },
                magnitude: delayTicks);
        }

        /// <summary>DeferredTrigger 重载：仅指定 delayTicks（默认 triggerTick=0）。</summary>
        public static MapEnvironmentEvent DeferredTrigger(GridCoord coord, int delayTicks)
            => DeferredTrigger(coord, delayTicks, triggerTick: 0);

        /// <summary>Phase 4 / 6 复合：地块重建（与 ReconstructTileCommand 对应）。</summary>
        public static MapEnvironmentEvent TileReconstruct(GridCoord coord, int triggerTick = 0)
        {
            if (coord == null)
                throw new ArgumentNullException(nameof(coord));
            return new MapEnvironmentEvent(
                EnvironmentEventKind.TileReconstruct,
                triggerTick,
                new[] { coord },
                magnitude: 0);
        }

        // ──────────── 等值 / 排序 / 哈希 ────────────

        public bool Equals(MapEnvironmentEvent other)
        {
            if (other == null) return false;
            if (Kind != other.Kind) return false;
            if (TriggerTick != other.TriggerTick) return false;
            if (Magnitude != other.Magnitude) return false;
            if (!SequenceEqual(AffectedCoords, other.AffectedCoords)) return false;
            if (!SequenceEqual(Tags, other.Tags)) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is MapEnvironmentEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind;
                h = (h * 397) ^ TriggerTick;
                h = (h * 397) ^ Magnitude;
                if (AffectedCoords != null)
                    for (int i = 0; i < AffectedCoords.Count; i++)
                        h = (h * 397) ^ AffectedCoords[i].GetHashCode();
                if (Tags != null)
                    for (int i = 0; i < Tags.Count; i++)
                        h = (h * 397) ^ (Tags[i]?.GetHashCode() ?? 0);
                return h;
            }
        }

        private static bool SequenceEqual(IReadOnlyList<GridCoord> a, IReadOnlyList<GridCoord> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!a[i].Equals(b[i])) return false;
            return true;
        }

        private static bool SequenceEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) return false;
            return true;
        }

        public override string ToString()
        {
            return string.Format(
                "MapEnvironmentEvent(Kind={0}, Tick={1}, Magnitude={2}, Coords={3}, Tags={4})",
                Kind, TriggerTick, Magnitude, AffectedCoords.Count, Tags.Count);
        }
    }

    /// <summary>
    /// doc2 MAP-11b 环境事件 10 种（ADR-0008 §D2）。
    ///
    /// <para/>
    /// 位序固定（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议都依赖此位序。
    ///
    /// <para/>
    /// **阶段分配**（按 <see cref="EnvironmentPhaseIndex"/> 0..9）：
    /// <list type="number">
    /// <item><see cref="DeferredTrigger"/>     — phase 0 延迟机关</item>
    /// <item><see cref="LocalDamageAmount"/>   — phase 1 持续效果 / phase 2 局部 CV</item>
    /// <item><see cref="GlobalCVDelta"/>       — phase 3 全局 CV</item>
    /// <item><see cref="TileStabilityChange"/> — phase 4 地块状态</item>
    /// <item><see cref="FallTrigger"/>         — phase 5 坠落</item>
    /// <item><see cref="RegionActivation"/>    — phase 6 区域激活</item>
    /// <item><see cref="ReinforcementSpawn"/>  — phase 7 增援点</item>
    /// <item><see cref="MapEventRecord"/>      — phase 8 地图事件</item>
    /// <item><see cref="WarningEmitted"/>      — phase 9 预警</item>
    /// </list>
    /// <para/>
    /// 复合：<see cref="TileReconstruct"/>（phase 4 末）— 实际触发由 Resolver 调度对应命令。
    /// </summary>
    public enum EnvironmentEventKind : byte
    {
        /// <summary>无事件（默认值）。</summary>
        None = 0,

        /// <summary>延迟机关（phase 0）：定时触发，到期后才执行真正事件。</summary>
        DeferredTrigger = 1,

        /// <summary>局部 CV 累积（phase 1/2）：调用 <see cref="Starfall.Core.Map.Collapse.CollapseValueService.ApplyLocalDamage"/>。</summary>
        LocalDamageAmount = 2,

        /// <summary>全局 CV 偏移（phase 3）：调用 <see cref="Starfall.Core.Map.Collapse.ModifyGlobalCollapseValueCommand"/>。</summary>
        GlobalCVDelta = 3,

        /// <summary>地块状态变化（phase 4）：新 stability byte（与 <see cref="Starfall.Core.Map.Collapse.TileStability"/> 对齐）。</summary>
        TileStabilityChange = 4,

        /// <summary>坠落触发（phase 5）：调 <c>FallingCommand</c> + Emit <see cref="MapEventKind.OnTileFractured"/>。</summary>
        FallTrigger = 5,

        /// <summary>区域激活（phase 6）：调 <see cref="Starfall.Core.Map.Commands.TransitionRegionStateCommand"/>。</summary>
        RegionActivation = 6,

        /// <summary>增援点（phase 7）：调 <see cref="Starfall.Core.Map.Commands.PlaceMapObjectCommand"/> stub（MAP-10 完整版后续）。</summary>
        ReinforcementSpawn = 7,

        /// <summary>地图事件记录（phase 8）：Emit 复合 <see cref="MapEvent"/>（含 region cv 等）。</summary>
        MapEventRecord = 8,

        /// <summary>预警发射（phase 9）：调 <see cref="Starfall.Core.Map.Collapse.CollapseWarningService"/> + Emit <see cref="MapEventKind.OnAnomalyDetected"/>。</summary>
        WarningEmitted = 9,

        /// <summary>地块重建（phase 4 副相位）：调 <see cref="Starfall.Core.Map.Collapse.ReconstructTileCommand"/>。</summary>
        TileReconstruct = 10,
    }

    /// <summary>
    /// doc2 MAP-11b 10 步固定顺序枚举（ADR-0008 §D1）。
    ///
    /// <para/>
    /// 位序固定（AGENTS.md §11）：禁止重排或跳号。
    /// <see cref="EnvironmentPhaseResolver"/> 严格按 0..9 顺序执行。
    /// </summary>
    public enum EnvironmentPhaseIndex : byte
    {
        /// <summary>延迟机关（DeferredTriggers）：发射到期延迟事件。</summary>
        DeferredTriggers = 0,

        /// <summary>持续效果（ContinuousEffects）：每回合 tick 的持续伤害 / 治疗 / buff。</summary>
        ContinuousEffects = 1,

        /// <summary>局部 CV（LocalCollapseValue）：应用 LocalDamage 到指定格子。</summary>
        LocalCollapseValue = 2,

        /// <summary>全局 CV（GlobalCollapseValue）：应用 GlobalCVDelta。</summary>
        GlobalCollapseValue = 3,

        /// <summary>地块状态（TileStability）：调用 CollapseTileCommand / ReconstructTileCommand。</summary>
        TileStability = 4,

        /// <summary>坠落（Falling）：触发 FallingCommand + Emit OnTileFractured。</summary>
        Falling = 5,

        /// <summary>区域激活（RegionActivation）：调用 TransitionRegionStateCommand。</summary>
        RegionActivation = 6,

        /// <summary>增援点（ReinforcementSpawn）：调用 PlaceMapObjectCommand stub。</summary>
        ReinforcementSpawn = 7,

        /// <summary>地图事件（MapEvent）：Emit MapEvent 记录。</summary>
        MapEvent = 8,

        /// <summary>预警（WarningEmitted）：调用 CollapseWarningService 评估并 Emit OnAnomalyDetected。</summary>
        WarningEmitted = 9,
    }
}
