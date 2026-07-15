using System.Collections.Generic;
using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 完整化后的 <c>IMapCommand</c>（覆盖 MAP-08 stub）。
    /// <para/>
    /// **角色**：地图状态修改的唯一入口 ——
    /// 任何对 <see cref="MapState"/> 集合 / Version / GlobalCollapseValue / 运行时状态
    /// （PhaseFlipStateService / AnchorStateService / MapTileState）的变化都必须
    /// 通过 <see cref="MapCommandExecutor"/> 调 <see cref="Execute"/> 完成。
    /// <para/>
    /// **与 <see cref="Starfall.Core.Command.ICommand"/> 的边界**：
    /// <list type="bullet">
    /// <item><see cref="Starfall.Core.Command.ICommand"/>：战斗规则入口（HP / 状态 / 回合），
    ///       写 <see cref="Starfall.Core.Command.BattleEvent"/>。并发 <c>BattleRunner</c>。</item>
    /// <item><c>IMapCommand</c>：地图结构 / 拓扑 / 锚点 / 全局 CV。并发 <c>MapCommandExecutor</c>。</item>
    /// <item>两者并行不交叉：每条命令只允许写其中一类事件。</item>
    /// </list>
    /// <para/>
    /// **执行语义**：
    /// <list type="bullet">
    /// <item><see cref="Execute"/> 接受 <see cref="MapState"/> 作参数，
    ///       返回 <see cref="MapCommandResult"/>（成功 / 失败 + 事件 + 新版本号）。</item>
    /// <item>**失败不修改状态**：命令应在任何写操作前完成校验；失败时返回
    ///       <see cref="MapCommandResult.Fail(string)"/>，map 状态完全不变。</item>
    /// <item><see cref="Undo"/> 由 <see cref="MapCommandExecutor"/> 在 <c>UndoLast</c> 中调用，
    ///       必须严格反向单步操作；不可互相撤销（不允许级联 Undo）。</item>
    /// <item>所有命令必须实现 <see cref="CommandId"/>（稳定标识） + <see cref="Dependencies"/>
    ///       （显式依赖声明，executor 校验）。</item>
    /// </list>
    /// <para/>
    /// **版本号（<see cref="Version"/>）**：
    /// 每次成功执行后由 <see cref="MapCommandExecutor"/> + 1，所有命令统一遵守。
    /// 单条命令的 <c>Version</c> 字段表示命令实现自身的契约版本，不被 executor
    /// 改动。
    /// </summary>
    public interface IMapCommand
    {
        /// <summary>
        /// 在 <paramref name="mapState"/> 上执行命令。
        /// </summary>
        /// <param name="mapState">当前 <see cref="MapState"/>（map commands 不修改 BattleState）。</param>
        /// <returns>
        /// 成功 → <see cref="MapCommandResult.Ok(System.Collections.Generic.IReadOnlyList{Starfall.Core.Map.MapEvent},int)"/>；
        /// 失败 → <see cref="MapCommandResult.Fail(string)"/>，mapState 完全不变。
        /// </returns>
        MapCommandResult Execute(MapState mapState);

        /// <summary>
        /// 单步撤销：在 <paramref name="mapState"/> 上反向执行上一次成功 <see cref="Execute"/> 的效果。
        /// <para/>
        /// **契约**：
        /// <list type="bullet">
        /// <item>只能撤销**最近一次由本 executor 执行的同一条命令**；
        ///       调用方负责保证这一前提（executor 自身仅从 history stack 弹 1 条）。</item>
        /// <item>**实现方职责**：记录 Execute 前的快照 / 关键值；Undo 时严格反向修改。</item>
        /// <item>**不可级联**：本方法不允许再调用其它 <c>Undo</c>。</item>
        /// </list>
        /// <para/>
        /// **失败**：若命令不支持 Undo（如尚未实现），可抛
        /// <see cref="System.NotSupportedException"/>。executor 默认 catch 并 Continue。
        /// </summary>
        void Undo(MapState mapState);

        /// <summary>
        /// 命令**实现自身的契约版本号**（与 <see cref="MapState.Version"/> 自增字段不同）。
        /// <para/>
        /// 含义：
        /// <list type="bullet">
        /// <item>同一类命令的多个实例返回相同 <c>Version</c> 值。</item>
        /// <item>命令的字段 / 行为语义如果发生**不向后兼容**的变化，应自增本字段。</item>
        /// <li>用作 replay / undo 兼容性检查的字段。</li>
        /// </list>
        /// </summary>
        int Version { get; }

        /// <summary>
        /// 稳定标识（type / scope / summary 模式）。
        /// <para/>
        /// **格式约定**：<c>{type-scope}:{summary-or-id}</c>。
        /// 例：<c>"transform-tile:100"</c>、<c>"modify-global-cv"</c>。
        /// <para/>
        /// **唯一性**：相同 MapState 上执行两条命令，若 <c>CommandId</c> 完全相同 +
        /// <c>Dependencies</c> 完全相同，则视为"等效命令"。executor 内部不强制去重，
        /// 允许两条相同 CommandId 的命令在同一 map 上各执行一次。
        /// </summary>
        string CommandId { get; }

        /// <summary>
        /// 显式声明依赖的其它 <see cref="CommandId"/> 列表。
        /// <para/>
        /// executor 在 <see cref="MapCommandExecutor.Run"/> 时会校验：
        /// "列表中的每个 CommandId 都必须在当前 map 的 history 中出现至少一次"。
        /// <list type="bullet">
        /// <item>不存在任何依赖时返回 <c>Array.Empty&lt;string&gt;()</c>。</item>
        /// <li>依赖必须按升序字典序排序（AGENTS.md §11）。</li>
        /// </list>
        /// </summary>
        IReadOnlyList<string> Dependencies { get; }
    }
}
