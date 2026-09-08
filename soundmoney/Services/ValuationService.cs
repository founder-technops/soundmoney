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
            decimal blendedIntrinsicValue = (primaryValue > 0 && secondaryValue > 0)
                ? Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2)
                : Math.Max(primaryValue, secondaryValue);

            decimal cmp = current.CurrentPrice;
            decimal marginOfSafety = 0m;
            string verdict;

            if (blendedIntrinsicValue <= 0 || cmp <= 0)
            {
                verdict = "INSUFFICIENT current";
            }
            else
            {
                marginOfSafety = Math.Round(((blendedIntrinsicValue - cmp) / cmp) * 100m, 2);
                verdict = cmp switch
                {
                    var p when p <= blendedIntrinsicValue * 0.70m => "STRONG BUY",
                    var p when p <= blendedIntrinsicValue => "BUY",
                    var p when p <= blendedIntrinsicValue * 1.20m => "HOLD",
                    _ => "OVERVALUED"
                };
            }

            int soundScore = FinancialAlgorithms.CalculateSoundScore(marginOfSafety, current, historical);

            if (verdict == "INSUFFICIENT current")
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

            var result = new StockValuation
            {
                Symbol = valuationdata.Symbol,
                Sector = valuationdata.Sector,
                CompanyName = valuationdata.CompanyName,
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
            return methodName switch
            {
                "Excess Returns Model" => FinancialAlgorithms.CalculateExcessReturns(current),
                "Price-to-TBV (Tangible Book Value)" or "Price-to-Book (P/B)" or "Price-to-Book (P/B) Intrinsic Multiples" => FinancialAlgorithms.CalculatePbIntrinsicValue(current),

                "EV/Sales Relative Multiple" => FinancialAlgorithms.CalculateEvSalesMultiple(current),
                "Price-to-Sales (P/S)" => FinancialAlgorithms.CalculatePriceToSales(current),

                "Net Asset Value (NAV)" => FinancialAlgorithms.CalculateNavPerShare(current),
                "Normalized Mid-Cycle P/E" => FinancialAlgorithms.CalculateNormalizedPe(current, historicals),

                "Exit Multiple DCF (FCFF)" or "Exit Multiple DCF" => FinancialAlgorithms.CalculateExitMultipleDcf(current, historicals),
                "EV/EBITDA Relative Multiple" => FinancialAlgorithms.CalculateEvEbitdaMultiple(current),

                "Dividend Discount Model (DDM)" => FinancialAlgorithms.CalculateDdm(current),
                "Dividend Discount Model (Pass-Through Yield)" => FinancialAlgorithms.CalculateDdmPassThroughYield(current),
                "Gordon Growth Model" or "Gordon Growth DDM" => FinancialAlgorithms.CalculateGordonGrowthDdm(current),

                "Buffett Owner Earnings Model" => FinancialAlgorithms.CalculateOwnerEarnings(current, historicals),
                "2-Stage FCFE DCF" or "2-Stage Discounted Cash Flow (DCF)" => FinancialAlgorithms.CalculateTwoStageDcf(current, historicals),
                "Price-to-Earnings-to-Growth (PEG)" => FinancialAlgorithms.CalculatePegRatioValue(current, historicals),

                "Price-to-Earnings (P/E) Multiple" => FinancialAlgorithms.CalculatePriceToEarnings(current),
                "Discounted Cash Flow (DCF)" or "Standard DCF" => FinancialAlgorithms.CalculateStandardDcf(current, historicals),
                "Adjusted Net Asset Value (SOTP with HoldCo Discount)" => FinancialAlgorithms.CalculateHoldingCompanyValue(current),
                _ => FinancialAlgorithms.CalculateStandardDcf(current, historicals)
            };
        }

        #endregion

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
            Financial current,
            IEnumerable<Financial> historicals)
        {
            var ctx = BuildContext(current, historicals);

            return Rules
                .OrderBy(r => r.Priority)
                .First(r => r.IsMatch(ctx))
                .Result(ctx);
        }

        private static EvaluationContext BuildContext(Financial current, IEnumerable<Financial> historicals)
        {
            var historyList = historicals?.OrderBy(h => h.Year).ToList() ?? new List<Financial>();

            decimal debtToEbit = FinancialAlgorithms.CalculateDebtToEbit(current);
            decimal capexToOcf = FinancialAlgorithms.CalculateCapexToOcf(current);
            decimal fcfCr = FinancialAlgorithms.CalculateFreeCashFlow(current);

            decimal ocfToNp = FinancialAlgorithms.CalculateOcfToNetProfit(current);
            decimal fcfToNp = FinancialAlgorithms.CalculateFcfToNetProfit(current);

            // Advanced Derived Metrics
            decimal roicPercent = FinancialAlgorithms.CalculateRoic(current);
            decimal croicPercent = FinancialAlgorithms.CalculateCroic(current);

            decimal sloanRatio = FinancialAlgorithms.CalculateSloanRatio(current);

            decimal interestCoverage = FinancialAlgorithms.CalculateInterestCoverage(current);

            decimal actualNetDebt = FinancialAlgorithms.CalculateNetDebt(current);

            decimal opmPercent = (current.SalesCr > 0m && !current.IsFinancialSector) ? FinancialAlgorithms.CalculateOperatingProfitMargin(current) : 0m;
            decimal avgHistoricalOpm = historyList.Count >= 3 ? historyList.Average(h => FinancialAlgorithms.CalculateOperatingProfitMargin(h)): opmPercent;
            decimal marginTrend = opmPercent - avgHistoricalOpm;

            // Cash predictability incorporating FCF conversion and Sloan Ratio quality
            bool cashPredictable = FinancialAlgorithms.CheckCashPredictable(current);

            int negativeOcfYears = historyList.Count(h => h.CashFromOperationsCr <= 0);
            if (negativeOcfYears > 1) cashPredictable = false;

            bool isInfraUtility = (debtToEbit >= 3.5m || capexToOcf >= 0.75m) && !current.IsFinancialSector;

            bool cyclical = false;
            if (historyList.Count >= 3 && !isInfraUtility)
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

                decimal minProfit = historyList.Min(h => h.NetProfitCr);
                if (minProfit <= 0m || trendReversals >= 2)
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