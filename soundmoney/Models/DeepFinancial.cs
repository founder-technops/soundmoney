using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SoundMoney.Models
{
    public class DeepFinancial
    {
        public string Symbol { get; set; } = string.Empty;

        // --- Header & Market Metrics ---
        public decimal CurrentPrice { get; set; }
        public decimal MarketCapCr { get; set; }
        public decimal BookValuePerShare { get; set; }
        public decimal TotalSharesCr { get; set; }
        public decimal ReportedRoePercent { get; set; }
        public decimal DividendYieldPercent { get; set; }
        public decimal FaceValue { get; set; }
        public decimal Beta { get; set; } = 1.0m; // Scraped or benchmark fallback

        // --- Sector Classifier ---
        public bool IsFinancialSector { get; set; }

        // --- P&L Metrics (Cr) ---
        public decimal RevenueCr { get; set; }
        public decimal OperatingProfitCr { get; set; }
        public decimal OperatingProfitEbitdaCr { get; set; }
        public decimal EbitCr { get; set; }
        public decimal InterestExpenseCr { get; set; } // Added for Cost of Debt (Rd)
        public decimal ProfitBeforeTaxCr { get; set; } // Added for Tax Rate (T)
        public decimal TaxPercent { get; set; }        // Added for Tax Rate (T)
        public decimal DepreciationCr { get; set; }
        public decimal NetProfitCr { get; set; }
        public decimal DividendPayoutPercent { get; set; }

        // --- Balance Sheet Metrics (Cr) ---
        public decimal ShareCapitalCr { get; set; }
        public decimal ReservesCr { get; set; }
        public decimal TotalEquityCr { get; set; }
        public decimal TotalBorrowingsCr { get; set; }
        public decimal NetFixedAssetsCr { get; set; }
        public decimal CwipCr { get; set; }
        public decimal CashAndEquivalentsCr { get; set; }
        public decimal IntangibleAssetsCr { get; set; }
        public decimal InvestmentsCr { get; set; }
        public decimal TotalLiabilitiesCr { get; set; }
        public decimal TotalAssetsCr { get; set; }
        public decimal NetCashCr { get; set; }

        // --- Cash Flow Metrics (Cr) ---
        public decimal CashFromOperationsCr { get; set; }
        public decimal CashFromInvestmentCr { get; set; }
        public decimal CashFromFinanceCr { get; set; }
        public decimal GrossCapexCr { get; set; }
        public decimal FreeCashFlowCr { get; set; }

        // OR calculated directly from Balance Sheet components:
        public decimal CurrentAssetsCr { get; set; }
        public decimal CurrentLiabilitiesCr { get; set; }

        public decimal CapitalAdequacyPercent => IsFinancialSector && TotalEquityCr > 0 && TotalAssetsCr > 0
            ? Math.Round((TotalEquityCr / TotalAssetsCr) * 100m, 2)
            : 0m;

        public decimal ReportedRoaPercent => NetProfitCr > 0m && TotalAssetsCr > 0m 
            ? ((NetProfitCr / TotalAssetsCr) * 100m) : IsFinancialSector ? 1.0m : 0m;

        // --- Derived WACC Calculators ---
        public decimal EffectiveTaxRate => TaxPercent > 0
            ? Math.Clamp(TaxPercent / 100m, 0.0m, 0.35m)
            : 0.25m;

        public decimal CostOfDebt => TotalBorrowingsCr > 0 && InterestExpenseCr > 0
            ? Math.Clamp(InterestExpenseCr / TotalBorrowingsCr, 0.03m, 0.18m)
            : 0.08m; // Default corporate borrowing rate fallback
    }
}