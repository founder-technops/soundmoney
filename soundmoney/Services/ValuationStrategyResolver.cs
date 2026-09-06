using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public class EvaluationContext
    {
        public DeepFinancial Data { get; set; }
        public List<HistoricalFinancial> Historicals { get; set; }
        public decimal ActualNetDebtCr { get; set; }
        public decimal DebtToEbit { get; set; }
        public decimal CapexToOcf { get; set; }
        public decimal OcfToNetProfit { get; set; }
        public decimal FcfCr { get; set; }
        public bool IsCashPredictable { get; set; }
        public bool IsCyclical { get; set; }
        public bool IsInfrastructureUtility { get; set; } // Flagged to prevent incorrect cyclical routing

        /// <summary>
        /// True when there's enough positive cash-flow/EBIT signal for an FCFF-style DCF
        /// (Exit Multiple DCF, Standard/Two-Stage DCF) to actually compute a non-zero value.
        /// A company can be genuinely capital-intensive/high-leverage (matching
        /// HighLeverageCapitalIntensiveRule) while sitting at a cyclical trough with
        /// negative free cash flow — in that case a DCF-family method would just return 0,
        /// so rules should prefer a multiple-based method instead.
        /// </summary>
        public bool CanComputeCashFlowDcf => FcfCr > 0m && Data.EbitCr > 0m;
    }

    /// <summary>
    /// Named priority tiers for IValuationRule.Priority. Lower runs first; the resolver picks
    /// the first matching rule. Keeping these named (rather than inline magic numbers) makes
    /// the intended ordering — and where a new rule should slot in — explicit at a glance.
    /// </summary>
    internal static class RulePriority
    {
        public const int FinancialSector = 10;
        public const int ReinvestingGrowth = 20;
        public const int DistressTurnaround = 30;

        // Must run before Cyclical: a capital-intensive/high-leverage utility or
        // infrastructure company (e.g. a renewables build-out) can also show the earnings
        // volatility that CyclicalEarningsRule looks for, but its debt-funded-growth profile
        // needs an enterprise-value method, not a cycle-normalized P/E.
        public const int HighLeverageCapitalIntensive = 40;
        public const int CyclicalEarnings = 50;
        public const int MatureHighPayout = 60;
        public const int AssetLightMoat = 70;
        public const int DefaultFallback = 999;
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
            new HighLeverageCapitalIntensiveRule(), // Priority boosted above Cyclical Rule for Infrastructure/Utilities
            new CyclicalEarningsRule(),
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

            // When the scraped cash-flow history is too short to trust the roll-forward cash
            // balance (see DeepFinancial.IsCashEstimateReliable), net debt built from it can
            // be overstated for long-lived, cash-rich companies. Fall back to gross
            // borrowings against EBIT in that case rather than compounding an unreliable
            // cash figure into the leverage read.
            decimal actualNetDebt = data.IsCashEstimateReliable
                ? data.TotalBorrowingsCr - data.CashAndEquivalentsCr
                : data.TotalBorrowingsCr;
            decimal debtToEbit = (actualNetDebt > 0 && data.EbitCr > 0) ? (actualNetDebt / data.EbitCr) : 0m;
            decimal capexToOcf = data.CashFromOperationsCr > 0 ? (data.GrossCapexCr / data.CashFromOperationsCr) : 1.0m;

            decimal fcfCr = data.FreeCashFlowCr != 0 ? data.FreeCashFlowCr : (data.CashFromOperationsCr - data.GrossCapexCr);
            decimal ocfToNp = data.NetProfitCr > 0 ? (data.CashFromOperationsCr / data.NetProfitCr) : 0m;

            bool cashPredictable = fcfCr > 0 && ocfToNp >= 0.8m;
            int negativeOcfYears = historyList.Count(h => h.HistoricalOcfCr <= 0);
            if (negativeOcfYears > 1) cashPredictable = false;

            // FIX: Identify infrastructure/utility companies with high debt + heavy ongoing CapEx (e.g., ADANIGREEN)
            bool isInfraUtility = (debtToEbit >= 3.5m || capexToOcf >= 0.75m) && !data.IsFinancialSector;

            bool cyclical = false;
            if (historyList.Count >= 3 && !isInfraUtility)
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
                FcfCr = fcfCr,
                IsCashPredictable = cashPredictable,
                IsCyclical = cyclical,
                IsInfrastructureUtility = isInfraUtility
            };
        }
    }

    #region Rule Definitions

    public class FinancialSectorRule : IValuationRule
    {
        public int Priority => RulePriority.FinancialSector;
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
        public int Priority => RulePriority.ReinvestingGrowth;
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
        public int Priority => RulePriority.DistressTurnaround;
        // Only route to an asset-based floor when net income is negative. A negative book
        // value alone (common for profitable companies that have funded buybacks with debt)
        // should not force a NAV/P-B valuation, since both those methods return 0 whenever
        // BookValuePerShare <= 0 — that would zero out a genuinely profitable business.
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.NetProfitCr <= 0;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Negative net income and cash flow; falling back to asset-based liquidation floor."
        };
    }

    public class CyclicalEarningsRule : IValuationRule
    {
        public int Priority => RulePriority.CyclicalEarnings;
        public bool IsMatch(EvaluationContext ctx) => ctx.IsCyclical;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Normalized Mid-Cycle P/E",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "High earnings volatility detected in non-utility entity; using cycle-normalized metrics to prevent peak/trough errors."
        };
    }

    public class HighLeverageCapitalIntensiveRule : IValuationRule
    {
        public int Priority => RulePriority.HighLeverageCapitalIntensive;
        public bool IsMatch(EvaluationContext ctx) => ctx.DebtToEbit >= 2.5m || ctx.CapexToOcf >= 0.60m || ctx.IsInfrastructureUtility;
        public ValuationMethodology Result(EvaluationContext ctx)
        {
            // FIX: the leverage/capex signals that route a company here say nothing about
            // whether it currently has usable free cash flow — a capital-intensive company
            // can be genuinely mid-build (or at a cyclical trough) with negative FCF. Picking
            // "Exit Multiple DCF" unconditionally in that case just returns 0 downstream.
            // Degrade gracefully to a multiple that's still computable.
            if (ctx.CanComputeCashFlowDcf)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Exit Multiple DCF (FCFF)",
                    SecondaryMethod = "EV/EBITDA Relative Multiple",
                    Rationale = "Capital-intensive balance sheet or high leverage utility/infrastructure profile; evaluating Enterprise Value (FCFF) models."
                };
            }

            if (ctx.Data.EbitCr > 0m)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "EV/EBITDA Relative Multiple",
                    SecondaryMethod = "Price-to-Book (P/B)",
                    Rationale = "Capital-intensive/high-leverage profile with currently negative free cash flow (build-out phase or cyclical trough); using an EBITDA-based multiple instead of a cash-flow DCF that would return no value."
                };
            }

            return new ValuationMethodology
            {
                PrimaryMethod = "Price-to-Book (P/B)",
                SecondaryMethod = "Net Asset Value (NAV)",
                Rationale = "Capital-intensive/high-leverage profile with neither positive free cash flow nor positive EBIT currently; falling back to an asset-based floor."
            };
        }
    }

    public class MatureHighPayoutRule : IValuationRule
    {
        public int Priority => RulePriority.MatureHighPayout;
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
        public int Priority => RulePriority.AssetLightMoat;
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
        public int Priority => RulePriority.DefaultFallback;
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