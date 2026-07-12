using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;

namespace Starfall.Core.Replay
{
    /// <summary>
    /// Replay 重放器：克隆 BattleState，从干净状态重放所有记录，
    /// 验证最终 PostStateHash 与预期一致。
    /// </summary>
    public static class ReplayPlayer
    {
        public class ReplayResult
        {
            public bool HashMatches { get; set; }
            public ulong FinalHash { get; set; }
            public ulong ExpectedHash { get; set; }
            public int ReplayedCount { get; set; }
        }

        public static ReplayResult Replay(BattleState initialState, IReadOnlyList<CommandRecord> records, ulong expectedFinalHash)
        {
            var state = BattleStateCloner.Clone(initialState);
            int count = 0;
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                CommandExecutor.Run(state, r.Command, out _);
                count++;
            }
            ulong finalHash = state.PostStateHash;
            return new ReplayResult
            {
                HashMatches = finalHash == expectedFinalHash,
                FinalHash = finalHash,
                ExpectedHash = expectedFinalHash,
                ReplayedCount = count,
            };
        }
    }
}