using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Regions;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 运行时唯一真相源。
    ///
    /// <para/>
    /// 持有：
    /// <list type="bullet">
    /// <item>不可变 <see cref="MapDefinition"/>（创建后不修改 Definition 字段）。</item>
    /// <item>运行时整数状态：<see cref="Version"/>、<see cref="ActiveLayer"/>、<see cref="GlobalCollapseValue"/>。</item>
    /// <item>6 个集合（Tiles / Anchors / Regions / MapObjects / RegionStates / SpawnPoints），对外暴露
    ///       <see cref="IReadOnlyList{T}"/>；内部使用 <see cref="List{T}"/>，由 <see cref="MapStateCloner"/>
    ///       完整深拷贝。</item>
    /// </list>
    ///
    /// <para/>
    /// 本轮（MAP-09）在 MAP-02 基础上**新增**：
    /// <list type="bullet">
    /// <item><see cref="RegionStates"/>：强类型运行时区域集合（与 <see cref="MapRegionDefinition"/>
    ///       + <see cref="MapRegionState"/> 配合）。</item>
    /// <item><see cref="SpawnPoints"/>：出生点集合（<see cref="MapSpawnPoint"/>）。</item>
    /// </list>
    ///
    /// <para/>
    /// 既有 <see cref="Regions"/>（MAP-02 占位 <see cref="MapRegion"/> POCO）保留 ——
    /// 它服务于 MAP-03/08 的 <c>FlipRegionPhaseCommand</c> / <c>CreateConstellationAreaCommand</c>
    /// 等通过 RegionId + TileCoords 字典式访问的路径。新旧 region 字段共存，由不同命令路径访问，
    /// 互不干扰。MAP-09 阶段不删除 legacy 字段（[MAP_SYSTEM_AUDIT §3.3] 兼容性约束）。
    ///
    /// <para/>
    /// 本轮（MAP-02）只建容器，不接任何 <c>IMapCommand</c>（MAP-03）。后续
    /// <c>MapCommandExecutor</c> 应在每次成功执行命令后：
    /// <list type="number">
    /// <item>修改集合 + 自增 <see cref="Version"/>；</item>
    /// <item>不要缓存 <see cref="PostStateHash"/>，由 <see cref="MapStateHasher"/> 按需计算；
    ///       任何缓存层必须随 Version 自增失效。</item>
    /// </list>
    ///
    /// <para/>
    /// 集合元素的稳定顺序在 <see cref="MapStateHasher"/> 与 <see cref="MapStateCloner"/>
    /// 各自负责，<see cref="MapState"/> 自身不保证集合顺序（List 保留插入顺序）。
    /// </summary>
    public sealed class MapState
    {
        public MapDefinition Definition { get; }

        /// <summary>每次成功的 MapCommand 自增 1（MAP-03 接入）。MAP-02 阶段不修改。</summary>
        public int Version { get; set; }

        /// <summary>当前激活维度（Reality / Astral）。</summary>
        public DimensionLayer ActiveLayer { get; set; }

        /// <summary>
        /// 全局坍塌值（doc1 §13.1，0..100；MAP-02 占位字段，MAP-11a 保留向后兼容）。
        /// <para/>
        /// **MAP-11a 升级**：本字段保留不变（int 影子），但业务代码应使用
        /// <see cref="GlobalCV"/>（typed wrapper）—— 它包含 Stage / TickAccumulated
        /// 派生信息。本字段与 <see cref="GlobalCV"/>.Value 保持同步，
        /// <c>GlobalCollapseValue = GlobalCV.Value</c>。
        /// </summary>
        public int GlobalCollapseValue
        {
            get => GlobalCV.Value;
            set => GlobalCV = new GlobalCollapseValue(value, GlobalCV.TickAccumulated);
        }

        /// <summary>
        /// doc2 MAP-11a typed 全局坍塌值（含 Stage / Threshold / TickAccumulated）。
        /// </summary>
        public GlobalCollapseValue GlobalCV { get; set; }

        /// <summary>
        /// doc2 MAP-03 调试开关：必须显式开启才能运行 <c>SetMapDebugValueCommand</c>。
        /// <para/>
        /// **生产规则**：仅 <c>SetMapDevTestFlag = true</c> 后命令 <c>SetMapDebugValueCommand</c> 才会成功；
        /// 默认 = false（拒绝所有调试写入）。该开关不进入 <see cref="PostStateHash"/>。
        /// </summary>
        public bool DevTestModeEnabled { get; private set; }

        private Dictionary<string, string> _debugValuesInternal;

        // 集合字段使用 internal：允许同程序集内的 MapStateCloner 写入新元素（深拷贝时）；
        // 对外（测试 / 业务代码）只能通过 IReadOnlyList + Add*/Remove* 方法操作。
        // 这与 AGENTS.md §10.1 硬约束（Core 无 UnityEngine）兼容：internal 仅在程序集内可见。
        internal readonly List<GridCoord> TilesInternal;
        internal readonly List<AnchorZone> AnchorsInternal;
        internal readonly List<MapRegion> RegionsInternal;
        internal readonly List<MapObjectInstance> MapObjectsInternal;
        // MAP-09 新增：强类型运行时区域集合（与 legacy Regions 并存）。
        internal readonly List<MapRegionState> RegionStatesInternal;
        // MAP-09 新增：出生点集合。
        internal readonly List<MapSpawnPoint> SpawnPointsInternal;
        // MAP-11a 新增：每个 tile 独立的 CV（按 GridCoord 索引）。空 = 业务未使用。
        internal readonly Dictionary<GridCoord, LocalCollapseValue> LocalCVsInternal;
        // MAP-12 新增：AnchorLink 集合（与 legacy Anchors 并存；不删除 legacy 字段）。
        internal readonly List<AnchorLink> AnchorLinksInternal;

        public IReadOnlyList<GridCoord> Tiles => TilesInternal;
        public IReadOnlyList<AnchorZone> Anchors => AnchorsInternal;
        public IReadOnlyList<MapRegion> Regions => RegionsInternal;
        public IReadOnlyList<MapObjectInstance> MapObjects => MapObjectsInternal;
        public IReadOnlyList<MapRegionState> RegionStates => RegionStatesInternal;
        public IReadOnlyList<MapSpawnPoint> SpawnPoints => SpawnPointsInternal;
        /// <summary>
        /// doc2 MAP-12 <see cref="Starfall.Core.Map.Anchor.AnchorLink"/> 集合（只读视图）。
        /// 写入由 <see cref="AddAnchorLink"/> / <see cref="RemoveAnchorLink"/> 统一入口。
        /// 与 legacy <see cref="Anchors"/>（MAP-02 <see cref="AnchorZone"/>）共存。
        /// </summary>
        public IReadOnlyList<AnchorLink> AnchorLinks => AnchorLinksInternal;

        /// <summary>
        /// doc2 MAP-11a 每个 tile 的局部 CV 字典（只读视图）。写入由
        /// <see cref="AddLocalCV"/> / <see cref="RemoveLocalCV"/> 统一入口。
        /// </summary>
        public IReadOnlyDictionary<GridCoord, LocalCollapseValue> LocalCVs => LocalCVsInternal;

        /// <summary>
        /// doc2 MAP-11a 当前阶段（derived from <see cref="GlobalCV"/>.Value，
        /// 自动计算；不存储）。
        /// </summary>
        public CollapseStage CurrentStage => GlobalCV.Stage;

        public MapState(MapDefinition definition)
        {
            Definition = definition;
            Version = 0;
            ActiveLayer = definition.InitialActiveLayer;
            GlobalCV = new GlobalCollapseValue(definition.InitialGlobalCollapseValue, 0);
            // 同步影子字段（向后兼容：旧代码仍读 GlobalCollapseValue = GlobalCV.Value）
            TilesInternal = new List<GridCoord>();
            AnchorsInternal = new List<AnchorZone>();
            RegionsInternal = new List<MapRegion>();
            MapObjectsInternal = new List<MapObjectInstance>();
            RegionStatesInternal = new List<MapRegionState>();
            SpawnPointsInternal = new List<MapSpawnPoint>();
            LocalCVsInternal = new Dictionary<GridCoord, LocalCollapseValue>();
            AnchorLinksInternal = new List<AnchorLink>();
        }

        // ──────────── 集合修改入口（MAP-02 阶段仅供 Cloner / Test 使用）────────────

        /// <summary>
        /// doc2 MAP-03 开启调试模式。仅 <c>SetDevTestMode(true)</c> 后
        /// <c>SetMapDebugValueCommand</c> 才能成功执行。
        /// </summary>
        public void EnableDevTestMode() => DevTestModeEnabled = true;

        /// <summary>禁用在测试阀称。</summary>
        public void DisableDevTestMode() => DevTestModeEnabled = false;

        /// <summary>test-only：设置调试 key → value。该字典不进入 PostStateHash。</summary>
        public void SetDebugValue(string key, string value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (_debugValuesInternal == null)
                _debugValuesInternal = new Dictionary<string, string>();
            _debugValuesInternal[key] = value;
        }

        /// <summary>test-only：读取调试 key 返回值；不存在 → null。</summary>
        public string TryGetDebugValue(string key)
        {
            if (key == null) return null;
            if (_debugValuesInternal == null) return null;
            return _debugValuesInternal.TryGetValue(key, out var v) ? v : null;
        }

        /// <summary>test-only：移除调试 key。</summary>
        public bool RemoveDebugValue(string key)
        {
            if (key == null || _debugValuesInternal == null) return false;
            return _debugValuesInternal.Remove(key);
        }

        /// <summary>test-only：谨慎暴露调试字典（按枚举器迭代）。该字典不进入 PostStateHash。</summary>
        public IEnumerable<KeyValuePair<string, string>> EnumerateDebugValues()
        {
            if (_debugValuesInternal == null) yield break;
            foreach (var kv in _debugValuesInternal) yield return kv;
        }

        /// <summary>添加一个 Tile（MAP-02 不做越界 / 重复检查，由 MAP-04 TileOccupancyService 接管）。</summary>
        public void AddTile(GridCoord tile)
        {
            if (!tile.IsInBounds(Definition.Size))
                throw new ArgumentOutOfRangeException(nameof(tile), tile,
                    $"Tile {tile} is out of bounds for map {Definition.Size}.");
            TilesInternal.Add(tile);
        }

        /// <summary>移除一个 Tile（不存在则返回 false）。</summary>
        public bool RemoveTile(GridCoord tile) => TilesInternal.Remove(tile);

        public void AddAnchor(AnchorZone zone)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            AnchorsInternal.Add(zone);
        }

        public bool RemoveAnchor(int zoneId)
        {
            for (int i = 0; i < AnchorsInternal.Count; i++)
            {
                if (AnchorsInternal[i].ZoneId == zoneId)
                {
                    AnchorsInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void AddRegion(MapRegion region)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            RegionsInternal.Add(region);
        }

        public bool RemoveRegion(int regionId)
        {
            for (int i = 0; i < RegionsInternal.Count; i++)
            {
                if (RegionsInternal[i].RegionId == regionId)
                {
                    RegionsInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // ──────────── MAP-09 新增入口（RegionStates / SpawnPoints）────────────

        /// <summary>添加一个 <see cref="MapRegionState"/>（由 <see cref="MapRegionService.Register"/> / 命令调用）。</summary>
        public void AddRegionState(MapRegionState regionState)
        {
            if (regionState == null) throw new ArgumentNullException(nameof(regionState));
            RegionStatesInternal.Add(regionState);
        }

        /// <summary>按 RegionId 移除 <see cref="MapRegionState"/>。</summary>
        public bool RemoveRegionState(int regionId)
        {
            for (int i = 0; i < RegionStatesInternal.Count; i++)
            {
                if (RegionStatesInternal[i].Definition.RegionIdValue.Value == regionId)
                {
                    RegionStatesInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>添加一个 <see cref="MapSpawnPoint"/>（由 <see cref="Starfall.Core.Map.Commands.PlaceSpawnPointCommand"/> 调用）。</summary>
        public void AddSpawnPoint(MapSpawnPoint spawnPoint)
        {
            if (spawnPoint.SpawnIdValue.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnPoint), spawnPoint,
                    "SpawnPoint.SpawnId must be >= 0.");
            // SpawnId 不重复
            for (int i = 0; i < SpawnPointsInternal.Count; i++)
            {
                if (SpawnPointsInternal[i].SpawnIdValue.Value == spawnPoint.SpawnIdValue.Value)
                    throw new InvalidOperationException(
                        $"Duplicate SpawnId: {spawnPoint.SpawnIdValue.Value}.");
            }
            SpawnPointsInternal.Add(spawnPoint);
        }

        /// <summary>按 SpawnId 移除 <see cref="MapSpawnPoint"/>。</summary>
        public bool RemoveSpawnPoint(int spawnId)
        {
            for (int i = 0; i < SpawnPointsInternal.Count; i++)
            {
                if (SpawnPointsInternal[i].SpawnIdValue.Value == spawnId)
                {
                    SpawnPointsInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void AddMapObject(MapObjectInstance obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            MapObjectsInternal.Add(obj);
        }

        // ──────────── MAP-11a 新增入口（LocalCVs）────────────

        /// <summary>添加 / 覆盖一个 tile 的 <see cref="LocalCollapseValue"/>。</summary>
        public void AddLocalCV(LocalCollapseValue lcv) => LocalCVsInternal[lcv.Coord] = lcv;

        /// <summary>按 tile 移除一个 <see cref="LocalCollapseValue"/>。</summary>
        public bool RemoveLocalCV(GridCoord coord) => LocalCVsInternal.Remove(coord);

        /// <summary>按 tile 读取 <see cref="LocalCollapseValue"/>；不存在 → null。</summary>
        public LocalCollapseValue? TryGetLocalCV(GridCoord coord)
        {
            if (LocalCVsInternal.TryGetValue(coord, out var v)) return v;
            return null;
        }

        public bool RemoveMapObject(int objectId)
        {
            for (int i = 0; i < MapObjectsInternal.Count; i++)
            {
                if (MapObjectsInternal[i].ObjectId == objectId)
                {
                    MapObjectsInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        // ──────────── MAP-12 新增入口（AnchorLinks）────────────

        /// <summary>添加一个 <see cref="AnchorLink"/>（Id 必须唯一，重复则抛 <see cref="InvalidOperationException"/>）。</summary>
        public void AddAnchorLink(AnchorLink link)
        {
            if (link == null) throw new ArgumentNullException(nameof(link));
            for (int i = 0; i < AnchorLinksInternal.Count; i++)
            {
                if (AnchorLinksInternal[i].Id.Equals(link.Id))
                    throw new InvalidOperationException($"Duplicate AnchorLink.Id: {link.Id}.");
            }
            AnchorLinksInternal.Add(link);
        }

        /// <summary>按 <see cref="AnchorLinkId"/> 移除 <see cref="AnchorLink"/>。</summary>
        public bool RemoveAnchorLink(AnchorLinkId linkId)
        {
            for (int i = 0; i < AnchorLinksInternal.Count; i++)
            {
                if (AnchorLinksInternal[i].Id.Equals(linkId))
                {
                    AnchorLinksInternal.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>按 <see cref="AnchorLinkId"/> 查找 <see cref="AnchorLink"/>；找到 → true。</summary>
        public bool TryGetAnchorLink(AnchorLinkId linkId, out AnchorLink link)
        {
            link = null;
            for (int i = 0; i < AnchorLinksInternal.Count; i++)
            {
                if (AnchorLinksInternal[i].Id.Equals(linkId))
                {
                    link = AnchorLinksInternal[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>派生视图：所有 AnchorLinks 升序按 <see cref="AnchorLinkId"/> 排序（仅快照；不修改源集合）。</summary>
        public List<AnchorLink> AllAnchorLinks
        {
            get
            {
                if (AnchorLinksInternal.Count <= 1) return new List<AnchorLink>(AnchorLinksInternal);
                var sorted = new List<AnchorLink>(AnchorLinksInternal);
                sorted.Sort((a, b) => string.CompareOrdinal(a.Id.Value, b.Id.Value));
                return sorted;
            }
        }

        // ──────────── 确定性哈希（按需计算）────────────

        /// <summary>
        /// doc2 MAP-02 确定性哈希（FNV-1a 64 位）。由 <see cref="MapStateHasher"/> 实现，
        /// 这里只是直传，避免循环依赖。
        /// </summary>
        public ulong PostStateHash => MapStateHasher.CalculateDeterministicHash(this);

        public override string ToString()
            => $"MapState(Def={Definition}, Ver={Version}, Layer={ActiveLayer}, CV={GlobalCollapseValue}, Stage={CurrentStage}, Tiles={TilesInternal.Count}, Anchors={AnchorsInternal.Count}, Regions={RegionsInternal.Count}, Objects={MapObjectsInternal.Count}, RegionStates={RegionStatesInternal.Count}, SpawnPoints={SpawnPointsInternal.Count}, LocalCVs={LocalCVsInternal.Count}, AnchorLinks={AnchorLinksInternal.Count})";
    }
}