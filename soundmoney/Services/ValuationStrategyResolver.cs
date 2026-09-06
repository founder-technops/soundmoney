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

        // Advanced Capital & Earnings Quality Metrics
        public decimal RoicPercent { get; set; }
        public decimal CroicPercent { get; set; }
        public decimal FcfToNetProfit { get; set; }
        public decimal SloanRatio { get; set; }
        public decimal InterestCoverage { get; set; }
        public decimal CashConversionCycleDays { get; set; }
        public decimal MarginTrend { get; set; }

        public bool IsCashPredictable { get; set; }
        public bool IsCyclical { get; set; }
        public bool IsInfrastructureUtility { get; set; }

        public bool CanComputeCashFlowDcf => FcfCr > 0m && Data.EbitCr > 0m && SloanRatio <= 12m;
    }

    internal static class RulePriority
    {
        public const int CoreInvestmentCompany = 8;
        public const int FinancialSector = 10;
        public const int ReinvestingGrowth = 20;
        public const int DistressTurnaround = 30;
        public const int HighLeverageCapitalIntensive = 40;
        public const int CyclicalEarnings = 50;
        public const int MatureHighPayout = 60;
        public const int AssetLightMoat = 70;
        public const int PoorCashConversionOrAccrual = 85;
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
            new CoreInvestmentCompanyRule(),
            new FinancialSectorRule(),
            new ReinvestingGrowthRule(),
            new DistressTurnaroundRule(),
            new HighLeverageCapitalIntensiveRule(),
            new CyclicalEarningsRule(),
            new MatureHighPayoutRule(),
            new AssetLightMoatRule(),
            new PoorCashConversionOrAccrualRule(),
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

            decimal actualNetDebt = data.IsCashEstimateReliable
                ? data.TotalBorrowingsCr - data.CashAndEquivalentsCr
                : data.TotalBorrowingsCr;

            decimal debtToEbit = (actualNetDebt > 0 && data.EbitCr > 0) ? (actualNetDebt / data.EbitCr) : 0m;
            decimal capexToOcf = data.CashFromOperationsCr > 0 ? (data.GrossCapexCr / data.CashFromOperationsCr) : 1.0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            decimal ocfToNp = data.NetProfitCr > 0 ? (data.CashFromOperationsCr / data.NetProfitCr) : 0m;
            decimal fcfToNp = data.NetProfitCr > 0 ? (fcfCr / data.NetProfitCr) : 0m;

            // Advanced Derived Metrics
            decimal roicPercent = data.RoicPercent;
            decimal croicPercent = (data.InvestmentsCr > 0m && !data.IsFinancialSector)
                ? (fcfCr / data.InvestmentsCr) * 100m
                : 0m;

            decimal sloanRatio = (data.TotalAssetsCr > 0m && !data.IsFinancialSector)
                ? ((data.NetProfitCr - data.CashFromOperationsCr) / data.TotalAssetsCr) * 100m
                : 0m;

            decimal interestCoverage = (data.TotalBorrowingsCr > 0m && data.InterestExpenseCr > 0m && !data.IsFinancialSector)
                ? (data.EbitCr / data.InterestExpenseCr)
                : (data.TotalBorrowingsCr <= 0m ? 999m : 0m);

            decimal opmPercent = (data.SalesCr > 0m && !data.IsFinancialSector) ? data.OperatingProfitMargin : 0m;
            decimal avgHistoricalOpm = historyList.Count >= 3 ? historyList.Average(h => h.HistoricalOpmPercent) : opmPercent;
            decimal marginTrend = opmPercent - avgHistoricalOpm;

            // Cash predictability incorporating FCF conversion and Sloan Ratio quality
            bool cashPredictable = fcfCr > 0
                && ocfToNp >= 0.8m
                && fcfToNp >= 0.50m
                && sloanRatio <= 10.0m;

            int negativeOcfYears = historyList.Count(h => h.HistoricalOcfCr <= 0);
            if (negativeOcfYears > 1) cashPredictable = false;

            bool isInfraUtility = (debtToEbit >= 3.5m || capexToOcf >= 0.75m) && !data.IsFinancialSector;

            bool cyclical = false;
            if (historyList.Count >= 3 && !isInfraUtility)
            {
                int trendReversals = 0;
                for (int i = 1; i < historyList.Count - 1; i++)
                {
                    decimal prevChange = historyList[i].HistoricalNetProfitCr - historyList[i - 1].HistoricalNetProfitCr;
                    decimal nextChange = historyList[i + 1].HistoricalNetProfitCr - historyList[i].HistoricalNetProfitCr;

                    if ((prevChange > 0m && nextChange < 0m) || (prevChange < 0m && nextChange > 0m))
                    {
                        trendReversals++;
                    }
                }

                decimal minProfit = historyList.Min(h => h.HistoricalNetProfitCr);
                if (minProfit <= 0m || trendReversals >= 2)
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
                RoicPercent = roicPercent,
                CroicPercent = croicPercent,
                FcfToNetProfit = fcfToNp,
                SloanRatio = sloanRatio,
                InterestCoverage = interestCoverage,
                CashConversionCycleDays = data.CashConversionCycleDays,
                MarginTrend = marginTrend,
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
            Rationale = "Financial institution: Operational inventory is capital; requiring equity residual income models."
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
        public bool IsMatch(EvaluationContext ctx) =>
            ctx.Data.NetProfitCr <= 0 || (ctx.InterestCoverage < 1.8m && !ctx.Data.IsFinancialSector);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Severe earnings distress or interest coverage strain (< 1.8x); falling back to asset liquidation floor."
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
            Rationale = "High earnings volatility detected; using mid-cycle normalized metrics to avoid peak/trough valuation errors."
        };
    }

    public class HighLeverageCapitalIntensiveRule : IValuationRule
    {
        public int Priority => RulePriority.HighLeverageCapitalIntensive;
        public bool IsMatch(EvaluationContext ctx) => ctx.DebtToEbit >= 2.5m || ctx.CapexToOcf >= 0.60m || ctx.IsInfrastructureUtility;
        public ValuationMethodology Result(EvaluationContext ctx)
        {
            if (ctx.CanComputeCashFlowDcf && ctx.CroicPercent >= 8.0m)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Exit Multiple DCF (FCFF)",
                    SecondaryMethod = "EV/EBITDA Relative Multiple",
                    Rationale = "Capital-intensive profile with adequate Cash Return on Invested Capital (CROIC >= 8%); using Enterprise FCFF DCF."
                };
            }

            if (ctx.Data.EbitCr > 0m)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "EV/EBITDA Relative Multiple",
                    SecondaryMethod = "Price-to-Book (P/B)",
                    Rationale = "Capital-intensive/high-leverage profile with low cash return efficiency; using EV/EBITDA multiple."
                };
            }

            return new ValuationMethodology
            {
                PrimaryMethod = "Price-to-Book (P/B)",
                SecondaryMethod = "Net Asset Value (NAV)",
                Rationale = "High leverage asset-heavy profile lacking positive cash flow; falling back to asset-based floor."
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
            Rationale = "Mature entity distributing over 40% of earnings with predictable free cash flow support."
        };
    }

    public class AssetLightMoatRule : IValuationRule
    {
        public int Priority => RulePriority.AssetLightMoat;
        public bool IsMatch(EvaluationContext ctx) =>
            ctx.IsCashPredictable && (ctx.RoicPercent >= 15.0m || ctx.CroicPercent >= 12.0m);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = (ctx.Data.ReportedRoePercent >= 20m && ctx.MarginTrend >= 0m)
                ? "Buffett Owner Earnings Model"
                : "2-Stage FCFE DCF",
            SecondaryMethod = "Price-to-Earnings-to-Growth (PEG)",
            Rationale = "High ROIC/CROIC moat confirmed with predictable FCF; suitable for equity-level discounted cash flow modeling."
        };
    }

    public class PoorCashConversionOrAccrualRule : IValuationRule
    {
        public int Priority => RulePriority.PoorCashConversionOrAccrual;
        public bool IsMatch(EvaluationContext ctx) =>
            !ctx.Data.IsFinancialSector
            && ctx.Data.NetProfitCr > 0m
            && (ctx.OcfToNetProfit < 0.30m || ctx.FcfToNetProfit < 0.20m || ctx.SloanRatio > 12.0m);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "High accrual risk (Sloan Ratio > 12%) or severe paper profits (FCF conversion < 20%). Overriding cash/earnings multiples with asset floor."
        };
    }

    public class CoreInvestmentCompanyRule : IValuationRule
    {
        public int Priority => RulePriority.CoreInvestmentCompany;
        public bool IsMatch(EvaluationContext ctx) => ctx.Data.IsFinancialSector && ctx.Data.IsCoreInvestmentCompany;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Adjusted Net Asset Value (SOTP with HoldCo Discount)",
            SecondaryMethod = "Dividend Discount Model (Pass-Through Yield)",
            Rationale = "Core Investment / Holding Company detected: Applying standard 50% Holding Company discount to NAV."
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