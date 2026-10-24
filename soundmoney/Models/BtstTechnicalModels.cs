namespace SoundMoney.Models;

public sealed record BtstRuleCheck(string Name, string Observed, string Requirement, bool Passed, string Category = "Technical");

public sealed record BtstTechnicalSignal
{
    public string Status { get; init; } = "Unavailable";
    public bool DataIsStale { get; init; }
    public bool? IsSetup { get; init; }
    public bool? IsTechnicalSetup { get; init; }
    public bool? IsFundamentallyStrong { get; init; }
    public int ChecksPassed { get; init; }
    public int ChecksTotal { get; init; }
    public int TrendChecksPassed { get; init; }
    public int TrendChecksTotal { get; init; }
    public string ChecksSummary { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string DataSource { get; init; } = string.Empty;
    public DateOnly? SessionDate { get; init; }
    public DateTimeOffset? RetrievedAtUtc { get; init; }
    public decimal? LatestClose { get; init; }
    public decimal? Dma50 { get; init; }
    public decimal? Dma200 { get; init; }
    public decimal? DmaSpreadPercent { get; init; }
    public decimal? PriceAboveDma50Percent { get; init; }
    public decimal? TwentySessionReturnPercent { get; init; }
    public decimal? FortySessionReturnPercent { get; init; }
    public decimal? OneDayReturnPercent { get; init; }
    public decimal? LatestVolume { get; init; }
    public decimal? AverageVolume20Sessions { get; init; }
    public decimal? VolumeRatio { get; init; }
    public decimal? PotentialEntryMarginOfSafety { get; init; }
    public decimal? TargetPrice30Percent { get; init; }
    public decimal? TargetPrice40Percent { get; init; }
    public IReadOnlyList<BtstRuleCheck> Checks { get; init; } = Array.Empty<BtstRuleCheck>();
}
