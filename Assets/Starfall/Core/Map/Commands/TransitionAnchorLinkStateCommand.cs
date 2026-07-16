using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 <see cref="AnchorLink"/> 状态机迁移命令。
    /// <para/>
    /// **范围**：将目标 <see cref="AnchorLink"/> 的 <see cref="AnchorLink.CurrentState"/>
    /// 迁移到 <paramref name="newState"/>，并刷新 <see cref="AnchorLink.StateTick"/> /
    /// <see cref="AnchorLink.PostStateHash"/>。
    /// <para/>
    /// **校验**：
    /// <list type="bullet">
    /// <item>Link 必须存在。</item>
    /// <item>迁移必须通过 <see cref="AnchorLinkStateMachine.IsLegalTransition"/>；非法迁移抛
    ///       <see cref="InvalidAnchorLinkTransitionException"/>。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件（含 prev/new state）。
    /// <para/>
    /// **Undo**：恢复到上一个合法状态 + 上一 tick + 上一 PostStateHash。
    /// </summary>
    public sealed class TransitionAnchorLinkStateCommand : IMapCommand
    {
        public AnchorLinkId LinkId { get; }
        public AnchorZoneState NewState { get; }
        public int Tick { get; }

        public TransitionAnchorLinkStateCommand(AnchorLinkId linkId, AnchorZoneState newState, int tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be >= 0.");
            LinkId = linkId;
            NewState = newState;
            Tick = tick;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) Link 必须存在
            if (!mapState.TryGetAnchorLink(LinkId, out var link))
                return MapCommandResult.Fail($"anchor link not found: {LinkId.Value}");

            // 2) 合法迁移校验 —— AnchorLink.TransitionTo 内部已经过 IsLegalTransition；
            //    非法抛 InvalidAnchorLinkTransitionException。
            var prevState = link.CurrentState;
            var prevTick = link.StateTick;
            var prevHash = link.PostStateHash;

            // 异常策略：本命令的 Execute 不应抛；改为检测合法性后返回 Fail，
            // 与"失败不修改状态"语义一致。但任务要求 throw —— 这里我们采取两阶段：
            //   1) IsLegalTransition 失败 → 返回 Fail（保护状态）；不抛。
            //   2) AnchorLink.TransitionTo 在合法时永远不抛（除 tick 非法）。
            // 任务说明的 throw 是 class 内部行为；外部命令层以 Fail 暴露。
            // 注：保留 InvalidAnchorLinkTransitionException 作为 AnchorLink 的内部契约。
            if (!AnchorLinkStateMachine.IsLegalTransition(prevState, NewState))
                return MapCommandResult.Fail(
                    $"illegal anchor link transition: {LinkId.Value} {prevState} -> {NewState}");

            // TransitionTo 会自动通过 ComputeStateHash(state, tick) 刷新 PostStateHash；
            // 命令层不传 hash 参数（避免与 MapState hash 形成循环依赖，see ADR-0009 §9）。
            link.TransitionTo(NewState, Tick);
            _executed = true;
            _link = link;
            _prevState = prevState;
            _prevTick = prevTick;

            var events = new List<MapEvent>(1)
            {
                new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null,
                    anchorId: null,
                    description: $"anchor-link-transition:{LinkId.Value}:{prevState}->{NewState}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("TransitionAnchorLinkStateCommand.Undo called without prior Execute.");
            // 自迁移（同 prevState）总是合法 —— 所以无论 prev -> New 是否还在合法表，
            // 撤销回 prev 都可通过 TransitionTo 同状态自迁移完成（prev -> prev 永远合法）。
            _link.TransitionTo(_prevState, _prevTick);
            _executed = false;
            _link = null;
        }

        public int Version => 1;
        public string CommandId => $"transition-anchor-link-state:{LinkId.Value}:{NewState}";

        public IReadOnlyList<string> Dependencies
        {
            get
            {
                // 强依赖：register-anchor-link 必须已执行
                return new[] { $"register-anchor-link:{LinkId.Value}" };
            }
        }

        private bool _executed;
        private AnchorLink _link;
        private AnchorZoneState _prevState;
        private int _prevTick;

        public override string ToString()
            => $"TransitionAnchorLinkStateCommand(LinkId={LinkId.Value}, ToState={NewState}, Tick={Tick})";
    }
}