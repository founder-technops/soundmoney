namespace SoundMoney.Models;

public class StockValuation
{
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public decimal CurrentPrice { get; set; }
    public string Sector { get; set; } = "";
    public string PrimaryMethod { get; set; } = "";
    public string SecondaryMethod { get; set; } = "";

    public decimal IntrinsicValue { get; set; }
    public decimal MarginOfSafety { get; set; }
    public string Verdict { get; set; } = "";

    public DateTime FetchedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? ErrorMessage { get; set; } // Optional property to store error messages

    public decimal SoundScore { get; set; }
    public string SoundScoreRating { get; set; } = "";

    // Added metrics
    public decimal DividendYieldPercent { get; set; }
    public bool IsDividendConsistent { get; set; } // Or string/int depending on your consistency evaluation logic
}