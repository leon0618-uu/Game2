using System;

namespace Starfall.Core.Map.Height
{
    /// <summary>
    /// doc2 MAP-06 §4.1 地块高度等级（0..4）。
    ///
    /// <para/>
    /// 0 = 地面层（默认），1 = 低地台，2 = 高地台，3 = 塔楼底层，4 = 塔顶。
    /// 高度差 ≥ 1 即形成"高地优势"（<see cref="LineOfSight.LineOfSightService"/>）。
    ///
    /// <para/>
    /// 是 <c>readonly struct</c>，便于内联 + 避免堆分配；实现
    /// <see cref="IEquatable{T}"/> + <see cref="IComparable{T}"/> 以便在
    /// 集合（List / Dictionary / SortedSet）中稳定使用，遵循 AGENTS.md §11。
    ///
    /// <para/>
    /// **排序键**仅由 <see cref="Value"/> 决定；不含 <see cref="Starfall.Core.Map.Coordinates.DimensionLayer"/>
    /// 信息——跨维（含相位翻转）的高度比较由调用方自行处理（跨维不等价）。
    /// </summary>
    public readonly struct HeightLevel : IEquatable<HeightLevel>, IComparable<HeightLevel>
    {
        /// <summary>最小允许高度（含地面层）。</summary>
        public const int MinValue = 0;

        /// <summary>最大允许高度（4 = 塔顶）。</summary>
        public const int MaxValue = 4;

        /// <summary>未指定高度（缺省 = 0，地面层）。</summary>
        public static readonly HeightLevel Ground = new HeightLevel(MinValue);

        /// <summary>裸值，范围 [<see cref="MinValue"/>, <see cref="MaxValue"/>]。</summary>
        public readonly int Value;

        public HeightLevel(int value)
        {
            // 静默 clamp：调用方传 -1 / 5 这种越界值时不会抛错，而是 clamp 到合法范围。
            // 这是 doc2 §10.5 推荐：高度作为"视觉/玩法级别"，非业务关键字段，
            // 越界仅意味着地形数据配置错误，不应当让 LOS 服务崩溃。
            if (value < MinValue) value = MinValue;
            if (value > MaxValue) value = MaxValue;
            Value = value;
        }

        // ──────────── 等值 / 哈希 ────────────

        public bool Equals(HeightLevel other) => Value == other.Value;

        public override bool Equals(object obj) => obj is HeightLevel other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(HeightLevel a, HeightLevel b) => a.Value == b.Value;

        public static bool operator !=(HeightLevel a, HeightLevel b) => a.Value != b.Value;

        // ──────────── 排序（仅按 Value）────────────

        public int CompareTo(HeightLevel other) => Value.CompareTo(other.Value);

        public static bool operator <(HeightLevel a, HeightLevel b) => a.Value < b.Value;

        public static bool operator >(HeightLevel a, HeightLevel b) => a.Value > b.Value;

        public static bool operator <=(HeightLevel a, HeightLevel b) => a.Value <= b.Value;

        public static bool operator >=(HeightLevel a, HeightLevel b) => a.Value >= b.Value;

        // ──────────── 算术（只读）────────────

        /// <summary>两高度的差值（to - from）。正数 = to 比 from 高；负数 = 低。</summary>
        public static int operator -(HeightLevel a, HeightLevel b) => a.Value - b.Value;

        /// <summary>两高度的和（结果仍 clamp 到 [<see cref="MinValue"/>, <see cref="MaxValue"/>]）。</summary>
        public static HeightLevel operator +(HeightLevel a, HeightLevel b)
            => new HeightLevel(a.Value + b.Value);

        // ──────────── 字符串 ────────────

        public override string ToString() => $"H{Value}";

        public static HeightLevel Min => new HeightLevel(MinValue);

        public static HeightLevel Max => new HeightLevel(MaxValue);
    }
}
