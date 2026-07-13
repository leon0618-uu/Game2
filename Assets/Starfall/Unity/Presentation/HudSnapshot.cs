using Starfall.Core.Model;

namespace Starfall.Unity.Presentation
{
    /// <summary>给 HUD 用的战斗 UI 快照。</summary>
    public readonly struct HudSnapshot
    {
        public int TurnNumber { get; }
        public Owner ActivePlayer { get; }
        public string Outcome { get; }  // "Ongoing" / "PlayerWins" / "EnemyWins" / "Draw"

        public HudSnapshot(int turnNumber, Owner activePlayer, string outcome)
        {
            TurnNumber = turnNumber;
            ActivePlayer = activePlayer;
            Outcome = outcome;
        }

        public static HudSnapshot FromState(BattleState state, Starfall.Core.Combat.BattleOutcome outcome)
        {
            return new HudSnapshot(state.TurnNumber, state.ActivePlayer, outcome.ToString());
        }
    }
}