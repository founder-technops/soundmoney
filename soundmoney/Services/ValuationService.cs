using SoundMoney.Data;
using SoundMoney.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Algorithms;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        StockValuation EvaluateData(StockValuation valuationData, DeepFinancial deepData, List<Financial> historicalData);
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

        public StockValuation EvaluateData(
            StockValuation valuationData,
            DeepFinancial deepData,
            List<Financial> historicalData)
        {
            historicalData ??= new List<Financial>();

            if (valuationData == null || deepData == null)
            {
                _logger.LogWarning("Cannot calculate valuation: Missing core dataset input.");
                return null;
            }

            // 1. Resolve strategy dynamically using context rules
            ValuationMethodology methodology = ValuationStrategyResolver.ResolveMethodology(deepData, historicalData);

            _logger.LogInformation("Valuing {Symbol}. Dynamic Strategy Primary: {Primary}, Secondary: {Secondary} | Rationale: {Rationale}",
                valuationData.Symbol, methodology.PrimaryMethod, methodology.SecondaryMethod, methodology.Rationale);

            // 2. Compute Intrinsic Values based on resolved methods
            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, deepData, historicalData);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, deepData, historicalData);

            // 3. Blend intrinsic values
            decimal blendedIntrinsicValue = (primaryValue > 0 && secondaryValue > 0)
                ? Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2)
                : Math.Max(primaryValue, secondaryValue);

            decimal cmp = deepData.CurrentPrice;
            decimal marginOfSafety = 0m;
            string verdict;

            if (blendedIntrinsicValue <= 0 || cmp <= 0)
            {
                verdict = "INSUFFICIENT DATA";
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

            int soundScore = FinancialAlgorithms.CalculateSoundScore(marginOfSafety, deepData, historicalData);

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

            DividendAnalysisResult dividendAnalysis = FinancialAlgorithms.CalculateDividend(deepData, historicalData);

            var result = new StockValuation
            {
                Symbol = valuationData.Symbol,
                Sector = valuationData.Sector,
                CompanyName = valuationData.CompanyName,
                PrimaryMethod = methodology.PrimaryMethod,
                SecondaryMethod = methodology.SecondaryMethod,
                CurrentPrice = cmp,
                IntrinsicValue = blendedIntrinsicValue,
                MarginOfSafety = marginOfSafety,
                DividendYieldPercent = deepData.DividendYieldPercent,
                IsDividendConsistent = dividendAnalysis.IsConsistent,
                Verdict = verdict,
                SoundScore = soundScore,
                SoundScoreRating = soundRating,
                UpdatedAt = DateTime.Now
            };

            return result;
        }

        #region Method Execution Router

        public static decimal ComputeValueByMethod(string methodName, DeepFinancial data, IEnumerable<Financial> historicals)
        {
            return methodName switch
            {
                "Excess Returns Model" => FinancialAlgorithms.CalculateExcessReturns(data),
                "Price-to-TBV (Tangible Book Value)" or "Price-to-Book (P/B)" or "Price-to-Book (P/B) Intrinsic Multiples" => FinancialAlgorithms.CalculatePbIntrinsicValue(data),

                "EV/Sales Relative Multiple" => FinancialAlgorithms.CalculateEvSalesMultiple(data),
                "Price-to-Sales (P/S)" => FinancialAlgorithms.CalculatePriceToSales(data),

                "Net Asset Value (NAV)" => FinancialAlgorithms.CalculateNavPerShare(data),
                "Normalized Mid-Cycle P/E" => FinancialAlgorithms.CalculateNormalizedPe(data, historicals),

                "Exit Multiple DCF (FCFF)" or "Exit Multiple DCF" => FinancialAlgorithms.CalculateExitMultipleDcf(data, historicals),
                "EV/EBITDA Relative Multiple" => FinancialAlgorithms.CalculateEvEbitdaMultiple(data),

                "Dividend Discount Model (DDM)" => FinancialAlgorithms.CalculateDdm(data),
                "Dividend Discount Model (Pass-Through Yield)" => FinancialAlgorithms.CalculateDdmPassThroughYield(data),
                "Gordon Growth Model" or "Gordon Growth DDM" => FinancialAlgorithms.CalculateGordonGrowthDdm(data),

                "Buffett Owner Earnings Model" => FinancialAlgorithms.CalculateOwnerEarnings(data, historicals),
                "2-Stage FCFE DCF" or "2-Stage Discounted Cash Flow (DCF)" => FinancialAlgorithms.CalculateTwoStageDcf(data, historicals),
                "Price-to-Earnings-to-Growth (PEG)" => FinancialAlgorithms.CalculatePegRatioValue(data, historicals),

                "Price-to-Earnings (P/E) Multiple" => FinancialAlgorithms.CalculatePriceToEarnings(data),
                "Discounted Cash Flow (DCF)" or "Standard DCF" => FinancialAlgorithms.CalculateStandardDcf(data, historicals),
                "Adjusted Net Asset Value (SOTP with HoldCo Discount)" => FinancialAlgorithms.CalculateHoldingCompanyValue(data),
                _ => FinancialAlgorithms.CalculateStandardDcf(data, historicals)
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
            DeepFinancial data,
            IEnumerable<Financial> historicals)
        {
            var ctx = BuildContext(data, historicals);

            return Rules
                .OrderBy(r => r.Priority)
                .First(r => r.IsMatch(ctx))
                .Result(ctx);
        }

        private static EvaluationContext BuildContext(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            var historyList = historicals?.OrderBy(h => h.Year).ToList() ?? new List<Financial>();

            decimal debtToEbit = FinancialAlgorithms.CalculateDebtToEbit(data);
            decimal capexToOcf = FinancialAlgorithms.CalculateCapexToOcf(data);
            decimal fcfCr = FinancialAlgorithms.CalculateFreeCashFlow(data);

            decimal ocfToNp = FinancialAlgorithms.CalculateOcfToNetProfit(data);
            decimal fcfToNp = FinancialAlgorithms.CalculateFcfToNetProfit(data);

            // Advanced Derived Metrics
            decimal roicPercent = FinancialAlgorithms.CalculateRoic(data);
            decimal croicPercent = FinancialAlgorithms.CalculateCroic(data);

            decimal sloanRatio = FinancialAlgorithms.CalculateSloanRatio(data);

            decimal interestCoverage = FinancialAlgorithms.CalculateInterestCoverage(data);

            decimal actualNetDebt = FinancialAlgorithms.CalculateNetDebt(data);

            decimal opmPercent = (data.SalesCr > 0m && !data.IsFinancialSector) ? data.OperatingProfitMargin : 0m;
            decimal avgHistoricalOpm = historyList.Count >= 3 ? historyList.Average(h => h.OperatingProfitMargin) : opmPercent;
            decimal marginTrend = opmPercent - avgHistoricalOpm;

            // Cash predictability incorporating FCF conversion and Sloan Ratio quality
            bool cashPredictable = fcfCr > 0 && ocfToNp >= 0.8m && fcfToNp >= 0.50m
                && sloanRatio <= 10.0m;

            int negativeOcfYears = historyList.Count(h => h.CashFromOperationsCr <= 0);
            if (negativeOcfYears > 1) cashPredictable = false;

            bool isInfraUtility = (debtToEbit >= 3.5m || capexToOcf >= 0.75m) && !data.IsFinancialSector;

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

}