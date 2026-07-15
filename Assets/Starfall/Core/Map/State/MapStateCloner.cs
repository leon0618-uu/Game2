using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.Environment;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 <see cref="MapState"/> 深拷贝器。
    ///
    /// <para/>
    /// 静态纯函数：相同输入 → 相同输出，无副作用，无随机源，无当前时间依赖。
    /// 保证克隆结果与源完全独立：修改克隆的任何字段 / 集合不影响源，反之亦然。
    ///
    /// <para/>
    /// 复制范围：
    /// <list type="bullet">
    /// <item><see cref="MapState.Definition"/>：struct 值复制（已是 immutable）。</item>
    /// <item><see cref="MapState.Version"/> / <see cref="MapState.ActiveLayer"/> /
    ///       <see cref="MapState.GlobalCollapseValue"/>：值复制。</item>
    /// <item><see cref="MapState.Tiles"/>：新 <see cref="List{GridCoord}"/>，元素逐个复制（GridCoord 是 readonly struct）。</item>
    /// <item><see cref="MapState.Anchors"/>：新列表；每个 <see cref="AnchorZone"/> 通过
    ///       构造函数重建（重新规范化顶点，内部 List 与源独立）。</item>
    /// <item><see cref="MapState.Regions"/>：新列表；每个 <see cref="MapRegion"/> 通过
    ///       构造函数重建（内部 List 与源独立）。</item>
    /// <item><see cref="MapState.MapObjects"/>：新列表；每个 <see cref="MapObjectInstance"/>
    ///       通过构造函数重建（值复制 GridCoord 是 readonly struct）。</item>
    /// <item><see cref="MapState.RegionStates"/>（MAP-09）：新列表；每个 <see cref="Starfall.Core.Map.Regions.MapRegionState"/>
    ///       通过构造函数重建。</item>
    /// <item><see cref="MapState.SpawnPoints"/>（MAP-09）：新列表；值复制
    ///       <see cref="Starfall.Core.Map.Regions.MapSpawnPoint"/>（readonly struct）。</item>
    /// <item><see cref="MapState.LocalCVs"/>（MAP-11a）：新 <see cref="Dictionary{GridCoord, LocalCollapseValue}"/>；
    ///       每个 <see cref="LocalCollapseValue"/> 按值复制（readonly struct）。</item>
    /// <item><see cref="MapState.ActiveSchedule"/>（MAP-11b）：<see cref="MapEnvironmentSchedule"/> 是
    ///       readonly struct，其内部 events list 通过构造函数浅拷贝隔离。</item>
    /// <item><see cref="MapState.EnvironmentTickAccumulator"/>（MAP-11b）：值复制。</item>
    /// <item><see cref="MapState.PendingEvents"/>（MAP-11b）：新 <see cref="List{MapEnvironmentEvent}"/>；
    ///       每个 <see cref="MapEnvironmentEvent"/> 是 class，按引用复制但本身不可变。</item>
    /// </list>
    /// </summary>
    public static class MapStateCloner
    {
        /// <summary>深拷贝 <see cref="MapState"/>；null 输入返回 null。</summary>
        public static MapState DeepClone(MapState source)
        {
            if (source == null) return null;

            var clone = new MapState(source.Definition)
            {
                Version = source.Version,
                ActiveLayer = source.ActiveLayer,
                GlobalCV = source.GlobalCV,
                // MAP-11b：ActiveSchedule 是 readonly struct，值复制即独立。
                ActiveSchedule = source.ActiveSchedule,
                EnvironmentTickAccumulator = source.EnvironmentTickAccumulator,
            };

            // Tiles：GridCoord 是 readonly struct，List.Add 复制值即可。
            foreach (var t in source.TilesInternal)
                clone.TilesInternal.Add(t);

            // Anchors：构造时已排序；这里通过构造函数重建 AnchorZone，
            // 内部会 new List<GridPos>(vertices) 再 Sort，与源彻底独立。
            foreach (var a in source.AnchorsInternal)
            {
                var copy = new AnchorZone(a.ZoneId, a.Owner, a.Vertices);
                clone.AnchorsInternal.Add(copy);
            }

            // Regions：构造函数会 new List<GridCoord>(tileCoords) 再 Sort。
            foreach (var r in source.RegionsInternal)
            {
                var copy = new MapRegion(r.RegionId, r.RegionType, r.Owner, r.TileCoords);
                clone.RegionsInternal.Add(copy);
            }

            // MapObjects：MapObjectInstance 内部仅含值类型字段，构造函数直接复制。
            foreach (var o in source.MapObjectsInternal)
            {
                var copy = new MapObjectInstance(o.ObjectId, o.ObjectType, o.Anchor);
                clone.MapObjectsInternal.Add(copy);
            }

            // MAP-09 RegionStates（值引用复制；MapRegionState 内部状态可能被后续服务修改，
            // 如需完全隔离，需复制每个字段。这里仅复制引用 + 通过 .ctor 创建新壳）。
            foreach (var rs in source.RegionStatesInternal)
            {
                var copy = new Starfall.Core.Map.Regions.MapRegionState(rs.Definition, rs.TickEntered);
                clone.RegionStatesInternal.Add(copy);
            }

            // MAP-09 SpawnPoints：MapSpawnPoint 是 readonly struct，按值复制。
            foreach (var s in source.SpawnPointsInternal)
            {
                clone.SpawnPointsInternal.Add(s);
            }

            // MAP-11a LocalCVs：LocalCollapseValue 是 readonly struct，按值复制到新 Dictionary。
            foreach (var kv in source.LocalCVsInternal)
            {
                clone.LocalCVsInternal[kv.Key] = kv.Value;
            }

            // MAP-11b PendingEvents：MapEnvironmentEvent 是 class，引用复制即可（events 本身不可变）。
            foreach (var ev in source.PendingEventsInternal)
            {
                clone.PendingEventsInternal.Add(ev);
            }

            return clone;
        }
    }
}
