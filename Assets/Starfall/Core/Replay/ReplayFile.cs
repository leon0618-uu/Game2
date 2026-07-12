using System.Collections.Generic;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// Replay 文件 JSON 顶层：finalHash + commands 数组。
    /// </summary>
    public sealed class ReplayFile
    {
        public ulong FinalHash { get; set; }
        public int InitialTurnNumber { get; set; }
        public string InitialActivePlayer { get; set; } = "Player";
        public List<ReplayEntry> Commands { get; set; } = new List<ReplayEntry>();
    }
}
