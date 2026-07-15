using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 出生点查询服务（静态方法）。
    ///
    /// <para/>
    /// 给定 <see cref="MapState"/> 与 side，返回当前可用的 <see cref="MapSpawnPoint"/> 列表。
    /// "可用" 定义：<see cref="MapSpawnPoint.Active"/> == true 且
    /// <see cref="MapSpawnPoint.OwnerSide"/> 等于查询 side。
    ///
    /// <para/>
    /// 同时也支持"按 region 找 spawn"（用于 <c>MapRegionService.Register</c> 自动生成 spawn）。
    /// </summary>
    public static class MapSpawnService
    {
        /// <summary>
        /// 查找指定 side 的所有可用 spawn（按 SpawnId 升序）。
        /// </summary>
        public static IReadOnlyList<MapSpawnPoint> GetAvailableSpawns(MapState mapState, int side)
        {
            if (mapState == null)
                return System.Array.Empty<MapSpawnPoint>();
            var list = new List<MapSpawnPoint>();
            foreach (var s in mapState.SpawnPoints)
            {
                if (s.Active && s.OwnerSide == side)
                    list.Add(s);
            }
            list.Sort((a, b) => a.SpawnIdValue.CompareTo(b.SpawnIdValue));
            return list;
        }

        /// <summary>
        /// 查找指定 region 内（<see cref="MapSpawnPoint.RegionIdValue"/> == regionId）的所有 spawn（按 SpawnId 升序）。
        /// </summary>
        public static IReadOnlyList<MapSpawnPoint> GetSpawnsInRegion(MapState mapState, int regionId)
        {
            if (mapState == null)
                return System.Array.Empty<MapSpawnPoint>();
            var list = new List<MapSpawnPoint>();
            foreach (var s in mapState.SpawnPoints)
            {
                if (s.RegionIdValue == regionId)
                    list.Add(s);
            }
            list.Sort((a, b) => a.SpawnIdValue.CompareTo(b.SpawnIdValue));
            return list;
        }

        /// <summary>
        /// 检查某个 (coord, side) 是否存在空闲 spawn。
        /// </summary>
        public static bool HasFreeSpawnAt(MapState mapState, GridCoord coord, int side)
        {
            if (mapState == null) return false;
            foreach (var s in mapState.SpawnPoints)
            {
                if (s.Active && s.OwnerSide == side && s.Coord.Equals(coord))
                    return true;
            }
            return false;
        }
    }
}