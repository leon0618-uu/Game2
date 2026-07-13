using Starfall.Core.Model;

namespace Starfall.Core.Decree
{
    /// <summary>
    /// 律令实例。绑定目标锚点 + 持续回合数。
    /// </summary>
    public sealed class Decree
    {
        public int DecreeId { get; }
        public DecreeKind Kind { get; }
        public int TargetZoneId { get; }
        public int RemainingTurns { get; set; }
        public Owner IssuingPlayer { get; }

        public Decree(int decreeId, DecreeKind kind, int targetZoneId, int remainingTurns, Owner issuingPlayer)
        {
            DecreeId = decreeId;
            Kind = kind;
            TargetZoneId = targetZoneId;
            RemainingTurns = remainingTurns;
            IssuingPlayer = issuingPlayer;
        }
    }
}
