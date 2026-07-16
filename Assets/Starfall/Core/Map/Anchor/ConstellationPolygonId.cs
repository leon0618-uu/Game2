using System;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 星座多边形 ID（不可变值类型）。
    /// <para/>
    /// **约束**：
    /// <list type="bullet">
    /// <item><c>Value</c> 不可为 null。</item>
    /// <item>构造期校验长度 &gt; 0；空字符串抛 <see cref="ArgumentException"/>。</item>
    /// <item>实现 <see cref="IEquatable{T}"/>，按字符串大小写敏感相等。</item>
    /// </list>
    /// <para/>
    /// 与 MVP 的 <c>int ZoneId</c>（<see cref="Starfall.Core.Anchor.AnchorZone"/>）共存：
    /// 本类型是字符串 ID，与未来 <see cref="AnchorLink"/> / <see cref="AnchorLinkId"/>
    /// 的命名空间一致；老 <c>int ZoneId</c> 路径不删除（向后兼容）。
    /// </summary>
    public readonly struct ConstellationPolygonId : IEquatable<ConstellationPolygonId>
    {
        public string Value { get; }

        public ConstellationPolygonId(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentException("ConstellationPolygonId.Value must be non-empty.", nameof(value));
            Value = value;
        }

        public bool Equals(ConstellationPolygonId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ConstellationPolygonId other && Equals(other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public static bool operator ==(ConstellationPolygonId a, ConstellationPolygonId b) => a.Equals(b);

        public static bool operator !=(ConstellationPolygonId a, ConstellationPolygonId b) => !a.Equals(b);

        public override string ToString() => Value ?? "<null>";
    }
}