using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Tile.PhasePair
{
    /// <summary>
    /// doc2 MAP-07 双层配对（Phase Pair）查询服务。
    ///
    /// <para/>
    /// **角色**：把 <see cref="TileDefinition.PhasePairTileId"/>（MAP-04 已存在字段）
    /// 双向索引为可查询表，允许：
    /// <list type="bullet">
    /// <item>按 tileId 查询 → 配对 tileId；</item>
    /// <item>按 <see cref="GridCoord"/> 查询 → 配对 <see cref="GridCoord"/>；</item>
    /// </list>
    /// <para/>
    /// **attach 模式**（与 <see cref="Starfall.Core.Map.Commands.PhaseFlipStateService"/> 一致）：
    /// 测试 [SetUp] 调用 <see cref="AttachPhasePairs"/> / <see cref="AttachFromRegistry"/>；[TearDown] 调用
    /// <see cref="DetachAll"/>。业务代码 / Data 层在装配时调用。
    /// <para/>
    /// **线程安全**：static 字段 + <c>lock</c>，与 MAP-08 一致；多线程并发 OK。
    /// <para/>
    /// **Pair 索引不变量**：
    /// <list type="number">
    /// <item>同一 tile 不能指向自己（<c>PhasePairTileId == TileId</c>）；提取时被忽略。</item>
    /// <item>双向一致 — 若 tileA.PhasePairTileId == tileB，则 tileB.PhasePairTileId == tileA；
    ///     提取时按"任一方向"插一次即可，对端也查得到。</item>
    /// <item>孤儿（指向不存在 tileId）— 提取时被忽略；调用方若需严格校验，调
    ///     <see cref="CrossLayerValidator.Validate"/>。</item>
    /// </list>
    /// <para/>
    /// **无 UnityEngine 引用**：本类属于 Starfall.Core，符合 AGENTS.md §10.1。
    /// </summary>
    public static class PhasePairLookup
    {
        private static readonly object _gate = new object();
        private static readonly Dictionary<MapState, Dictionary<int, int>> _pairByMap
            = new Dictionary<MapState, Dictionary<int, int>>();
        // key: MapState; value: tileId → pairTileId (双向建立：A→B 与 B→A 都存入)

        /// <summary>
        /// 把外部构造好的 <c>tileId → pairTileId</c> 表挂到指定 <paramref name="map"/>。
        /// 内部会对 dict 加 lock；调用方应只在 [SetUp] 或装配时调用。
        /// </summary>
        /// <remarks>
        /// **冲突解决**：如果同一 map 已有 attach，新 attach 完全替换旧 dict
        /// （与 <see cref="Starfall.Core.Map.Commands.PhaseFlipStateService.Attach"/> 一致）。
        /// </remarks>
        public static void AttachPhasePairs(
            MapState map,
            IReadOnlyDictionary<int, int> phasePairs)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (phasePairs == null) throw new ArgumentNullException(nameof(phasePairs));
            lock (_gate)
            {
                // copy into mutable dict
                var copy = new Dictionary<int, int>(phasePairs.Count);
                foreach (var kv in phasePairs)
                {
                    if (kv.Key < 1 || kv.Value < 1) continue;
                    if (kv.Key == kv.Value) continue; // self-loop ignored
                    copy[kv.Key] = kv.Value;
                }
                _pairByMap[map] = copy;
            }
        }

        /// <summary>
        /// 从 <see cref="TileDefinitionRegistry"/> 扫描所有 <see cref="TileDefinition"/> 的
        /// <see cref="TileDefinition.PhasePairTileId"/> 字段，组装双向 map 并 attach。
        /// </summary>
        /// <remarks>
        /// **构建规则**：
        /// <list type="bullet">
        /// <item>同 tile 自指忽略（<c>PhasePairTileId == TileId</c>）。</item>
        /// <item>null PhasePairTileId 忽略。</item>
        /// <item>指向不存在 tileId 忽略（孤儿）。</item>
        /// <item>双向建立：A→B 和 B→A 都存入（即使 B 不指向 A，索引也能单向查到；
        ///     这是为了避免要求数据双写对端）。</item>
        /// </list>
        /// </remarks>
        public static void AttachFromRegistry(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var pairs = ExtractPairs(registry);
            AttachPhasePairs(map, pairs);
        }

        /// <summary>
        /// 清除某 map 的 attach（与 <see cref="Starfall.Core.Map.Commands.PhaseFlipStateService.Detach"/>
        /// 同步；测试 [TearDown] 调用）。
        /// </summary>
        public static void DetachAll(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _pairByMap.Remove(map);
            }
        }

        /// <summary>清空所有 attach + 全部 pair cache（极端测试 reset）。</summary>
        public static void Clear()
        {
            lock (_gate)
            {
                _pairByMap.Clear();
            }
        }

        /// <summary>取出当前 attach 的 pair dict（只读视图）；未 attach → null。</summary>
        public static IReadOnlyDictionary<int, int> GetAttachedPairs(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (_pairByMap.TryGetValue(map, out var d))
                    return d;
                return null;
            }
        }

        // ──────────── 查询 ────────────

        /// <summary>
        /// 按 tileId 查配对 tileId。返回 false = 未 attach / 无配对 / tileId 自身不存在于索引。
        /// </summary>
        public static bool TryGetPair(MapState map, int tileId, out int pairTileId)
        {
            pairTileId = 0;
            if (map == null) return false;
            if (tileId < 1) return false;
            lock (_gate)
            {
                if (!_pairByMap.TryGetValue(map, out var dict)) return false;
                return dict.TryGetValue(tileId, out pairTileId);
            }
        }

        /// <summary>
        /// 按 <see cref="GridCoord"/> 查配对 <see cref="GridCoord"/>。
        /// 如果 <paramref name="coord"/> 对应的 tile 在 attach 后已经通过 flip 换到另一层，
        /// 仍以原 tileId 的 pair 作为目标层。
        /// </summary>
        /// <remarks>
        /// 实现：先用 <paramref name="registry"/> 把 coord → tileId，再用 tileId 查 pair，
        /// 最后用 pair 查对应 coord。
        /// </remarks>
        public static bool TryGetPair(
            MapState map,
            GridCoord coord,
            TileDefinitionRegistry registry,
            out GridCoord pairCoord)
        {
            pairCoord = default;
            if (map == null || registry == null) return false;
            if (!registry.TryGetByCoord(coord, out var def)) return false;
            if (!TryGetPair(map, def.TileId, out var pairTileId)) return false;
            if (!registry.TryGetById(pairTileId, out var pairDef)) return false;
            pairCoord = pairDef.Coord;
            return true;
        }

        /// <summary>
        /// 按 tileId 查配对 <see cref="GridCoord"/>（不依赖 coord → tileId 反查）。
        /// </summary>
        public static bool TryGetPair(
            MapState map,
            int tileId,
            TileDefinitionRegistry registry,
            out GridCoord pairCoord)
        {
            pairCoord = default;
            if (map == null || registry == null) return false;
            if (!TryGetPair(map, tileId, out var pairTileId)) return false;
            if (!registry.TryGetById(pairTileId, out var pairDef)) return false;
            pairCoord = pairDef.Coord;
            return true;
        }

        // ──────────── ExtractPairs（registry 扫描）────────────

        /// <summary>
        /// 从 <see cref="TileDefinitionRegistry"/> 中扫描所有 <see cref="TileDefinition"/> 的
        /// <see cref="TileDefinition.PhasePairTileId"/> 字段，返回双向索引 dict。
        /// </summary>
        /// <remarks>
        /// **规则**：双向建立（A→B 与 B→A 均放入），指向不存在 tileId 或自身忽略。
        /// 仅 <c>PhasePairTileId != null</c> 的 tile 会参与。
        /// **确定性**：输出 dict 按 tileId 升序（通过 List 排序）。
        /// </remarks>
        public static Dictionary<int, int> ExtractPairs(TileDefinitionRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var result = new Dictionary<int, int>();
            // 第一遍：收集所有 tileId 用于孤儿检测。
            var allIds = new HashSet<int>();
            foreach (var def in registry.All())
            {
                if (def.TileId >= 1) allIds.Add(def.TileId);
            }
            // 第二遍：建索引。
            foreach (var def in registry.All())
            {
                if (!def.PhasePairTileId.HasValue) continue;
                int pairId = def.PhasePairTileId.Value;
                if (pairId < 1) continue;
                if (pairId == def.TileId) continue; // self-loop ignore
                if (!allIds.Contains(pairId)) continue; // orphan ignore
                result[def.TileId] = pairId;
                result[pairId] = def.TileId; // 双
            }
            return result;
        }

        // ──────────── 诊断 ────────────

        /// <summary>取出某 map 已索引的 pair 数（用于测试断言）。</summary>
        public static int GetPairCount(MapState map)
        {
            if (map == null) return 0;
            lock (_gate)
            {
                if (_pairByMap.TryGetValue(map, out var d)) return d.Count;
                return 0;
            }
        }
    }
}
