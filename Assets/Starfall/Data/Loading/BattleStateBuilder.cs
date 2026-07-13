using System.Collections.Generic;
using Starfall.Core.Model;
using Starfall.Data.Definition;

namespace Starfall.Data.Loading
{
    /// <summary>
    /// 将 BattleDefinition 装入 BattleState。
    /// tile 填充按 Definition.Tiles 顺序（BoardState 由 validator 保证无重复）；
    /// unit 填充按 Definition.Units 顺序（Hash 链按 UnitId 升序，PostStateHash 稳定）。
    /// </summary>
    public static class BattleStateBuilder
    {
        public static BattleState Build(BattleDefinition def)
        {
            var tiles = new Dictionary<GridPos, TileState>();
            foreach (var t in def.Board.Tiles)
            {
                tiles[new GridPos(t.X, t.Y)] = System.Enum.Parse<TileState>(t.State, true);
            }
            var board = new BoardState(def.Board.Width, def.Board.Height, tiles);

            var state = new BattleState(
                def.TurnNumber,
                System.Enum.Parse<Owner>(def.ActivePlayer, true),
                board,
                null);
            foreach (var u in def.Units)
            {
                var phase = System.Enum.Parse<Phase>(u.Phase, true);
                var owner = System.Enum.Parse<Owner>(u.Owner, true);
                state.AddUnit(new UnitState(u.UnitId, new GridPos(u.X, u.Y), u.Hp, u.Hp, phase, owner));
            }
            return state;
        }
    }
}