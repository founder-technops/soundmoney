namespace SoundMoney.Models;

public class ScreenerResultRow
{
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public SectorCategory Sector { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal IntrinsicValue { get; set; }
    public decimal MarginOfSafetyPercent { get; set; }
    public string Verdict { get; set; } = "";
    public string SoundScoreRating { get; set; } = "";
}

public class ScreenerViewModel
{
    public List<ScreenerResultRow> Results { get; set; } = new();
    public string? SearchQuery { get; set; }
    public decimal MinMarginOfSafety { get; set; }
    public List<string> SelectedScores { get; set; } = new();
}