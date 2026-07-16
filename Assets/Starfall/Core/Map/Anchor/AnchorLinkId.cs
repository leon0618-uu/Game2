using System;

namespace Starfall.Core.Map.Anchor
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> ID（字符串，不可变值类型）。
    /// <para/>
    /// 与 <see cref="ConstellationPolygonId"/> 同模式：
    /// <list type="bullet">
    /// <item><c>Value</c> 不可为 null；</item>
    /// <item>长度 &gt; 0；空字符串抛 <see cref="ArgumentException"/>；</item>
    /// <item>按字符串大小写敏感比较。</item>
    /// </list>
    /// <para/>
    /// **不与 <c>int ZoneId</c> 冲突**：本类型是字符串 ID；
    /// MVP <c>int ZoneId</c> 路径（<see cref="Starfall.Core.Anchor.AnchorZone"/>）保留不变。
    /// </summary>
    public readonly struct AnchorLinkId : IEquatable<AnchorLinkId>
    {
        public string Value { get; }

        public AnchorLinkId(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentException("AnchorLinkId.Value must be non-empty.", nameof(value));
            Value = value;
        }

        public bool Equals(AnchorLinkId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AnchorLinkId other && Equals(other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public static bool operator ==(AnchorLinkId a, AnchorLinkId b) => a.Equals(b);

        public static bool operator !=(AnchorLinkId a, AnchorLinkId b) => !a.Equals(b);

        public override string ToString() => Value ?? "<null>";
    }
}