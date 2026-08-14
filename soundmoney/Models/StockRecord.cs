namespace SoundMoney.Models;

/// <summary>
/// Represents a stock record in the SQLite database.
/// Stores all fundamental data fetched from Gemini API.
/// </summary>
public class StockValuation
{
    public string Symbol { get; set; } = "";
    
    public string CompanyName { get; set; } = "";
    
    public decimal CurrentPrice { get; set; }
    
    public string Sector { get; set; } = "";

    public string IntrinsicMethod { get; set; } = "";

    /// <summary>
    /// Calculated intrinsic value based on sector-specific strategy.
    /// </summary>
    public decimal IntrinsicValue { get; set; }
    
    /// <summary>
    /// Margin of safety percentage.
    /// </summary>
    public decimal MarginOfSafety{ get; set; }
    
    /// <summary>
    /// Verdict: "Undervalued", "Fair value", or "Overvalued"
    /// </summary>
    public string Verdict { get; set; } = "";
    
    /// <summary>
    /// Timestamp when the record was fetched/updated from Gemini.
    /// </summary>
    public DateTime FetchedAt { get; set; }
    
    /// <summary>
    /// Timestamp when the record was last updated in the database.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
