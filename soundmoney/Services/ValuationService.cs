using SoundMoney.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Algorithms;
using SoundMoney.Data;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        StockValuation Evaluate(StockValuation valuationdata, Financial current, List<Financial> historical);
    }

    public class ValuationService : IValuationService
    {
        private readonly IFinancialRepository _repository;
        private readonly ILogger<ValuationService> _logger;

        public ValuationService(IFinancialRepository repository, ILogger<ValuationService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public StockValuation Evaluate(
            StockValuation valuationdata,
            Financial current,
            List<Financial> historical)
        {
            historical ??= new List<Financial>();

            if (valuationdata == null || current == null)
            {
                _logger.LogWarning("Cannot calculate valuation: Missing core currentset input.");
                return null;
            }

            // 1. Resolve strategy dynamically using context rules
            ValuationMethodology methodology = ValuationStrategyResolver.ResolveMethodology(current, historical);

            _logger.LogInformation("Valuing {Symbol}. Dynamic Strategy Primary: {Primary}, Secondary: {Secondary} | Rationale: {Rationale}",
                valuationdata.Symbol, methodology.PrimaryMethod, methodology.SecondaryMethod, methodology.Rationale);

            // 2. Compute Intrinsic Values based on resolved methods
            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, current, historical);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, current, historical);

            // 3. Blend intrinsic values
            List<decimal> validValues = new List<decimal> { primaryValue, secondaryValue }
                                        .Where(v => v > 0)
                                        .ToList();

            decimal blendedIntrinsicValue = validValues.Count switch
            {
                2 => Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2),
                1 => validValues[0],
                _ => 0m
            };

            decimal cmp = current.CurrentPrice;
            decimal marginOfSafety = 0m;
            string verdict;

            if (blendedIntrinsicValue <= 0 || cmp <= 0)
            {
                verdict = "INSUFFICIENT DATA";
            }
            else
            {
                // FIX 1: Consistent Margin of Safety formula using Intrinsic Value denominator across all cases
                marginOfSafety = Math.Round(((blendedIntrinsicValue - cmp) / blendedIntrinsicValue) * 100m, 2);

                verdict = cmp switch
                {
                    var p when p <= blendedIntrinsicValue * 0.70m => "STRONG BUY",
                    var p when p <= blendedIntrinsicValue => "BUY",
                    var p when p <= blendedIntrinsicValue * 1.20m => "HOLD",
                    _ => "OVERVALUED"
                };
            }

            int soundScore = FinancialAlgorithms.CalculateSoundScore(marginOfSafety, current, historical);

            if (verdict == "INSUFFICIENT DATA")
            {
                soundScore = 0;
            }

            string soundRating = soundScore switch
            {
                >= 80 => "STRONG SOUND",
                >= 60 => "SOUND",
                >= 40 => "NEUTRAL",
                _ => "UNSOUND"
            };

            DividendAnalysisResult dividendAnalysis = FinancialAlgorithms.CalculateDividend(current, historical);

            // FIX 2: UTC Timestamp normalization and default fallback
            DateTime fetchedAt = valuationdata.FetchedAt != default
                ? valuationdata.FetchedAt
                : DateTime.Now;

            var result = new StockValuation
            {
                Symbol = valuationdata.Symbol,
                Sector = valuationdata.Sector,
                CompanyName = valuationdata.CompanyName,
                FetchedAt = fetchedAt,
                PrimaryMethod = methodology.PrimaryMethod,
                SecondaryMethod = methodology.SecondaryMethod,
                CurrentPrice = cmp,
                IntrinsicValue = blendedIntrinsicValue,
                MarginOfSafety = marginOfSafety,
                DividendYieldPercent = current.DividendYieldPercent,
                IsDividendConsistent = dividendAnalysis.IsConsistent,
                Verdict = verdict,
                SoundScore = soundScore,
                SoundScoreRating = soundRating,
                UpdatedAt = DateTime.Now
            };

            return result;
        }

        #region Method Execution Router

        public static decimal ComputeValueByMethod(string methodName, Financial current, IEnumerable<Financial> historicals)
        {
            if (!MethodNamesExtensions.TryParse(methodName, out var resolvedMethod))
            {
                return FallbackExecution(methodName, current, historicals);
            }

            return resolvedMethod switch
            {
                MethodNames.ExcessReturns => FinancialAlgorithms.CalculateExcessReturns(current),
                MethodNames.PriceToTangibleBookValue => FinancialAlgorithms.CalculatePbIntrinsicValue(current),
                MethodNames.EvSalesRelativeMultiple => FinancialAlgorithms.CalculateEvSalesMultiple(current),
                MethodNames.PriceToSales => FinancialAlgorithms.CalculatePriceToSales(current),
                MethodNames.NetAssetValue => FinancialAlgorithms.CalculateNavPerShare(current),
                MethodNames.NormalizedMidCyclePe => FinancialAlgorithms.CalculateNormalizedPe(current, historicals),
                MethodNames.ExitMultipleDcf => FinancialAlgorithms.CalculateExitMultipleDcf(current, historicals),
                MethodNames.EvEbitdaRelativeMultiple => FinancialAlgorithms.CalculateEvEbitdaMultiple(current),
                MethodNames.DividendDiscountModel => FinancialAlgorithms.CalculateDdm(current),
                MethodNames.DividendDiscountModelPassThrough => FinancialAlgorithms.CalculateDdmPassThroughYield(current),
                MethodNames.GordonGrowthModel => FinancialAlgorithms.CalculateGordonGrowthDdm(current),
                MethodNames.BuffettOwnerEarnings => FinancialAlgorithms.CalculateOwnerEarnings(current, historicals),
                MethodNames.TwoStageFcfeDcf => FinancialAlgorithms.CalculateTwoStageDcf(current, historicals),
                MethodNames.PriceToEarningsToGrowth => FinancialAlgorithms.CalculatePegRatioValue(current, historicals),
                MethodNames.PriceToEarnings => FinancialAlgorithms.CalculatePriceToEarnings(current),
                MethodNames.DiscountedCashFlowDcf => FinancialAlgorithms.CalculateStandardDcf(current, historicals),
                MethodNames.AdjustedNetAssetValue => FinancialAlgorithms.CalculateHoldingCompanyValue(current),
                MethodNames.DefaultFallback => FinancialAlgorithms.CalculatePriceToEarnings(current),
                _ => FallbackExecution(methodName, current, historicals)
            };
        }

        private static decimal FallbackExecution(string methodName, Financial current, IEnumerable<Financial> historicals)
        {
            System.Diagnostics.Trace.WriteLine($"[Warning] Unrecognized method name '{methodName}'. Falling back to Standard DCF.");
            return FinancialAlgorithms.CalculateStandardDcf(current, historicals);
        }

        #endregion
    }

    public static class ValuationStrategyResolver
    {
        private static readonly List<IValuationRule> Rules = new List<IValuationRule>
        {
            new CoreInvestmentCompanyRule(),
            new WealthManagementAndAMCRule(), // Added Priority 10 rule for Asset-Light Wealth/Broking
            new FinancialSectorRule(),
            new ReinvestingGrowthRule(),
            new DistressTurnaroundRule(),
            new HighLeverageCapitalIntensiveRule(),
            new CyclicalEarningsRule(),
            new MatureHighPayoutRule(),
            new AssetLightMoatRule(),
            new PoorCashConversionOrAccrualRule(),
            new DefaultFallbackRule()
        }.OrderBy(r => r.Priority).ToList();

        public static ValuationMethodology ResolveMethodology(
            Financial current,
            IEnumerable<Financial> historicals)
        {
            if (current == null)
                throw new ArgumentNullException(nameof(current));

            var ctx = BuildContext(current, historicals);

            foreach (var rule in Rules)
            {
                bool isMatch;
                try
                {
                    isMatch = rule.IsMatch(ctx);
                }
                catch
                {
                    continue;
                }

                if (!isMatch) continue;

                try
                {
                    return rule.Result(ctx);
                }
                catch
                {
                    continue;
                }
            }

            return new DefaultFallbackRule().Result(ctx);
        }

        private static EvaluationContext BuildContext(Financial current, IEnumerable<Financial> historicals)
        {
            var historyList = historicals?.OrderBy(h => h.Year).ToList() ?? new List<Financial>();

            decimal debtToEbit = FinancialAlgorithms.CalculateDebtToEbit(current);
            decimal capexToOcf = FinancialAlgorithms.CalculateCapexToOcf(current);
            decimal fcfCr = FinancialAlgorithms.CalculateFreeCashFlow(current);

            decimal ocfToNp = FinancialAlgorithms.CalculateOcfToNetProfit(current);
            decimal fcfToNp = FinancialAlgorithms.CalculateFcfToNetProfit(current);

            decimal roicPercent = FinancialAlgorithms.CalculateRoic(current);
            decimal croicPercent = FinancialAlgorithms.CalculateCroic(current);
            decimal sloanRatio = FinancialAlgorithms.CalculateSloanRatio(current);
            decimal interestCoverage = FinancialAlgorithms.CalculateInterestCoverage(current);
            decimal actualNetDebt = FinancialAlgorithms.CalculateNetDebt(current);

            decimal opmPercent = (current.SalesCr > 0m && !current.IsFinancialSector) ? FinancialAlgorithms.CalculateOperatingProfitMargin(current) : 0m;
            decimal avgHistoricalOpm = historyList.Count >= 3 ? historyList.Average(h => FinancialAlgorithms.CalculateOperatingProfitMargin(h)) : opmPercent;
            decimal marginTrend = (current.SalesCr > 0m && !current.IsFinancialSector)
                    ? opmPercent - avgHistoricalOpm
                    : 0m;

            bool cashPredictable = FinancialAlgorithms.CheckCashPredictable(current);
            int negativeOcfYears = historyList.Count(h => h.CashFromOperationsCr <= 0);
            if (negativeOcfYears > 1) cashPredictable = false;

            // A single unusually heavy capex/investment year (e.g. building a new
            // facility) can depress the CURRENT year's FCF/NetProfit and OCF/NetProfit
            // ratios well below CheckCashPredictable's bar even for a company with an
            // excellent, consistent multi-year cash-conversion record - the single-year
            // check has no way to distinguish "one big capex year" from "this business's
            // cash quality is genuinely deteriorating". When the trailing multi-year
            // average clears a stronger bar than the single-year check requires, trust
            // the track record over the one-off year rather than penalizing quality
            // businesses for investing in growth. This only ever turns cashPredictable ON
            // when the single-year check turned it off - it never weakens the check for a
            // company that's genuinely losing cash-conversion quality.
            if (!cashPredictable && historyList.Count >= 3)
            {
                var recentProfitableYears = historyList
                    .TakeLast(5)
                    .Where(h => h.NetProfitCr > 0m)
                    .ToList();

                if (recentProfitableYears.Count >= 3)
                {
                    decimal avgFcfToNp = recentProfitableYears.Average(h => h.FreeCashFlowCr / h.NetProfitCr);
                    decimal avgOcfToNp = recentProfitableYears.Average(h => h.CashFromOperationsCr / h.NetProfitCr);

                    if (avgFcfToNp >= 0.60m && avgOcfToNp >= 0.80m)
                    {
                        cashPredictable = true;
                    }
                }
            }

            bool isInfraUtility = (debtToEbit >= 3.5m || capexToOcf >= 0.75m) && !current.IsFinancialSector;

            // FIX 3: Sector-aware cyclicality classification
            string sector = current.Sector ?? string.Empty;
            bool isKnownCyclicalSector = sector.Equals("Heavy Electrical Equipment", StringComparison.OrdinalIgnoreCase)
                                     || sector.Equals("Capital Goods", StringComparison.OrdinalIgnoreCase)
                                     || sector.Equals("Industrial Machinery", StringComparison.OrdinalIgnoreCase)
                                     || sector.Equals("Metals & Mining", StringComparison.OrdinalIgnoreCase);

            bool cyclical = isKnownCyclicalSector;
            if (!cyclical && historyList.Count >= 3 && !isInfraUtility)
            {
                int trendReversals = 0;
                for (int i = 1; i < historyList.Count - 1; i++)
                {
                    decimal prevChange = historyList[i].NetProfitCr - historyList[i - 1].NetProfitCr;
                    decimal nextChange = historyList[i + 1].NetProfitCr - historyList[i].NetProfitCr;

                    if ((prevChange > 0m && nextChange < 0m) || (prevChange < 0m && nextChange > 0m))
                    {
                        trendReversals++;
                    }
                }

                int comparisonPoints = historyList.Count - 2;
                decimal reversalRate = comparisonPoints > 0 ? (decimal)trendReversals / comparisonPoints : 0m;
                bool hasRecurringReversals = trendReversals >= 2 && reversalRate >= 0.35m;

                // Strict "< 0" rather than "<= 0": Screener's whole-Crore display rounds
                // a small, young company's tiny-but-genuinely-positive early-year profits
                // down to "0" in the P&L table (e.g. 3B Blackbio's FY2015/16 EPS of 0.36
                // and 0.51 - real profit, just small). If the scraper stores that rounded
                // "0" literally, using <= 0 here means a company that was NEVER actually
                // loss-making gets counted as having 2+ "loss years" purely from display
                // rounding, which is enough to misfire this exact check and flag it
                // cyclical. A true loss (strictly negative) is a real signal; a profit
                // that merely rounds to zero on a whole-Crore display is not.
                int lossYears = historyList.Count(h => h.NetProfitCr < 0m);

                // A company that lost money during an early growth/scale-up phase (very
                // common for recently-IPO'd tech/D2C/e-commerce businesses - Nykaa, for
                // instance, was loss-making for years post-IPO before turning sustainably
                // profitable) is not "cyclical" in the commodity boom-bust sense just
                // because lossYears >= 2. That's a one-way structural maturation, not a
                // recurring pattern - a genuine cyclical (steel, cement, sugar) keeps
                // swinging through its recent history too, it doesn't settle into a
                // multi-year stretch of consistent, growing profit. If the most recent 3
                // years are all solidly profitable and non-declining, treat that as having
                // matured past the early losses rather than flagging cyclical from
                // lossYears alone.
                bool recentlyStabilized = false;
                if (lossYears >= 2 && historyList.Count >= 3)
                {
                    var mostRecentYears = historyList.TakeLast(3).ToList();
                    recentlyStabilized = mostRecentYears.Count == 3
                        && mostRecentYears.All(h => h.NetProfitCr > 0m)
                        && mostRecentYears[2].NetProfitCr >= mostRecentYears[0].NetProfitCr;
                }

                if ((lossYears >= 2 && !recentlyStabilized) || hasRecurringReversals)
                {
                    cyclical = true;
                }
            }

            return new EvaluationContext
            {
                Current = current,
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
                CashConversionCycleDays = current.CashConversionCycleDays,
                MarginTrend = marginTrend,
                IsCashPredictable = cashPredictable,
                IsCyclical = cyclical,
                IsInfrastructureUtility = isInfraUtility
            };
        }
    }
}