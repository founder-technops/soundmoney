using System.ComponentModel.DataAnnotations;

namespace SoundMoney.Models;

/// <summary>
/// Represents year-by-year metric data for trend analysis
/// </summary>
public class YearlyMetric
{
    public int Year { get; set; }
    public decimal Value { get; set; }
    public decimal? ChangePercent { get; set; }
    public string? TrendDirection { get; set; } // "Up", "Down", "Flat"
}

/// <summary>
/// Collection of historical metrics for a specific metric category
/// </summary>
public class TrendMetricCollection
{
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public List<YearlyMetric> YearlyData { get; set; } = new();
    public decimal? AvgChange { get; set; }
    public string? OverallTrend { get; set; } // "Improving", "Declining", "Stable"
    public string? TrendHealthRating { get; set; } // "Excellent", "Good", "Fair", "Poor"
}

/// <summary>
/// Contains all trend analysis data for display
/// </summary>
public class TrendAnalysisData
{
    public List<TrendMetricCollection> Revenue { get; set; } = new();
    public List<TrendMetricCollection> Profitability { get; set; } = new();
    public List<TrendMetricCollection> Returns { get; set; } = new();
    public List<TrendMetricCollection> Debt { get; set; } = new();
    public List<TrendMetricCollection> CashFlow { get; set; } = new();
}

public class ScreenerResultRow
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public MacroSector Sector { get; set; }
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

public class StockDetailsViewModel
{
    // 1. Basic Information
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public DateTime? LastAnalyzed { get; set; }
    public bool IsFinancialSector { get; set; }

