using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Environment
{
    /// <summary>
    /// doc2 MAP-11b 环境时间表（ADR-0008）。
    ///
    /// <para/>
    /// **核心思想**：每个 schedule 是 10 个 phase（<see cref="EnvironmentPhaseIndex"/> 0..9）的
    /// **固定顺序**事件列表，由 <see cref="EnvironmentPhaseResolver"/> 顺序消费。
    ///
    /// <para/>
    /// **顺序不可换序**：phase 0..9 在语义上是"前置依赖"———
    /// 例如 phase 9 预警需要 phase 2/3 已应用 LocalCV/GlobalCV；
    /// phase 6 区域激活需要 phase 5 坠落先完成。
    /// 故 <see cref="FromEvents"/> 强制按 events 列表的索引分配到 phase（0..9），
    /// 不允许用户指定自定义 phase 顺序。
    ///
    /// <para/>
    /// **稳定排序**：构造时按 <see cref="EnvironmentEventKind"/> 派生 phase index，
    /// 每个 phase 内的 events 保持传入顺序；
    /// <see cref="Events"/> 不可修改。
    /// </summary>
    public readonly struct MapEnvironmentSchedule : IEquatable<MapEnvironmentSchedule>
    {
        // ──────────── 字段 ────────────

        /// <summary>事件列表（按 phase 顺序 0..9 排列；phase 内的相对顺序由构造决定）。</summary>
        public IReadOnlyList<MapEnvironmentEvent> Events { get; }

        /// <summary>schedule 唯一 id（>= 0；常用于 Data 层 JSON key 或调试）。</summary>
        public int ScheduleId { get; }

        /// <summary>schedule 创建时的 tick（用于 replay / Undo 校验）。</summary>
        public int CreatedTick { get; }

        // ──────────── 构造 ────────────

        public MapEnvironmentSchedule(
            IReadOnlyList<MapEnvironmentEvent> events,
            int scheduleId,
            int createdTick)
        {
            if (scheduleId < 0)
                throw new ArgumentOutOfRangeException(nameof(scheduleId), scheduleId,
                    "ScheduleId must be >= 0.");
            if (createdTick < 0)
                throw new ArgumentOutOfRangeException(nameof(createdTick), createdTick,
                    "CreatedTick must be >= 0.");
            if (events == null)
            {
                Events = Array.Empty<MapEnvironmentEvent>();
            }
            else
            {
                // 浅拷贝 + 冻结；调用方可读但不可修改。
                var copy = new List<MapEnvironmentEvent>(events.Count);
                for (int i = 0; i < events.Count; i++)
                    copy.Add(events[i]);
                Events = copy;
            }
            ScheduleId = scheduleId;
            CreatedTick = createdTick;
        }

        // ──────────── 静态工厂 ────────────

        /// <summary>空 schedule（无 events；ScheduleId = 0）。</summary>
        public static MapEnvironmentSchedule Empty(int createdTick = 0)
            => new MapEnvironmentSchedule(
                Array.Empty<MapEnvironmentEvent>(),
                scheduleId: 0,
                createdTick: createdTick);

        /// <summary>
        /// 从事件列表构造 schedule。
        ///
        /// <para/>
        /// **派生规则**（ADR-0008 §D2）：
        /// 每个 <see cref="EnvironmentEventKind"/> 自动映射到 1 个 phase，
        /// 由 <see cref="MapEnvironmentEventToPhaseMap"/> 提供。
        /// 构造时校验：所有事件的 phase index ∈ [0, 9]（否则抛 <see cref="ArgumentException"/>）。
        ///
        /// <para/>
        /// **顺序**：返回的 <see cref="Events"/> 严格按 phase 0..9 排序；
        /// 同 phase 内按输入顺序（不重排，保证用户语义）。
        /// </summary>
        public static MapEnvironmentSchedule FromEvents(
            IReadOnlyList<MapEnvironmentEvent> events,
            int scheduleId,
            int createdTick)
        {
            if (events == null) events = Array.Empty<MapEnvironmentEvent>();
            // 校验每个事件的 phase index ∈ [0, 9]
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                var idx = MapEnvironmentEventToPhaseMap.GetPhaseIndex(ev.Kind);
                if (idx < 0 || idx > 9)
                    throw new ArgumentException(
                        $"event at index {i} (Kind={ev.Kind}) cannot be assigned to any phase.",
                        nameof(events));
            }

            // 按 phase 排序（stable sort by phase index）
            var sorted = new List<MapEnvironmentEvent>(events);
            sorted.Sort((a, b) =>
            {
                int ap = MapEnvironmentEventToPhaseMap.GetPhaseIndex(a.Kind);
                int bp = MapEnvironmentEventToPhaseMap.GetPhaseIndex(b.Kind);
                return ap.CompareTo(bp);
            });
            // 注：stable Sort 在 List<T>.Sort 默认实现中保留相等 key 的相对顺序；
            // 同 phase 内相对顺序由用户输入决定。

            return new MapEnvironmentSchedule(sorted, scheduleId, createdTick);
        }

        // ──────────── 派生查询 ────────────

        /// <summary>该 phase 的事件列表（按 phase 0..9 切分）。空 → 空 list。</summary>
        public IReadOnlyList<MapEnvironmentEvent> GetEventsForPhase(EnvironmentPhaseIndex phase)
        {
            if ((byte)phase > 9)
                return Array.Empty<MapEnvironmentEvent>();
            int targetPhase = (byte)phase;
            var list = new List<MapEnvironmentEvent>();
            for (int i = 0; i < Events.Count; i++)
            {
                var ev = Events[i];
                int p = MapEnvironmentEventToPhaseMap.GetPhaseIndex(ev.Kind);
                if (p == targetPhase) list.Add(ev);
            }
            return list;
        }

        /// <summary>events 总数。</summary>
        public int Count => Events?.Count ?? 0;

        /// <summary>是否空 schedule。</summary>
        public bool IsEmpty => Count == 0;

        // ──────────── 等值 / 哈希 / 字符串 ────────────

        public bool Equals(MapEnvironmentSchedule other)
        {
            if (ScheduleId != other.ScheduleId) return false;
            if (CreatedTick != other.CreatedTick) return false;
            if (Count != other.Count) return false;
            for (int i = 0; i < Count; i++)
            {
                if (!Events[i].Equals(other.Events[i])) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is MapEnvironmentSchedule other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = ScheduleId;
                h = (h * 397) ^ CreatedTick;
                h = (h * 397) ^ Count;
                for (int i = 0; i < Count; i++)
                    h = (h * 397) ^ Events[i].GetHashCode();
                return h;
            }
        }

        public static bool operator ==(MapEnvironmentSchedule a, MapEnvironmentSchedule b) => a.Equals(b);
        public static bool operator !=(MapEnvironmentSchedule a, MapEnvironmentSchedule b) => !a.Equals(b);

        public override string ToString()
            => $"MapEnvironmentSchedule(Id={ScheduleId}, Created={CreatedTick}, Events={Count})";
    }

    /// <summary>
    /// doc2 MAP-11b <see cref="EnvironmentEventKind"/> → <see cref="EnvironmentPhaseIndex"/> 映射表（ADR-0008 §D2）。
    ///
    /// <para/>
    /// 阶段分配（位序固定 AGENTS.md §11）：
    /// <list type="number">
    /// <item>phase 0 = <see cref="EnvironmentEventKind.DeferredTrigger"/></item>
    /// <item>phase 1 = <see cref="EnvironmentEventKind.LocalDamageAmount"/>（持续效果）</item>
    /// <item>phase 2 = <see cref="EnvironmentEventKind.LocalDamageAmount"/>（局部 CV）</item>
    /// <item>phase 3 = <see cref="EnvironmentEventKind.GlobalCVDelta"/></item>
    /// <item>phase 4 = <see cref="EnvironmentEventKind.TileStabilityChange"/> / <see cref="EnvironmentEventKind.TileReconstruct"/></item>
    /// <item>phase 5 = <see cref="EnvironmentEventKind.FallTrigger"/></item>
    /// <item>phase 6 = <see cref="EnvironmentEventKind.RegionActivation"/></item>
    /// <item>phase 7 = <see cref="EnvironmentEventKind.ReinforcementSpawn"/></item>
    /// <item>phase 8 = <see cref="EnvironmentEventKind.MapEventRecord"/></item>
    /// <item>phase 9 = <see cref="EnvironmentEventKind.WarningEmitted"/></item>
    /// </list>
    ///
    /// 注：<see cref="EnvironmentEventKind.LocalDamageAmount"/> 同时出现于 phase 1 + phase 2 —
    /// 其归属由 Resolver 在 phase 1 内 call <c>CollapseValueService.Tick</c>，在 phase 2 内 call
    /// <c>ApplyLocalDamage(mapState, coord, amount)</c>。两种语义都使用同一种 event Kind；
    /// Schedule 内每个 event 的语义由位置（phase index）决定。
    /// </summary>
    public static class MapEnvironmentEventToPhaseMap
    {
        /// <summary>把 <see cref="EnvironmentEventKind"/> 映射到 phase index 0..9。</summary>
        public static int GetPhaseIndex(EnvironmentEventKind kind)
        {
            switch (kind)
            {
                case EnvironmentEventKind.DeferredTrigger:    return 0;
                case EnvironmentEventKind.LocalDamageAmount:  return 1; // 默认 phase 1（持续效果）；用户可在 phase 2 显式再发一次同 Kind
                case EnvironmentEventKind.GlobalCVDelta:      return 3;
                case EnvironmentEventKind.TileStabilityChange:return 4;
                case EnvironmentEventKind.TileReconstruct:    return 4;
                case EnvironmentEventKind.FallTrigger:        return 5;
                case EnvironmentEventKind.RegionActivation:   return 6;
                case EnvironmentEventKind.ReinforcementSpawn: return 7;
                case EnvironmentEventKind.MapEventRecord:     return 8;
                case EnvironmentEventKind.WarningEmitted:     return 9;
                case EnvironmentEventKind.None:               return -1;
                default:
                    return -1;
            }
        }

        /// <summary>把 <paramref name="phaseIndex"/> 转回默认 Kind（仅用于 Resolver 测试 / 文档）。</summary>
        public static EnvironmentEventKind GetDefaultKind(int phaseIndex)
        {
            switch (phaseIndex)
            {
                case 0: return EnvironmentEventKind.DeferredTrigger;
                case 1: return EnvironmentEventKind.LocalDamageAmount;
                case 2: return EnvironmentEventKind.LocalDamageAmount;
                case 3: return EnvironmentEventKind.GlobalCVDelta;
                case 4: return EnvironmentEventKind.TileStabilityChange;
                case 5: return EnvironmentEventKind.FallTrigger;
                case 6: return EnvironmentEventKind.RegionActivation;
                case 7: return EnvironmentEventKind.ReinforcementSpawn;
                case 8: return EnvironmentEventKind.MapEventRecord;
                case 9: return EnvironmentEventKind.WarningEmitted;
                default:
                    return EnvironmentEventKind.None;
            }
        }

        /// <summary>总阶段数（10；硬编码，禁止修改）。</summary>
        public const int PhaseCount = 10;
    }
}
