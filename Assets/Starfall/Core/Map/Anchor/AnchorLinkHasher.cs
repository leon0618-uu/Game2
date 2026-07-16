using System;
using System.Text;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> 确定性哈希（FNV-1a 64 位）。
    /// <para/>
    /// **字节编码协议**（per ADR-0009 §9）：
    /// <list type="number">
    /// <item><c>0x43</c>（AnchorLinkId） + LinkId 字节（UTF-8 + 长度前缀）；</item>
    /// <item>Vertex 段：VertexCount（uint32 LE 长度前缀）+ 每顶点 <c>0x44</c>（VertexEntry）+ X（int32 LE）+ Y（int32 LE）+ Layer（int32 LE）；</item>
    /// <item><c>0x45</c>（AnchorLinkCurrentState） + CurrentState（int32 LE）；</item>
    /// <item><c>0x46</c>（AnchorLinkStateTick） + StateTick（int32 LE）；</item>
    /// <item><c>0x47</c>（AnchorLinkPostStateHash） + <see cref="ComputeStateHash"/>（uint64 LE）。</item>
    /// </list>
    /// <para/>
    /// **tag 字节选择说明**（per ADR-0009 §9 + Alternatives F）：
    /// <list type="bullet">
    /// <item>任务原 spec 建议 <c>0x40/0x41</c>，但 [ADR-0003 §4] 既有字段已占用
    ///       <c>0x40 = TagAnchorZoneId</c>、<c>0x41 = TagAnchorOwner</c>、<c>0x42 = TagAnchorVertex</c>。
    ///       若按原 spec 会破坏既有 294+ EditMode 测试的字节流期望。</item>
    /// <item>本实现改用 <c>0x43-0x47</c>（与 legacy anchor sub-tags <c>0x40-0x42</c> 邻接但**无碰撞**），
    ///       与 ADR-0009 §9 完全一致。</item>
    /// <item>顶层 <see cref="Starfall.Core.Map.State.MapStateHasher"/> 集合段使用
    ///       <c>0x38</c>（TagAnchorLinks），与 <c>0x37</c>（TagLocalCVs）邻接但无碰撞。</item>
    /// </list>
    /// <para/>
    /// **PostStateHash 独立性**（per ADR-0009 §9 ComputeStateHash）：
    /// <see cref="PostStateHash"/> 由 <see cref="ComputeStateHash"/> 计算，
    /// 仅依赖（<see cref="AnchorLink.CurrentState"/>、<see cref="AnchorLink.StateTick"/>）——
    /// 零循环依赖（<see cref="MapState"/> hash 不影响 <see cref="PostStateHash"/>）。
    /// <para/>
    /// **null 安全**：null 输入返回 FNV-1a offset basis（与 MapStateHasher 同语义）。
    /// </summary>
    public static class AnchorLinkHasher
    {
        public const ulong Fnv1aOffsetBasis = 0xCBF29CE484222325UL;
        public const ulong Fnv1aPrime = 0x100000001B3UL;

        // ADR-0009 §9 — AnchorLink sub-tags (与 legacy anchor 0x40-0x42 邻接但不碰撞)
        public const byte TagAnchorLinkId = 0x43;
        public const byte TagVertexEntry = 0x44;
        public const byte TagAnchorLinkCurrentState = 0x45;
        public const byte TagAnchorLinkStateTick = 0x46;
        public const byte TagAnchorLinkPostStateHash = 0x47;

        /// <summary>
        /// 计算 <see cref="AnchorLink"/> 的完整 FNV-1a 64 哈希。
        /// 字节布局严格按 ADR-0009 §9。
        /// </summary>
        public static ulong CalculateDeterministicHash(AnchorLink link)
        {
            ulong h = Fnv1aOffsetBasis;
            if (link == null) return h;
            return WriteLinkSubstructure(h, link);
        }

        /// <summary>
        /// 把单个 <see cref="AnchorLink"/> 的子结构（含 0x43-0x47 子 tag + 顶点序列）
        /// 追加到当前 FNV-1a 链 <paramref name="h"/>，返回新哈希值。
        /// 供 <see cref="Starfall.Core.Map.State.MapStateHasher"/> 在 collection
        /// 段（tag 0x38）逐 link 写入时复用。
        /// </summary>
        public static ulong WriteLinkSubstructure(ulong h, AnchorLink link)
        {
            if (link == null) return h;

            // ──────── 1. 0x43 AnchorLinkId ────────
            h = MixByte(h, TagAnchorLinkId);
            h = MixString(h, link.Id.Value);

            // ──────── 2. Vertex 段（隐式长度前缀 + 每顶点 0x44）────────
            // 注：VertexCount 无独立 tag 字节；作为长度前缀写在 tag 0x38 集合内
            // （与 MapStateHasher 既有的 collection-with-length-prefix 模式一致）。
            var poly = link.Polygon;
            h = MixInt32(h, poly.Vertices.Count);
            for (int i = 0; i < poly.Vertices.Count; i++)
            {
                h = MixByte(h, TagVertexEntry);
                var v = poly.Vertices[i].Coord;
                h = MixInt32(h, v.X);
                h = MixInt32(h, v.Y);
                h = MixInt32(h, (int)v.Layer);
            }

            // ──────── 3. 0x45 CurrentState ────────
            h = MixByte(h, TagAnchorLinkCurrentState);
            h = MixInt32(h, (int)link.CurrentState);

            // ──────── 4. 0x46 StateTick ────────
            h = MixByte(h, TagAnchorLinkStateTick);
            h = MixInt32(h, link.StateTick);

            // ──────── 5. 0x47 PostStateHash ────────
            h = MixByte(h, TagAnchorLinkPostStateHash);
            h = MixUInt64(h, link.PostStateHash);

            return h;
        }

        /// <summary>
        /// 计算 <see cref="AnchorLink.PostStateHash"/>（per ADR-0009 §9 ComputeStateHash）。
        /// <para/>
        /// 算法（独立 FNV-1a 链，仅依赖 state + tick）：
        /// <code>
        /// hash = Fnv1aOffsetBasis
        /// mix 0x45 + state_byte LE
        /// mix 0x46 + tick LE 4
        /// mix 0x47
        /// return hash
        /// </code>
        /// <para/>
        /// 零循环依赖（不读 MapState hash、不读 Polygon 顶点）。<see cref="AnchorLink"/>
        /// 在构造期 / 状态迁移后自动调用本方法刷新 <see cref="AnchorLink.PostStateHash"/>。
        /// </summary>
        public static ulong ComputeStateHash(AnchorLink link)
        {
            ulong h = Fnv1aOffsetBasis;
            if (link == null) return h;

            h = MixByte(h, TagAnchorLinkCurrentState);
            h = MixInt32(h, (int)link.CurrentState);
            h = MixByte(h, TagAnchorLinkStateTick);
            h = MixInt32(h, link.StateTick);
            h = MixByte(h, TagAnchorLinkPostStateHash);
            return h;
        }

        // ──────────── 原子混合函数（FNV-1a 单步，与 MapStateHasher 同模式）────────────

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

        public static ulong MixString(ulong h, string s)
        {
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