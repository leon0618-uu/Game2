using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Height;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.5 <see cref="TileDefinition"/> 的全局登记表。
    ///
    /// <para/>
    /// **角色**：以 <see cref="MapState"/> 拥有的 <c>GridMap</c> 形式集中管理所有
    /// <see cref="TileDefinition"/>。提供按 <see cref="GridCoord"/> 查询、按
    /// <see cref="TileDefinition.TileId"/> 查询、添加、移除、遍历等接口。
    ///
    /// <para/>
    /// **确定性**：
    /// <list type="bullet">
    /// <item>所有遍历接口（<see cref="All"/>、<see cref="AllCoords"/>）按
    ///       <see cref="GridCoord.CompareTo"/> 升序输出（Y → X → Layer），符合 AGENTS.md §11。</item>
    /// <item>重复添加同 <see cref="GridCoord"/> 的 <see cref="TileDefinition"/> 抛
    ///       <see cref="ArgumentException"/>（构造期捕获，避免运行时歧义）。</item>
    /// <item>越界 <see cref="GridCoord"/> 抛 <see cref="ArgumentOutOfRangeException"/>。</item>
    /// </list>
    ///
    /// <para/>
    /// **职责边界**：
    /// <list type="bullet">
    /// <item>本类只持有"定义"（静态不可变配置），不持有运行时状态（稳定性 / 占用）。</item>
    /// <item>运行时状态由 <see cref="MapTileState"/> 持有，<see cref="MapStateLookupAdapter"/>
    ///       在构造时一次性把所有 <see cref="TileDefinition"/> 转换为查询表。</item>
    /// </list>
    /// </summary>
    public sealed class TileDefinitionRegistry
    {
        private readonly MapSize _size;
        private readonly Dictionary<GridCoord, TileDefinition> _byCoord;
        private readonly Dictionary<int, GridCoord> _byTileId;
        private readonly List<TileDefinition> _sortedCache;

        /// <summary>当前登记的 tile 数。</summary>
        public int Count => _byCoord.Count;

        /// <summary>地图尺寸（用于越界检查）。</summary>
        public MapSize Size => _size;

        public TileDefinitionRegistry(MapSize size)
        {
            if (size == null) throw new ArgumentNullException(nameof(size));
            _size = size;
            _byCoord = new Dictionary<GridCoord, TileDefinition>();
            _byTileId = new Dictionary<int, GridCoord>();
            _sortedCache = new List<TileDefinition>();
        }

        // ──────────── 添加 / 查询 / 移除 ────────────

        /// <summary>
        /// 注册一个 <see cref="TileDefinition"/>。
        /// 越界 / 重复 <see cref="GridCoord"/> / 重复 <see cref="TileDefinition.TileId"/>
        /// 时抛 <see cref="ArgumentException"/> 或 <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public void Register(TileDefinition definition)
        {
            if (!definition.Coord.IsInBounds(_size))
                throw new ArgumentOutOfRangeException(nameof(definition), definition,
                    $"Tile {definition.Coord} is out of bounds for map {_size}.");
            if (_byCoord.ContainsKey(definition.Coord))
                throw new ArgumentException(
                    $"A TileDefinition is already registered at {definition.Coord}.",
                    nameof(definition));
            if (_byTileId.ContainsKey(definition.TileId))
                throw new ArgumentException(
                    $"TileId {definition.TileId} is already registered.",
                    nameof(definition));

            _byCoord.Add(definition.Coord, definition);
            _byTileId.Add(definition.TileId, definition.Coord);
            _sortedCache.Add(definition);
            // 维持 sortedCache 按 (Y, X, Layer) 升序。
            _sortedCache.Sort((a, b) => a.Coord.CompareTo(b.Coord));
        }

        /// <summary>按 <see cref="GridCoord"/> 查找；未注册返回 false。</summary>
        public bool TryGetByCoord(GridCoord coord, out TileDefinition definition)
            => _byCoord.TryGetValue(coord, out definition);

        /// <summary>按 <see cref="TileDefinition.TileId"/> 查找；未注册返回 false。</summary>
        public bool TryGetById(int tileId, out TileDefinition definition)
        {
            if (_byTileId.TryGetValue(tileId, out var coord))
                return _byCoord.TryGetValue(coord, out definition);
            definition = default;
            return false;
        }

        /// <summary>按 <see cref="GridCoord"/> 移除；不存在返回 false。</summary>
        public bool Remove(GridCoord coord)
        {
            if (!_byCoord.TryGetValue(coord, out var def)) return false;
            _byCoord.Remove(coord);
            _byTileId.Remove(def.TileId);
            _sortedCache.Remove(def);
            return true;
        }

        /// <summary>按 <see cref="TileDefinition.TileId"/> 移除；不存在返回 false。</summary>
        public bool RemoveById(int tileId)
        {
            if (!_byTileId.TryGetValue(tileId, out var coord)) return false;
            return Remove(coord);
        }

        /// <summary>清空所有登记。</summary>
        public void Clear()
        {
            _byCoord.Clear();
            _byTileId.Clear();
            _sortedCache.Clear();
        }

        // ──────────── 确定性遍历 ────────────

        /// <summary>所有 <see cref="TileDefinition"/>，按 <see cref="GridCoord"/> 升序（Y → X → Layer）。</summary>
        public IReadOnlyList<TileDefinition> All()
        {
            // _sortedCache 已在 Register/Remove 时维持顺序；返回只读视图避免外部修改。
            return _sortedCache.AsReadOnly();
        }

        /// <summary>所有已登记的 <see cref="GridCoord"/>，按 <see cref="GridCoord.CompareTo"/> 升序。</summary>
        public IEnumerable<GridCoord> AllCoords()
        {
            foreach (var def in _sortedCache)
                yield return def.Coord;
        }

        // ──────────── 工厂 ────────────

        /// <summary>
        /// 从一组 <see cref="TileDefinition"/> 构造注册表的便捷方法。
        /// 任何 <see cref="TileDefinition"/> 越界或重复 <see cref="GridCoord"/> 都会抛异常。
        /// </summary>
        public static TileDefinitionRegistry Create(MapSize size, IEnumerable<TileDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var registry = new TileDefinitionRegistry(size);
            foreach (var def in definitions)
                registry.Register(def);
            return registry;
        }

        /// <summary>
        /// 简单工厂：构造一个完整的 <see cref="TileDefinition"/>，使用
        /// <see cref="TerrainRegistry.GetStandard"/> 取 <see cref="TerrainDefinition"/> 默认值。
        /// </summary>
        public static TileDefinition Make(
            int tileId,
            GridCoord coord,
            TerrainType terrainType,
            HeightLevel height = default,
            int? baseMoveCost = null,
            bool? blocksMovement = null,
            bool? blocksVision = null,
            bool? blocksProjectile = null,
            TileTags tags = TileTags.None)
        {
            var terrain = TerrainRegistry.GetStandard(terrainType);
            return new TileDefinition(
                tileId: tileId,
                coord: coord,
                terrainType: terrainType,
                terrain: terrain,
                height: height,
                baseMoveCost: baseMoveCost,
                blocksMovement: blocksMovement,
                blocksVision: blocksVision,
                blocksProjectile: blocksProjectile,
                coverLevel: null,
                coverDirections: null,
                phasePairTileId: null,
                tags: tags);
        }
    }
}