using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.8 单位 / 对象的 tile 占用服务（static, single-map-global）。
    ///
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item>把 <c>unitId</c> 放在指定 <see cref="Footprint"/> 形状的 anchor 上。</item>
    /// <item>移除 <c>unitId</c> 占用的所有 tile（<see cref="Footprint"/> 内的全部格）。</item>
    /// <item>查询指定坐标是否被占用 / 被哪个单位或对象占用。</item>
    /// </list>
    ///
    /// <para/>
    /// **存储结构**（static 字段，单一进程全局）：
    /// <list type="bullet">
    /// <item><c>Dictionary&lt;int, List&lt;GridCoord&gt;&gt; _unitCells</c>：unitId → cells。</item>
    /// <item><c>Dictionary&lt;GridCoord, int&gt; _cellToUnit</c>：cell → unitId（反向索引）。</item>
    /// <item><c>Dictionary&lt;int, List&lt;GridCoord&gt;&gt; _objectCells</c>：objectId → cells。</item>
    /// <item><c>Dictionary&lt;GridCoord, int&gt; _cellToObject</c>：cell → objectId（反向索引）。</item>
    /// </list>
    ///
    /// <para/>
    /// **tile 拓扑信息依赖**（attach 模式）：
    /// <list type="bullet">
    /// <item><see cref="AttachTileDefinitionRegistry"/>：挂载 <see cref="TileDefinitionRegistry"/>，
    ///       之后所有 <see cref="TryPlaceUnit"/> / <see cref="TryPlaceObject"/> 通过
    ///       <see cref="TileDefinition.BlocksMovement"/> 判断 cell 是否阻挡。</item>
    /// <item><see cref="AttachRuntimeStates"/>：挂载 <c>Dictionary&lt;GridCoord, MapTileState&gt;</c>，
    ///       之后通过 <see cref="MapTileState.Stability"/> 判断 cell 是否已坍塌。</item>
    /// <item>未挂载 TileDefinitionRegistry 时，cell 阻挡 = "不在 <see cref="MapState.Tiles"/>"，
    ///       默认在 Tiles 列表内的 cell 视为可通行的 Plain（向后兼容旧测试）。</item>
    /// <item>未挂载 RuntimeStates 时，所有 cell 默认 <c>Stability = 100</c>（稳定）。</item>
    /// </list>
    ///
    /// <para/>
    /// **失败语义**（<see cref="TryPlaceUnit"/> / <see cref="TryPlaceObject"/>）：
    /// <list type="bullet">
    /// <item>false = (a) anchor 全部 cell 越界，
    ///       (b) 任一 cell 已被单元或对象占用，
    ///       (c) 任一 cell 的 <see cref="TileDefinition.BlocksMovement"/> = true，
    ///       (d) 任一 cell 的 <see cref="MapTileState.Stability"/> = 0。</item>
    /// <item>失败时不修改任何状态（atomic）。</item>
    /// </list>
    ///
    /// <para/>
    /// **多 MapState 支持**：占用记录本身不绑定 MapState（全局唯一），
    /// 但跨 MapState 的 cell 坐标仍可区分（<see cref="GridCoord.Layer"/> +
    /// <c>MapState.Definition.Size</c> 越界检查会在不同地图间自动失败）。
    /// 测试应在 [SetUp] / [TearDown] 调用 <see cref="Clear"/> + <see cref="DetachAll"/> 重置。
    /// </summary>
    public static class TileOccupancyService
    {
        // ──────────── 静态占用记录 ────────────

        private static readonly object _gate = new object();
        private static readonly Dictionary<int, List<GridCoord>> _unitCells
            = new Dictionary<int, List<GridCoord>>();
        private static readonly Dictionary<GridCoord, int> _cellToUnit
            = new Dictionary<GridCoord, int>();
        private static readonly Dictionary<int, List<GridCoord>> _objectCells
            = new Dictionary<int, List<GridCoord>>();
        private static readonly Dictionary<GridCoord, int> _cellToObject
            = new Dictionary<GridCoord, int>();

        // ──────────── attach（TileDefinitionRegistry + MapTileState 字典）────────────

        private static readonly Dictionary<MapState, TileDefinitionRegistry> _registryAttach
            = new Dictionary<MapState, TileDefinitionRegistry>();
        private static readonly Dictionary<MapState, Dictionary<GridCoord, MapTileState>> _runtimeStatesAttach
            = new Dictionary<MapState, Dictionary<GridCoord, MapTileState>>();

        /// <summary>把 <see cref="TileDefinitionRegistry"/> 挂载到 <paramref name="map"/>。
        /// 多次调用会覆盖前一次。生产装配层（MAP-05/08）调用；测试在 [SetUp] 调用。</summary>
        public static void AttachTileDefinitionRegistry(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _registryAttach[map] = registry;
            }
        }

        /// <summary>挂载运行时 <see cref="MapTileState"/> 字典到 <paramref name="map"/>。</summary>
        public static void AttachRuntimeStates(
            MapState map,
            Dictionary<GridCoord, MapTileState> states)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (states == null) throw new ArgumentNullException(nameof(states));
            lock (_gate)
            {
                _runtimeStatesAttach[map] = states;
            }
        }

        /// <summary>解除 <paramref name="map"/> 的所有挂载并清空该 map 上的占用记录。
        /// 测试 [TearDown] 调用。</summary>
        public static void DetachAll(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                _registryAttach.Remove(map);
                _runtimeStatesAttach.Remove(map);
                // 同时清掉该 map 范围内所有占用记录。
                var removedUnits = new List<int>();
                foreach (var kv in _unitCells)
                {
                    bool allInBounds = true;
                    foreach (var c in kv.Value)
                    {
                        if (!c.IsInBounds(map.Definition.Size)) { allInBounds = false; break; }
                    }
                    if (allInBounds) removedUnits.Add(kv.Key);
                }
                foreach (var u in removedUnits)
                {
                    if (_unitCells.TryGetValue(u, out var cells))
                        foreach (var c in cells) _cellToUnit.Remove(c);
                    _unitCells.Remove(u);
                }
                var removedObjects = new List<int>();
                foreach (var kv in _objectCells)
                {
                    bool allInBounds = true;
                    foreach (var c in kv.Value)
                    {
                        if (!c.IsInBounds(map.Definition.Size)) { allInBounds = false; break; }
                    }
                    if (allInBounds) removedObjects.Add(kv.Key);
                }
                foreach (var o in removedObjects)
                {
                    if (_objectCells.TryGetValue(o, out var cells))
                        foreach (var c in cells) _cellToObject.Remove(c);
                    _objectCells.Remove(o);
                }
            }
        }

        /// <summary>清空所有占用记录 + 挂载（极端测试 / 进程级 reset）。</summary>
        public static void Clear()
        {
            lock (_gate)
            {
                _unitCells.Clear();
                _cellToUnit.Clear();
                _objectCells.Clear();
                _cellToObject.Clear();
                _registryAttach.Clear();
                _runtimeStatesAttach.Clear();
            }
        }

        // ──────────── 放置 / 移除单位 ────────────

        /// <summary>
        /// 把 <paramref name="unitId"/> 放在 anchor 坐标，footprint = SingleCell / TwoByTwo / ThreeByThree。
        /// </summary>
        /// <returns>true = 成功放置；false = 失败（详见类注释失败语义）。</returns>
        public static bool TryPlaceUnit(MapState map, int unitId, Footprint footprint, GridCoord anchor)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (unitId < 0)
                throw new ArgumentOutOfRangeException(nameof(unitId), unitId,
                    "unitId must be >= 0 (negative reserved for sentinel).");

            lock (_gate)
            {
                if (_unitCells.ContainsKey(unitId)) return false;

                IReadOnlyList<GridCoord> cells;
                try
                {
                    cells = FootprintExtensions.GetOccupiedCells(footprint, anchor, map.Definition.Size);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }

                if (!CanOccupyCells(map, cells)) return false;

                var cellList = new List<GridCoord>(cells);
                _unitCells[unitId] = cellList;
                foreach (var c in cells)
                    _cellToUnit[c] = unitId;
                return true;
            }
        }

        /// <summary>
        /// 移走 <paramref name="unitId"/> 占用的所有 tile（Footprint）。
        /// </summary>
        /// <returns>true = 成功移除；false = unitId 未占用任何 tile。</returns>
        public static bool TryRemoveUnit(MapState map, int unitId)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (!_unitCells.TryGetValue(unitId, out var cells)) return false;
                foreach (var c in cells)
                    _cellToUnit.Remove(c);
                _unitCells.Remove(unitId);
                return true;
            }
        }

        // ──────────── 放置 / 移除对象 ────────────

        /// <summary>放置一个对象（<see cref="MapState.MapObjects"/> 占位）；语义同 <see cref="TryPlaceUnit"/>。</summary>
        public static bool TryPlaceObject(MapState map, int objectId, Footprint footprint, GridCoord anchor)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (objectId < 0)
                throw new ArgumentOutOfRangeException(nameof(objectId), objectId,
                    "objectId must be >= 0.");

            lock (_gate)
            {
                if (_objectCells.ContainsKey(objectId)) return false;

                IReadOnlyList<GridCoord> cells;
                try
                {
                    cells = FootprintExtensions.GetOccupiedCells(footprint, anchor, map.Definition.Size);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }

                if (!CanOccupyCells(map, cells)) return false;

                var cellList = new List<GridCoord>(cells);
                _objectCells[objectId] = cellList;
                foreach (var c in cells)
                    _cellToObject[c] = objectId;
                return true;
            }
        }

        /// <summary>移走对象占用的所有 tile；false = objectId 未占用。</summary>
        public static bool TryRemoveObject(MapState map, int objectId)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (!_objectCells.TryGetValue(objectId, out var cells)) return false;
                foreach (var c in cells)
                    _cellToObject.Remove(c);
                _objectCells.Remove(objectId);
                return true;
            }
        }

        // ──────────── 查询 ────────────

        /// <summary>查询指定 <see cref="GridCoord"/> 是否被任一 footprint 占用（单元或对象）。</summary>
        public static bool IsOccupied(MapState map, GridCoord coord)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                return _cellToUnit.ContainsKey(coord) || _cellToObject.ContainsKey(coord);
            }
        }

        /// <summary>查询指定 cell 的占用单位 id；null = 无单位占用。</summary>
        public static int? GetOccupantUnit(MapState map, GridCoord coord)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (_cellToUnit.TryGetValue(coord, out var uid)) return uid;
                return null;
            }
        }

        /// <summary>查询指定 cell 的占用对象 id；null = 无对象占用。</summary>
        public static int? GetOccupantObject(MapState map, GridCoord coord)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                if (_cellToObject.TryGetValue(coord, out var oid)) return oid;
                return null;
            }
        }

        /// <summary>查询 <paramref name="unitId"/> 当前占用的全部 cells（Footprint 内）；null = 未占用。</summary>
        public static IReadOnlyList<GridCoord> GetUnitCells(int unitId)
        {
            lock (_gate)
            {
                if (_unitCells.TryGetValue(unitId, out var cells))
                    return cells.AsReadOnly();
                return null;
            }
        }

        /// <summary>查询 <paramref name="objectId"/> 当前占用的全部 cells；null = 未占用。</summary>
        public static IReadOnlyList<GridCoord> GetObjectCells(int objectId)
        {
            lock (_gate)
            {
                if (_objectCells.TryGetValue(objectId, out var cells))
                    return cells.AsReadOnly();
                return null;
            }
        }

        // ──────────── 内部：cell 合法性检查 ────────────

        /// <summary>所有 cell 是否均可被占用（越界 / 已占用 / 阻挡 / 坍塌 任一失败 → false）。</summary>
        private static bool CanOccupyCells(MapState map, IReadOnlyList<GridCoord> cells)
        {
            foreach (var c in cells)
            {
                if (_cellToUnit.ContainsKey(c)) return false;
                if (_cellToObject.ContainsKey(c)) return false;
                if (!IsCellPassable(map, c)) return false;
            }
            return true;
        }

        /// <summary>指定 cell 在 <paramref name="map"/> 中是否可通行（结合 BoundsMovement + Stability）。</summary>
        public static bool IsCellPassable(MapState map, GridCoord coord)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            lock (_gate)
            {
                // 已占用 → 不可通过（被单元或对象占据）。
                if (_cellToUnit.ContainsKey(coord)) return false;
                if (_cellToObject.ContainsKey(coord)) return false;

                // 1) TileDefinitionRegistry 优先（更精确）。
                if (_registryAttach.TryGetValue(map, out var registry))
                {
                    if (!registry.TryGetByCoord(coord, out var def))
                    {
                        // 单元在 MapState.Tiles 中但未在 registry 中：视为不可通过。
                        // 我们不要求 map.Tiles 与 registry 完全一致；只信任 registry。
                        return false;
                    }
                    if (def.BlocksMovement) return false;
                    // 2) 检查稳定性（如挂载了 RuntimeStates）。
                    if (_runtimeStatesAttach.TryGetValue(map, out var states)
                        && states.TryGetValue(coord, out var ts)
                        && ts.Stability <= 0)
                    {
                        return false;
                    }
                    return true;
                }

                // 3) 无 registry：回退到 MapState.Tiles（仅坐标匹配）。
                if (!ContainsCoord(map.Tiles, coord)) return false;
                // 4) 仍检查 RuntimeStates（如挂载）。
                if (_runtimeStatesAttach.TryGetValue(map, out var states2)
                    && states2.TryGetValue(coord, out var ts2)
                    && ts2.Stability <= 0)
                {
                    return false;
                }
                return true;
            }
        }
 

        // 兼容 IReadOnlyList.Contains 在某些 .NET 版本上的扩展方法歧义。
        private static bool ContainsCoord(IReadOnlyList<GridCoord> list, GridCoord coord)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Equals(coord)) return true;
            return false;
        }
    }
}