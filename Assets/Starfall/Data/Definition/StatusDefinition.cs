namespace Starfall.Data.Definition
{
    public sealed class StatusDefinition
    {
        public int InstanceId { get; set; }
        public string Kind { get; set; } = "None";  // Burn / Root / PhaseInvert
        public int RemainingTurns { get; set; }
        public int SourceUnitId { get; set; }
    }
}