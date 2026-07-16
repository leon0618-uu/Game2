using System;
using System.Text;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> 确定性哈希（FNV-1a 64 位）。
    /// <para/>
    /// **字节编码协议**（AGENTS.md §11 + doc2 §12 稳定顺序）：
    /// <list type="number">
    /// <item>AnchorLink header byte (<c>0x40</c>) + 长度前缀（uint32 LE）+ LinkId (UTF-8) + State (int32 LE) + StateTick (int32 LE)；</item>
    /// <item>Vertex 段：byte <c>0x41</c>（"vertex entry"）+ 多边形 Id (UTF-8 + uint32 长度) + 顶点数 (uint32 LE) + 每顶点 (X int32, Y int32, Layer byte)。</item>
    /// </list>
    /// <para/>
    /// **tag 字节选择说明**：
    /// <list type="bullet">
    /// <item><c>0x40</c>（AnchorLink header）和 <c>0x41</c>（Vertex entry）在本类作用域内；
    ///       不与 <see cref="Starfall.Core.Map.State.MapStateHasher"/> 在同一上下文冲突——
    ///       MapState 调 <see cref="CalculateDeterministicHash(AnchorLink)"/> 时只混合 8 字节 ulong，
    ///       内部 tag 字节不会出现在 MapState 顶层 tag 流里。</item>
    /// <item>MapStateHasher 顶层使用 <c>0x38</c>（TagAnchorLinks）作为集合段标识。</item>
    /// </list>
    /// <para/>
    /// **状态参与哈希**：<see cref="AnchorLink.CurrentState"/>、<see cref="AnchorLink.StateTick"/>、
    /// <see cref="AnchorLink.PostStateHash"/> 都参与哈希 ——
    /// 因为状态机变化会改变逻辑结果，Replay 必须能区分。
    /// <para/>
    /// **null 安全**：null 输入返回 FNV-1a offset basis（与 MapStateHasher 同语义）。
    /// </summary>
    public static class AnchorLinkHasher
    {
        public const ulong Fnv1aOffsetBasis = 0xCBF29CE484222325UL;
        public const ulong Fnv1aPrime = 0x100000001B3UL;

        // AnchorLink 内部协议 tag（在 AnchorLinkHasher 作用域内唯一）。
        public const byte TagAnchorLinkHeader = 0x40;
        public const byte TagAnchorLinkVertexEntry = 0x41;

        public static ulong CalculateDeterministicHash(AnchorLink link)
        {
            ulong h = Fnv1aOffsetBasis;
            if (link == null) return h;

            // ──────── 1. AnchorLink header ────────
            h = MixByte(h, TagAnchorLinkHeader);
            // LinkId：string（UTF-8 + 长度前缀）
            h = MixString(h, link.Id.Value);
            // CurrentState：int32（强转避免 enum 字节宽度不一致）
            h = MixInt32(h, (int)link.CurrentState);
            // StateTick：int32
            h = MixInt32(h, link.StateTick);
            // PostStateHash：ulong（LE 8 字节）—— 包含最近一次状态变更后的全局哈希
            h = MixUInt64(h, link.PostStateHash);

            // ──────── 2. Vertex 段（多边形顶点）────────
            var poly = link.Polygon;
            h = MixByte(h, TagAnchorLinkVertexEntry);
            // 多边形 Id（string，长度前缀 + UTF-8）
            h = MixString(h, poly.Id.Value);
            // 顶点数
            h = MixInt32(h, poly.Vertices.Count);
            // 每顶点：(X int32 LE, Y int32 LE, Layer byte)
            for (int i = 0; i < poly.Vertices.Count; i++)
            {
                var v = poly.Vertices[i].Coord;
                h = MixInt32(h, v.X);
                h = MixInt32(h, v.Y);
                h = MixByte(h, (byte)v.Layer);
            }

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