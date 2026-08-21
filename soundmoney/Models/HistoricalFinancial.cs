namespace SoundMoney.Models
{
    public class HistoricalFinancial
    {
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Financial Year (e.g., 2024, 2025)
        /// </summary>
        public int Year { get; set; }

        public decimal EquityCapitalCr { get; set; }

        /// <summary>
        /// Historical Total Revenue / Sales in Crores (₹ Cr)
        /// </summary>
        public decimal HistoricalRevenueCr { get; set; } = 0m;

        /// <summary>
        /// Historical Net Profit After Tax (PAT) in Crores (₹ Cr)
        /// </summary>
        public decimal HistoricalNetProfitCr { get; set; } = 0m;

        /// <summary>
        /// Historical Operating Cash Flow (OCF) in Crores (₹ Cr)
        /// </summary>
        public decimal HistoricalOcfCr { get; set; } = 0m;

        /// <summary>
        /// Historical Gross Capital Expenditure (CapEx) in Crores (₹ Cr)
        /// </summary>
        public decimal HistoricalCapexCr { get; set; } = 0m;

        // --- Computed Helper Properties ---

        /// <summary>
        /// Derived Historical Free Cash Flow: Operating Cash Flow - CapEx (in Crores)
        /// </summary>
        public decimal HistoricalFcfCr { get; set; } = 0m;

        public decimal HistoricalPatCr { get; set; } = 0m;

        public decimal HistoricalSharesCr { get; set; } = 0m;

        /// <summary>
        /// Historical Cash Conversion Ratio (OCF / Net Profit)
        /// </summary>
        public decimal CashConversionRatio => HistoricalNetProfitCr > 0
        ? Math.Round(HistoricalOcfCr / HistoricalNetProfitCr, 2) : 0m;
    }
}