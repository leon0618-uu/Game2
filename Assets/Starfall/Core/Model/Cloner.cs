using System.Collections.Generic;
using Starfall.Core.Map.State;

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

            // doc2 MAP-02：MapState 必须深拷贝；否则 Undo RestoreState / Replay Round-trip
            // 会共享 MapState 集合引用，违反隔离原则。
            var mapStateCopy = MapStateCloner.DeepClone(source.MapState);

            return new BattleState(
                source.TurnNumber,
                source.ActivePlayer,
                newBoard,
                unitsCopy,
                mapStateCopy)
            {
                // _units 已在构造函数中填好
                // Task 19 关卡闭环字段：阶段计数 / 门槛 / 撤离格独立拷贝
                CurrentPhase = source.CurrentPhase,
                GuardsCompleted = source.GuardsCompleted,
                GuardsRequired = source.GuardsRequired,
                ExitTile = source.ExitTile.HasValue
                    ? new GridPos(source.ExitTile.Value.X, source.ExitTile.Value.Y)
                    : (GridPos?)null,
                // MAP-02：BattleState.MapState 已通过 5 参构造注入（mapStateCopy）。
                // Statuses / Anchors / Decrees：当前 BattleState 构造期初始化为空集合，
                // 现有 BattleStateCloner 不复制这些；保留此限制直至 Task 19-完成（M-00 系列）。
            };
        }
    }
}
