using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-08 per-tile "phase flip" 副作用状态的 attach 模式存储器。
    /// <para/>
    /// **角色**：作为 MAP-08 的临时层，托管"翻转过的 tile" 字典（tileId → active layer）。
    /// 完整 per-tile <c>ActiveDimension</c> 字段由 MAP-07 引入后将本服务淘汰并迁移
    /// 到 <see cref="MapTileState"/>；本轮的 flip / fall / compression 决策
    /// 都通过该服务读"当前激活层"。
    /// <para/>
    /// **存储结构**（static fields，单进程内全局）：
    /// <list type="bullet">
    /// <item><c>_registryAttach</c>：map → <see cref="TileDefinitionRegistry"/>（用于按 tileId 查 coord）。</item>
    /// <item><c>_flipStateAttach</c>：map → <see cref="PhaseFlipState"/>（tileId → active layer）。</item>
    /// </list>
    /// <para/>
    /// **失败语义**：未 attach registry 时上层 Flip 命令直接返回 <c>"no tile registry attached"</c>。
    /// <para/>
    /// **测试 fixture**：测试 [SetUp] 调用 <see cref="Attach"/>；[TearDown] 调用 <see cref="Detach"/>。
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
    /// doc2 MAP-08 PhaseFlipStateService (static attach 模式)。
    /// <para/>
    /// **线程安全**：所有写入 <c>lock</c> + 内部 Dictionary；
    /// 与 <see cref="TileOccupancyService"/> 一致。
    /// <para/>
    /// **失败语义**：attach / clear / detach 都不会抛（除 null 检查）；调用方负责 setUp 时正确装配。
    /// </summary>
    public static class PhaseFlipStateService
    {
        private static readonly object _gate = new object();
        private static readonly Dictionary<MapState, TileDefinitionRegistry> _registryAttach
            = new Dictionary<MapState, TileDefinitionRegistry>();
        private static readonly Dictionary<MapState, PhaseFlipState> _flipStateAttach
            = new Dictionary<MapState, PhaseFlipState>();

        /// <summary>一次性 attach registry + 取出 PhaseFlipState。测试 [SetUp] 调用。</summary>
        public static PhaseFlipState Attach(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            lock (_gate)
            {
                _registryAttach[map] = registry;
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

        /// <summary>取出当前 attach 的 registry；未 attach → null（Flip 命令据此返回 Fail）。</summary>
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
            }
        }

        /// <summary>清空所有 attach + 全部 flip state（极端测试 reset）。</summary>
        public static void Clear()
        {
            lock (_gate)
            {
                _registryAttach.Clear();
                _flipStateAttach.Clear();
            }
        }
    }
}
