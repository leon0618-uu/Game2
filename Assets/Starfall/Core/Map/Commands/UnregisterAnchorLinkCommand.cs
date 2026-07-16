using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 撤销注册 <see cref="AnchorLink"/> 命令。
    /// <para/>
    /// **范围**：按 <see cref="AnchorLinkId"/> 从 <see cref="MapState.AnchorLinks"/> 集合移除。
    /// <para/>
    /// **校验**：Link 必须存在（否则 <c>"anchor link not found"</c>）。
    /// <para/>
    /// **Emit**：单 <see cref="MapEventKind.OnRegionChanged"/> 事件。
    /// </summary>
    public sealed class UnregisterAnchorLinkCommand : IMapCommand
    {
        public AnchorLinkId LinkId { get; }

        public UnregisterAnchorLinkCommand(AnchorLinkId linkId)
        {
            LinkId = linkId;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // 1) 存在性校验
            if (!mapState.TryGetAnchorLink(LinkId, out var existing))
                return MapCommandResult.Fail($"anchor link not found: {LinkId.Value}");

            // 2) 直接移除（MapState.RemoveAnchorLink 已保证安全）
            bool removed = mapState.RemoveAnchorLink(LinkId);
            if (!removed)
                return MapCommandResult.Fail($"anchor link remove failed: {LinkId.Value}");

            _executed = true;
            _removedLink = existing;
            _removedPostStateHash = mapState.PostStateHash;

            var events = new List<MapEvent>(1)
            {
                new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null,
                    anchorId: null,
                    description: $"anchor-link-unregistered:{LinkId.Value}")
            };
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("UnregisterAnchorLinkCommand.Undo called without prior Execute.");
            mapState.AddAnchorLink(_removedLink);
            _executed = false;
            _removedLink = null;
        }

        public int Version => 1;
        public string CommandId => $"unregister-anchor-link:{LinkId.Value}";
        public IReadOnlyList<string> Dependencies => Array.Empty<string>();

        private bool _executed;
        private AnchorLink _removedLink;
        private ulong _removedPostStateHash;

        public override string ToString()
            => $"UnregisterAnchorLinkCommand(LinkId={LinkId.Value})";
    }
}