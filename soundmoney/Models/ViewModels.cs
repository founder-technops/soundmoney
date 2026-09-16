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

    // 2. Core Valuation Output
    public decimal IntrinsicValue { get; set; }
    public decimal MarginOfSafetyPercent { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public string SoundScoreRating { get; set; } = string.Empty;

    // 3. Deep Financial Indicators
    public decimal PE { get; set; }
    public decimal PB { get; set; }
    public decimal EvToEbitda { get; set; }
    public decimal ROEPercent { get; set; }
    public decimal ROCEPercent { get; set; }
    public decimal NetProfitMarginPercent { get; set; }
    public decimal DebtToEquity { get; set; }
    public decimal InterestCoverageRatio { get; set; }
    public decimal CurrentRatio { get; set; }
    public decimal FreeCashFlowCr { get; set; }
    public decimal DividendYieldPercent { get; set; }
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

    public (string Label, string CssClass) GetDebtToEquityIndication() =>
        DebtToEquity switch
        {
            < 0.5m => ("Safe", "bg-success"),
            <= 1.0m => ("Moderate", "bg-warning text-dark"),
            _ => ("Risky", "bg-danger")
        };

    public (string Label, string CssClass) GetInterestCoverageIndication() =>
        InterestCoverageRatio switch
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

    public (string Label, string CssClass) GetAltmanZIndication() =>
        AltmanZScore switch
        {
            >= 2.99m => ("Safe Zone", "bg-success"),
            >= 1.81m => ("Grey Zone", "bg-warning text-dark"),
            _ => ("Distress Risk", "bg-danger")
        };

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
        MScore switch
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