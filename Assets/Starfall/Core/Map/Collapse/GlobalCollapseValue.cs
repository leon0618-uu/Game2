using System;
using System.Text;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a 全局坍塌值（readonly struct，ADR-0007）。
    ///
    /// <para/>
    /// 范围 [0, 100]（doc1 §13.1 + 5 阶段状态机）。任何赋值都自动 clamp；并自动
    /// 派生 <see cref="Stage"/>（按 <see cref="CollapseStageMapping.FromValue"/>）。
    ///
    /// <para/>
    /// <b>字段</b>：
    /// <list type="bullet">
    /// <item><see cref="Value"/>：当前 CV 值（clamp 后 ∈ [0, 100]）。</item>
    /// <item><see cref="Stage"/>：根据 <see cref="Value"/> 自动计算（构造时固化，不再改变）。</item>
    /// <item><see cref="Threshold"/>：当前阶段上限，用于阶段切换检测（与 <see cref="CollapseStageMapping.MaxValue"/> 一致）。</item>
    /// <item><see cref="TickAccumulated"/>：累积的回合数（每 Tick +1）。</item>
    /// </list>
    ///
    /// <para/>
    /// <b>不可变性</b>：所有字段 readonly；任何"修改"通过工厂 <see cref="Of"/> /
    /// <see cref="FromStage"/> 返回新实例。
    /// </summary>
    public readonly struct GlobalCollapseValue : IEquatable<GlobalCollapseValue>
    {
        /// <summary>CV 值 ∈ [0, 100]。构造时 clamp。</summary>
        public readonly int Value;

        /// <summary>当前阶段（按 <see cref="Value"/> 计算，构造时固化）。</summary>
        public readonly CollapseStage Stage;

        /// <summary>当前阶段上限（含），用于阶段切换检测。</summary>
        public readonly int Threshold;

        /// <summary>累积的回合数（每 Tick +1）。</summary>
        public readonly int TickAccumulated;

        public GlobalCollapseValue(int value, int tickAccumulated)
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            Value = value;
            Stage = CollapseStageMapping.FromValue(value);
            Threshold = CollapseStageMapping.MaxValue(Stage);
            TickAccumulated = tickAccumulated;
        }

        // ──────────── 工厂 ────────────

        /// <summary>零值（CV=0, Tick=0）。</summary>
        public static GlobalCollapseValue Zero => new GlobalCollapseValue(0, 0);

        /// <summary>按值构造（自动 clamp，自动派生 Stage / Threshold）。</summary>
        public static GlobalCollapseValue Of(int value) => new GlobalCollapseValue(value, 0);

        /// <summary>按值 + tick 构造。</summary>
        public static GlobalCollapseValue Of(int value, int tickAccumulated) => new GlobalCollapseValue(value, tickAccumulated);

        /// <summary>从阶段构造（取该阶段 <see cref="CollapseStageMapping.MaxValue"/> 作为 value）。</summary>
        public static GlobalCollapseValue FromStage(CollapseStage stage)
        {
            int v = CollapseStageMapping.MaxValue(stage);
            return new GlobalCollapseValue(v, 0);
        }

        // ──────────── 派生操作（返回新实例）────────────

        /// <summary>把 <see cref="Value"/> 加 delta（clamp 到 [0, 100]），TickAccumulated 保持不变。</summary>
        public GlobalCollapseValue WithDelta(int delta)
        {
            return new GlobalCollapseValue(Value + delta, TickAccumulated);
        }

        /// <summary>把 <see cref="Value"/> 设为 newValue，TickAccumulated 保持不变。</summary>
        public GlobalCollapseValue WithValue(int newValue)
        {
            return new GlobalCollapseValue(newValue, TickAccumulated);
        }

        /// <summary>Tick 推进（TickAccumulated +1）。</summary>
        public GlobalCollapseValue WithIncrementedTick()
        {
            return new GlobalCollapseValue(Value, TickAccumulated + 1);
        }

        // ──────────── 等值 / 字符串 ────────────

        public bool Equals(GlobalCollapseValue other)
            => Value == other.Value
               && TickAccumulated == other.TickAccumulated;

        public override bool Equals(object obj) => obj is GlobalCollapseValue other && Equals(other);

        public override int GetHashCode()
        {
            // FNV-like fold（避开 object/string.GetHashCode 跨语言不稳定）
            unchecked
            {
                int h = Value * 397;
                h = (h * 397) ^ TickAccumulated;
                h = (h * 397) ^ (int)Stage;
                return h;
            }
        }

        public static bool operator ==(GlobalCollapseValue a, GlobalCollapseValue b) => a.Equals(b);
        public static bool operator !=(GlobalCollapseValue a, GlobalCollapseValue b) => !a.Equals(b);

        public override string ToString()
            => $"GlobalCV(Val={Value}, Stage={Stage}, Threshold={Threshold}, Tick={TickAccumulated})";
    }

    /// <summary>
    /// <see cref="GlobalCollapseValue"/> 序列化辅助：UTF-8 字节流（不含 BOM）。
    /// 格式：<c>{Value}|{StageByte}|{Threshold}|{TickAccumulated}</c>。
    /// 仅用于 Replay / 调试；正式 Replay 由 <c>MapStateHasher</c> 走 FNV-1a 协议。
    /// </summary>
    public static class GlobalCollapseValueCodec
    {
        public static byte[] Serialize(GlobalCollapseValue gcv)
        {
            string s = $"{gcv.Value}|{(byte)gcv.Stage}|{gcv.Threshold}|{gcv.TickAccumulated}";
            return Encoding.UTF8.GetBytes(s);
        }

        public static GlobalCollapseValue Deserialize(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("bytes is null or empty", nameof(bytes));
            string s = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrEmpty(s))
                throw new ArgumentException("decoded string is empty", nameof(bytes));
            string[] parts = s.Split('|');
            if (parts.Length != 4)
                throw new ArgumentException($"invalid format: '{s}' (expected 4 parts)", nameof(bytes));
            int v = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            byte st = byte.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            int th = int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            int tk = int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            var result = new GlobalCollapseValue(v, tk);
            // 校验：st / th 必须与构造结果一致
            if ((byte)result.Stage != st)
                throw new ArgumentException($"stage mismatch: got {st}, expected {(byte)result.Stage}", nameof(bytes));
            if (result.Threshold != th)
                throw new ArgumentException($"threshold mismatch: got {th}, expected {result.Threshold}", nameof(bytes));
            return result;
        }
    }
}
