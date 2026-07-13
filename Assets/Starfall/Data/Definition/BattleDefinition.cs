using System.Collections.Generic;

namespace Starfall.Data.Definition
{
    /// <summary>
    /// 战斗定义根（JSON 顶层）。
    /// </summary>
    public sealed class BattleDefinition
    {
        public int TurnNumber { get; set; }
        public string ActivePlayer { get; set; } = "Player";
        public BoardDefinition Board { get; set; } = new BoardDefinition();
        public List<UnitDefinition> Units { get; set; } = new List<UnitDefinition>();
    }
}