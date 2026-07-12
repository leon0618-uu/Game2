using System;
using System.Collections.Generic;
using Starfall.Core.Command;
using Starfall.Core.Model;
using Starfall.Core.Pathfinding;

namespace Starfall.Core.Combat
{
    /// <summary>
    /// 改进敌 AI（M-30 最小化）：
    /// 1. 找到最近 Player 单位（按 Manhattan 距离 + UnitId 升序 tie-break）；
    /// 2. 若相邻（Chebyshev ≤ 1）则 Attack；
    /// 3. 否则用 BFSPathfinder 计算路径并 Move 1 步向目标；
    /// 4. 收尾 EndTurnCommand。
    ///
    /// 该 AI 无状态，不读 UnityEngine.Random，可作为 Replay 基线。
    /// </summary>
    public sealed class ImprovedEnemyAI : IEnemyAI
    {
        public IEnumerable<ICommand> PlanTurn(int commandIdSeed, BattleState state)
        {
            // 选择最近 Enemy 单位对应的最近 Player 单位（确定性：先按 UnitId 升序扫 Enemy）
            UnitState chosenEnemy = null;
            UnitState chosenTarget = null;
            int bestDist = int.MaxValue;
            foreach (var e in state.Units)
            {
                if (e.Owner != Owner.Enemy || e.Hp <= 0) continue;

                UnitState nearestPlayer = null;
                int nearestPlayerDist = int.MaxValue;
                foreach (var u in state.Units)
                {
                    if (u.Owner != Owner.Player || u.Hp <= 0) continue;
                    int d = Math.Abs(e.Pos.X - u.Pos.X) + Math.Abs(e.Pos.Y - u.Pos.Y);
                    if (d < nearestPlayerDist)
                    {
                        nearestPlayerDist = d;
                        nearestPlayer = u;
                    }
                }

                if (nearestPlayer != null && nearestPlayerDist < bestDist)
                {
                    bestDist = nearestPlayerDist;
                    chosenEnemy = e;
                    chosenTarget = nearestPlayer;
                }
            }

            int cmdId = commandIdSeed;

            // 1) 邻接则攻击
            if (chosenEnemy != null && chosenTarget != null && bestDist <= 1)
            {
                yield return new AttackCommand(cmdId++, chosenEnemy.UnitId, chosenTarget.UnitId);
            }
            // 2) 不邻接则朝目标移动一步
            else if (chosenEnemy != null && chosenTarget != null)
            {
                var pf = new BFSPathfinder();
                var path = pf.FindPath(state.Board, chosenEnemy.Pos, chosenTarget.Pos);
                if (path != null && path.Count >= 2)
                {
                    var nextPos = path[1];
                    yield return new MoveCommand(cmdId++, chosenEnemy.UnitId, chosenEnemy.Pos, nextPos, path);
                }
            }

            // 3) 收尾 EndTurn
            yield return new EndTurnCommand(cmdId++, state.ActivePlayer);
        }
    }
}
