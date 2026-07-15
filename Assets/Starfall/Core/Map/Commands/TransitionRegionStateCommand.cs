using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Regions;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-09 区域状态机转换命令。
    ///
    /// <para/>
    /// **范围**：通过 <see cref="MapRegionService.TransitionState"/> 修改
    /// <see cref="MapState.RegionStates"/> 中指定 region 的 <see cref="RegionState"/>。
    ///
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>RegionId 必须存在。</item>
    /// <item>(from → to) 必须合法（参考 <see cref="MapRegionService.IsTransitionAllowed"/>）。</item>
    /// </list>
    ///
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 RegionId + OldValue=pre-state, NewValue=new-state）。
    /// </summary>
    public sealed class TransitionRegionStateCommand : IMapCommand
    {
        public int RegionId { get; }
        public RegionState NewState { get; }
        public string Reason { get; }

        public TransitionRegionStateCommand(int regionId, RegionState newState, string reason = null)
        {
            if (regionId < 0)
                throw new ArgumentOutOfRangeException(nameof(regionId), regionId,
                    "RegionId must be >= 0.");
            RegionId = regionId;
            NewState = newState;
            Reason = reason ?? string.Empty;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) 查找 region
            MapRegionState target = null;
            for (int i = 0; i < mapState.RegionStates.Count; i++)
            {
                if (mapState.RegionStates[i].Definition.RegionIdValue.Value == RegionId)
                {
                    target = mapState.RegionStates[i];
                    break;
                }
            }
            if (target == null)
                return MapCommandResult.Fail($"region {RegionId} not found");

            // 2) 转换合法性校验
            if (!MapRegionService.IsTransitionAllowed(target.State, NewState))
                return MapCommandResult.Fail(
                    $"illegal transition {target.State} -> {NewState}");

            // 3) 应用 + emit event
            var old = target.State;
            target.SetStateInternal(NewState, target.TickEntered);
            if (NewState == RegionState.Sealed)
                target.ClearOccupiedCellsInternal();
            if (NewState == RegionState.Active)
                target.SetActivationProgressInternal(0, target.TickEntered);
            _executed = true;
            _previousState = old;

            var events = new List<MapEvent>(1)
            {
                MapRegionService.MakeStateChangedEvent(RegionId, old, NewState, Reason)
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException(
                    "TransitionRegionStateCommand.Undo called without prior Execute.");
            // 简单回滚：直接调 service 把状态改回去（合法性已通过前置检查）
            var service = new MapRegionService();
            try
            {
                service.TransitionState(mapState, new RegionId(RegionId), _previousState, "undo");
                _executed = false;
            }
            catch
            {
                // 不重抛（无法 undo 时业务已记录）
            }
        }

        public int Version => 1;
        public string CommandId => $"transition-region-state:{RegionId}:{(byte)NewState}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private RegionState _previousState;

        public override string ToString()
            => $"TransitionRegionStateCommand(RegionId={RegionId}, NewState={NewState}, Reason={Reason})";
    }
}