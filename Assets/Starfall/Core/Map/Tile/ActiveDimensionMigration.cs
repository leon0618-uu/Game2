using System.Collections.Generic;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-07 ActiveDimension 字段迁移助手。
    ///
    /// <para/>
    /// **角色**：MAP-08 阶段 (commit <c>e8ae405</c>) 的 <see cref="PhaseFlipStateService"/>
    /// 把 per-tile "激活层" 存在 attach-mode <c>PhaseFlipState._flipped</c> dict 里。
    /// MAP-07 引入 <see cref="MapTileState.ActiveDimension"/> 字段后，
    /// 旧代码 / 旧 Replay 数据仍走 dict 路径。本助手提供：
    /// <list type="bullet">
    /// <item><see cref="MigrateFromDict"/>：把现有 _flipped dict 复制到
    ///     <see cref="MapTileState.ActiveDimension"/> 字段（一次性迁移，幂等）。</item>
    /// <item><see cref="BuildRuntimeStatesFromRegistry"/>：从
    ///     <see cref="TileDefinitionRegistry"/> 构造 runtime states 字典
    ///     （给 <see cref="PhaseFlipStateService.AttachRuntimeStates"/> 用）。</item>
    /// <item><see cref="BuildRuntimeStatesFromRegistryWithFlips"/>：同上，但预设 ActiveDimension
    ///     从传入的 <c>flipped</c> dict（tileId → layer）传入。</item>
    /// </list>
    /// <para/>
    /// **静态助手**：仅静态方法；不在 attach 字典里写入新内容（避免副作用）。
    /// <para/>
    /// **无 UnityEngine 引用**：纯 C#，符合 AGENTS.md §10.1。
    /// </summary>
    public static class ActiveDimensionMigration
    {
        /// <summary>
        /// 把 <paramref name="sourceDict"/>（MAP-08 _flipped dict 投影）中每个条目
        /// 应用到对应 <see cref="MapTileState"/> 的 <see cref="MapTileState.ActiveDimension"/>
        /// 字段。
        /// </summary>
        /// <remarks>
        /// **不变性**：仅对 <paramref name="runtimeStates"/> 中存在的 tileId 起作用；
        /// sourceDict 中多余 tileId 静默忽略（视为迁移期权威数据缺失）。
        /// **幂等**：重复调用同一 sourceDict 不会引发副作用。
        /// </remarks>
        public static int MigrateFromDict(
            IReadOnlyDictionary<int, MapTileState> runtimeStates,
            IReadOnlyDictionary<int, DimensionLayer> sourceDict)
        {
            if (runtimeStates == null) return 0;
            if (sourceDict == null) return 0;
            int count = 0;
            foreach (var kv in sourceDict)
            {
                if (runtimeStates.TryGetValue(kv.Key, out var state))
                {
                    state.SetActiveDimensionDirect(kv.Value);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 从 <see cref="TileDefinitionRegistry"/> 扫描所有 <see cref="TileDefinition"/> 构造
        /// <c>tileId → MapTileState</c> 字典。每个 MapTileState 初始 ActiveDimension = Reality。
        /// </summary>
        public static Dictionary<int, MapTileState> BuildRuntimeStatesFromRegistry(
            TileDefinitionRegistry registry)
        {
            if (registry == null) throw new System.ArgumentNullException(nameof(registry));
            var dict = new Dictionary<int, MapTileState>();
            foreach (var def in registry.All())
            {
                if (dict.ContainsKey(def.TileId)) continue; // id collision ignored (should not happen)
                dict[def.TileId] = new MapTileState(def);
            }
            return dict;
        }

        /// <summary>
        /// 从 <see cref="TileDefinitionRegistry"/> 构造 runtime states，并应用
        /// <paramref name="flipped"/> 预设（tileId → ActiveDimension）。
        /// </summary>
        /// <remarks>
        /// **用法**：测试 fixture 用此构造"已经部分翻转"的 MapState 等价数据。
        /// </remarks>
        public static Dictionary<int, MapTileState> BuildRuntimeStatesFromRegistryWithFlips(
            TileDefinitionRegistry registry,
            IReadOnlyDictionary<int, DimensionLayer> flipped)
        {
            var states = BuildRuntimeStatesFromRegistry(registry);
            if (flipped != null)
            {
                foreach (var kv in flipped)
                {
                    if (states.TryGetValue(kv.Key, out var state))
                        state.SetActiveDimensionDirect(kv.Value);
                }
            }
            return states;
        }

        /// <summary>
        /// 双向绑定：把 runtime states 注入 <see cref="PhaseFlipStateService"/>，
        /// 并同步到 PhaseFlipStateService.GetRuntimeStates 的可读视图上。
        /// </summary>
        /// <remarks>
        /// 测试 [SetUp] 中常用此组合：
        /// <code>
        /// var states = ActiveDimensionMigration.BuildRuntimeStatesFromRegistry(registry);
        /// ActiveDimensionMigration.BindToPhaseFlipService(map, registry, states);
        /// </code>
        /// </remarks>
        public static void BindToPhaseFlipService(
            MapState map,
            TileDefinitionRegistry registry,
            IReadOnlyDictionary<int, MapTileState> runtimeStates)
        {
            PhaseFlipStateService.AttachMapState(map, registry);
            PhaseFlipStateService.AttachRuntimeStates(map, runtimeStates);
        }
    }
}
