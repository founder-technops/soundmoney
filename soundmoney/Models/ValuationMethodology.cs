namespace SoundMoney.Models
{
    public record ValuationMethodology
    {
        public required string PrimaryMethod { get; init; }
        public required string SecondaryMethod { get; init; }
        public required string Rationale { get; init; }
    }
}
