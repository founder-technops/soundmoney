using System;

namespace SoundMoney.Models
{
    public class DeepFinancial
    {
        public string Symbol { get; set; } = string.Empty;

        // --- Header & Market Metrics ---
        public decimal CurrentPrice { get; set; }
        public decimal MarketCapCr { get; set; }
        public decimal ReportedPePercent { get; set; }
        public decimal BookValuePerShare { get; set; }
        public decimal DividendYieldPercent { get; set; }
        public decimal ReportedRocePercent { get; set; }
        public decimal ReportedRoePercent { get; set; }
        public decimal FaceValue { get; set; }

        // --- Shareholding Metrics ---
        public decimal TotalSharesCr => CurrentPrice > 0m && MarketCapCr > 0m
            ? Math.Round(MarketCapCr / CurrentPrice, 4)
            : 0m;
        public decimal Beta { get; set; } = 1.0m;
        public decimal PromoterPledgePercent { get; set; }

        // --- Sector Classifier ---
        public bool IsFinancialSector { get; set; }

        // --- P&L Metrics (Cr) ---
        public decimal SalesCr { get; set; }
        public decimal ExpenseCr { get; set; }
        public decimal OperatingProfitCr { get; set; }
        public decimal OperatingProfitMargin => SalesCr > 0m ? Math.Round((OperatingProfitCr / SalesCr) * 100m, 2) : 0m;
        public decimal OtherIncomeCr { get; set; }
        public decimal IntrestIncomeCr { get; set; }
        public decimal DepreciationCr { get; set; }
        public decimal ProfitBeforeTaxCr { get; set; }
        public decimal TaxPercent { get; set; }
        public decimal NetProfitCr { get; set; }
        public decimal InterestExpenseCr { get; set; }
        public decimal Eps { get; set; }
        public decimal DividendPayoutPercent { get; set; }

        // --- Derived P&L Metrics (Cr) ---
        public decimal EbitCr => (OperatingProfitCr + OtherIncomeCr) - DepreciationCr;
        public decimal EbitdaCr => OperatingProfitCr + OtherIncomeCr;
        // --- Balance Sheet Metrics (Cr) ---
        public decimal ShareCapitalCr { get; set; }
        public decimal ReservesCr { get; set; }
        public decimal TotalBorrowingsCr { get; set; }
        public decimal OtherLiabilitiesCr { get; set; }
        public decimal TotalLiabilitiesCr => ShareCapitalCr + ReservesCr + TotalBorrowingsCr + OtherLiabilitiesCr;
        public decimal NetFixedAssetsCr { get; set; }
        public decimal CwipCr { get; set; }
        public decimal InvestmentsCr { get; set; }
        public decimal OtherAssetsCr { get; set; }
        public decimal TotalAssetsCr => NetFixedAssetsCr + CwipCr + InvestmentsCr + OtherAssetsCr;

        // --- Derived Balance Sheet Metrics (Cr) ---
        public decimal TotalEquityCapitalCr => ShareCapitalCr + ReservesCr;

        /// <summary>
        /// Audited / Derived Cash & Cash Equivalents populated by ScraperService.
        /// NOTE: Screener.in does not expose a dedicated Cash & Equivalents balance-sheet
        /// line (it is folded into Other Assets), so this is typically derived by rolling
        /// forward net cash flows from the earliest scraped year. For companies with a long
        /// operating history relative to how many years of cash-flow data were scraped
        /// (see <see cref="CashHistoryYears"/> / <see cref="IsCashEstimateReliable"/>), this
        /// will understate true cash because it assumes an opening balance of zero.
        /// </summary>
        public decimal CashAndEquivalentsCr { get; set; }

        /// <summary>
        /// Number of years of cash-flow history the CashAndEquivalentsCr roll-forward was
        /// built from. Populated by ScraperService. 0 if unknown.
        /// </summary>
        public int CashHistoryYears { get; set; }

        /// <summary>
        /// False when the roll-forward has too little history to be trusted as an absolute
        /// cash balance (e.g. a decades-old company with only ~10 years of scraped cash-flow
        /// data). Consumers should treat NetCashCr as directional, not exact, when this is
        /// false, and prefer gross-borrowings-based leverage checks instead.
        /// </summary>
        public bool IsCashEstimateReliable { get; set; } = true;

