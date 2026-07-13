namespace Starfall.Core.Map.Coordinates
{
    /// <summary>
    /// 网格方向枚举（doc2 MAP-01 §4.5）。
    ///
    /// **固定顺序**：North → East → South → West（上、右、下、左）。
    /// 此顺序由 AGENTS.md §11 确定性规则强制约束：
    ///   - BFS / A* 寻路邻居遍历顺序；
    ///   - 网格线段 / 视线扫描方向；
    ///   - 锚点围区 / 律令多边形顶点规范化。
    /// 任何违反此顺序的代码都会破坏 Replay 确定性并导致哈希分歧。
    ///
    /// 数值固定：North=0, East=1, South=2, West=3，禁止乱序赋值。
    /// </summary>
    public enum GridDirection : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }
}