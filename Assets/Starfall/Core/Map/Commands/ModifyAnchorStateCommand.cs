using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 §21.1 锚点状态修改命令（7 状态枚举）。
    /// <para/>
    /// **范围**：将锚点状态切换到 7 个允许状态之一。
    /// <para/>
    /// **依赖**<paramref name="anchorId" /> 必须已在 <see cref="AnchorRegistry"/> 中注册
    ///（否则 <c>"anchor not found"</c>）；本命令自身不创建 anchor ———
    /// 创建通过 <see cref="CreateAnchorLinkCommand"/> 完成。
    /// <para/>
    /// **状态切换约束**：
    /// <list type="bullet">
    /// <item><see cref="AnchorZoneState.Destroyed"/> 不可由本命令设置（一旦 destroyed，规则禁止复活；需走"重建"路径）。</item>
    /// <item>从 <see cref="AnchorZoneState.Locked"/> 切换到非 Locked 时强制 warning 通道（业务层面提示）。</item>
    /// </list>
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件，含 AnchorId / Description。
    /// （区域 / 锚点的事件统一走 OnRegionChanged；锚点专一事件 OnAnchorLinkCreated 留给 CreateAnchorLinkCommand）。
    /// </summary>
    public sealed class ModifyAnchorStateCommand : IMapCommand
    {
        public int AnchorId { get; }
        public AnchorZoneState NewState { get; }

        public ModifyAnchorStateCommand(int anchorId, AnchorZoneState newState)
        {
            if (anchorId < 0)
                throw new ArgumentOutOfRangeException(nameof(anchorId), anchorId,
                    "AnchorId must be >= 0.");
            AnchorId = anchorId;
            NewState = newState;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) anchor 必须存在 — MapState.Anchors 集合（由 CreateAnchorLinkCommand 先加入）。
            //    防御性扫描：避免盲目成功。
            bool found = false;
            for (int i = 0; i < mapState.Anchors.Count; i++)
            {
                if (mapState.Anchors[i].ZoneId == AnchorId) { found = true; break; }
            }
            if (!found)
                return MapCommandResult.Fail("anchor not found");

            // 2) 拒绝 Destroyed
            if (NewState == AnchorZoneState.Destroyed)
                return MapCommandResult.Fail("use Destroy anchor flow; modify-state-to-destroyed forbidden");

            // 3) 改写状态
            AnchorZoneServicePostAttach(mapState);
            AnchorZoneState prev = AnchorStateService.GetOrDefault(mapState, AnchorId);
            if (prev == NewState)
                return MapCommandResult.Fail("no-op: state unchanged");

            _previousState = prev;
            AnchorStateService.SetState(mapState, AnchorId, NewState);
            _executed = true;

            var events = new List<MapEvent>(1)
            {
                new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null,
                    anchorId: AnchorId,
                    description: $"anchor-state:{prev}->{NewState}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        private static void AnchorZoneServicePostAttach(MapState mapState)
        {
            // 防御性：保证 AnchorStateService 已 attach 当前 mapState。
            // TryGetState 会因未 attach 而返回 false / Inactive；为避免 Execute 失败我们先 Attach。
            AnchorStateService.Attach(mapState);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("ModifyAnchorStateCommand.Undo called without prior Execute.");
            AnchorStateService.SetState(mapState, AnchorId, _previousState);
            _executed = false;
        }

        public int Version => 1;
        public string CommandId => $"modify-anchor-state:{AnchorId}";

        public IReadOnlyList<string> Dependencies
        {
            get
            {
                // 强依赖：必须有创建锚点 link 的命令先跑过。
                return new[] { $"create-anchor-link:{AnchorId}" };
            }
        }

        private bool _executed;
        private AnchorZoneState _previousState;

        public override string ToString()
            => $"ModifyAnchorStateCommand(AnchorId={AnchorId}, NewState={NewState})";
    }
}
