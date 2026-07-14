using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Cover;
using Starfall.Core.Map.Height;
using Starfall.Core.Map.LineOfSight;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Tile
{
    /// <summary>
    /// doc2 MAP-04 §4.9 <see cref="MapState"/> → MAP-06 三接口的适配器。
    ///
    /// <para/>
    /// **角色**：把 <see cref="MapState"/> 上挂载的 <see cref="TileDefinition"/> 数据
    /// （通过 <see cref="TileOccupancyService.AttachTileDefinitionRegistry"/> 注入）
    /// 转换为 MAP-06 已定义的三个查询接口（<see cref="IHeightLookup"/> /
    /// <see cref="ICoverLookup"/> / <see cref="IBlockingLookup"/>），从而让
    /// <see cref="LineOfSightService"/> / <see cref="CoverQueryService"/> /
    /// <see cref="HeightTraversalService"/> 不需要直接访问 <see cref="MapState"/>
    /// 的内部字段。
    ///
    /// <para/>
    /// **装配层契约**：
    /// <list type="number">
    /// <item>在调用 <see cref="LineOfSightService.ComputeLineOfSight"/> 之前，
    ///       必须先 <see cref="TileOccupancyService.AttachTileDefinitionRegistry"/>(map, registry)
    ///       把 <see cref="TileDefinitionRegistry"/> 挂到 <paramref name="map"/> 上。</item>
    /// <item>然后 <c>new MapStateLookupAdapter(map)</c> 一次性把所有
    ///       <see cref="TileDefinition"/> 转换为三个 lookup。</item>
    /// <item>把 adapter 传给 <see cref="LineOfSightService.ComputeLineOfSight"/>
    ///       (它接受 <see cref="IHeightLookup"/>, <see cref="ICoverLookup"/>,
    ///       <see cref="IBlockingLookup"/>) 即可。</item>
    /// </list>
    ///
    /// <para/>
    /// **本轮（MAP-04）**：本类只做"实现 + 单元测试"，不实际调用
    /// <see cref="LineOfSightService"/> —— 装配由 MAP-05 / MAP-08 真正接入。
    /// </summary>
    public sealed class MapStateLookupAdapter : IHeightLookup, ICoverLookup, IBlockingLookup
    {
        private readonly MapState _map;
        private readonly Dictionary<GridCoord, int> _heights;
        private readonly Dictionary<GridCoord, CoverLevel> _covers;
        private readonly HashSet<GridCoord> _blocksLineOfSight;

        /// <summary>构造时一次性从挂载的 <see cref="TileDefinitionRegistry"/> 提取所有 lookup 数据。</summary>
        /// <param name="map"><see cref="MapState"/>；必须已通过
        /// <see cref="TileOccupancyService.AttachTileDefinitionRegistry"/> 挂载了 registry。</param>
        public MapStateLookupAdapter(MapState map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));

            _map = map;
            _heights = new Dictionary<GridCoord, int>();
            _covers = new Dictionary<GridCoord, CoverLevel>();
            _blocksLineOfSight = new HashSet<GridCoord>();

            // 通过 TileOccupancyService 的 attach 字典查找挂载的 registry。
            // 该 attach 是 MAP-05/08 装配层或测试 [SetUp] 写入的。
            // 我们使用反射来访问 private static 字典是不优雅的；因此提供
            // 一个轻量级的"快照注入"：构造时也可以直接传入 registry。
            // 见 MapStateLookupAdapter(MapState, TileDefinitionRegistry) 重载。
            throw new InvalidOperationException(
                "MapStateLookupAdapter requires explicit TileDefinitionRegistry injection. " +
                "Use the MapStateLookupAdapter(MapState, TileDefinitionRegistry) constructor instead. " +
                "(MAP-05/08 装配层将提供更便捷的 factory.)");
        }

        /// <summary>显式传入 <see cref="TileDefinitionRegistry"/> 的构造重载（推荐用于装配与测试）。</summary>
        public MapStateLookupAdapter(MapState map, TileDefinitionRegistry registry)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            _map = map;
            _heights = new Dictionary<GridCoord, int>();
            _covers = new Dictionary<GridCoord, CoverLevel>();
            _blocksLineOfSight = new HashSet<GridCoord>();

            foreach (var def in registry.All())
            {
                _heights[def.Coord] = def.Height.Value;
                _covers[def.Coord] = def.CoverLevel;
                if (def.BlocksVision || def.CoverLevel == CoverLevel.Full)
                    _blocksLineOfSight.Add(def.Coord);
            }
        }

        // ──────────── IHeightLookup ────────────

        /// <summary>取指定坐标的高度值（[0, 4]）；越界或未注册返回 0（地面层）。</summary>
        public int GetHeight(GridCoord coord)
        {
            if (_heights.TryGetValue(coord, out var h)) return h;
            return 0;
        }

        // ──────────── ICoverLookup ────────────

        /// <summary>取指定坐标的掩体等级；null = 无掩体信息（视为 None）。</summary>
        public CoverLevel? GetCover(GridCoord coord)
        {
            if (_covers.TryGetValue(coord, out var c)) return c;
            return null;
        }

        // ──────────── IBlockingLookup ────────────

        /// <summary>true = 该 tile 完全阻挡视线（BlocksVision 或 Full Cover）。</summary>
        public bool BlocksLineOfSight(GridCoord coord) => _blocksLineOfSight.Contains(coord);

        // ──────────── 诊断 ────────────

        /// <summary>已缓存的高度条目数（用于测试断言）。</summary>
        public int HeightEntryCount => _heights.Count;

        /// <summary>已缓存的掩体条目数（用于测试断言）。</summary>
        public int CoverEntryCount => _covers.Count;

        /// <summary>已缓存的阻挡视线条目数（用于测试断言）。</summary>
        public int BlockingEntryCount => _blocksLineOfSight.Count;
    }
}