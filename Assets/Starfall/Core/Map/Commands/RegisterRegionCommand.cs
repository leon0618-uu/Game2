using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-09 注册新 <see cref="MapRegionState"/> 命令。
    ///
    /// <para/>
    /// **范围**：将一个新的 region 加入 <see cref="MapState.RegionStates"/> 集合；
    /// 通过 <see cref="MapRegionService.Register"/> 完成。
    ///
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item><c>RegionId</c> 与现有 region 不重复。</item>
    /// <item>Definition 字段全部合法（<see cref="MapRegionDefinition"/> 构造已校验）。</item>
    /// </list>
    ///
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 RegionId + OldValue=0=Disabled, NewValue=initial state）。
    /// </summary>
    public sealed class RegisterRegionCommand : IMapCommand
    {
        public MapRegionDefinition Definition { get; }

        public RegisterRegionCommand(MapRegionDefinition definition)
        {
            Definition = definition;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // RegionId 不重复
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                if (mapState.RegionStates[i].Definition.RegionIdValue == Definition.RegionIdValue)
                    return MapCommandResult.Fail($"duplicate region id {Definition.RegionIdValue.Value}");
            }

            // 通过 service 注册
            var service = new MapRegionService();
            try
            {
                var rs = service.Register(mapState, Definition);
                _executed = true;
                _registeredRegionState = rs;

                var events = new List<MapEvent>(1)
                {
                    MapRegionService.MakeStateChangedEvent(
                        Definition.RegionIdValue.Value, RegionState.Disabled, rs.State, "registered")
                };
                events.Sort();
                return MapCommandResult.Ok(events, newVersion);
            }
            catch (Exception ex)
            {
                return MapCommandResult.Fail($"register failed: {ex.Message}");
            }
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "RegisterRegionCommand.Undo called without prior Execute.");
            mapState.RemoveRegionState(Definition.RegionIdValue.Value);
            _executed = false;
            _registeredRegionState = null;
        }

        public int Version => 1;
        public string CommandId => $"register-region:{Definition.RegionIdValue.Value}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private MapRegionState _registeredRegionState;

        public override string ToString()
            => $"RegisterRegionCommand(RegionId={Definition.RegionIdValue.Value}, Kind={Definition.Kind})";
    }
}