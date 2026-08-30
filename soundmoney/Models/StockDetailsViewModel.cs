namespace SoundMoney.Models
{
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

        // 4. Historical Trends
        public decimal RevenueCagr3Yr { get; set; }
        public decimal RevenueCagr5Yr { get; set; }
        public decimal ProfitCagr3Yr { get; set; }
        public decimal ProfitCagr5Yr { get; set; }
        public decimal AverageRoe3Yr { get; set; }
        public decimal AverageRoe5Yr { get; set; }
        public int ConsecutiveDividendYears { get; set; }
    }
}