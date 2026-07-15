using System;
using System.Collections.Generic;
using System.Text;
using Starfall.Core.Anchor;
using Starfall.Core.Map.Collapse;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.State
{
    /// <summary>
    /// doc2 MAP-02 <see cref="MapState"/> 确定性哈希（FNV-1a 64 位）。
    ///
    /// <para/>
    /// 字节编码协议（type-tag + 长度前缀 + LE 值；AGENTS.md §11 稳定顺序）：
    /// <list type="bullet">
    /// <item>每个字段写入顺序：<c>uint8 type-tag</c> + <c>uint32 length</c> LE
    ///       （变长字段：string / 集合），定长字段省略 length。</item>
    /// <item>字符串：UTF-8 字节序列（不含 BOM）。</item>
    /// <item>枚举：显式 <c>int</c> 强转 LE 4 字节（避免 <c>enum</c> 字节宽度不一致）。</item>
    /// <item>浮点：本轮（MAP-02）不允许出现在哈希作用域内。</item>
    /// <item>集合：写入前先排序（按 AGENTS.md §11），空集合长度写 <c>0x00000000</c>。</item>
    /// <item>FNV-1a 参数：<c>offset_basis = 0xCBF29CE484222325</c>，
    ///       <c>prime = 0x100000001B3</c>（与 <c>BattleState</c> 一致）。</item>
    /// </list>
    ///
    /// <para/>
    /// 字段顺序（写入顺序，与 BattleState.PostStateHash 的"MapState 字节先发"约束一致）：
    /// <list type="number">
    /// <item><c>MapId</c> (tag=0x10, string)</item>
    /// <item><c>Width</c> (tag=0x11, int)</item>
    /// <item><c>Height</c> (tag=0x12, int)</item>
    /// <item><c>InitialActiveLayer</c> (tag=0x13, int)</item>
    /// <item><c>InitialGlobalCollapseValue</c> (tag=0x14, int)</item>
    /// <item><c>TilesetId</c> (tag=0x15, string)</item>
    /// <item><c>EnvironmentScheduleId</c> (tag=0x16, string)</item>
    /// <item><c>Version</c> (tag=0x20, int)</item>
    /// <item><c>ActiveLayer</c> (tag=0x21, int)</item>
    /// <item><c>GlobalCollapseValue</c> (tag=0x22, int)</item>
    /// <item><c>Tiles</c> (tag=0x30, collection-of-struct，按 GridCoord.CompareTo 排序)</item>
    /// <item><c>Anchors</c> (tag=0x31, collection，按 AnchorZone.ZoneId 升序；每个 zone 写入 ZoneId + 顶点规范化序列)</item>
    /// <item><c>Regions</c> (tag=0x32, collection，按 RegionId 升序)</item>
    /// <item><c>MapObjects</c> (tag=0x33, collection，按 ObjectId 升序)</item>
    /// <item><c>RegionStates</c> (tag=0x34, collection，按 RegionId 升序；MAP-09 增量)</item>
    /// <item><c>SpawnPoints</c> (tag=0x35, collection，按 SpawnId 升序；MAP-09 增量)</item>
    /// <item><c>GlobalCV</c> (tag=0x36, struct; 4 子标签写入 Value/Stage/Threshold/Tick; MAP-11a 增量)</item>
    /// <item><c>LocalCVs</c> (tag=0x37, collection，按 GridCoord.CompareTo 排序；MAP-11a 增量)</item>
    /// </list>
    /// </summary>
    public static class MapStateHasher
    {
        public const ulong Fnv1aOffsetBasis = 0xCBF29CE484222325UL;
        public const ulong Fnv1aPrime = 0x100000001B3UL;

        // ──────────── 字段类型标签（公开用于调试 / 跨模块校验）────────────

        public const byte TagMapId = 0x10;
        public const byte TagWidth = 0x11;
        public const byte TagHeight = 0x12;
        public const byte TagInitialActiveLayer = 0x13;
        public const byte TagInitialGlobalCollapseValue = 0x14;
        public const byte TagTilesetId = 0x15;
        public const byte TagEnvironmentScheduleId = 0x16;

        public const byte TagVersion = 0x20;
        public const byte TagActiveLayer = 0x21;
        public const byte TagGlobalCollapseValue = 0x22;

        public const byte TagTiles = 0x30;
        public const byte TagAnchors = 0x31;
        public const byte TagRegions = 0x32;
        public const byte TagMapObjects = 0x33;

        // Anchor 子标签
        public const byte TagAnchorZoneId = 0x40;
        public const byte TagAnchorOwner = 0x41;
        public const byte TagAnchorVertex = 0x42;

        // Region 子标签
        public const byte TagRegionId = 0x50;
        public const byte TagRegionType = 0x51;
        public const byte TagRegionOwner = 0x52;
        public const byte TagRegionTileCoord = 0x53;

        // Object 子标签
        public const byte TagObjectId = 0x60;
        public const byte TagObjectType = 0x61;
        public const byte TagObjectAnchorX = 0x62;
        public const byte TagObjectAnchorY = 0x63;
        public const byte TagObjectAnchorLayer = 0x64;

        // MAP-09 新增：强类型运行时 RegionState + SpawnPoint
        public const byte TagRegionStates = 0x34;
        public const byte TagSpawnPoints = 0x35;

        // MAP-11a 新增：GlobalCV (typed) + LocalCVs dictionary
        public const byte TagGlobalCV = 0x36;
        public const byte TagLocalCVs = 0x37;

        // RegionState 子标签（与 MapRegionStateHasher 同协议但 tag 偏移以避免冲突）
        public const byte TagRStateId = 0x90;
        public const byte TagRStateKind = 0x91;
        public const byte TagRStateOwnerSide = 0x92;
        public const byte TagRStatePriority = 0x93;
        public const byte TagRStateActivation = 0x94;
        public const byte TagRStateBoundsCount = 0x95;
        public const byte TagRStateBoundsVertex = 0x96;
        public const byte TagRStateTriggersCount = 0x97;
        public const byte TagRStateTriggerKind = 0x98;
        public const byte TagRStateTriggerTag = 0x99;
        public const byte TagRStateTriggerThreshold = 0x9A;
        public const byte TagRStateState = 0x9B;
        public const byte TagRStateCurrentOwnerSide = 0x9C;
        public const byte TagRStateOccupantCount = 0x9D;
        public const byte TagRStateTickEntered = 0x9E;
        public const byte TagRStateActivationProgress = 0x9F;
        public const byte TagRStateOccupiedCells = 0xA0;

        // SpawnPoint 子标签
        public const byte TagSpawnId = 0xA1;
        public const byte TagSpawnRegionId = 0xA2;
        public const byte TagSpawnCoordX = 0xA3;
        public const byte TagSpawnCoordY = 0xA4;
        public const byte TagSpawnCoordLayer = 0xA5;
        public const byte TagSpawnOwnerSide = 0xA6;
        public const byte TagSpawnCapacity = 0xA7;
        public const byte TagSpawnActive = 0xA8;

        // GlobalCollapseValue 子标签（MAP-11a）
        public const byte TagGlobalCVValue = 0xB0;
        public const byte TagGlobalCVStage = 0xB1;
        public const byte TagGlobalCVThreshold = 0xB2;
        public const byte TagGlobalCVTick = 0xB3;

        // LocalCollapseValue 子标签（MAP-11a）
        public const byte TagLocalCVCoordX = 0xB4;
        public const byte TagLocalCVCoordY = 0xB5;
        public const byte TagLocalCVCoordLayer = 0xB6;
        public const byte TagLocalCVValue = 0xB7;
        public const byte TagLocalCVStability = 0xB8;
        public const byte TagLocalCVTick = 0xB9;

        /// <summary>计算 <see cref="MapState"/> 的 FNV-1a 64 位哈希；null 输入返回 offset_basis。</summary>
        public static ulong CalculateDeterministicHash(MapState state)
        {
            ulong h = Fnv1aOffsetBasis;
            if (state == null) return h;

            // ──────────── Definition ────────────
            var def = state.Definition;
            h = MixString(h, TagMapId, def.MapId);
            h = MixInt32(h, TagWidth, def.Width);
            h = MixInt32(h, TagHeight, def.Height);
            h = MixInt32(h, TagInitialActiveLayer, (int)def.InitialActiveLayer);
            h = MixInt32(h, TagInitialGlobalCollapseValue, def.InitialGlobalCollapseValue);
            h = MixString(h, TagTilesetId, def.TilesetId);
            h = MixString(h, TagEnvironmentScheduleId, def.EnvironmentScheduleId);

            // ──────────── 运行时整数状态 ────────────
            h = MixInt32(h, TagVersion, state.Version);
            h = MixInt32(h, TagActiveLayer, (int)state.ActiveLayer);
            h = MixInt32(h, TagGlobalCollapseValue, state.GlobalCollapseValue);

            // ──────────── Tiles：按 GridCoord.CompareTo 排序 ────────────
            // 拷贝并排序，避免修改源集合。
            var sortedTiles = new List<GridCoord>(state.Tiles);
            sortedTiles.Sort();
            h = MixByte(h, TagTiles);
            h = MixInt32(h, sortedTiles.Count);
            foreach (var t in sortedTiles)
            {
                // 每个 tile 不写 tag，只写 (X, Y, Layer) LE；集合层已有 tag+length 区分。
                h = MixInt32(h, t.X);
                h = MixInt32(h, t.Y);
                h = MixInt32(h, (int)t.Layer);
            }

            // ──────────── Anchors：按 ZoneId 升序 ────────────
            // AnchorZone 构造时已按 GridPos.CompareTo 排序顶点；本步骤只对 zone 间排序。
            var sortedAnchors = new List<AnchorZone>(state.Anchors);
            sortedAnchors.Sort((a, b) => a.ZoneId.CompareTo(b.ZoneId));
            h = MixByte(h, TagAnchors);
            h = MixInt32(h, sortedAnchors.Count);
            foreach (var z in sortedAnchors)
            {
                h = MixInt32(h, TagAnchorZoneId, z.ZoneId);
                h = MixString(h, TagAnchorOwner, z.Owner);
                h = MixByte(h, TagAnchorVertex);
                h = MixInt32(h, z.Vertices.Count);
                foreach (var v in z.Vertices)
                {
                    h = MixInt32(h, v.X);
                    h = MixInt32(h, v.Y);
                }
            }

            // ──────────── Regions：按 RegionId 升序 ────────────
            var sortedRegions = new List<MapRegion>(state.Regions);
            sortedRegions.Sort((a, b) => a.RegionId.CompareTo(b.RegionId));
            h = MixByte(h, TagRegions);
            h = MixInt32(h, sortedRegions.Count);
            foreach (var r in sortedRegions)
            {
                h = MixInt32(h, TagRegionId, r.RegionId);
                h = MixString(h, TagRegionType, r.RegionType);
                h = MixString(h, TagRegionOwner, r.Owner);
                h = MixByte(h, TagRegionTileCoord);
                h = MixInt32(h, r.TileCoords.Count);
                foreach (var t in r.TileCoords)
                {
                    h = MixInt32(h, t.X);
                    h = MixInt32(h, t.Y);
                    h = MixInt32(h, (int)t.Layer);
                }
            }

            // ──────────── MapObjects：按 ObjectId 升序 ────────────
            var sortedObjects = new List<MapObjectInstance>(state.MapObjects);
            sortedObjects.Sort((a, b) => a.ObjectId.CompareTo(b.ObjectId));
            h = MixByte(h, TagMapObjects);
            h = MixInt32(h, sortedObjects.Count);
            foreach (var o in sortedObjects)
            {
                h = MixInt32(h, TagObjectId, o.ObjectId);
                h = MixString(h, TagObjectType, o.ObjectType);
                h = MixInt32(h, TagObjectAnchorX, o.Anchor.X);
                h = MixInt32(h, TagObjectAnchorY, o.Anchor.Y);
                h = MixInt32(h, TagObjectAnchorLayer, (int)o.Anchor.Layer);
            }

            // ──────────── MAP-09：RegionStates（按 RegionId 升序）────────────
            var sortedRegionStates = new List<Starfall.Core.Map.Regions.MapRegionState>(state.RegionStates);
            sortedRegionStates.Sort((a, b) =>
                a.Definition.RegionIdValue.Value.CompareTo(b.Definition.RegionIdValue.Value));
            h = MixByte(h, TagRegionStates);
            h = MixInt32(h, sortedRegionStates.Count);
            foreach (var rs in sortedRegionStates)
            {
                var rdef = rs.Definition;
                h = MixInt32(h, TagRStateId, rdef.RegionIdValue.Value);
                h = MixInt32(h, TagRStateKind, (int)rdef.Kind);
                h = MixInt32(h, TagRStateOwnerSide, rdef.OwnerSide);
                h = MixInt32(h, TagRStatePriority, rdef.Priority);
                h = MixInt32(h, TagRStateActivation, (int)rdef.Activation);
                // Bounds: 拷贝后按 GridCoord.CompareTo 排序以保证哈希确定（不管输入顺序）。
                var sortedBounds = new List<Starfall.Core.Map.Coordinates.GridCoord>(rdef.Bounds);
                sortedBounds.Sort();
                h = MixInt32(h, TagRStateBoundsCount, sortedBounds.Count);
                for (int i = 0; i < sortedBounds.Count; i++)
                {
                    var v = sortedBounds[i];
                    h = MixByte(h, TagRStateBoundsVertex);
                    h = MixInt32(h, v.X);
                    h = MixInt32(h, v.Y);
                    h = MixInt32(h, (int)v.Layer);
                }
                h = MixInt32(h, TagRStateTriggersCount, rdef.Triggers.Count);
                for (int i = 0; i < rdef.Triggers.Count; i++)
                {
                    var t = rdef.Triggers[i];
                    h = MixInt32(h, TagRStateTriggerKind, (int)t.Kind);
                    h = MixString(h, TagRStateTriggerTag, t.Tag);
                    h = MixInt32(h, TagRStateTriggerThreshold, t.Threshold);
                }
                h = MixInt32(h, TagRStateState, (int)rs.State);
                h = MixInt32(h, TagRStateCurrentOwnerSide, rs.CurrentOwnerSide);
                h = MixInt32(h, TagRStateOccupantCount, rs.OccupantCount);
                h = MixInt32(h, TagRStateTickEntered, rs.TickEntered);
                h = MixInt32(h, TagRStateActivationProgress, rs.ActivationProgress);
                h = MixByte(h, TagRStateOccupiedCells);
                h = MixInt32(h, rs.CurrentlyOccupiedCells.Count);
                for (int i = 0; i < rs.CurrentlyOccupiedCells.Count; i++)
                {
                    var c = rs.CurrentlyOccupiedCells[i];
                    h = MixInt32(h, c.X);
                    h = MixInt32(h, c.Y);
                    h = MixInt32(h, (int)c.Layer);
                }
            }

            // ──────────── MAP-09：SpawnPoints（按 SpawnId 升序）────────────
            var sortedSpawns = new List<Starfall.Core.Map.Regions.MapSpawnPoint>(state.SpawnPoints);
            sortedSpawns.Sort((a, b) => a.SpawnIdValue.Value.CompareTo(b.SpawnIdValue.Value));
            h = MixByte(h, TagSpawnPoints);
            h = MixInt32(h, sortedSpawns.Count);
            foreach (var s in sortedSpawns)
            {
                h = MixInt32(h, TagSpawnId, s.SpawnIdValue.Value);
                h = MixInt32(h, TagSpawnRegionId, s.RegionIdValue);
                h = MixInt32(h, TagSpawnCoordX, s.Coord.X);
                h = MixInt32(h, TagSpawnCoordY, s.Coord.Y);
                h = MixInt32(h, TagSpawnCoordLayer, (int)s.Coord.Layer);
                h = MixInt32(h, TagSpawnOwnerSide, s.OwnerSide);
                h = MixInt32(h, TagSpawnCapacity, s.Capacity);
                h = MixInt32(h, TagSpawnActive, s.Active ? 1 : 0);
            }

            // ──────────── MAP-11a：GlobalCollapseValue（typed）────────────
            // 写入 4 个子字段（Value / Stage / Threshold / Tick）保证显式参与哈希。
            // 与运行时影子 int GlobalCollapseValue 同步；这里只读 GlobalCV。
            h = MixByte(h, TagGlobalCV);
            h = MixInt32(h, TagGlobalCVValue, state.GlobalCV.Value);
            h = MixInt32(h, TagGlobalCVStage, (int)state.GlobalCV.Stage);
            h = MixInt32(h, TagGlobalCVThreshold, state.GlobalCV.Threshold);
            h = MixInt32(h, TagGlobalCVTick, state.GlobalCV.TickAccumulated);

            // ──────────── MAP-11a：LocalCollapseValues（按 GridCoord.CompareTo 排序）────────────
            // 拷贝 keys 后排序；Dictionary 本体保持插入顺序。
            var sortedLocalCvKeys = new List<GridCoord>(state.LocalCVsInternal.Keys);
            sortedLocalCvKeys.Sort();
            h = MixByte(h, TagLocalCVs);
            h = MixInt32(h, sortedLocalCvKeys.Count);
            foreach (var c in sortedLocalCvKeys)
            {
                var lcv = state.LocalCVsInternal[c];
                h = MixInt32(h, TagLocalCVCoordX, lcv.Coord.X);
                h = MixInt32(h, TagLocalCVCoordY, lcv.Coord.Y);
                h = MixInt32(h, TagLocalCVCoordLayer, (int)lcv.Coord.Layer);
                h = MixInt32(h, TagLocalCVValue, lcv.Value);
                h = MixInt32(h, TagLocalCVStability, (int)lcv.Stability);
                h = MixInt32(h, TagLocalCVTick, lcv.TickAccumulated);
            }

            return h;
        }

        // ──────────── 原子混合函数（FNV-1a 单步）────────────

        /// <summary>FNV-1a 单步：h = (h ^ b) * prime。</summary>
        public static ulong MixByte(ulong h, byte b)
        {
            h ^= b;
            h *= Fnv1aPrime;
            return h;
        }

        /// <summary>FNV-1a 写入 int（LE 4 字节）。</summary>
        public static ulong MixInt32(ulong h, int v)
        {
            h = MixByte(h, (byte)(v & 0xFF));
            h = MixByte(h, (byte)((v >> 8) & 0xFF));
            h = MixByte(h, (byte)((v >> 16) & 0xFF));
            h = MixByte(h, (byte)((v >> 24) & 0xFF));
            return h;
        }

        /// <summary>FNV-1a 写入 tag + int（LE 4 字节）。用于定长有 tag 字段。</summary>
        public static ulong MixInt32(ulong h, byte tag, int v)
        {
            h = MixByte(h, tag);
            return MixInt32(h, v);
        }

        /// <summary>FNV-1a 写入 ulong（LE 8 字节）。</summary>
        public static ulong MixUInt64(ulong h, ulong v)
        {
            h = MixByte(h, (byte)(v & 0xFF));
            h = MixByte(h, (byte)((v >> 8) & 0xFF));
            h = MixByte(h, (byte)((v >> 16) & 0xFF));
            h = MixByte(h, (byte)((v >> 24) & 0xFF));
            h = MixByte(h, (byte)((v >> 32) & 0xFF));
            h = MixByte(h, (byte)((v >> 40) & 0xFF));
            h = MixByte(h, (byte)((v >> 48) & 0xFF));
            h = MixByte(h, (byte)((v >> 56) & 0xFF));
            return h;
        }

        /// <summary>FNV-1a 写入 tag + uint32 长度前缀 + UTF-8 字节序列。</summary>
        public static ulong MixString(ulong h, byte tag, string s)
        {
            h = MixByte(h, tag);
            if (s == null)
            {
                // 长度 0，避免 null/"" 抖动。
                h = MixInt32(h, 0);
                return h;
            }
            // 用 UTF-8 无 BOM 编码；Length 用 byte 数（不是 char 数）以保证跨语言稳定。
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            h = MixInt32(h, bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                h = MixByte(h, bytes[i]);
            return h;
        }
    }
}
