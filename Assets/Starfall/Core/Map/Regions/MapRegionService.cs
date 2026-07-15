using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 区域服务：注册 / 状态机转换 / 单位进出 / 每回合 Tick / 事件触发。
    ///
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item>注册 / 注销 <see cref="MapRegionState"/> 到 <see cref="MapState.RegionStates"/>。</item>
    /// <item>校验 <see cref="RegionState"/> 转换合法性，按合法性表拒绝非法转换。</item>
    /// <item>记录"单位进入 / 离开区域"事件；维护 OccupantCount。</item>
    /// <item>每 Tick 推进：Hidden → Available、ActivationProgress 累加、100 → Completed。</item>
    /// <item>提供事件工厂方法（<see cref="MakeStateChangedEvent"/> 等）供命令编排。</item>
    /// </list>
    ///
    /// <para/>
    /// **设计原则**：服务不缓存状态——所有读写直接走 <see cref="MapState"/>。
    /// 服务实例仅持有 <see cref="CurrentTick"/>。
    /// </summary>
    public sealed class MapRegionService
    {
        // ──────────── 状态机合法性表 ────────────

        private static readonly Dictionary<RegionState, RegionState[]> _allowedTransitions =
            new Dictionary<RegionState, RegionState[]>
            {
                { RegionState.Disabled, new[] { RegionState.Hidden, RegionState.Available, RegionState.Sealed } },
                { RegionState.Hidden, new[] { RegionState.Available, RegionState.Sealed } },
                { RegionState.Available, new[] { RegionState.Active, RegionState.Sealed } },
                { RegionState.Active, new[] { RegionState.Contested, RegionState.Completed, RegionState.Failed, RegionState.Sealed } },
                { RegionState.Contested, new[] { RegionState.Active, RegionState.Completed, RegionState.Failed, RegionState.Sealed } },
                { RegionState.Completed, new[] { RegionState.Sealed } },
                { RegionState.Failed, new[] { RegionState.Sealed } },
                { RegionState.Sealed, System.Array.Empty<RegionState>() },
            };

        /// <summary>检查 (from → to) 是否合法（from == to 视为非法）。</summary>
        public static bool IsTransitionAllowed(RegionState from, RegionState to)
        {
            if (from == to) return false;
            if (!_allowedTransitions.TryGetValue(from, out var allowed))
                return false;
            for (int i = 0; i < allowed.Length; i++)
                if (allowed[i] == to) return true;
            return false;
        }

        /// <summary>返回所有合法转换目标（按 byte 排序）。</summary>
        public static IReadOnlyList<RegionState> GetAllowedTransitions(RegionState from)
        {
            if (!_allowedTransitions.TryGetValue(from, out var allowed))
                return System.Array.Empty<RegionState>();
            var copy = new List<RegionState>(allowed);
            copy.Sort((a, b) => ((byte)a).CompareTo((byte)b));
            return copy;
        }

        // ──────────── 实例字段 ────────────

        /// <summary>当前 Tick（每调用一次 <see cref="Tick"/> 自增 1）。</summary>
        public int CurrentTick { get; private set; }

        public MapRegionService()
        {
            CurrentTick = 0;
        }

        // ──────────── 注册 / 注销 ────────────

        /// <summary>注册新 region 到 <see cref="MapState.RegionStates"/>。</summary>
        public MapRegionState Register(MapState mapState, MapRegionDefinition def)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (def.RegionIdValue.Value < 0)
                throw new ArgumentException("RegionIdValue must be >= 0.", nameof(def));
            // RegionId 不重复
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                if (mapState.RegionStates[i].Definition.RegionIdValue == def.RegionIdValue)
                    throw new InvalidOperationException(
                        $"Duplicate RegionId: {def.RegionIdValue}.");
            }
            var rs = new MapRegionState(def, CurrentTick);
            mapState.AddRegionState(rs);
            return rs;
        }

        /// <summary>注销 region；不存在 → 返 false。</summary>
        public bool Unregister(MapState mapState, RegionId id)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            return mapState.RemoveRegionState(id.Value);
        }

        // ──────────── 状态机转换 ────────────

        /// <summary>尝试转换状态；非法 → 抛异常，mapState 不变。</summary>
        public void TransitionState(MapState mapState, RegionId id, RegionState newState, string reason)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            var rs = FindRegion(mapState, id);
            if (rs == null)
                throw new InvalidOperationException(
                    $"Region {id} not found.");
            if (!IsTransitionAllowed(rs.State, newState))
                throw new InvalidOperationException(
                    $"Illegal transition: {rs.State} -> {newState} for region {id}.");
            var old = rs.State;
            rs.SetStateInternal(newState, CurrentTick);
            // 状态变为 Sealed 时清空占用格（终态）
            if (newState == RegionState.Sealed)
                rs.ClearOccupiedCellsInternal();
            // 状态变 Active 时清零 ActivationProgress（重新开始计算）
            if (newState == RegionState.Active)
                rs.SetActivationProgressInternal(0, CurrentTick);
        }

        /// <summary>无副作用尝试版本；返回 (success, oldState)。</summary>
        public bool TryTransitionState(MapState mapState, RegionId id, RegionState newState, out RegionState oldState)
        {
            oldState = RegionState.Disabled;
            if (mapState == null) return false;
            var rs = FindRegion(mapState, id);
            if (rs == null) return false;
            if (!IsTransitionAllowed(rs.State, newState)) return false;
            oldState = rs.State;
            rs.SetStateInternal(newState, CurrentTick);
            if (newState == RegionState.Sealed)
                rs.ClearOccupiedCellsInternal();
            if (newState == RegionState.Active)
                rs.SetActivationProgressInternal(0, CurrentTick);
            return true;
        }

        // ──────────── 单位进出 ────────────

        /// <summary>单位进入 tile。</summary>
        public void NotifyUnitEntered(MapState mapState, GridCoord coord, int unitSide)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                var def = rs.Definition;
                if (!def.Contains(coord)) continue;
                rs.AddOccupiedCellInternal(coord);
                rs.SetOccupantCountInternal(rs.CurrentlyOccupiedCells.Count, CurrentTick);
            }
        }

        /// <summary>单位离开 tile。</summary>
        public void NotifyUnitExited(MapState mapState, GridCoord coord, int unitSide)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                var def = rs.Definition;
                if (!def.Contains(coord)) continue;
                rs.RemoveOccupiedCellInternal(coord);
                rs.SetOccupantCountInternal(rs.CurrentlyOccupiedCells.Count, CurrentTick);
            }
        }

        // ──────────── 每回合 Tick ────────────

        /// <summary>推进 1 个 Tick：自增 CurrentTick；处理 Hidden→Available、ActivationProgress 累加。</summary>
        public void Tick(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            CurrentTick++;
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                if (rs.State == RegionState.Hidden)
                {
                    // Hidden → Available（自动）
                    if (IsTransitionAllowed(rs.State, RegionState.Available))
                        rs.SetStateInternal(RegionState.Available, CurrentTick);
                }
                else if (rs.State == RegionState.Active || rs.State == RegionState.Contested)
                {
                    // 进度累加（Capture / Defense / Escort 等）
                    int newProg = rs.ActivationProgress + 1;
                    rs.SetActivationProgressInternal(newProg, CurrentTick);
                    if (newProg >= 100)
                    {
                        // Capture / Defense / Escort 成功 → Completed
                        if (rs.Definition.Kind == RegionKind.Capture ||
                            rs.Definition.Kind == RegionKind.Defense ||
                            rs.Definition.Kind == RegionKind.Escort ||
                            rs.Definition.Kind == RegionKind.Extraction)
                        {
                            if (IsTransitionAllowed(rs.State, RegionState.Completed))
                                rs.SetStateInternal(RegionState.Completed, CurrentTick);
                        }
                    }
                }
            }
        }

        // ──────────── 查询 ────────────

        /// <summary>所有包含指定 tile 的 region（按 RegionId 升序）。</summary>
        public IReadOnlyList<MapRegionState> GetRegionsContaining(MapState mapState, GridCoord coord)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
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

        /// <summary>按 ID 查找 region；不存在 → null。</summary>
        public MapRegionState FindRegion(MapState mapState, RegionId id)
        {
            if (mapState == null) return null;
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                var rs = mapState.RegionStates[i];
                if (rs.Definition.RegionIdValue == id)
                    return rs;
            }
            return null;
        }

        // ──────────── 事件构造（工厂）────────────

        /// <summary>构造 OnRegionStateChanged 事件（kind = OnRegionChanged）。</summary>
        public static MapEvent MakeStateChangedEvent(int regionId, RegionState oldState, RegionState newState, string reason)
        {
            return new MapEvent(
                MapEventKind.OnRegionChanged,
                regionId: regionId,
                oldValue: (int)oldState,
                newValue: (int)newState,
                description: reason ?? string.Empty);
        }

        /// <summary>构造 OnRegionEntered 事件。</summary>
        public static MapEvent MakeEnteredEvent(int regionId, GridCoord coord, int unitSide)
        {
            return new MapEvent(
                MapEventKind.OnRegionChanged,
                regionId: regionId,
                coord: coord,
                newValue: unitSide,
                description: "entered");
        }

        /// <summary>构造 OnRegionExited 事件。</summary>
        public static MapEvent MakeExitedEvent(int regionId, GridCoord coord, int unitSide)
        {
            return new MapEvent(
                MapEventKind.OnRegionChanged,
                regionId: regionId,
                coord: coord,
                newValue: unitSide,
                description: "exited");
        }

        /// <summary>构造 OnRegionActivated 事件。</summary>
        public static MapEvent MakeActivatedEvent(int regionId, int progress)
        {
            return new MapEvent(
                MapEventKind.OnRegionChanged,
                regionId: regionId,
                newValue: progress,
                description: "activated");
        }
    }
}