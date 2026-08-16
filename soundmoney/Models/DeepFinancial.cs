namespace SoundMoney.Models
{
    public class DeepFinancial
    {
        public string Symbol { get; set; }

        // Header Metrics
        public double CurrentPrice { get; set; }
        public double MarketCapCr { get; set; }
        public double BookValuePerShare { get; set; }
        public double TotalSharesCr { get; set; }
        public double ReportedRoePercent { get; set; }
        public double DividendYieldPercent { get; set; }

        // P&L Metrics (Latest Year / TTM in Crores)
        public double RevenueCr { get; set; }
        public double OperatingProfitEbitdaCr { get; set; }
        public double DepreciationCr { get; set; }
        public double NetProfitCr { get; set; }
        public double DividendPayoutPercent { get; set; }

        // Balance Sheet Metrics (Latest Year in Crores)
        public double ShareCapitalCr { get; set; }
        public double ReservesCr { get; set; }
        public double TotalEquityCr { get; set; }
        public double TotalBorrowingsCr { get; set; }
        public double NetFixedAssetsCr { get; set; }
        public double CwipCr { get; set; }
        public double CashAndEquivalentsCr { get; set; }
        public double IntangibleAssetsCr { get; set; }
        public double TotalLiabilitiesCr { get; set; }
        public double TotalAssetsCr { get; set; }

        // Cash Flow Metrics (Latest Year in Crores)
        public double CashFromOperationsCr { get; set; }
        public double GrossCapexCr { get; set; }
        public double FreeCashFlowCr { get; set; }
    }
}
