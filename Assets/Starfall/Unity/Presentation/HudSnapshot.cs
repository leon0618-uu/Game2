using System.Collections.Generic;
using Starfall.Core.Combat;
using Starfall.Core.Model;
using Starfall.Unity.Input;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 HUD 用的战斗 UI 快照。</summary>
    /// <remarks>
    /// Task 18 扩展：
    /// - <see cref="CurrentPhase"/>：当前 active player 的"主导相位"（取首个己方单位的 phase）；
    /// - <see cref="ObjectiveText"/>：防守/撤离目标描述（Task 19 之前固定 Guard）；
    /// - <see cref="AnchorTileCount"/>：玩家锚点围区瓦片数（用于 HUD 进度条占位）；
    /// - <see cref="ActiveUnit"/>：当前激活单位（InputState.SelectedUnitId）的 HUD 快照；
    /// - <see cref="LastInputMessage"/>：InputController.LastMessage → HUD 提示；
    /// - <see cref="InputModeHint"/> / <see cref="DamagePreview"/>：HUD 状态条 + 伤害预览数字。
    /// 全部派生自 BattleState + InputState，不持有第二真值。
    /// </remarks>
    public readonly struct HudSnapshot
    {
        public int TurnNumber { get; }
        public Owner ActivePlayer { get; }
        public string Outcome { get; }  // "Ongoing" / "PlayerWins" / "EnemyWins" / "Draw"
        public Phase CurrentPhase { get; }
        public string ObjectiveText { get; }
        public int AnchorTileCount { get; }
        public UnitSnapshot? ActiveUnit { get; }
        public string LastInputMessage { get; }
        public InputMode InputModeHint { get; }
        public int? DamagePreview { get; }

        public HudSnapshot(int turnNumber, Owner activePlayer, string outcome)
        {
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer;
            Outcome = outcome;
            CurrentPhase = Phase.Light;
            ObjectiveText = "Guard: hold any player anchor zone";
            AnchorTileCount = 0;
            ActiveUnit = null;
            LastInputMessage = string.Empty;
            InputModeHint = InputMode.None;
            DamagePreview = null;
        }

        public HudSnapshot(
            int turnNumber,
            Owner activePlayer,
            string outcome,
            Phase currentPhase,
            string objectiveText,
            int anchorTileCount,
            UnitSnapshot? activeUnit,
            string lastInputMessage,
            InputMode inputModeHint,
            int? damagePreview)
        {
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer;
            Outcome = outcome;
            CurrentPhase = currentPhase;
            ObjectiveText = objectiveText;
            AnchorTileCount = anchorTileCount;
            ActiveUnit = activeUnit;
            LastInputMessage = lastInputMessage ?? string.Empty;
            InputModeHint = inputModeHint;
            DamagePreview = damagePreview;
        }

        public static HudSnapshot FromState(BattleState state, BattleOutcome outcome)
        {
            return new HudSnapshot(state.TurnNumber, state.ActivePlayer, outcome.ToString());
        }

        /// <summary>
        /// Task 18 完整版：从 BattleState + InputState 派生 HUD 字段。
        /// </summary>
        public static HudSnapshot FromStateWithInput(
            BattleState state,
            BattleOutcome outcome,
            int? selectedUnitId,
            InputMode inputMode,
            string lastInputMessage,
            int? damagePreview = null)
        {
            // Current phase：取首个 active player 单位的 phase
            Phase phase = Phase.Light;
            int activeCount = 0;
            foreach (var u in state.Units)
            {
                if (u.Owner == state.ActivePlayer)
                {
                    activeCount++;
                    if (u == state.Units[0] || phase == Phase.Light) phase = u.Phase;
                }
            }

            // Anchor tile count：玩家锚点围区面积（去重、按 (Y,X) 升序）
            var seen = new HashSet<GridPos>();
            int playerAnchorTiles = 0;
            foreach (var z in state.Anchors.ZonesInOrder)
            {
                if (z.Owner == "Player")
                {
                    foreach (var v in z.Vertices)
                    {
                        if (seen.Add(v)) playerAnchorTiles++;
                    }
                }
            }
            string objective = playerAnchorTiles > 0
                ? $"Guard: hold any player anchor zone ({playerAnchorTiles} tiles)"
                : "Guard: no player anchor zones — fallback to survival";

            // 激活单位 HUD 快照
            UnitSnapshot? activeSnap = null;
            if (selectedUnitId.HasValue)
            {
                foreach (var u in state.Units)
                {
                    if (u.UnitId == selectedUnitId.Value)
                    {
                        var d = LegalPreviewHelper.DeriveStats(u, state.Statuses);
                        activeSnap = new UnitSnapshot(u.UnitId, u.Pos, u.Hp, u.MaxHp, u.Phase, u.Owner, d.Pv, d.Ap, d.Cv);
                        break;
                    }
                }
            }

            return new HudSnapshot(
                turnNumber: state.TurnNumber,
                activePlayer: state.ActivePlayer,
                outcome: outcome.ToString(),
                currentPhase: phase,
                objectiveText: objective,
                anchorTileCount: playerAnchorTiles,
                activeUnit: activeSnap,
                lastInputMessage: lastInputMessage ?? string.Empty,
                inputModeHint: inputMode,
                damagePreview: damagePreview);
        }
    }
}
