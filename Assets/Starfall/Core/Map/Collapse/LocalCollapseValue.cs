using System;
using System.Text;
using Starfall.Core.Map.Coordinates;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 局部坍塌值（每个格子独立 CV，readonly struct，ADR-0007）。
    ///
    /// <para/>
    /// 范围 [0, 100]，与 <see cref="GlobalCollapseValue"/> 范围一致。
    /// <b>关键差异</b>：每个 tile 独立累积；用于局部坍塌 / 断裂检测。
    ///
    /// <para/>
    /// <b>字段</b>：
    /// <list type="bullet">
    /// <item><see cref="Coord"/>：所属 tile。</item>
    /// <item><see cref="Value"/>：CV 值（clamp 后 ∈ [0, 100]）。</item>
    /// <item><see cref="Stability"/>：根据 <see cref="Value"/> 自动派生（构造时固化）。</item>
    /// <item><see cref="TickAccumulated"/>：累积的回合数。</item>
    /// </list>
    ///
    /// <para/>
    /// <b>Stability 派生规则</b>：
    /// <list type="bullet">
    /// <item>0 = <see cref="TileStability.Stable"/></item>
    /// <item>1..29 = <see cref="TileStability.Unstable"/></item>
    /// <item>30..49 = <see cref="TileStability.Unstable"/>（仍可通行，CV 中等累积）</item>
    /// <item>50..69 = <see cref="TileStability.Fractured"/></item>
    /// <item>70..89 = <see cref="TileStability.Collapsing"/></item>
    /// <item>90..100 = <see cref="TileStability.Collapsed"/></item>
    /// </list>
    /// <b>注</b>：<see cref="TileStability.Reconstructed"/> 只能由 <c>ReconstructTileCommand</c> 显式设置。
    /// </summary>
    public readonly struct LocalCollapseValue : IEquatable<LocalCollapseValue>
    {
        /// <summary>所属 tile（强制包含 Layer）。</summary>
        public readonly GridCoord Coord;

        /// <summary>CV 值 ∈ [0, 100]。</summary>
        public readonly int Value;

        /// <summary>该 tile 的稳定性（按 <see cref="Value"/> 派生，构造时固化）。</summary>
        public readonly TileStability Stability;

        /// <summary>累积的回合数（每 Tick +1）。</summary>
        public readonly int TickAccumulated;

        public LocalCollapseValue(GridCoord coord, int value, int tickAccumulated)
        {
            Coord = coord;
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            Value = value;
            TickAccumulated = tickAccumulated;
            Stability = DeriveStability(value);
        }

        // ──────────── 工厂 ────────────

        /// <summary>零值（CV=0, Tick=0）。</summary>
        public static LocalCollapseValue Zero(GridCoord coord) => new LocalCollapseValue(coord, 0, 0);

        /// <summary>按坐标 + 值构造。</summary>
        public static LocalCollapseValue Of(GridCoord coord, int value) => new LocalCollapseValue(coord, value, 0);

        /// <summary>按坐标 + 值 + tick 构造。</summary>
        public static LocalCollapseValue Of(GridCoord coord, int value, int tickAccumulated) => new LocalCollapseValue(coord, value, tickAccumulated);

        /// <summary>派生 <see cref="TileStability"/>（按 <see cref="Value"/>）。</summary>
        public static TileStability DeriveStability(int value)
        {
            if (value <= 0) return TileStability.Stable;
            if (value < 50) return TileStability.Unstable;
            if (value < 70) return TileStability.Fractured;
            if (value < 90) return TileStability.Collapsing;
            return TileStability.Collapsed;
        }

        // ──────────── 派生操作 ────────────

        /// <summary>把 <see cref="Value"/> 加 delta（clamp），Stability 重新派生。</summary>
        public LocalCollapseValue WithDelta(int delta)
        {
            return new LocalCollapseValue(Coord, Value + delta, TickAccumulated);
        }

        /// <summary>设置 <see cref="Value"/>，Stability 重新派生。</summary>
        public LocalCollapseValue WithValue(int newValue)
        {
            return new LocalCollapseValue(Coord, newValue, TickAccumulated);
        }

        /// <summary>Tick 推进（TickAccumulated +1）。</summary>
        public LocalCollapseValue WithIncrementedTick()
        {
            return new LocalCollapseValue(Coord, Value, TickAccumulated + 1);
        }

        // ──────────── 等值 / 字符串 ────────────

        public bool Equals(LocalCollapseValue other)
            => Coord.Equals(other.Coord)
               && Value == other.Value
               && TickAccumulated == other.TickAccumulated;

        public override bool Equals(object obj) => obj is LocalCollapseValue other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Coord.GetHashCode();
                h = (h * 397) ^ Value;
                h = (h * 397) ^ TickAccumulated;
                h = (h * 397) ^ (int)Stability;
                return h;
            }
        }

        public static bool operator ==(LocalCollapseValue a, LocalCollapseValue b) => a.Equals(b);
        public static bool operator !=(LocalCollapseValue a, LocalCollapseValue b) => !a.Equals(b);

        public override string ToString()
            => $"LocalCV(Coord={Coord}, Val={Value}, Stability={Stability}, Tick={TickAccumulated})";
    }

    /// <summary>
    /// <see cref="LocalCollapseValue"/> 序列化辅助。
    /// 格式：<c>{X},{Y},{LayerByte}|{Value}|{StabilityByte}|{Tick}</c>。
    /// </summary>
    public static class LocalCollapseValueCodec
    {
        public static byte[] Serialize(LocalCollapseValue lcv)
        {
            string s = $"{lcv.Coord.X},{lcv.Coord.Y},{(byte)lcv.Coord.Layer}|{lcv.Value}|{(byte)lcv.Stability}|{lcv.TickAccumulated}";
            return Encoding.UTF8.GetBytes(s);
        }

        public static LocalCollapseValue Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("bytes is null or empty", nameof(bytes));
            string s = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException("decoded string is empty", nameof(bytes));
            string[] parts = s.Split('|');
            if (parts.Length != 4)
                throw new ArgumentException($"invalid format: '{s}' (expected 4 parts)", nameof(bytes));
            string[] coordParts = parts[0].Split(',');
            if (coordParts.Length != 3)
                throw new ArgumentException($"invalid coord: '{parts[0]}' (expected X,Y,Layer)", nameof(bytes));
            int x = int.Parse(coordParts[0], System.Globalization.CultureInfo.InvariantCulture);
            int y = int.Parse(coordParts[1], System.Globalization.CultureInfo.InvariantCulture);
            byte layer = byte.Parse(coordParts[2], System.Globalization.CultureInfo.InvariantCulture);
            int v = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            byte st = byte.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            int tk = int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            var coord = new GridCoord(x, y, (Starfall.Core.Map.Coordinates.DimensionLayer)layer);
            var result = new LocalCollapseValue(coord, v, tk);
            if ((byte)result.Stability != st)
                throw new ArgumentException($"stability mismatch: got {st}, expected {(byte)result.Stability}", nameof(bytes));
            return result;
        }
    }
}
