using System.Text;
using System.Collections.Generic;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Regions
{
    /// <summary>
    /// doc2 MAP-09 <see cref="MapRegionState"/> 确定性哈希（FNV-1a 64 位）。
    ///
    /// <para/>
    /// 与 <see cref="MapStateHasher"/> 同协议：type-tag + length-prefix + LE 值。
    /// 参数：<c>offset_basis = 0xCBF29CE484222325</c>，<c>prime = 0x100000001B3</c>。
    ///
    /// <para/>
    /// **字段顺序**（按 ADR-0003 §4 扩展）：
    /// <list type="number">
    /// <item><c>RegionIdValue</c> (tag=0x70, int)</item>
    /// <item><c>Kind</c> (tag=0x71, int cast)</item>
    /// <item><c>OwnerSide</c> (tag=0x72, int)</item>
    /// <item><c>Priority</c> (tag=0x73, int)</item>
    /// <item><c>Activation</c> (tag=0x74, int cast)</item>
    /// <item><c>BoundsCount</c> (tag=0x75, int)</item>
    /// <item><c>Bounds[i]</c> (tag=0x76 per coord, GridCoord LE: X, Y, Layer)</item>
    /// <item><c>TriggersCount</c> (tag=0x77, int)</item>
    /// <item><c>Triggers[i].Kind</c> (tag=0x78, int cast)</item>
    /// <item><c>Triggers[i].Tag</c> (tag=0x79, string)</item>
    /// <item><c>Triggers[i].Threshold</c> (tag=0x7A, int)</item>
    /// <item><c>State</c> (tag=0x80, int cast)</item>
    /// <item><c>CurrentOwnerSide</c> (tag=0x81, int)</item>
    /// <item><c>OccupantCount</c> (tag=0x82, int)</item>
    /// <item><c>TickEntered</c> (tag=0x83, int)</item>
    /// <item><c>ActivationProgress</c> (tag=0x84, int)</item>
    /// <item><c>OccupiedCells</c> (tag=0x85, collection-of-struct，按 GridCoord.CompareTo 排序)</item>
    /// </list>
    /// </summary>
    public static class MapRegionStateHasher
    {
        public const ulong Fnv1aOffsetBasis = 0xCBF29CE484222325UL;
        public const ulong Fnv1aPrime = 0x100000001B3UL;

        // 字段标签
        public const byte TagRegionId = 0x70;
        public const byte TagKind = 0x71;
        public const byte TagOwnerSide = 0x72;
        public const byte TagPriority = 0x73;
        public const byte TagActivation = 0x74;
        public const byte TagBoundsCount = 0x75;
        public const byte TagBoundsVertex = 0x76;
        public const byte TagTriggersCount = 0x77;
        public const byte TagTriggerKind = 0x78;
        public const byte TagTriggerTag = 0x79;
        public const byte TagTriggerThreshold = 0x7A;

        public const byte TagState = 0x80;
        public const byte TagCurrentOwnerSide = 0x81;
        public const byte TagOccupantCount = 0x82;
        public const byte TagTickEntered = 0x83;
        public const byte TagActivationProgress = 0x84;
        public const byte TagOccupiedCells = 0x85;

        public static ulong CalculateDeterministicHash(MapRegionState state)
        {
            ulong h = Fnv1aOffsetBasis;
            if (state == null) return h;

            var def = state.Definition;

            // ──────────── Def 字段 ────────────
            h = MixInt32(h, TagRegionId, def.RegionIdValue.Value);
            h = MixInt32(h, TagKind, (int)def.Kind);
            h = MixInt32(h, TagOwnerSide, def.OwnerSide);
            h = MixInt32(h, TagPriority, def.Priority);
            h = MixInt32(h, TagActivation, (int)def.Activation);

            // Bounds（按 GridCoord.CompareTo 排序以保证哈希确定；原顺序保留为 polygon edge 顺序）
            var sortedBounds = new List<GridCoord>(def.Bounds);
            sortedBounds.Sort();
            h = MixInt32(h, TagBoundsCount, sortedBounds.Count);
            for (int i = 0; i < sortedBounds.Count; i++)
            {
                var v = sortedBounds[i];
                h = MixByte(h, TagBoundsVertex);
                h = MixInt32(h, v.X);
                h = MixInt32(h, v.Y);
                h = MixInt32(h, (int)v.Layer);
            }

            // Triggers（已排序：Kind → Threshold → Tag ordinal）
            h = MixInt32(h, TagTriggersCount, def.Triggers.Count);
            for (int i = 0; i < def.Triggers.Count; i++)
            {
                var t = def.Triggers[i];
                h = MixInt32(h, TagTriggerKind, (int)t.Kind);
                h = MixString(h, TagTriggerTag, t.Tag);
                h = MixInt32(h, TagTriggerThreshold, t.Threshold);
            }

            // ──────────── 运行时字段 ────────────
            h = MixInt32(h, TagState, (int)state.State);
            h = MixInt32(h, TagCurrentOwnerSide, state.CurrentOwnerSide);
            h = MixInt32(h, TagOccupantCount, state.OccupantCount);
            h = MixInt32(h, TagTickEntered, state.TickEntered);
            h = MixInt32(h, TagActivationProgress, state.ActivationProgress);

            // Occupied cells（已按 GridCoord.CompareTo 排序）
            h = MixByte(h, TagOccupiedCells);
            h = MixInt32(h, state.CurrentlyOccupiedCells.Count);
            for (int i = 0; i < state.CurrentlyOccupiedCells.Count; i++)
            {
                var c = state.CurrentlyOccupiedCells[i];
                h = MixInt32(h, c.X);
                h = MixInt32(h, c.Y);
                h = MixInt32(h, (int)c.Layer);
            }

            return h;
        }

        // ──────────── 原子混合函数 ────────────

        public static ulong MixByte(ulong h, byte b)
        {
            h ^= b;
            h *= Fnv1aPrime;
            return h;
        }

        public static ulong MixInt32(ulong h, int v)
        {
            h = MixByte(h, (byte)(v & 0xFF));
            h = MixByte(h, (byte)((v >> 8) & 0xFF));
            h = MixByte(h, (byte)((v >> 16) & 0xFF));
            h = MixByte(h, (byte)((v >> 24) & 0xFF));
            return h;
        }

        public static ulong MixInt32(ulong h, byte tag, int v)
        {
            h = MixByte(h, tag);
            return MixInt32(h, v);
        }

        public static ulong MixString(ulong h, byte tag, string s)
        {
            h = MixByte(h, tag);
            if (s == null)
            {
                h = MixInt32(h, 0);
                return h;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            h = MixInt32(h, bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
                h = MixByte(h, bytes[i]);
            return h;
        }
    }
}