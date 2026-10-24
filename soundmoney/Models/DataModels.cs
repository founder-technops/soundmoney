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

/// <summary>
/// Persisted copy of one successful ScraperService.ScrapeStockAsync result - the full
/// scraped StockValuation (company/sector/price at scrape time), current-period Financial,
/// and historical Financial list, serialized as JSON. Lets the Details/Summary page
/// (ScreenerService.RunScreenDetailsAsync) reuse the same day's scrape instead of hitting
/// Screener.in again on every page view, and lets AutomationService's own daily background
/// scrape populate this same cache so the two pipelines never redundantly scrape the same
/// symbol on the same day. This is deliberately separate from StockValuations: that table
/// is a lightweight summary for the screener list/filter page; this one holds the full
/// data needed to rebuild a complete StockDetailsViewModel without re-scraping.
/// </summary>
public class ScrapedFinancialCache
{
    public string Symbol { get; set; } = "";
    public string StockValuationJson { get; set; } = "";
    public string CurrentFinancialJson { get; set; } = "";
    public string HistoricalFinancialsJson { get; set; } = "";
    public DateTime ScrapedAt { get; set; }
}

public sealed record CachedFinancialEntry(
    string Symbol,
    Financial Current,
    List<Financial> Historical,
    StockValuation Summary,
    DateTime ScrapedAt);