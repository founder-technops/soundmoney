namespace SoundMoney.Models
{
    public class DeepFinancial
    {
        public string Symbol { get; set; }

        // Header Metrics
        public decimal CurrentPrice { get; set; }
        public decimal MarketCapCr { get; set; }
        public decimal BookValuePerShare { get; set; }
        public decimal TotalSharesCr { get; set; }
        public decimal ReportedRoePercent { get; set; }
        public decimal DividendYieldPercent { get; set; }

        // P&L Metrics (Latest Year / TTM in Crores)
        public decimal RevenueCr { get; set; }
        public decimal OperatingProfitEbitdaCr { get; set; }
        public decimal DepreciationCr { get; set; }
        public decimal NetProfitCr { get; set; }
        public decimal DividendPayoutPercent { get; set; }

        // Balance Sheet Metrics (Latest Year in Crores)
        public decimal ShareCapitalCr { get; set; }
        public decimal ReservesCr { get; set; }
        public decimal TotalEquityCr { get; set; }
        public decimal TotalBorrowingsCr { get; set; }
        public decimal NetFixedAssetsCr { get; set; }
        public decimal CwipCr { get; set; }
        public decimal CashAndEquivalentsCr { get; set; }
        public decimal IntangibleAssetsCr { get; set; }
        public decimal TotalLiabilitiesCr { get; set; }
        public decimal TotalAssetsCr { get; set; }

        // Cash Flow Metrics (Latest Year in Crores)
        public decimal CashFromOperationsCr { get; set; }
        public decimal GrossCapexCr { get; set; }
        public decimal FreeCashFlowCr { get; set; }
    }
}
