using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 修改全局坍塌值（CV）命令。
    /// <para/>
    /// **范围**：[0, 100]，由 doc1 §13.1 约束，与 <see cref="MapState.GlobalCollapseValue"/>
    /// 共同参与单位受坍塌伤害概率 / tile 稳定性下降。
    /// <para/>
    /// **实现路径**：直接修改 <see cref="MapState.GlobalCollapseValue"/>（clamp 到 [0, 100]）。
    /// <para/>
    /// **失败条件**：
    /// <list type="bullet">
    /// <item>新值已在当前值（避免无效命令） → <c>"no-op: value unchanged"</c>。</item>
    /// <item>新值越界（构造时已拒绝）；运行时仍防御性拒绝 → <c>"invalid value"</c>。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnGlobalCVChanged"/> 事件，含 old / new 值。
    /// <para/>
    /// **依赖**：无（独立命令；CV 是 map 全局状态）。
    /// </summary>
    public sealed class ModifyGlobalCVCommand : IMapCommand
    {
        public int NewGlobalCV { get; }

        public ModifyGlobalCVCommand(int newGlobalCV)
        {
            if (newGlobalCV < 0 || newGlobalCV > 100)
                throw new ArgumentOutOfRangeException(nameof(newGlobalCV), newGlobalCV,
                    "NewGlobalCV must be in [0, 100] (doc1 §13.1).");
            NewGlobalCV = newGlobalCV;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 运行时再校验（防御）
            if (NewGlobalCV < 0 || NewGlobalCV > 100)
                return MapCommandResult.Fail("invalid value");

            int oldCV = mapState.GlobalCollapseValue;
            if (oldCV == NewGlobalCV)
                return MapCommandResult.Fail("no-op: value unchanged");

            _previousGlobalCV = oldCV;
            mapState.GlobalCollapseValue = NewGlobalCV;
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                MapEvent.GlobalCVChanged(oldCV, NewGlobalCV, "global-cv")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("ModifyGlobalCVCommand.Undo called without prior Execute.");
            mapState.GlobalCollapseValue = _previousGlobalCV;
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"modify-global-cv";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private int _previousGlobalCV;

        public override string ToString()
            => $"ModifyGlobalCVCommand(NewCV={NewGlobalCV})";
    }
}
