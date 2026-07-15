using System;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map
{
    /// <summary>
    /// doc2 MAP-03 §21.1 <c>MapEvent</c> 类型。
    /// <para/>
    /// 地图命令（<see cref="Starfall.Core.Map.Commands.IMapCommand"/>）执行成功的副作用
    /// 事件载体；与 <see cref="Starfall.Core.Command.BattleEvent"/> 平行但**不互通**：
    /// <list type="bullet">
    /// <item><see cref="Starfall.Core.Command.BattleEvent"/> 表达战斗状态变化（HP / 状态 / 回合），</item>
    /// <item><c>MapEvent</c> 表达纯地图状态变化（地形 / 锚点 / 路径图 / LOS）。</item>
    /// </list>
    /// <para/>
    /// **本类型是 <c>readonly struct</c>**；与 <see cref="MapCommandResult"/> 同模式
    /// （失败时返回空 list，不抛异常）。
    ///
    /// <para/>
    /// **稳定排序**：
    /// 所有 <see cref="MapEvent"/> 字段写入顺序固定（按 builder 顺序构造），且
    /// 实现 <see cref="IComparable{T}"/> 以便 <see cref="List{T}.Sort()"/> /
    /// <see cref="MapCommandResult.Events"/> 的读取方保证确定性排序。
    /// 排序键：<c>(Kind byte, Coord.Y, Coord.X, Coord.Layer byte, NumericId, Description byte-order)</c>。
    ///
    /// <para/>
    /// **与 <see cref="Starfall.Core.Map.State.MapState.PostStateHash"/> 的关系**：
    /// 本类**不直接进入 MapState 哈希作用域**（map state 哈希由 <c>MapStateHasher</c>
    /// 计算 runtime 集合字段而非 event 列表）。但每个事件的 <c>Coord</c> /
    /// <c>RegionId</c> / <c>AnchorId</c> 等字段会间接在
    /// <see cref="Starfall.Core.Map.State.MapState"/> 集合层反应，使 map state 哈希
    /// 与事件流保持一致（与 ADR-0003 §4 Event 一致性一致）。
    /// </summary>
    public readonly struct MapEvent : IEquatable<MapEvent>, IComparable<MapEvent>
    {
        // ──────────── 事件种类 ────────────

        /// <summary>事件种类（doc2 §21.1 枚举）。</summary>
        public readonly MapEventKind Kind;

        // ──────────── 负载字段（按 Kind 选择填充，其余 null/default）────────────

        /// <summary>事件对应 tile（OnTileChanged / OnTileStabilityChanged / OnPathGraphInvalidated 等）。</summary>
        public readonly GridCoord? Coord;

        /// <summary>事件对应 region id（OnRegionChanged / OnConstellationPolygonCreated）。</summary>
        public readonly int? RegionId;

        /// <summary>事件对应 anchor zone id（OnAnchorLinkCreated / OnRegionChanged）。</summary>
        public readonly int? AnchorId;

        /// <summary>事件对应 link id（OnAnchorLinkCreated）。</summary>
        public readonly int? LinkId;

        /// <summary>事件对应旧 / 新稳定性值 / CV 值（OnTileStabilityChanged / OnGlobalCVChanged）。</summary>
        public readonly int? OldValue;

        /// <summary>事件对应新稳定性值 / CV 值（OnTileStabilityChanged / OnGlobalCVChanged）。</summary>
        public readonly int? NewValue;

        /// <summary>可读描述（用于日志 / 调试；不影响稳定性）。</summary>
        public readonly string Description;

        // ──────────── 构造 ────────────

        public MapEvent(
            MapEventKind kind,
            GridCoord? coord = null,
            int? regionId = null,
            int? anchorId = null,
            int? linkId = null,
            int? oldValue = null,
            int? newValue = null,
            string description = null)
        {
            Kind = kind;
            Coord = coord;
            RegionId = regionId;
            AnchorId = anchorId;
            LinkId = linkId;
            OldValue = oldValue;
            NewValue = newValue;
            // 字符串 null 归一化为空串以保证 to-string 显示稳定；事件 compareTo 不依赖 Description。
            Description = description ?? string.Empty;
        }

        // ──────────── 工厂 ────────────

        public static MapEvent TileChanged(GridCoord coord, string description = null)
            => new MapEvent(MapEventKind.OnTileChanged, coord: coord, description: description);

        public static MapEvent RegionChanged(int regionId, string description = null)
            => new MapEvent(MapEventKind.OnRegionChanged, regionId: regionId, description: description);

        public static MapEvent PathGraphInvalidated(GridCoord? origin = null, string description = null)
            => new MapEvent(MapEventKind.OnPathGraphInvalidated, coord: origin, description: description);

        public static MapEvent LineOfSightInvalidated(GridCoord? origin = null, string description = null)
            => new MapEvent(MapEventKind.OnLineOfSightInvalidated, coord: origin, description: description);

        public static MapEvent AnchorLinkCreated(int anchorId, int linkId, string description = null)
            => new MapEvent(
                MapEventKind.OnAnchorLinkCreated,
                anchorId: anchorId,
                linkId: linkId,
                description: description);

        public static MapEvent ConstellationPolygonCreated(int regionId, string description = null)
            => new MapEvent(
                MapEventKind.OnConstellationPolygonCreated,
                regionId: regionId,
                description: description);

        public static MapEvent GlobalCVChanged(int oldValue, int newValue, string description = null)
            => new MapEvent(
                MapEventKind.OnGlobalCVChanged,
                oldValue: oldValue,
                newValue: newValue,
                description: description);

        public static MapEvent TileStabilityChanged(GridCoord coord, int oldValue, int newValue, string description = null)
            => new MapEvent(
                MapEventKind.OnTileStabilityChanged,
                coord: coord,
                oldValue: oldValue,
                newValue: newValue,
                description: description);

        public static MapEvent MapDebugValueChanged(string key, string description = null)
            => new MapEvent(MapEventKind.OnMapDebugValueChanged, description: description);

        public static MapEvent MapObjectPlaced(int anchorId, string description = null)
            => new MapEvent(
                MapEventKind.OnMapObjectPlaced,
                anchorId: anchorId,
                description: description);

        public static MapEvent MapObjectRemoved(int anchorId, string description = null)
            => new MapEvent(
                MapEventKind.OnMapObjectRemoved,
                anchorId: anchorId,
                description: description);

        public static MapEvent UnitMovedOnMap(int anchorId, GridCoord from, GridCoord to, string description = null)
            => new MapEvent(
                MapEventKind.OnUnitMovedOnMap,
                coord: to,
                anchorId: anchorId,
                description: description);

        public static MapEvent PhaseCompressed(GridCoord coord, int anchorId, string description = null)
            => new MapEvent(
                MapEventKind.OnPhaseCompressed,
                coord: coord,
                anchorId: anchorId,
                description: description);

        public static MapEvent PhaseDecompressed(GridCoord coord, int anchorId, string description = null)
            => new MapEvent(
                MapEventKind.OnPhaseDecompressed,
                coord: coord,
                anchorId: anchorId,
                description: description);

        // ──────────── 等值 / 排序 ────────────

        public bool Equals(MapEvent other)
            => Kind == other.Kind
               && Nullable.Equals(Coord, other.Coord)
               && Nullable.Equals(RegionId, other.RegionId)
               && Nullable.Equals(AnchorId, other.AnchorId)
               && Nullable.Equals(LinkId, other.LinkId)
               && Nullable.Equals(OldValue, other.OldValue)
               && Nullable.Equals(NewValue, other.NewValue)
               && string.Equals(Description, other.Description, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is MapEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (int)Kind;
                h = (h * 397) ^ (Coord?.GetHashCode() ?? 0);
                h = (h * 397) ^ (RegionId ?? 0);
                h = (h * 397) ^ (AnchorId ?? 0);
                h = (h * 397) ^ (LinkId ?? 0);
                h = (h * 397) ^ (OldValue ?? 0);
                h = (h * 397) ^ (NewValue ?? 0);
                h = (h * 397) ^ (Description?.GetHashCode() ?? 0);
                return h;
            }
        }

        /// <summary>
        /// 稳定排序键：
        /// <list type="number">
        /// <item>Kind byte</item>
        /// <item>Coord.Y / Coord.X / Coord.Layer byte（若有）</item>
        /// <item>RegionId（若有）</item>
        /// <item>AnchorId（若有）</item>
        /// <item>LinkId（若有）</item>
        /// <item>OldValue（若有）</item>
        /// <item>NewValue（若有）</item>
        /// <item>Description byte-order（StringComparer.Ordinal）</item>
        /// </list>
        /// </summary>
        public int CompareTo(MapEvent other)
        {
            int c = ((byte)Kind).CompareTo((byte)other.Kind);
            if (c != 0) return c;
            // Coord
            if (Coord.HasValue && other.Coord.HasValue)
            {
                c = Coord.Value.CompareTo(other.Coord.Value);
                if (c != 0) return c;
            }
            else if (Coord.HasValue != other.Coord.HasValue)
            {
                return Coord.HasValue ? 1 : -1;
            }
            // RegionId
            c = Nullable.Compare(RegionId, other.RegionId);
            if (c != 0) return c;
            // AnchorId
            c = Nullable.Compare(AnchorId, other.AnchorId);
            if (c != 0) return c;
            // LinkId
            c = Nullable.Compare(LinkId, other.LinkId);
            if (c != 0) return c;
            // OldValue
            c = Nullable.Compare(OldValue, other.OldValue);
            if (c != 0) return c;
            // NewValue
            c = Nullable.Compare(NewValue, other.NewValue);
            if (c != 0) return c;
            // Description
            return string.CompareOrdinal(Description, other.Description);
        }

        public override string ToString()
            => $"MapEvent({Kind}, Coord={Coord?.ToString() ?? "-"}, Region={RegionId?.ToString() ?? "-"}, Anchor={AnchorId?.ToString() ?? "-"}, Link={LinkId?.ToString() ?? "-"}, Old={OldValue?.ToString() ?? "-"}, New={NewValue?.ToString() ?? "-"}, Desc={Description})";
    }

    /// <summary>
    /// doc2 MAP-03 §21.1 地图事件种类（8 类基础 + 5 类扩展）。
    /// <para/>
    /// **位序固定**（AGENTS.md §11）：禁止重排或跳号；任何序列化 / 哈希 / 网络协议
    /// 都依赖此位序。
    /// </summary>
    public enum MapEventKind : byte
    {
        /// <summary>无事件（默认值）。</summary>
        None = 0,

        /// <summary>tile 的 immutable 字段变化（Terrain / Tags / Height / PhasePairTileId 等）。</summary>
        OnTileChanged = 1,

        /// <summary>region 集合变化（add / remove / 边界重定义）。</summary>
        OnRegionChanged = 2,

        /// <summary>路径图失效（attacker 应重新规划寻路）。</summary>
        OnPathGraphInvalidated = 3,

        /// <summary>视线图失效（重新计算 LOS）。</summary>
        OnLineOfSightInvalidated = 4,

        /// <summary>锚点 link 建立。</summary>
        OnAnchorLinkCreated = 5,

        /// <summary>星座区域 polygon 建立（doc2 §13.4）。</summary>
        OnConstellationPolygonCreated = 6,

        /// <summary>全局坍塌值变化。</summary>
        OnGlobalCVChanged = 7,

        /// <summary>tile stability 变化（含坍塌结果）。</summary>
        OnTileStabilityChanged = 8,

        // ──────────── MAP-03 扩展（doc2 §21.1 衍生）────────────

        /// <summary>test-only 调试值变化（必须仅由 <c>SetMapDebugValueCommand</c> 在测试场景使用）。</summary>
        OnMapDebugValueChanged = 9,

        /// <summary><see cref="Starfall.Core.Map.Commands.PlaceMapObjectCommand"/> 成功放置对象。</summary>
        OnMapObjectPlaced = 10,

        /// <summary><see cref="Starfall.Core.Map.Commands.RemoveMapObjectCommand"/> 成功移除对象。</summary>
        OnMapObjectRemoved = 11,

        /// <summary><see cref="Starfall.Core.Map.Commands.MoveUnitOnMapCommand"/> 移动单位（仅触发事件，不替代 BattleRunner.MoveCommand）。</summary>
        OnUnitMovedOnMap = 12,

        /// <summary><see cref="Starfall.Core.Map.Commands.CompressPhaseCommand"/> 触发相位挤压。</summary>
        OnPhaseCompressed = 13,

        /// <summary><see cref="Starfall.Core.Map.Commands.DecompressPhaseCommand"/> 触发相位解挤压（撤销）。</summary>
        OnPhaseDecompressed = 14,
    }
}
