using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Collapse
{
    /// <summary>
    /// doc2 MAP-11a §21.1 修改全局坍塌值命令（IMapCommand；ADR-0007）。
    ///
    /// <para/>
    /// **与 MAP-03 ModifyGlobalCVCommand 的区别**：
    /// <list type="bullet">
    /// <item>MAP-03 是"设置"语义（NewGlobalCV 绝对值）。</item>
    /// <item>MAP-11a 是"delta"语义（Delta = +N / -N，相对修改）。</item>
    /// <item>使用 typed <see cref="MapState.GlobalCV"/> 字段；同步 <see cref="MapState.GlobalCollapseValue"/> 影子。</item>
    /// <item>Emit <see cref="MapEventKind.OnGlobalCVChanged"/> 事件，含 old / new value。</item>
    /// </list>
    ///
    /// <para/>
    /// **范围**：[−100, +100]（构造时校验），执行后值自动 clamp 到 [0, 100]。
    ///
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>Delta 越界（构造时已拒绝）。</item>
    /// <item>Delta = 0：视为 no-op，返回 <c>"no-op: delta is zero"</c>。</item>
    /// <item>Undo 未执行就调用：抛 <see cref="InvalidOperationException"/>。</item>
    /// </list>
    /// </summary>
    public sealed class ModifyGlobalCollapseValueCommand : IMapCommand
    {
        public const int MaxDeltaMagnitude = 100;

        /// <summary>相对变化量 ∈ [-100, +100]。</summary>
        public int Delta { get; }

        /// <summary>可读原因（写入事件 Description，不影响确定性）。</summary>
        public string Reason { get; }

        public ModifyGlobalCollapseValueCommand(int delta, string reason = null)
        {
            if (delta < -MaxDeltaMagnitude || delta > MaxDeltaMagnitude)
                throw new ArgumentOutOfRangeException(nameof(delta), delta,
                    $"Delta must be in [-{MaxDeltaMagnitude}, +{MaxDeltaMagnitude}].");
            Delta = delta;
            Reason = reason ?? string.Empty;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 运行时再校验（防御）
            if (Delta < -MaxDeltaMagnitude || Delta > MaxDeltaMagnitude)
                return MapCommandResult.Fail("invalid delta");

            if (Delta == 0)
                return MapCommandResult.Fail("no-op: delta is zero");

            int oldValue = mapState.GlobalCV.Value;
            int newValue = oldValue + Delta;
            // clamp
            if (newValue < 0) newValue = 0;
            if (newValue > 100) newValue = 100;

            // 存储 prior（用于 Undo）
            _previousGlobalCV = mapState.GlobalCV;
            mapState.GlobalCV = mapState.GlobalCV.WithValue(newValue);
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.GlobalCVChanged(oldValue, newValue, string.IsNullOrEmpty(Reason) ? "modify-global-cv" : Reason)
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("ModifyGlobalCollapseValueCommand.Undo called without prior Execute.");
            mapState.GlobalCV = _previousGlobalCV;
            _executed = false;
        }

        public int Version => 1;

        public string CommandId => "modify-global-collapse-value";

        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private GlobalCollapseValue _previousGlobalCV;

        public override string ToString()
            => $"ModifyGlobalCollapseValueCommand(Delta={Delta}, Reason={Reason})";
    }
}
