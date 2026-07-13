namespace Starfall.Data.Definition
{
    /// <summary>
    /// 单位静态定义（从 JSON 加载）。运行时实例 = UnitState。
    /// </summary>
    public sealed class UnitDefinition
    {
        public int UnitId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public string Phase { get; set; } = "Light";  // "Light" / "Dark"
        public string Owner { get; set; } = "Player"; // "Player" / "Enemy"
    }
}