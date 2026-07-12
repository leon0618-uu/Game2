using System.Collections.Generic;

namespace Starfall.Core.Model
{
    public static class BattleStateCloner
    {
        /// <summary>深拷贝 BattleState。返回的新对象与 source 完全独立。</summary>
        public static BattleState Clone(BattleState source)
        {
            if (source == null) return null;

            var newTiles = new Dictionary<GridPos, TileState>();
            foreach (var kv in source.Board.Tiles)
                newTiles[kv.Key] = kv.Value;

            var newBoard = new BoardState(source.Board.Width, source.Board.Height, newTiles);

            var unitsCopy = new List<UnitState>(source.Units.Count);
            foreach (var u in source.Units)
                unitsCopy.Add(new UnitState(u.UnitId, u.Pos, u.Hp, u.MaxHp, u.Phase, u.Owner));

            return new BattleState(source.TurnNumber, source.ActivePlayer, newBoard, unitsCopy)
            {
                // _units 已在构造函数中填好
            };
        }
    }
}
