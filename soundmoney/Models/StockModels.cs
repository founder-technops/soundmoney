namespace SoundMoney.Models;

public class ScreenerResultRow
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public SectorCategory Sector { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal IntrinsicValue { get; set; }
    public decimal MarginOfSafetyPercent { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string SoundScoreRating { get; set; } = string.Empty;
    public DateTime? LastAnalyzed { get; set; } // Added field

    public decimal DividendYieldPercent { get; set; }
    public bool IsDividendConsistent { get; set; }
}

public class ScreenerViewModel
{
    public List<ScreenerResultRow> Results { get; set; } = new();
    public string? SearchQuery { get; set; }
    public decimal MinMarginOfSafety { get; set; }
    public List<string> SelectedScores { get; set; } = new();
}