    // 2. Core Valuation Output
    public decimal IntrinsicValue { get; set; }
    public decimal MarginOfSafetyPercent { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string SoundScoreRating { get; set; } = string.Empty;

    // Numeric Sound Score (0-100) behind the rating, plus - when a value-trap cap held it
    // down - the score it would have had and the plain-language reasons for the cap.
    public int SoundScore { get; set; }
    public int SoundScoreRaw { get; set; }
    public List<string> ScoreCapReasons { get; set; } = new();
    public bool IsScoreCapped => ScoreCapReasons.Count > 0;

    public string PrimaryMethod { get; set; } = string.Empty;
    public string SecondaryMethod { get; set; } = string.Empty;

    // Entry/exit price levels: the same 0.70x / 1.00x / 1.20x thresholds that decide the
    // Verdict above, translated into actual rupee prices rather than left as an abstract
    // label. These are computed, not stored - they're pure functions of IntrinsicValue,
    // so they can never drift out of sync with however the Verdict itself is calculated.
    public decimal StrongBuyBelowPrice => Math.Round(IntrinsicValue * 0.70m, 2);
    public decimal BuyBelowPrice => Math.Round(IntrinsicValue, 2);
    public decimal SellAbovePrice => Math.Round(IntrinsicValue * 1.20m, 2);

    // Simple per-share / company snapshot facts - not "good or bad" on their own, just
    // concrete reference numbers a common man can anchor the ratios above to.
    public decimal MarketCapCr { get; set; }
    public decimal Eps { get; set; }
    public decimal BookValuePerShareAmount { get; set; }
    public decimal FaceValue { get; set; }
    public decimal Beta { get; set; }
    public decimal CashConversionCycleDays { get; set; }

    // 3. Deep Financial Indicators
    public decimal PE { get; set; }
    public decimal PB { get; set; }
    public decimal EvToEbitda { get; set; }
    public decimal PegRatio { get; set; }
    public decimal ROEPercent { get; set; }
    public decimal ROCEPercent { get; set; }
    public decimal ROICPercent { get; set; }
    public decimal NetProfitMarginPercent { get; set; }
    public decimal OperatingProfitMarginPercent { get; set; }
    public decimal SloanRatio { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal DebtToEbitda { get; set; }
    public decimal InterestCoverageRatio { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal FreeCashFlowCr { get; set; }
    public decimal FcfConversionPercent { get; set; }
    public decimal DividendYieldPercent { get; set; }
    public decimal DividendPayoutPercent { get; set; }
    public bool IsDividendConsistent { get; set; }

    // Health & Solvency Scores
    public decimal AltmanZScore { get; set; }
    public int PiotroskiFScore { get; set; }
    public decimal MScore { get; set; }

    // 4. Historical Trends (Simple CAGR)
    public decimal RevenueCagr3Yr { get; set; }
    public decimal RevenueCagr5Yr { get; set; }
    public decimal ProfitCagr3Yr { get; set; }
    public decimal ProfitCagr5Yr { get; set; }
    public decimal AverageRoe3Yr { get; set; }
    public decimal AverageRoe5Yr { get; set; }
    public int ConsecutiveDividendYears { get; set; }

    // 5. Detailed Trend Analysis (Year-by-Year Collections)
    public TrendAnalysisData? DetailedTrends { get; set; }
    public List<YearlyMetric> RevenueByYear { get; set; } = new();
    public List<YearlyMetric> ProfitByYear { get; set; } = new();
    public List<YearlyMetric> RoeByYear { get; set; } = new();
    public List<YearlyMetric> RoceByYear { get; set; } = new();
    public List<YearlyMetric> DebtToEquityByYear { get; set; } = new();
    public List<YearlyMetric> FreeCashFlowByYear { get; set; } = new();

    // 6. Trend Summary Indicators
    public string? RevenueTrend { get; set; }
    public string? ProfitTrend { get; set; }
    public string? RoeTrend { get; set; }
    public string? RoceTrend { get; set; }
    public string? DebtTrend { get; set; }
    public string? CashFlowTrend { get; set; }

    // --- Qualitative Metric Indications Helpers ---

    public (string Label, string CssClass) GetPeIndication() =>
        PE switch
        {
            <= 0 => ("Negative", "bg-secondary"),
            < 15 => ("Cheaper", "bg-success"),
            <= 25 => ("Fair", "bg-info text-dark"),
            _ => ("Higher", "bg-warning text-dark")
        };

    public (string Label, string CssClass) GetPbIndication() =>
        PB switch
        {
            <= 0 => ("Negative", "bg-secondary"),
            < 1.5m => ("Cheaper", "bg-success"),
            <= 3.5m => ("Fair", "bg-info text-dark"),
            _ => ("Higher", "bg-warning text-dark")
        };

    public (string Label, string CssClass) GetEvEbitdaIndication() =>
        EvToEbitda switch
        {
            <= 0 => ("N/A", "bg-secondary"),
            < 10m => ("Cheaper", "bg-success"),
            <= 16m => ("Fair", "bg-info text-dark"),
            _ => ("Higher", "bg-warning text-dark")
        };

    // PEG: the P/E ratio divided by the expected earnings growth rate. Below 1 means
    // you're paying less for each point of growth than the market typically demands
    // (often read as attractive); around 1-2 is the broadly "fair" range; above 2 means
    // you're paying a rich premium for that growth. <= 0 covers loss-making or
    // no-growth cases where the ratio isn't meaningful at all.
    public (string Label, string CssClass) GetPegIndication() =>
        PegRatio switch
        {
            <= 0m => ("N/A", "bg-secondary"),
            < 1.0m => ("Attractive", "bg-success"),
            <= 2.0m => ("Fair", "bg-info text-dark"),
            _ => ("Expensive", "bg-warning text-dark")
        };

    // Beta: how much the stock swings compared to the overall market. This is the same
    // figure CalculateWacc already uses to set the discount rate behind every DCF-based
    // valuation method - showing it here doubles as a bit of transparency into what risk
    // assumption the valuation itself is built on, not just a standalone volatility fact.
    public (string Label, string CssClass) GetBetaIndication() =>
        Beta switch
        {
            < 0.8m => ("Low Volatility", "bg-success"),
            <= 1.2m => ("Market-like", "bg-info text-dark"),
            _ => ("High Volatility", "bg-warning text-dark")
        };

    // Cash Conversion Cycle: days between paying for inventory and collecting cash from
    // customers. Lower (even negative, which some retailers/e-commerce businesses
    // achieve by collecting from customers before paying suppliers) means less cash
    // tied up in day-to-day operations - a real efficiency signal, not a data error.
    public (string Label, string CssClass) GetCashConversionCycleIndication() =>
        CashConversionCycleDays switch
        {
            <= 30m => ("Efficient", "bg-success"),
            <= 90m => ("Moderate", "bg-info text-dark"),
            _ => ("Slow", "bg-warning text-dark")
        };

    public (string Label, string CssClass) GetDebtToEquityIndication() =>
        DebtToEquity switch
        {
            < 0.5m => ("Safe", "bg-success"),
            <= 1.0m => ("Moderate", "bg-warning text-dark"),
            _ => ("Risky", "bg-danger")
        };

    // Net Debt / EBITDA: how many years of core operating profit it would take to pay
    // off all debt. A negative value means the company holds more cash than debt (a net
    // cash position) - genuinely the safest case, not a data error, so it gets the same
    // "Very Safe" label as a very low positive ratio rather than being treated oddly.
    public (string Label, string CssClass) GetDebtToEbitdaIndication() =>
        DebtToEbitda switch
        {
            <= 0m => ("Net Cash", "bg-success"),
            <= 2.0m => ("Safe", "bg-success"),
            <= 4.0m => ("Moderate", "bg-warning text-dark"),
            _ => ("Risky", "bg-danger")
        };

    public (string Label, string CssClass) GetInterestCoverageIndication() =>
        IsFinancialSector
            ? ("Not Applicable", "bg-secondary")
            : InterestCoverageRatio switch
            {
                >= 5.0m => ("Safe", "bg-success"),
                >= 2.0m => ("Moderate", "bg-warning text-dark"),
                _ => ("Risky", "bg-danger")
            };

    public (string Label, string CssClass) GetCurrentRatioIndication() =>
        CurrentRatio switch
        {
            >= 1.5m => ("Safe", "bg-success"),
            >= 1.0m => ("Fair", "bg-info text-dark"),
            _ => ("Risky", "bg-danger")
        };

    public (string Label, string CssClass) GetRoeIndication() =>
        ROEPercent switch
        {
            >= 18.0m => ("High Return", "bg-success"),
            >= 12.0m => ("Fair", "bg-info text-dark"),
            _ => ("Low Return", "bg-secondary")
        };

    // ROIC (Return on Invested Capital) uses the same bands as ROE - it's a stricter,
    // leverage-neutral version of the same "how well does this business turn money into
    // profit" question, so a consistent scale makes the three return metrics easy to
    // compare against each other at a glance.
    public (string Label, string CssClass) GetRoicIndication() =>
        ROICPercent switch
        {
            >= 18.0m => ("High Return", "bg-success"),
            >= 12.0m => ("Fair", "bg-info text-dark"),
            _ => ("Low Return", "bg-secondary")
        };

    public (string Label, string CssClass) GetOperatingMarginIndication() =>
        OperatingProfitMarginPercent switch
        {
            < 0m => ("Loss-Making", "bg-danger"),
            < 10.0m => ("Thin Margin", "bg-secondary"),
            <= 20.0m => ("Healthy", "bg-info text-dark"),
            _ => ("High Margin", "bg-success")
        };

    // Sloan Ratio = (Net Profit - Operating Cash Flow) / Total Assets. Positive means
    // profit is running ahead of the cash actually coming in the door (a red flag);
    // negative means cash is running ahead of paper profit (a good sign). 12% is the
    // same threshold PoorCashConversionOrAccrualRule already uses internally to flag
    // high accrual risk, so this indicator always agrees with what's already factored
    // into that rule's decision.
    public (string Label, string CssClass) GetSloanRatioIndication() =>
        SloanRatio switch
        {
            <= 5.0m => ("Low Risk", "bg-success"),
            <= 12.0m => ("Some Risk", "bg-warning text-dark"),
            _ => ("High Risk", "bg-danger")
        };

    // FCF Conversion: what share of accounting profit actually showed up as real free
    // cash. A different lens on the same "is the profit real" question the Sloan Ratio
    // asks - one temporarily weak year (e.g. a heavy capex year) isn't necessarily a
    // red flag on its own, so this reads as a caution ("Weak"), not an alarm.
    public (string Label, string CssClass) GetFcfConversionIndication() =>
        FcfConversionPercent switch
        {
            >= 80.0m => ("Excellent", "bg-success"),
            >= 50.0m => ("Fair", "bg-info text-dark"),
            _ => ("Weak", "bg-warning text-dark")
        };

    // Dividend payout ratio: what share of profit is actually paid out, as opposed to
    // kept and reinvested. Distinct from Dividend Yield (which is a % of the SHARE
    // PRICE, not of profit) - a stock can have a low yield but a high payout ratio, or
    // vice versa. A very high payout leaves little buffer for a bad year or reinvestment.
    public (string Label, string CssClass) GetDividendPayoutIndication() =>
        DividendPayoutPercent switch
        {
            <= 0m => ("No Dividend", "bg-secondary"),
            <= 60.0m => ("Balanced", "bg-success"),
            <= 90.0m => ("High Payout", "bg-warning text-dark"),
            _ => ("Very High", "bg-danger")
        };

    public (string Label, string CssClass) GetAltmanZIndication() =>
        IsFinancialSector
            ? ("Not Applicable", "bg-secondary")
            : AltmanZScore switch
            {
                >= 2.99m => ("Safe Zone", "bg-success"),
                >= 1.81m => ("Grey Zone", "bg-warning text-dark"),
                _ => ("Distress Risk", "bg-danger")
            };

    // Piotroski F-Score indicator, matching the inline badge logic that used to live
    // directly in Details.cshtml - centralized here so it gets the same
    // IsFinancialSector guard as the other three health scores below, rather than
    // needing a separate, easy-to-forget check duplicated in the view.
    public (string Label, string CssClass) GetPiotroskiIndication() =>
        IsFinancialSector
            ? ("Not Applicable", "bg-secondary")
            : PiotroskiFScore switch
            {
                >= 7 => ("Strong", "bg-success"),
                >= 4 => ("Average", "bg-info text-dark"),
                _ => ("Weak", "bg-danger")
            };

    // Plain-language, one-line translations of the valuation method names into what each
    // one actually does, for someone without a finance background. Keyed on the exact
    // display strings the valuation engine produces (see MethodNames.ToDisplayString()).
    private static readonly Dictionary<string, string> MethodExplanations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Excess Returns Model"] = "Values the company by how much profit it earns above what investors could reasonably expect for the risk taken.",
        ["Price-to-TBV (Tangible Book Value)"] = "Based on the value of the company's physical assets alone, adjusted for how well it puts them to use.",
        ["Price-to-Book (P/B)"] = "Compares the price to the company's accounting net worth - what's left if it paid off every debt today.",
        ["Price-to-Book (P/B) Intrinsic Multiples"] = "Compares the price to net worth, adjusted up or down based on how profitable the company currently is.",
        ["EV/Sales Relative Multiple"] = "Values the company against the size of its revenue, compared to similar businesses.",
        ["Price-to-Sales (P/S)"] = "Compares the price to the company's total sales - useful when profit is too small or negative to judge by.",
        ["Net Asset Value (NAV)"] = "What would be left for shareholders if the company sold everything it owns and paid off all its debts today.",
        ["Normalized Mid-Cycle P/E"] = "Averages out unusually good and bad years to estimate a fair profit multiple for a business with ups and downs.",
        ["Normalized Mid-Cycle EV/EBITDA"] = "Same idea as Normalized P/E, but based on core operating profit instead of net profit.",
        ["Exit Multiple DCF (FCFF)"] = "Projects the cash the business will generate for years ahead and works out what that's worth today.",
        ["Exit Multiple DCF"] = "Projects the cash the business will generate for years ahead and works out what that's worth today.",
        ["EV/EBITDA Relative Multiple"] = "Compares the full value of the company (including its debt) to its core operating profit.",
        ["Dividend Discount Model (DDM)"] = "Values the company based on the dividends it's expected to keep paying shareholders.",
        ["Dividend Discount Model (Pass-Through Yield)"] = "Estimates fair value from the dividend the stock is already paying out right now.",
        ["Gordon Growth Model"] = "Assumes dividends grow at a steady, modest pace forever, and values the stock on that basis.",
        ["Gordon Growth DDM"] = "Assumes dividends grow at a steady, modest pace forever, and values the stock on that basis.",
        ["Buffett Owner Earnings Model"] = "Values the business on the real cash left over for owners after the reinvestment needed to keep it running.",
        ["2-Stage FCFE DCF"] = "Assumes a few years of faster growth followed by slower, steady growth, and values the cash this produces.",
        ["2-Stage Discounted Cash Flow (DCF)"] = "Assumes a few years of faster growth followed by slower, steady growth, and values the cash this produces.",
        ["Price-to-Earnings-to-Growth (PEG)"] = "Checks whether the price is reasonable given how fast the company's profit is actually growing.",
        ["Price-to-Earnings (P/E) Multiple"] = "Compares the price to how much profit the company makes per share - the most common yardstick.",
        ["Discounted Cash Flow (DCF)"] = "Projects the company's future cash flows and works out what all of that is worth today.",
        ["Standard DCF"] = "Projects the company's future cash flows and works out what all of that is worth today.",
        ["Adjusted Net Asset Value (SOTP with HoldCo Discount)"] = "Adds up the value of each part of the business separately, with a discount for the complexity of holding them together."
    };

    public string GetMethodExplanation(string? methodName) =>
        !string.IsNullOrWhiteSpace(methodName) && MethodExplanations.TryGetValue(methodName, out var explanation)
            ? explanation
            : "A financial model used to estimate what the company is really worth.";

    // Beneish M-Score: a forensic-accounting check for whether a company's reported
    // profit looks "too good to be true" compared to its actual cash and asset trends -
    // i.e. is it possibly dressing up its numbers rather than genuinely earning them.
    // -1.78 is the same threshold already used internally by the Sound Score calculation
    // (CalculateSoundScore's isBeneishManipulator flag), so this indicator always agrees
    // with what's already factored into the score above. -2.22 is the standard academic
    // "unlikely manipulator" cutoff from the original Beneish research, used here as a
    // stricter "Low Risk" band so the common case (most healthy companies) reads clearly
    // safe rather than merely "not flagged".
    public (string Label, string CssClass) GetMScoreIndication() =>
        IsFinancialSector
            ? ("Not Applicable", "bg-secondary")
            : MScore switch
            {
                <= -2.22m => ("Low Risk", "bg-success"),
                <= -1.78m => ("Some Risk", "bg-warning text-dark"),
                _ => ("High Risk", "bg-danger")
            };

    // Net profit margin: how many rupees of real, bottom-line profit a company keeps for
    // every Rs. 100 of sales. Thresholds are deliberately simple, general-purpose bands
    // (not sector-adjusted) so a non-specialist gets an honest, if rough, first read.
    public (string Label, string CssClass) GetNetProfitMarginIndication() =>
        NetProfitMarginPercent switch
        {
            < 0m => ("Loss-Making", "bg-danger"),
            < 5.0m => ("Thin Margin", "bg-secondary"),
            <= 15.0m => ("Healthy", "bg-info text-dark"),
            _ => ("High Margin", "bg-success")
        };
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Email or Username is required.")]
    [Display(Name = "Email or Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class UserViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Active", "Inactive", "Pending"
    public DateTime LastLogin { get; set; }
}

public class AccountDashboardViewModel
{
    public List<UserViewModel> Users { get; set; } = new();
}