        public decimal NetCashCr => CashAndEquivalentsCr - TotalBorrowingsCr;
        public decimal NonCurrentAssetsCr => NetFixedAssetsCr + CwipCr + InvestmentsCr;
        public decimal CurrentAssetsCr => Math.Max(0m, TotalAssetsCr - NonCurrentAssetsCr);
        public decimal WorkingCapitalCr => CurrentAssetsCr - OtherLiabilitiesCr;

        // --- Cash Flow Metrics (Cr) ---
        public decimal CashFromOperationsCr { get; set; }
        public decimal CashFromInvestmentCr { get; set; }
        public decimal CashFromFinanceCr { get; set; }
        public decimal FreeCashFlowCr { get; set; }
        public decimal netCashFlowCr => CashFromOperationsCr + CashFromInvestmentCr + CashFromFinanceCr;
        public decimal GrossCapexCr => CashFromOperationsCr - FreeCashFlowCr;

        /// <summary>
        /// Quality of Earnings Ratio (CFO / Operating Profit)
        /// </summary>
        public decimal CfoToOpRatio => OperatingProfitCr > 0m
            ? Math.Round(CashFromOperationsCr / OperatingProfitCr, 4)
            : 0m;

        // Computed metrics ensuring whole percentage representation
        public decimal CapitalAdequacyPercent => IsFinancialSector && TotalEquityCapitalCr > 0m && TotalAssetsCr > 0m
            ? Math.Round((TotalEquityCapitalCr / TotalAssetsCr) * 100m, 2)
            : 0m;

        public decimal ReportedRoaPercent => TotalAssetsCr > 0m
            ? Math.Round((NetProfitCr / TotalAssetsCr) * 100m, 2)
            : (IsFinancialSector ? 1.0m : 0m);

        // Derived WACC helper properties
        public decimal EffectiveTaxRate => TaxPercent > 0m
            ? Math.Clamp(TaxPercent / 100m, 0.0m, 0.35m)
            : 0.25m;

        public decimal CostOfDebt => TotalBorrowingsCr > 0m && InterestExpenseCr > 0m
            ? Math.Clamp(InterestExpenseCr / TotalBorrowingsCr, 0.03m, 0.18m)
            : 0.08m;

        /// <summary>
        /// Explicit flag set via metadata or sector classification tags.
        /// </summary>
        public bool IsCoreInvestmentCompanyExplicit { get; set; }

         
        /// <summary>
        /// Proportion of total balance sheet assets held in equity and investment holdings.
        /// </summary>
        public decimal InvestmentAssetsToTotalAssetsRatio =>
            TotalAssetsCr > 0m ? Math.Clamp(InvestmentsCr / TotalAssetsCr, 0m, 1m) : 0m;

        /// <summary>
        /// Proportion of total revenue generated via interest income (separates operating lenders from passive holding vehicles).
        /// </summary>
        public decimal InterestIncomeToTotalRevenueRatio =>
            SalesCr > 0m ? Math.Clamp(IntrestIncomeCr / SalesCr, 0m, 1m) : 0m;

        /// <summary>
        /// Identifies whether the entity operates as a Core Investment Company (CIC) or Holding Company
        /// using either explicit regulatory tags or balance sheet/revenue structure thresholds.
        /// </summary>
        public bool IsCoreInvestmentCompany =>
            IsCoreInvestmentCompanyExplicit ||
            (InvestmentAssetsToTotalAssetsRatio >= 0.70m && InterestIncomeToTotalRevenueRatio < 0.30m);

        /// <summary>
        /// Calculated Return on Invested Capital (ROIC %)
        /// NOPAT / (Total Equity + Total Borrowings - Cash & Equivalents)
        /// </summary>
        public decimal RoicPercent
        {
            get
            {
                decimal investedCapital = TotalEquityCapitalCr + TotalBorrowingsCr - CashAndEquivalentsCr;
                if (investedCapital <= 0m) return 0m;

                decimal nopat = EbitCr * (1m - EffectiveTaxRate);
                return Math.Round((nopat / investedCapital) * 100m, 2);
            }
        }
    }
}