using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Starfall.Core.Command;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Core.Status;

namespace Starfall.Core.Replay
{
    public static class ReplayCodec
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public static ReplayFile Capture(BattleState finalState, IReadOnlyList<CommandRecord> records)
        {
            return new ReplayFile
            {
                FinalHash = finalState.PostStateHash,
                InitialTurnNumber = 0,
                InitialActivePlayer = finalState.ActivePlayer == Owner.Player ? "Player" : "Enemy",
                Commands = records.Select(ToEntry).ToList(),
            };
        }

        public static void WriteFile(ReplayFile file, string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(file, Options));
        }

        public static ReplayFile ReadFile(string path)
        {
            if (!File.Exists(path)) throw new ReplayException("Replay file not found", path);
            var text = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ReplayFile>(text, Options);
            if (file == null) throw new ReplayException("Deserialized to null", path);
            return file;
        }

        /// <summary>从 ReplayFile 反推 ICommand 序列（注：不重建 path，仅重建无路径命令如 EndTurn/ApplyStatus；MoveCommand path 字段留空）。</summary>
        public static List<ICommand> ReconstructCommands(ReplayFile file)
        {
            var list = new List<ICommand>();
            foreach (var e in file.Commands)
            {
                switch (e.Type)
                {
                    case "Move":
                        // MoveCommand 需要 path 列表；MVP 简化为重建 [From, To] 两点 path
                        var path = new List<GridPos> {
                            new GridPos(e.X, e.Y), new GridPos(e.ToX, e.ToY)
                        };
                        list.Add(new MoveCommand(e.CommandId, e.UnitId, new GridPos(e.X, e.Y), new GridPos(e.ToX, e.ToY), path));
                        break;
                    case "EndTurn":
                        var player = e.Arg1 == 0 ? Owner.Player : Owner.Enemy;
                        list.Add(new EndTurnCommand(e.CommandId, player));
                        break;
                    case "ApplyStatus":
                        var kind = (StatusKind)e.Arg1;
                        list.Add(new ApplyStatusCommand(e.CommandId, e.UnitId, kind, e.Arg2, e.Arg3));
                        break;
                    case "RemoveStatus":
                        list.Add(new RemoveStatusCommand(e.CommandId, e.Arg1));
                        break;
                    case "TickEndTurn":
                        list.Add(new TickEndTurnCommand(e.CommandId));
                        break;
                    default:
                        throw new System.InvalidOperationException("Unknown replay entry type: " + e.Type);
                }
            }
            return list;
        }

        private static ReplayEntry ToEntry(CommandRecord r)
        {
            var e = new ReplayEntry { CommandId = r.Command.CommandId };
            switch (r.Command)
            {
                case MoveCommand m:
                    e.Type = "Move";
                    e.UnitId = m.UnitId;
                    e.X = m.From.X;
                    e.Y = m.From.Y;
                    e.ToX = m.To.X;
                    e.ToY = m.To.Y;
                    break;
                case EndTurnCommand et:
                    e.Type = "EndTurn";
                    e.Arg1 = (int)(et.ExpectedActivePlayer == Owner.Player ? 0 : 1);
                    break;
                case ApplyStatusCommand ap:
                    e.Type = "ApplyStatus";
                    e.UnitId = ap.TargetUnitId;
                    e.Arg1 = (int)ap.Kind;
                    e.Arg2 = ap.RemainingTurns;
                    e.Arg3 = ap.SourceUnitId;
                    break;
                case RemoveStatusCommand rs:
                    e.Type = "RemoveStatus";
                    e.Arg1 = rs.StatusInstanceId;
                    break;
                case TickEndTurnCommand t:
                    e.Type = "TickEndTurn";
                    break;
                default:
                    throw new System.InvalidOperationException("Unsupported command type: " + r.Command.GetType().Name);
            }
            return e;
        }
    }
}
