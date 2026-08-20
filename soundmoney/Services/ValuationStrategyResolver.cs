using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    // Context container holding calculated domain metrics
    public class EvaluationContext
    {
        public DeepFinancial Data { get; set; }
        public List<HistoricalFinancial> Historicals { get; set; }
        public decimal ActualNetDebtCr { get; set; }
        public decimal DebtToEbit { get; set; }
        public decimal CapexToOcf { get; set; }
        public decimal OcfToNetProfit { get; set; }
        public bool IsCashPredictable { get; set; }
        public bool IsCyclical { get; set; }
    }

    public interface IValuationRule
    {
        int Priority { get; }
        bool IsMatch(EvaluationContext ctx);
        ValuationMethodology Result(EvaluationContext ctx);
    }

    public static class ValuationStrategyResolver
    {
        private static readonly List<IValuationRule> Rules = new()
        {
            new FinancialSectorRule(),
            new ReinvestingGrowthRule(),
            new DistressTurnaroundRule(),
            new CyclicalEarningsRule(),
            new HighLeverageCapitalIntensiveRule(),
            new MatureHighPayoutRule(),
            new AssetLightMoatRule(),
            new DefaultFallbackRule()
        };

        public static ValuationMethodology ResolveMethodology(
            DeepFinancial data,
            IEnumerable<HistoricalFinancial> historicals)
        {
            var ctx = BuildContext(data, historicals);

            return Rules
                .OrderBy(r => r.Priority)
                .First(r => r.IsMatch(ctx))
                .Result(ctx);
        }

        private static EvaluationContext BuildContext(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            var historyList = historicals?.OrderBy(h => h.Year).ToList() ?? new List<HistoricalFinancial>();

            // Correct Net Debt: Total Debt - Cash & Equivalents
            decimal actualNetDebt = data.TotalBorrowingsCr - data.CashAndEquivalentsCr;
            decimal debtToEbit = (actualNetDebt > 0 && data.EbitCr > 0) ? (actualNetDebt / data.EbitCr) : 0m;
            decimal capexToOcf = data.CashFromOperationsCr > 0 ? (data.GrossCapexCr / data.CashFromOperationsCr) : 1.0m;

            decimal fcfCr = data.FreeCashFlowCr != 0 ? data.FreeCashFlowCr : (data.CashFromOperationsCr - data.GrossCapexCr);
            decimal ocfToNp = data.NetProfitCr > 0 ? (data.CashFromOperationsCr / data.NetProfitCr) : 0m;

            bool cashPredictable = fcfCr > 0 && ocfToNp >= 0.8m;
            if (historyList.Count >= 3 && historyList.Any(h => h.HistoricalOcfCr <= 0))
            {
                cashPredictable = false;
            }

            bool cyclical = false;
            if (historyList.Count >= 3)
            {
                decimal maxProfit = historyList.Max(h => h.HistoricalNetProfitCr);
                decimal minProfit = historyList.Min(h => h.HistoricalNetProfitCr);
                if (minProfit <= 0 || (maxProfit > 0 && (maxProfit - minProfit) / maxProfit > 0.35m))
                {
                    cyclical = true;
                }
            }

            return new EvaluationContext
            {
                Data = data,
                Historicals = historyList,
                ActualNetDebtCr = actualNetDebt,
                DebtToEbit = debtToEbit,
                CapexToOcf = capexToOcf,
                OcfToNetProfit = ocfToNp,
                IsCashPredictable = cashPredictable,
                IsCyclical = cyclical
            };
        }
    }

    #region Rule Definitions

    public class FinancialSectorRule : IValuationRule
    {
        public int Priority => 10;
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.IsFinancialSector;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Excess Returns Model",
            SecondaryMethod = "Price-to-TBV (Tangible Book Value)",
            Rationale = "Financial institution: Debt acts as operational inventory, requiring equity residual income models."
        };
    }

    public class ReinvestingGrowthRule : IValuationRule
    {
        public int Priority => 20;
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.NetProfitCr <= 0 && ctx.Data.CashFromOperationsCr > 0;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "EV/Sales Relative Multiple",
            SecondaryMethod = "Price-to-Sales (P/S)",
            Rationale = "Unprofitable on net income but cash-flow positive; valued on revenue scale and operating cash efficiency."
        };
    }

    public class DistressTurnaroundRule : IValuationRule
    {
        public int Priority => 30;
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.NetProfitCr <= 0 || ctx.Data.BookValuePerShare <= 0;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Negative net income and cash flow; falling back to asset-based liquidation floor."
        };
    }

    public class CyclicalEarningsRule : IValuationRule
    {
        public int Priority => 40;
        public bool IsMatch(EvaluationContext ctx) => ctx.IsCyclical;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Normalized Mid-Cycle P/E",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "High earnings volatility detected; using cycle-normalized metrics to prevent peak/trough errors."
        };
    }

    public class HighLeverageCapitalIntensiveRule : IValuationRule
    {
        public int Priority => 50;
        public bool IsMatch(EvaluationContext ctx) => ctx.DebtToEbit >= 2.5m || ctx.CapexToOcf >= 0.60m;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Exit Multiple DCF (FCFF)",
            SecondaryMethod = "EV/EBITDA Relative Multiple",
            Rationale = "Capital-intensive balance sheet or high leverage; evaluating Enterprise Value (FCFF) models."
        };
    }

    public class MatureHighPayoutRule : IValuationRule
    {
        public int Priority => 60;
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.DividendPayoutPercent >= 40m && ctx.IsCashPredictable;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Dividend Discount Model (DDM)",
            SecondaryMethod = "Gordon Growth Model",
            Rationale = "Mature entity distributing over 40% of net profits as dividends."
        };
    }

    public class AssetLightMoatRule : IValuationRule
    {
        public int Priority => 70;
        public bool IsMatch(EvaluationContext ctx) => ctx.IsCashPredictable;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = ctx.Data.ReportedRoePercent >= 20m ? "Buffett Owner Earnings Model" : "2-Stage FCFE DCF",
            SecondaryMethod = "Price-to-Earnings-to-Growth (PEG)",
            Rationale = "Low debt, low CapEx, and high cash flow predictability; ideal for equity-level discounted cash flow models."
        };
    }

    public class DefaultFallbackRule : IValuationRule
    {
        public int Priority => 999;
        public bool IsMatch(EvaluationContext ctx) => true;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Price-to-Earnings (P/E) Multiple",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Standard financial operating profile; applying relative earnings multiples."
        };
    }

    #endregion
}