using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Coordinates;

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
                GlobalCollapseValue = source.GlobalCollapseValue,
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

            return clone;
        }
    }
}
