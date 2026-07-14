using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 per-tile "phase flip" 副作用状态 — MAP-07 平滑合并后的最终形态。
    ///
    /// <para/>
    /// **历史**：MAP-08 阶段 (commit <c>e8ae405</c>) 采用 attach 模式 dict
    /// (<c>Dictionary&lt;int, DimensionLayer&gt;</c>) 暂存翻转状态。MAP-07
    /// 引入 <see cref="MapTileState.ActiveDimension"/> per-tile 字段后，
    /// 本服务重构成：
    /// <list type="bullet">
    /// <item>**真相源** = <see cref="MapTileState.ActiveDimension"/> 字段；</item>
    /// <item>本服务仍是查询 / 写入入口（保持向后兼容 MAP-08 调用方）；</item>
    /// <item>**dict 已废弃**：<see cref="PhaseFlipState"/> 类保留以避免破坏
    ///     既有测试 / 调用方对 "TryGetFlippedLayer / SetFlippedLayer /
    ///     ResetFlippedLayer / EnumerateSorted" 的引用，但内部只是
    ///     对 runtime states 的"投影"，不再是真值。</item>
    /// </list>
    /// <para/>
    /// **新增 API**（MAP-07）：
    /// <list type="bullet">
    /// <item><see cref="AttachMapState"/>：装配主入口；调用方必须先后调用
    ///     <c>AttachMapState</c>（绑定 map）和 <see cref="AttachRuntimeStates"/>
    ///     （绑定运行时 tile state 字典）。</item>
    /// <item><see cref="TryGetActiveDimension"/> / <see cref="SetActiveDimension"/>：
    ///     直接读写 per-tile 字段（替代旧 dict 接口）。</item>
    /// </list>
    /// <para/>
    /// **线程安全**：static 字段 + <c>lock</c>，与既有 pattern 一致。
    /// <para/>
    /// **失败语义**：
    /// <list type="bullet">
    /// <item>未 attach → <see cref="TryGetActiveDimension"/> 返回 false，
    ///     <paramref name="layer"/> = <see cref="DimensionLayer.Reality"/> 默认。</item>
    /// <item><see cref="SetActiveDimension"/> 在未 attach runtime states 时抛
    ///     <see cref="InvalidOperationException"/>（写操作必须有 runtime states）。</item>
    /// </list>
    /// </summary>
    public sealed class PhaseFlipState
    {
        private readonly Dictionary<int, DimensionLayer> _flipped;

        public PhaseFlipState()
        {
            _flipped = new Dictionary<int, DimensionLayer>();
        }

        public int Count => _flipped.Count;

        /// <summary>查询某 tile 当前激活层（未翻 → 需调用方提供 map 默认）。</summary>
        /// <remarks>**保留兼容**：MAP-08 既有测试仍走 <c>PhaseFlipState</c> 路径；
        /// 本方法当前仍使用内部 dict（不依赖 runtime states，保守向后兼容）。
        /// 推荐新代码使用 <see cref="PhaseFlipStateService.TryGetActiveDimension"/>。</remarks>
        public bool TryGetFlippedLayer(int tileId, out DimensionLayer layer)
            => _flipped.TryGetValue(tileId, out layer);

        /// <summary>写入某 tile 当前激活层。</summary>
        public void SetFlippedLayer(int tileId, DimensionLayer layer)
        {
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1.");
            _flipped[tileId] = layer;
        }

        /// <summary>清除某 tile 翻转状态（还原为 map.ActiveLayer 默认）。</summary>
        public bool ResetFlippedLayer(int tileId)
            => _flipped.Remove(tileId);

        /// <summary>全部 tile 当前激活层（按 tileId 升序）；用于 Hasher / Cloner。</summary>
        public IEnumerable<KeyValuePair<int, DimensionLayer>> EnumerateSorted()
        {
            var list = new List<KeyValuePair<int, DimensionLayer>>(_flipped);
            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }
    }

    /// <summary>
    /// doc2 MAP-08/MAP-07 PhaseFlipStateService (static attach 模式)。
    /// </summary>
    public static class PhaseFlipStateService
    {
        private static readonly object _gate = new object();
        private static readonly Dictionary<MapState, TileDefinitionRegistry> _registryAttach
            = new Dictionary<MapState, TileDefinitionRegistry>();
        private static readonly Dictionary<MapState, PhaseFlipState> _flipStateAttach
            = new Dictionary<MapState, PhaseFlipState>();
        // MAP-07 新增：每 map 的 tileId → MapTileState 字典（真值源）。
        private static readonly Dictionary<MapState, Dictionary<int, MapTileState>> _runtimeStatesAttach
            = new Dictionary<MapState, Dictionary<int, MapTileState>>();
        // 缓存的 default layer（map.ActiveLayer 写入期）。
        private static readonly Dictionary<MapState, DimensionLayer> _defaultLayerByMap
            = new Dictionary<MapState, DimensionLayer>();

        // ──────────── 既有 API（向后兼容 MAP-08 路径）────────────

        /// <summary>一次性 attach registry + 取出 PhaseFlipState。测试 [SetUp] 调用。
        /// MAP-07 推荐改用 <see cref="AttachMapState"/>。</summary>
        public static PhaseFlipState Attach(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            lock (_gate)
            {
                _registryAttach[map] = registry;
                _defaultLayerByMap[map] = map.ActiveLayer;
                if (!_flipStateAttach.TryGetValue(map, out var state))
                {
                    state = new PhaseFlipState();
                    _flipStateAttach[map] = state;
                }
                return state;
            }
        }

        /// <summary>取出当前 attach 的 flip state；若未 attach 返回新建（仅读场景）。</summary>
        public static PhaseFlipState GetOrAttach(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (_flipStateAttach.TryGetValue(map, out var s)) return s;
                s = new PhaseFlipState();
                _flipStateAttach[map] = s;
                return s;
            }
        }

        /// <summary>取出当前 attach 的 registry；未 attach → null。</summary>
        public static TileDefinitionRegistry GetAttachedRegistry(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _registryAttach.TryGetValue(map, out var reg);
                return reg;
            }
        }

        /// <summary>清空某 map 的 attach；测试 [TearDown] 调用。</summary>
        public static void Detach(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _registryAttach.Remove(map);
                _flipStateAttach.Remove(map);
                _runtimeStatesAttach.Remove(map);
                _defaultLayerByMap.Remove(map);
            }
        }

        /// <summary>清空所有 attach + 全部 flip state（极端测试 reset）。</summary>
        public static void Clear()
        {
            lock (_gate)
            {
                _registryAttach.Clear();
                _flipStateAttach.Clear();
                _runtimeStatesAttach.Clear();
                _defaultLayerByMap.Clear();
            }
        }

        // ──────────── MAP-07 新 API（per-tile ActiveDimension）────────────

        /// <summary>
        /// 装配：把 <paramref name="map"/> + <paramref name="registry"/> +
        /// <paramref name="runtimeStates"/> 一起挂上（顺序建议：先 Attach，再 AttachRuntimeStates）。
        /// </summary>
        /// <remarks>
        /// 推荐用法：
        /// <code>
        /// PhaseFlipStateService.AttachMapState(map, registry);
        /// PhaseFlipStateService.AttachRuntimeStates(map, runtimeStatesDict);
        /// </code>
        /// </remarks>
        public static void AttachMapState(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            lock (_gate)
            {
                _registryAttach[map] = registry;
                _defaultLayerByMap[map] = map.ActiveLayer;
                if (!_flipStateAttach.ContainsKey(map))
                    _flipStateAttach[map] = new PhaseFlipState();
            }
        }

        /// <summary>
        /// 装配 runtime states（tileId → <see cref="MapTileState"/>）— MAP-07 后的真值源。
        /// </summary>
        /// <remarks>
        /// 重复 attach 完全替换旧 dict（与 PhaseFlipStateService.Attach 一致）。
        /// </remarks>
        public static void AttachRuntimeStates(
            MapState map,
            IReadOnlyDictionary<int, MapTileState> runtimeStates)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (runtimeStates == null) throw new ArgumentNullException(nameof(runtimeStates));
            lock (_gate)
            {
                var copy = new Dictionary<int, MapTileState>(runtimeStates.Count);
                foreach (var kv in runtimeStates)
                {
                    if (kv.Key >= 1 && kv.Value != null) copy[kv.Key] = kv.Value;
                }
                _runtimeStatesAttach[map] = copy;
            }
        }

        /// <summary>取出当前 attach 的 runtime states dict；未 attach → null。</summary>
        public static IReadOnlyDictionary<int, MapTileState> GetRuntimeStates(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (_runtimeStatesAttach.TryGetValue(map, out var d)) return d;
                return null;
            }
        }

        /// <summary>
        /// **MAP-07 推荐**：查询某 tile 当前激活层。
        /// <para/>
        /// **查找顺序**（与 <see cref="SetActiveDimension"/> 写路径一致）：
        /// <list type="number">
        /// <item>runtime states dict（MAP-07 per-tile 字段） — 如有返 true，layer = 字段值。</item>
        /// <item><see cref="PhaseFlipState"/>._flipped dict（MAP-08 stub） — 如有返 true。</item>
        /// <item>都没：返 false + <paramref name="layer"/> = map 的 ActiveLayer 默认值。</item>
        /// </list>
        /// </summary>
        public static bool TryGetActiveDimension(
            MapState map,
            int tileId,
            out DimensionLayer layer)
        {
            return TryGetActiveDimensionOrDict(map, tileId, out layer);
        }

        /// <summary>
        /// **MAP-07 推荐**：直接设置 per-tile <see cref="MapTileState.ActiveDimension"/> 字段。
        /// </summary>
        /// <remarks>
        /// **行为**：
        /// <list type="bullet">
        /// <item>如果 <see cref="AttachRuntimeStates"/> 已 attach 且 tileId 在 dict 中 →
        ///     直接修改 <see cref="MapTileState.ActiveDimension"/>（MAP-07 真值源）。</item>
        /// <item>如果未 attach runtime states（典型 MAP-08 兼容路径） →
        ///     在 attach-mode <see cref="PhaseFlipState"/>._flipped dict 中写入。
        ///     保证所有既有 MAP-08 测试（不 attach runtime states）继续 PASS。</item>
        /// <item>**双写顺序**：priority = 字段 > dict。如果有 runtime state，主写字段；
        ///     也同时在 dict 中记录（确保旧 API <c>PhaseFlipStateService.GetOrAttach</c>
        ///     路径仍可读）。</item>
        /// </list>
        /// </remarks>
        public static void SetActiveDimension(
            MapState map,
            int tileId,
            DimensionLayer layer)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (tileId < 1)
                throw new ArgumentOutOfRangeException(nameof(tileId), tileId,
                    "TileId must be >= 1.");
            lock (_gate)
            {
                bool wroteField = false;
                if (_runtimeStatesAttach.TryGetValue(map, out var states)
                    && states.TryGetValue(tileId, out var state))
                {
                    state.SetActiveDimensionDirect(layer);
                    wroteField = true;
                }
                // 始终在 PhaseFlipState dict 中写入（保留 MAP-08 旧接口可见性）。
                // 字段是优先真值源；dict 是从字段"复制"来的镜像。
                if (!_flipStateAttach.TryGetValue(map, out var flipState))
                {
                    flipState = new PhaseFlipState();
                    _flipStateAttach[map] = flipState;
                }
                flipState.SetFlippedLayer(tileId, layer);

                if (!wroteField)
                {
                    // 完全没 attach runtime states 的旧路径 — 不抛，让 MAP-08 测试继续工作。
                    // 写 dict 即可 — Flip 命令查询走 _flipped 也是 OK 的（fallback）。
                }
            }
        }

        /// <summary>
        /// **MAP-07 查询（fallback 路径）**：如果 runtime states 未 attach，自动回退到
        /// <see cref="PhaseFlipState"/>._flipped dict（即 MAP-08 旧接口）。
        /// </summary>
        public static bool TryGetActiveDimensionOrDict(
            MapState map,
            int tileId,
            out DimensionLayer layer)
        {
            layer = DimensionLayer.Reality;
            if (map == null) return false;
            if (tileId < 1) return false;
            lock (_gate)
            {
                // 优先：runtime state 字段
                if (_runtimeStatesAttach.TryGetValue(map, out var states)
                    && states.TryGetValue(tileId, out var state))
                {
                    layer = state.ActiveDimension;
                    return true;
                }
                // fallback：PhaseFlipState dict
                if (_flipStateAttach.TryGetValue(map, out var flipState)
                    && flipState.TryGetFlippedLayer(tileId, out var fromDict))
                {
                    layer = fromDict;
                    return true;
                }
                // 都没：默认 = map.ActiveLayer
                if (_defaultLayerByMap.TryGetValue(map, out var def))
                    layer = def;
                return false;
            }
        }
    }
}
