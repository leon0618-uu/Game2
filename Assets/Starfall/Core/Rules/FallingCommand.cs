using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Map.Commands;
using Starfall.Core.Map.Commands.Fall;
using Starfall.Core.Map.Coordinates;
using Starfall.Core.Map.State;
using Starfall.Core.Map.Tile;
using Starfall.Core.Model;

namespace Starfall.Core.Rules
{
    /// <summary>
    /// 坠落命令：doc2 MAP-08 §6.1 重构版（与既有 MVP 兼容 + 增强）。
    /// <para/>
    /// **行为契约**（公签名 <see cref="ICommand.Execute"/> 不变）：
    /// <list type="number">
    /// <item>查找单位当前坐标（<see cref="UnitState.Pos"/> → <see cref="GridCoord"/>）。</item>
    /// <item>若 map 已 <see cref="PhaseFlipStateService.Attach"/> 并且坐标在 attached registry 中，
    ///       **优先** 调 <see cref="FallResolutionService.FindNearestLegalLanding"/> 找最近合法落点。
    ///       找到 → 通过 <see cref="TileOccupancyService"/> 迁移占用 + 同步 <see cref="UnitState.Pos"/> +
    ///       发 <see cref="BattleEventKind.UnitEnteredVoid"/>，**不**扣 HP。</item>
    /// <item>未找到 / 未 attach → fallback：扣 <see cref="FallDamage"/> HP，并发
    ///       <see cref="BattleEventKind.UnitEnteredVoid"/>（既有 MVP 语义）。</item>
    /// <item>fallDamage 仅在 fallback 扣血；MAP-08 把单位移走时 HP 不变。</item>
    /// </list>
    /// <para/>
    /// **承前兼容**：既有 MVP 的 <c>FallingCommand_ReducesHp</c> 测试断言 HP 减少。
    /// 本轮保留 fallback HP damage 语义，并新增 "MAP-08 重构后" 路径的测试覆盖。
    /// <para/>
    /// **dependency**：依赖 <see cref="PhaseFlipStateService"/> + <see cref="TileOccupancyService"/>
    /// <c>Attach</c> 后才走移动路径；未 attach 时直接走 fallback 路径。
    /// </summary>
    public sealed class FallingCommand : ICommand
    {
        public int CommandId { get; set; }
        public int UnitId { get; }
        public int FallDamage { get; }

        public FallingCommand(int commandId, int unitId, int fallDamage = 1)
        {
            CommandId = commandId;
            UnitId = unitId;
            FallDamage = fallDamage;
        }

        public bool CanExecute(BattleState state)
        {
            foreach (var u in state.Units) if (u.UnitId == UnitId) return true;
            return false;
        }

        public CommandResult Execute(BattleState state, out IReadOnlyList<BattleEvent> events)
        {
            events = System.Array.Empty<BattleEvent>();
            if (!CanExecute(state)) return CommandResult.Illegal;

            UnitState target = null;
            for (int i = 0; i < state.Units.Count; i++)
                if (state.Units[i].UnitId == UnitId) { target = state.Units[i]; break; }

            // 1) 尝试 MAP-08 移动路径（依赖 attach）。
            GridCoord? nearest = null;
            var mapState = state.MapState;
            GridCoord original = new GridCoord(target.Pos.X, target.Pos.Y, DimensionLayer.Reality);

            if (PhaseFlipStateService.GetAttachedRegistry(mapState) != null)
            {
                nearest = FallResolutionService.FindNearestLegalLanding(mapState, original, UnitId);
            }

            if (nearest.HasValue && nearest.Value != original)
            {
                // 2) 移动到 nearest：用 TileOccupancyService 迁移占用。
                if (TryMoveUnit(mapState, UnitId, target, nearest.Value))
                {
                    events = new[]
                    {
                        new BattleEvent(
                            kind: BattleEventKind.UnitEnteredVoid,
                            primaryUnitId: UnitId,
                            from: new GridPos(original.X, original.Y),
                            to: new GridPos(nearest.Value.X, nearest.Value.Y)),
                    };
                    return CommandResult.Success;
                }
            }

            // 3) Fallback：扣 HP + 标记位（既有 MVP 语义）+ 发 EnteredVoid 事件（不强行兼容 BattleEventKind）。
            target.Hp = System.Math.Max(0, target.Hp - FallDamage);
            events = new[]
            {
                new BattleEvent(BattleEventKind.UnitDamaged, UnitId, null, null),
                new BattleEvent(BattleEventKind.UnitEnteredVoid, UnitId, null, null),
            };
            return CommandResult.Success;
        }

        /// <summary>
        /// 通过 <see cref="TileOccupancyService"/> 把 unit 从 <paramref name="target"/> 的旧 Pos
        /// 迁移到 <paramref name="destination"/>；若 <paramref name="target"/> 的旧 Pos 不在
        /// 占用服务登记（既有 MVP 场景）则只更新 <c>Pos</c>。
        /// </summary>
        private static bool TryMoveUnit(MapState map, int unitId, UnitState target, GridCoord destination)
        {
            var registered = TileOccupancyService.GetUnitCells(unitId);
            if (registered != null && registered.Count > 0)
            {
                if (!TileOccupancyService.TryRemoveUnit(map, unitId)) return false;
                // MVP 没有 footprint — 直接用 SingleCell。
                if (!TileOccupancyService.TryPlaceUnit(map, unitId, Footprint.SingleCell, destination))
                {
                    return false;
                }
            }

            target.Pos = new GridPos(destination.X, destination.Y);
            return true;
        }
    }
}
