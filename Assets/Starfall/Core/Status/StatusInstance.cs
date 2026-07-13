namespace Starfall.Core.Status
{
    /// <summary>
    /// 单次状态实例（带唯一 InstanceId，便于 Replay 重放）。
    /// </summary>
    public sealed class StatusInstance
    {
        public int InstanceId { get; }
        public StatusKind Kind { get; }
        public int RemainingTurns { get; set; }
        public int SourceUnitId { get; }

        public StatusInstance(int instanceId, StatusKind kind, int remainingTurns, int sourceUnitId)
        {
            if (instanceId < 0) throw new System.ArgumentException("InstanceId must be >= 0", nameof(instanceId));
            if (remainingTurns <= 0) throw new System.ArgumentException("remainingTurns must be > 0", nameof(remainingTurns));
            InstanceId = instanceId;
            Kind = kind;
            RemainingTurns = remainingTurns;
            SourceUnitId = sourceUnitId;
        }
    }
}