using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 区域运行时状态（mutable class）。
    ///
    /// <para/>
    /// 与不可变 <see cref="MapRegionDefinition"/> 平行：定义层面只读，运行时实例化后才
    /// 拥有 <see cref="State"/> / <see cref="CurrentOwnerSide"/> / <see cref="OccupantCount"/>
    /// / <see cref="TickEntered"/> / <see cref="ActivationProgress"/> / <see cref="CurrentlyOccupiedCells"/>
    /// 等可变字段。
    ///
    /// <para/>
    /// **序列化契约**：<see cref="PostStateHash"/> 字段稳定（FNV-1a 64，按字段顺序），
    /// 由 <see cref="MapRegionStateHasher"/> 实现。
    ///
    /// <para/>
    /// **引用语义**：持有 <see cref="MapRegionDefinition"/> 的引用（不可变）；修改
    /// <see cref="MapRegionDefinition"/> 字段的代价 = 重建实例。
    /// </summary>
    public sealed class MapRegionState
    {
        // ──────────── 引用不可变部分 ────────────

        /// <summary>不可变区域定义引用。</summary>
        public MapRegionDefinition Definition { get; }

        // ──────────── 运行时可变字段 ────────────

        /// <summary>当前运行时状态（8 态状态机）。</summary>
        public RegionState State { get; private set; }

        /// <summary>当前归属方（-1 = 中立，0 = 玩家，1+ = 敌方 / NPC）。</summary>
        public int CurrentOwnerSide { get; private set; }

        /// <summary>当前区域内单位数（占用数）。</summary>
        public int OccupantCount { get; private set; }

        /// <summary>最近一次状态变化的 Tick（用于回放 / undo / hash 稳定）。</summary>
        public int TickEntered { get; private set; }

        /// <summary>激活进度（0-100；Capture / Escort / Defense 等进度类区域使用）。</summary>
        public int ActivationProgress { get; private set; }

        /// <summary>当前被单位占用的格子列表（按 GridCoord.CompareTo 排序）。</summary>
        public IReadOnlyList<GridCoord> CurrentlyOccupiedCells => _occupiedCellsInternal;

        private readonly List<GridCoord> _occupiedCellsInternal;

        // ──────────── 构造 ────────────

        public MapRegionState(MapRegionDefinition definition, int initialTick = 0)
        {
            if (definition.RegionIdValue.Value < 0)
                throw new ArgumentException("Definition.RegionIdValue must be >= 0.", nameof(definition));
            Definition = definition;
            // 初始 State 与 Definition.Activation 对齐；Sealed / Disabled → Disabled。
            State = definition.Activation switch
            {
                RegionActivation.Disabled => RegionState.Disabled,
                RegionActivation.Sealed => RegionState.Sealed,
                RegionActivation.Hidden => RegionState.Hidden,
                RegionActivation.Available => RegionState.Available,
                RegionActivation.Active => RegionState.Active,
                _ => RegionState.Disabled,
            };
            CurrentOwnerSide = definition.OwnerSide;
            OccupantCount = 0;
            TickEntered = initialTick;
            ActivationProgress = 0;
            _occupiedCellsInternal = new List<GridCoord>();
        }

        // ──────────── 内部变更入口（仅同程序集 Service 调用）────────────

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void SetStateInternal(RegionState newState, int tick)
        {
            State = newState;
            TickEntered = tick;
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void SetOwnerSideInternal(int ownerSide, int tick)
        {
            CurrentOwnerSide = ownerSide;
            TickEntered = tick;
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void SetOccupantCountInternal(int count, int tick)
        {
            OccupantCount = count;
            TickEntered = tick;
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void SetActivationProgressInternal(int progress, int tick)
        {
            if (progress < 0) progress = 0;
            if (progress > 100) progress = 100;
            ActivationProgress = progress;
            TickEntered = tick;
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void AddOccupiedCellInternal(GridCoord coord)
        {
            int idx = _occupiedCellsInternal.BinarySearch(coord);
            if (idx < 0)
                _occupiedCellsInternal.Insert(~idx, coord);
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void RemoveOccupiedCellInternal(GridCoord coord)
        {
            int idx = _occupiedCellsInternal.BinarySearch(coord);
            if (idx >= 0)
                _occupiedCellsInternal.RemoveAt(idx);
        }

        /// <summary>由 <see cref="MapRegionService"/> 调用；外部不应直接修改。</summary>
        internal void ClearOccupiedCellsInternal()
        {
            _occupiedCellsInternal.Clear();
        }

        // ──────────── 哈希（按需计算）────────────

        /// <summary>FNV-1a 64 位哈希；见 <see cref="MapRegionStateHasher"/>。</summary>
        public ulong PostStateHash => MapRegionStateHasher.CalculateDeterministicHash(this);

        // ──────────── 字符串 ────────────

        public override string ToString()
            => $"MapRegionState(Id={Definition.RegionIdValue}, Kind={Definition.Kind}, State={State}, Owner={CurrentOwnerSide}, Tick={TickEntered}, Prog={ActivationProgress}, Occ={OccupantCount}, Cells={_occupiedCellsInternal.Count})";

        // ──────────── 等值（仅 reference；便于断言 / 调试）────────────

        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
}