namespace SoundMoney.Models;

/// <summary>Live data pulled from the Twelve Data API.</summary>
public class MarketQuote
{
    public string Symbol { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string Industry { get; set; } = "";
    public decimal LastPrice { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal DayHigh { get; set; }
    public decimal DayLow { get; set; }
    public decimal YearHigh { get; set; }
    public decimal YearLow { get; set; }
    public DateTime FetchedAt { get; set; }
}

/// <summary>
/// Fundamental inputs used for valuation. NSE's public API does not expose
/// these cleanly, so they're read from App_Data/fundamentals.csv, which you
/// maintain from annual reports / exchange filings / another data vendor.
/// Column headers must match these property names.
/// </summary>
public class StockFundamentals
{
    public string Symbol { get; set; } = "";
    public string Sector { get; set; } = "";
    public decimal EPS { get; set; }
    public decimal BookValuePerShare { get; set; }
    public decimal ROE { get; set; }                  // percent, e.g. 18.5
    public decimal DividendPerShare { get; set; }
    public decimal EstimatedGrowthRate { get; set; }   // percent, e.g. 10
    public decimal RequiredRateOfReturn { get; set; }  // percent; 0 = use strategy default
}

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
    public decimal MinMarginOfSafety { get; set; }
    public SectorCategory? SelectedSector { get; set; }
    public string? SelectedScore { get; set; }
}
