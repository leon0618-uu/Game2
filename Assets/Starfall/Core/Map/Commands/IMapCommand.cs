using Starfall.Core.Map.State;

namespace Starfall.Core.Map.Commands
{
    /// <summary>
    /// doc2 MAP-03 / MAP-08 地图命令接口（Stub）。
    /// <para/>
    /// **本轮（MAP-08）范围**：仅 stub，让 <see cref="FlipTilePhaseCommand"/> /
    /// <see cref="FlipRegionPhaseCommand"/> 能编译 + 复用同一签名。完整 Undo /
    /// Version / 事件注入等由 MAP-03 阶段实现。
    /// <para/>
    /// **执行语义**：
    /// <list type="bullet">
    /// <item><see cref="Execute"/> 接受 <see cref="MapState"/> 作参数，
    ///       返回 <see cref="MapCommandResult"/>（成功 / 失败 + 影响 cells + 失败原因）。</item>
    /// <item>Command 不写 <see cref="Starfall.Core.Command.BattleEvent"/>，
    ///       由上层 <c>BattleRunner</c> 在执行成功后注入战斗事件流。</item>
    /// <item>无副作用纯函数：执行期间不读时间、不读线程、不读 Unity 实例。</item>
    /// </list>
    /// </summary>
    public interface IMapCommand
    {
        /// <summary>
        /// 在 <paramref name="map"/> 上执行命令。
        /// </summary>
        /// <param name="map">当前 <see cref="MapState"/>（map commands 不修改 BattleState）。</param>
        /// <returns>成功 → <see cref="MapCommandResult.Ok"/>；失败 → <see cref="MapCommandResult.Fail"/>。</returns>
        MapCommandResult Execute(MapState map);
    }
}
