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
        public decimal EbitCr => ((OperatingProfitCr + OtherIncomeCr + IntrestIncomeCr) - (InterestExpenseCr + DepreciationCr));

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
        public decimal TotalAssetsCr  => NetFixedAssetsCr + CwipCr + InvestmentsCr + OtherAssetsCr;

        // --- Derived Balance Sheet Metrics (Cr) ---
        public decimal TotalEquityCapitalCr => ShareCapitalCr + ReservesCr;
        public decimal CashAndEquivalentsCr => TotalAssetsCr - (NetFixedAssetsCr + CwipCr + InvestmentsCr + OtherAssetsCr); //wrong calculations - Need to explore
        public decimal NetCashCr => CashAndEquivalentsCr - TotalBorrowingsCr;
        public decimal NonCurrentAssetsCr => NetFixedAssetsCr + CwipCr + InvestmentsCr;
        public decimal CurrentAssetsCr => Math.Max(0m, TotalAssetsCr - NonCurrentAssetsCr);
        public decimal WorkingCapitalCr => CurrentAssetsCr - OtherLiabilitiesCr;
        
        // --- Cash Flow Metrics (Cr) ---
        public decimal CashFromOperationsCr { get; set; }
        public decimal CashFromInvestmentCr { get; set; }
        public decimal CashFromFinanceCr { get; set; }
        public decimal FreeCashFlowCr { get; set; }
        public decimal GrossCapexCr => CashFromOperationsCr - FreeCashFlowCr;


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
    }
}