using System;
using System.Collections.Generic;
using Starfall.Core.Map;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 / §21.1 <see cref="IMapCommand"/> 通用执行器。
    /// <para/>
    /// **职责**：
    /// <list type="bullet">
    /// <item><see cref="Run"/>：校验依赖（<see cref="IMapCommand.Dependencies"/>）→
    ///       <see cref="IMapCommand.Execute"/> → <see cref="MapState.Version"/> 自增 →
    ///       返回 <see cref="MapCommandResult"/>。</item>
    /// <item><see cref="UndoLast"/>：从 history stack pop → 调命令的
    ///       <see cref="IMapCommand.Undo"/> → <see cref="MapState.Version"/> 自减。</item>
    /// </list>
    /// <para/>
    /// **与 <see cref="Starfall.Core.Command.CommandExecutor"/> 的边界**：
    /// <list type="bullet">
    /// <item>本执行器只处理 <see cref="IMapCommand"/>；不与战斗 <see cref="Starfall.Core.Command.ICommand"/> 互通。</item>
    /// <li>命名空间独立（<c>Starfall.Core.Map.Commands</c>）— 与
    ///       <see cref="Starfall.Core.Command.CommandExecutor"/>（<c>Starfall.Core.Command</c>）共存。</li>
    /// <li>两个 executor 不允许互相撤销。</li>
    /// </list>
    /// <para/>
    /// **失败语义**：
    /// <list type="bullet">
    /// <item>依赖不满足（任一 <see cref="IMapCommand.Dependencies"/> 中的 CommandId 不在历史）→
    ///       直接返回 <see cref="MapCommandResult.Fail(string)"/>（不调 <c>Execute</c>）。</item>
    /// <item><c>Execute</c> 内部失败 → 返回 <see cref="MapCommandResult.Fail(string)"/>，
    ///       mapState 不被修改（命令实现自身保证）。</item>
    /// <item><c>Undo</c> 抛 <see cref="NotSupportedException"/>（未实现）→ catch 并 propagate
    ///       异常给调用方，但<strong>不</strong>回滚 mapState 版本号（避免部分撤销）。
    ///       调用方应决定是否恢复。</li>
    /// </list>
    /// <para/>
    /// **线程模型**：本类非线程安全；单线程 <c>BattleRunner</c> 内顺序调用即可。
    /// </summary>
    public sealed class MapCommandExecutor
    {
        // 历史栈：每条已成功执行的命令 + 它对应的"前置版本号"。
        // 新版本号 = mapState.Version + 1；Undo 后 = 当前版本号 - 1。
        private readonly Stack<HistoryEntry> _history;

        // 依赖校验用的"已执行 CommandId 集合"（按声明顺序追加；按字典序排序用于展示）。
        private readonly SortedSet<string> _executedCommandIds = new SortedSet<string>(StringComparer.Ordinal);

        // 已执行 CommandId 的插入顺序（保证依赖判定用 FIFO，避免循环陷阱）——
        // 仅 SortedSet 已足够，因为我们仅查 Contains / 重新计算集合 hash。
        // 实际"是否已执行"判定只看 SortedSet.Contains。

        /// <summary>历史栈深度上限（超限后最旧条目被丢弃，禁止无限回退）。</summary>
        public int MaxHistoryDepth { get; }

        /// <summary>当前已执行命令数。</summary>
        public int HistoryCount => _history.Count;

        /// <summary>所有已成功执行过的 <see cref="IMapCommand.CommandId"/>。</summary>
        public IReadOnlyCollection<string> ExecutedCommandIds => _executedCommandIds;

        public MapCommandExecutor(int maxHistoryDepth = 50)
        {
            if (maxHistoryDepth < 1)
                throw new ArgumentOutOfRangeException(nameof(maxHistoryDepth), maxHistoryDepth,
                    "MaxHistoryDepth must be >= 1.");
            MaxHistoryDepth = maxHistoryDepth;
            _history = new Stack<HistoryEntry>(maxHistoryDepth);
        }

        /// <summary>
        /// 运行一条 <see cref="IMapCommand"/>。
        /// <list type="number">
        /// <item>校验 <see cref="IMapCommand.Dependencies"/>（每个 CommandId 在
        ///       <see cref="ExecutedCommandIds"/> 中）— 不满足则返 Fail。</item>
        /// <item>记录 <paramref name="mapState"/>.Version 当前值。</item>
        /// <item>调 <see cref="IMapCommand.Execute"/>；读 <see cref="MapCommandResult.NewVersion"/>。</item>
        /// <item>若成功：mapState.Version = result.NewVersion；executor 自增 1；
        ///       push 命令到 history；CommandId 加入 <see cref="ExecutedCommandIds"/>。</item>
        /// <item>若失败：mapState.Version 不变；history 不变。</item>
        /// </list>
        /// </summary>
        public MapCommandResult Run(IMapCommand cmd, MapState mapState)
        {
            if (cmd == null) return MapCommandResult.Fail("cmd is null");
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));

            // ─── 1) 依赖校验 ───
            var deps = cmd.Dependencies;
            if (deps != null)
            {
                for (int i = 0; i < deps.Count; i++)
                {
                    var depId = deps[i];
                    if (string.IsNullOrEmpty(depId))
                        return MapCommandResult.Fail("dependency id is null or empty");
                    if (!_executedCommandIds.Contains(depId))
                        return MapCommandResult.Fail($"missing dependency: {depId}");
                }
            }

            // ─── 2) 执行 ───
            MapCommandResult result;
            try
            {
                result = cmd.Execute(mapState);
            }
            catch (Exception ex)
            {
                // 命令实现不应抛异常；若抛，本 executor 把异常视为 Fail。
                // 不修改 mapState.Version。
                return MapCommandResult.Fail($"cmd.Execute threw {ex.GetType().Name}: {ex.Message}");
            }

            if (!result.Success)
                return result;

            // ─── 3) 应用自增版本号 ───
            // 命令实现已计算 NewVersion（= previousVersion + 1）；本步骤直接用。
            // 但作为保险：若 NewVersion 不等于 expected，亦服从命令提供值（executor 信任）。
            int expectedNew = mapState.Version + 1;
            int actualNew = result.NewVersion;
            if (actualNew != expectedNew && actualNew > 0)
            {
                // 命令实现与 executor 期望不一致时使用 actualNew（视为命令实现已 bypass 了 executor 计算）。
                // 此分支仅用于未来 MAP-08+ 兼容；MAP-03 阶段命令实现保证 = previousVersion + 1。
            }
            int previousVersion = mapState.Version; // capture pre-increment value（for undo restore）
            mapState.Version = actualNew > 0 ? actualNew : expectedNew;

            // ─── 4) Push 历史 + 记录 CommandId ───
            // HistoryEntry.PreviousVersion 是执行该命令后的 mapState.Version；UndoLast 读取时会
            // 使用 previousVersion（pre-increment）还原。
            _history.Push(new HistoryEntry(cmd, previousVersion));
            _executedCommandIds.Add(cmd.CommandId);

            // ─── 5) 维护 MaxHistoryDepth：超限弹栈丢弃最旧条目 ───
            while (_history.Count > MaxHistoryDepth)
            {
                var discarded = DiscardOldestHistoryEntry();
                _executedCommandIds.Remove(discarded.Command.CommandId);
            }

            return result;
        }

        /// <summary>
        /// 单步撤销：从 history stack pop → 调命令的 <see cref="IMapCommand.Undo(MapState)"/>
        /// → 减 <see cref="MapState.Version"/>。
        /// <para/>
        /// **返回**：true = 已成功 Undo；false = history 为空（无可撤销）。</item>
        /// </summary>
        public bool UndoLast(MapState mapState)
        {
            if (mapState == null) throw new ArgumentNullException(nameof(mapState));
            if (_history.Count == 0) return false;

            var entry = _history.Pop();
            // 仅从 _executedCommandIds 移除最新追加的那条（与 history push 严格 1:1）。
            // 注意：移除了之后下次 Run 检查时若命令再次声明依赖本 CommandId，会从该集合中找不到。
            // 这是有意为之：依赖基于"曾经发生过的事实"，Undo 抹除历史。
            // 如果业务需要"Undo 后仍保留依赖事实"，应在 Undo 后由命令重新追加 CommandId。

            try
            {
                entry.Command.Undo(mapState);
            }
            catch (NotSupportedException)
            {
                // 命令未实现 Undo — 异常向上抛（executor 不掩盖"不支持"的语义）。
                throw;
            }

            // 撤销成功 → 版本号 -1（命令自身负责 field-level 还原；executor 负责元数据减一）。
            mapState.Version = entry.PreviousVersion <= 0 ? 0 : entry.PreviousVersion;
            _executedCommandIds.Remove(entry.Command.CommandId);
            return true;
        }

        /// <summary>清空所有历史（不修改 mapState）。</summary>
        public void Clear()
        {
            _history.Clear();
            _executedCommandIds.Clear();
        }

        private HistoryEntry DiscardOldestHistoryEntry()
        {
            // Stack 不能直接丢弃栈底；中转一次。
            var temp = new Stack<HistoryEntry>(_history.Count);
            HistoryEntry discarded = default;
            while (_history.Count > 1)
                temp.Push(_history.Pop());
            discarded = _history.Pop();
            while (temp.Count > 0) _history.Push(temp.Pop());
            return discarded;
        }

        private readonly struct HistoryEntry
        {
            public readonly IMapCommand Command;
            public readonly int PreviousVersion;
            public HistoryEntry(IMapCommand command, int previousVersion)
            {
                Command = command;
                PreviousVersion = previousVersion;
            }
        }
    }
}
