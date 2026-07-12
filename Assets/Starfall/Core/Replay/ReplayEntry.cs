using Starfall.Core.Command;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// 单条命令的 JSON 序列化形式（基于类型 + 字段）。
    /// MVP 最小集：Type + CommandId + UnitId/Player 等基础字段。
    /// </summary>
    public sealed class ReplayEntry
    {
        public string Type { get; set; }       // "Move" / "EndTurn" / "ApplyStatus" / "RemoveStatus" / "TickEndTurn"
        public int CommandId { get; set; }
        public int UnitId { get; set; }        // 0 if N/A
        public int X { get; set; }             // From.X
        public int Y { get; set; }             // From.Y
        public int ToX { get; set; }           // To.X (for Move)
        public int ToY { get; set; }           // To.Y
        public int Arg1 { get; set; }          // StatusKind / RemainingTurns / etc
        public int Arg2 { get; set; }          // StatusInstanceId / etc
        public int Arg3 { get; set; }          // SourceUnitId / etc
    }
}
