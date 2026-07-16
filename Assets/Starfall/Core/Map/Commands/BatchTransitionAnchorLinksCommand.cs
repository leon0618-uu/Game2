using System;
using System.Collections.Generic;
using Starfall.Core.Anchor;
using Starfall.Core.Map;
using Starfall.Core.Map.Anchor;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-12 批量状态迁移命令（多个 AnchorLink 一次提交，用于律令结算）。
    /// <para/>
    /// **范围**：对多条 (LinkId, NewState, Tick) 一次性提交；任一子迁移非法 → 整个命令失败、
    /// 状态零修改（与 <see cref="IMapCommand"/> "失败不修改" 语义一致）。
    /// <para/>
    /// **校验**：所有子 transition 必须通过 <see cref="AnchorLinkStateMachine.IsLegalTransition"/>；
    /// 任何非法抛 / 返回 Fail。
    /// <para/>
    /// **Undo**：单条命令 Undo 恢复所有已修改 link 的上一个状态 / tick / hash（顺序无关：
    /// 每个 link 自迁移到 prev 总是合法）。
    /// <para/>
    /// **依赖**：合并所有子 transition 依赖的 register-anchor-link:{id} 列表去重并按字典序排序。
    /// </summary>
    public sealed class BatchTransitionAnchorLinksCommand : IMapCommand
    {
        /// <summary>单条子迁移。</summary>
        public readonly struct TransitionEntry
        {
            public readonly AnchorLinkId LinkId;
            public readonly AnchorZoneState NewState;
            public readonly int Tick;

            public TransitionEntry(AnchorLinkId linkId, AnchorZoneState newState, int tick)
            {
                if (tick < 0)
                    throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must be >= 0.");
                LinkId = linkId;
                NewState = newState;
                Tick = tick;
            }
        }

        public IReadOnlyList<TransitionEntry> Entries { get; }

        public BatchTransitionAnchorLinksCommand(IReadOnlyList<TransitionEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (entries.Count == 0)
                throw new ArgumentException("Batch must have >= 1 entry.", nameof(entries));
            Entries = entries;
        }

        public MapCommandResult Execute(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            int previousVersion = mapState.Version;
            int newVersion = previousVersion + 1;

            // ── Phase 1: 校验所有 entries（不做任何修改） ──
            var resolveBuf = new List<(AnchorLink link, AnchorZoneState prevState, int prevTick)>(Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (!mapState.TryGetAnchorLink(e.LinkId, out var link))
                    return MapCommandResult.Fail($"anchor link not found: {e.LinkId.Value}");

                if (!AnchorLinkStateMachine.IsLegalTransition(link.CurrentState, e.NewState))
                {
                    return MapCommandResult.Fail(
                        $"illegal anchor link transition (batch index {i}): {e.LinkId.Value} {link.CurrentState} -> {e.NewState}");
                }

                resolveBuf.Add((link, link.CurrentState, link.StateTick));
            }

            // ── Phase 2: 应用所有修改（不会失败，因为 Phase 1 已校验）────────
            // 注：TransitionTo 内部自动 ComputeStateHash(state, tick) 刷新 PostStateHash，
            // 命令层不传 hash 参数（避免与 MapState hash 形成循环依赖，see ADR-0009 §9）。
            var events = new List<MapEvent>(Entries.Count);
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                var snap = resolveBuf[i];
                snap.link.TransitionTo(entry.NewState, entry.Tick);
                events.Add(new MapEvent(MapEventKind.OnRegionChanged,
                    regionId: null, anchorId: null,
                    description: $"anchor-link-batch-transition:{snap.link.Id.Value}:{snap.prevState}->{snap.link.CurrentState}"));
            }

            // 保存快照用于 Undo
            _executed = true;
            _snapshot = resolveBuf;
            events.Sort();
            return MapCommandResult.Ok(events, newVersion);
        }

        public void Undo(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (!_executed)
                throw new InvalidOperationException("BatchTransitionAnchorLinksCommand.Undo called without prior Execute.");
            // 每个 link 自迁移回 prev 总是合法（prev -> prev）。
            for (int i = 0; i < _snapshot.Count; i++)
            {
                var snap = _snapshot[i];
                snap.link.TransitionTo(snap.prevState, snap.prevTick);
            }
            _executed = false;
            _snapshot = null;
        }

        public int Version => 1;

        public string CommandId
        {
            get
            {
                // 稳定 CommandId：所有 entries 的 LinkId 升序拼成 summary。
                var ids = new List<string>(Entries.Count);
                for (int i = 0; i < Entries.Count; i++) ids.Add(Entries[i].LinkId.Value);
                ids.Sort(StringComparer.Ordinal);
                return "batch-transition-anchor-links:" + string.Join(",", ids);
            }
        }

        public IReadOnlyList<string> Dependencies
        {
            get
            {
                var deps = new SortedSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < Entries.Count; i++)
                {
                    deps.Add($"register-anchor-link:{Entries[i].LinkId.Value}");
                }
                var list = new List<string>(deps);
                return list;
            }
        }

        private bool _executed;
        private List<(AnchorLink link, AnchorZoneState prevState, int prevTick)> _snapshot;

        public override string ToString()
            => $"BatchTransitionAnchorLinksCommand(Count={Entries.Count})";
    }
}