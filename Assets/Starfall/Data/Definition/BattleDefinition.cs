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

        /// <summary>
        /// 防守次数门槛（Task 19 关卡闭环）。可选；缺省 = 3。
        /// 范围 [1, 100]：超出则由 <c>DefinitionValidator</c> 抛 DefinitionException。
        /// </summary>
        public int? GuardsRequired { get; set; } = null;

        /// <summary>
        /// 撤离格 X。可选；缺省 = null（无撤离）。
        /// </summary>
        public int? ExitTileX { get; set; } = null;

        /// <summary>
        /// 撤离格 Y。可选；缺省 = null（无撤离）。必须与 <see cref="ExitTileX"/> 同时设置。
        /// </summary>
        public int? ExitTileY { get; set; } = null;
    }
}