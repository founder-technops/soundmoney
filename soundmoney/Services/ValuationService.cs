using SoundMoney.Data;
using SoundMoney.Models;
using Microsoft.Extensions.Logging;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        StockValuation EvaluateData(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalData);
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
            List<HistoricalFinancial> historicalData)
        {
            historicalData ??= new List<HistoricalFinancial>();

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

            _logger.LogInformation("Completed valuation for {Symbol}. Intrinsic: {IV}, Verdict: {Verdict}",
                valuationData.Symbol, blendedIntrinsicValue, verdict);

            int soundScore = SoundScoreCalculator.CalculateSoundScore(marginOfSafety, deepData, historicalData);

            string soundRating = soundScore switch
            {
                >= 80 => "STRONG SOUND",
                >= 60 => "SOUND",
                >= 40 => "NEUTRAL",
                _ => "UNSOUND"
            };

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
                Verdict = verdict,
                SoundScore = soundScore,
                SoundScoreRating = soundRating,
                UpdatedAt = DateTime.Now
            };

            return result;
        }

        #region Method Execution Router

        private decimal ComputeValueByMethod(string methodName, DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            return methodName switch
            {
                // Mapped precisely to strategy resolver output strings
                "Excess Returns Model" => CalculateExcessReturns(data),
                "Price-to-TBV (Tangible Book Value)" or "Price-to-Book (P/B)" or "Price-to-Book (P/B) Intrinsic Multiples" => CalculatePbIntrinsicValue(data),

                "EV/Sales Relative Multiple" => CalculateEvSalesMultiple(data),
                "Price-to-Sales (P/S)" => CalculatePriceToSales(data),

                "Net Asset Value (NAV)" => CalculateNavPerShare(data),
                "Normalized Mid-Cycle P/E" => CalculateNormalizedPe(data, historicals),

                "Exit Multiple DCF (FCFF)" or "Exit Multiple DCF" => CalculateExitMultipleDcf(data, historicals),
                "EV/EBITDA Relative Multiple" => CalculateEvEbitdaMultiple(data),

                "Dividend Discount Model (DDM)" => CalculateDdm(data),
                "Gordon Growth Model" or "Gordon Growth DDM" => CalculateGordonGrowthDdm(data),

                "Buffett Owner Earnings Model" => CalculateOwnerEarnings(data, historicals),
                "2-Stage FCFE DCF" or "2-Stage Discounted Cash Flow (DCF)" => CalculateTwoStageDcf(data, historicals),
                "Price-to-Earnings-to-Growth (PEG)" => CalculatePegRatioValue(data, historicals),

                "Price-to-Earnings (P/E) Multiple" => CalculatePriceToEarnings(data),

                // Core DCF Fallbacks
                "Discounted Cash Flow (DCF)" or "Standard DCF" => CalculateStandardDcf(data, historicals),
                _ => CalculateStandardDcf(data, historicals)
            };
        }

        #endregion

        #region WACC & Dynamic Utilities

        /// <summary>
        /// Calculates Weighted Average Cost of Capital (WACC) dynamically.
        /// WACC = (E/V * Cost of Equity) + (D/V * Cost of Debt * (1 - Tax Rate))
        /// </summary>
        private static decimal CalculateWacc(DeepFinancial data, decimal riskFreeRate = 0.07m, decimal equityRiskPremium = 0.055m, decimal taxRate = 0.25m)
        {
            decimal equityValueCr = data.CurrentPrice * data.TotalSharesCr;
            decimal debtValueCr = Math.Max(0m, data.TotalBorrowingsCr);
            decimal totalCapitalCr = equityValueCr + debtValueCr;

            // Guardrail check against 0 capital base
            if (totalCapitalCr <= 0m) return 0.11m;

            decimal beta = data.Beta > 0 ? Math.Clamp(data.Beta, 0.5m, 2.5m) : 1.0m;
            decimal costOfEquity = riskFreeRate + (beta * equityRiskPremium);

            decimal costOfDebt = 0.08m;
            if (debtValueCr > 0 && data.InterestExpenseCr > 0)
            {
                costOfDebt = Math.Clamp(data.InterestExpenseCr / debtValueCr, 0.04m, 0.18m);
            }

            decimal weightEquity = equityValueCr / totalCapitalCr;
            decimal weightDebt = debtValueCr / totalCapitalCr;

            decimal wacc = (weightEquity * costOfEquity) + (weightDebt * costOfDebt * (1m - taxRate));
            return Math.Clamp(wacc, 0.085m, 0.18m);
        }

        private static decimal CalculateCagr(decimal initialValue, decimal finalValue, int periods)
        {
            if (initialValue <= 0 || finalValue <= 0 || periods <= 0)
                return 0m;

            double ratio = (double)(finalValue / initialValue);
            double cagr = Math.Pow(ratio, 1.0 / periods) - 1.0;

            return (decimal)cagr;
        }

        private static decimal ResolveDynamicGrowthRate(
            DeepFinancial data,
            IEnumerable<HistoricalFinancial> historicals,
            decimal defaultFallback = 0.08m)
        {
            if (data.ReportedRoePercent > 0)
            {
                decimal roe = data.ReportedRoePercent / 100m;
                decimal payoutRatio = Math.Max(0m, Math.Min(data.DividendPayoutPercent / 100m, 1m));
                decimal retentionRatio = 1m - payoutRatio;

                decimal fundamentalGrowth = roe * retentionRatio;

                if (fundamentalGrowth > 0)
                {
                    return Math.Clamp(fundamentalGrowth, 0.02m, 0.15m);
                }
            }

            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            if (historyList != null && historyList.Count >= 3)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (oldest.HistoricalOcfCr > 0 && newest.HistoricalOcfCr > 0)
                {
                    decimal ocfCagr = CalculateCagr(oldest.HistoricalOcfCr, newest.HistoricalOcfCr, periods);
                    if (ocfCagr > 0)
                    {
                        return Math.Clamp(ocfCagr, 0.02m, 0.15m);
                    }
                }
            }

            return defaultFallback;
        }

        #endregion

        #region Valuation Algorithms

        private static decimal CalculateStandardDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal discountRate = CalculateWacc(data);

            decimal terminalRate = 0.03m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = data.TotalBorrowingsCr - data.CashAndEquivalentsCr;
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateTwoStageDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal stage1Growth = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.10m);
            decimal stage2Growth = stage1Growth * 0.5m; // Decay stage
            decimal discountRate = CalculateWacc(data);
            decimal terminalRate = Math.Min(0.03m, stage2Growth);

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            // Stage 1 (Years 1-5)
            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + stage1Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            // Stage 2 (Years 6-10)
            for (int yr = 6; yr <= 10; yr++)
            {
                projectedFcf *= (1m + stage2Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 10);

            decimal equityValueCr = cumulativePv + pvTerminal;
            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateExitMultipleDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0 || data.EbitCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.10m);
            decimal discountRate = CalculateWacc(data);
            const decimal evEbitMultiple = 15.0m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal projectedEbit = data.EbitCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                projectedEbit *= (1m + growthRate);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal terminalValue = projectedEbit * evEbitMultiple;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 5);

            decimal netDebtCr = data.TotalBorrowingsCr - data.CashAndEquivalentsCr;
            decimal equityValueCr = (cumulativePv + pvTerminal) - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateExcessReturns(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            // Cost of Equity via WACC/CAPM principle
            decimal costOfEquity = CalculateWacc(data);
            const decimal terminalGrowth = 0.04m;
            decimal roeDecimal = data.ReportedRoePercent / 100m;

            if (roeDecimal <= costOfEquity) return Math.Round(data.BookValuePerShare, 2);

            decimal denominator = Math.Max(0.005m, costOfEquity - terminalGrowth);
            decimal excessReturnRate = roeDecimal - costOfEquity;
            decimal excessValue = (data.BookValuePerShare * excessReturnRate) / denominator;

            return Math.Round(data.BookValuePerShare + excessValue, 2);
        }

        private static decimal CalculateDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal dividendGrowth = 0.05m;

            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal denominator = Math.Max(0.005m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        private static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * (1m - payoutRatio), 0.06m);
            if (payoutGrowth >= costOfEquity) payoutGrowth = costOfEquity - 0.01m;

            decimal denominator = Math.Max(0.005m, costOfEquity - payoutGrowth);
            decimal d1 = d0 * (1m + payoutGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        private static decimal CalculateOwnerEarnings(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal avgOcf = historicals != null && historicals.Any() ? historicals.Average(h => h.HistoricalOcfCr) : data.CashFromOperationsCr;
            decimal avgCapex = historicals != null && historicals.Any() ? historicals.Average(h => h.HistoricalCapexCr) : data.GrossCapexCr;

            decimal ownerEarningsCr = avgOcf - avgCapex;
            if (ownerEarningsCr <= 0) return 0m;

            return Math.Round((ownerEarningsCr * 12m) / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNormalizedPe(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgNetProfitCr = historicals.Average(h => h.HistoricalNetProfitCr);
            if (avgNetProfitCr <= 0) return 0m;

            decimal normalizedEps = avgNetProfitCr / data.TotalSharesCr;
            const decimal targetPe = 15.0m;

            return Math.Round(normalizedEps * targetPe, 2);
        }

        private static decimal CalculatePegRatioValue(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, 0.10m) * 100m; // as percentage

            if (eps <= 0 || growthRate <= 0) return 0m;

            // Fair value PEG = 1.0 => Target P/E = Growth Rate
            decimal fairPe = Math.Clamp(growthRate, 4.0m, 30.0m);
            return Math.Round(eps * fairPe, 2);
        }

        private static decimal CalculateEvSalesMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.CashFromOperationsCr <= 0) return 0m;

            decimal targetEvSales = 2.5m;
            decimal RevenueCr = data.RevenueCr;
            decimal netDebtCr = data.TotalBorrowingsCr - data.CashAndEquivalentsCr;

            decimal targetEquityValueCr = (RevenueCr * targetEvSales) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculatePriceToSales(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.CashFromOperationsCr <= 0) return 0m;

            decimal estimatedEps = (data.CashFromOperationsCr * 0.15m) / data.TotalSharesCr;
            return Math.Round(estimatedEps * 12.0m, 2);
        }

        private static decimal CalculateEvEbitdaMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.EbitCr <= 0) return 0m;

            decimal estimatedEbitdaCr = data.EbitCr * 1.2m;
            const decimal targetEvEbitda = 10.0m;
            decimal netDebtCr = data.TotalBorrowingsCr - data.CashAndEquivalentsCr;

            decimal targetEquityValueCr = (estimatedEbitdaCr * targetEvEbitda) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculatePriceToEarnings(DeepFinancial data)
        {
            if (data.NetProfitCr <= 0 || data.TotalSharesCr <= 0) return 0m;

            decimal eps = data.NetProfitCr / data.TotalSharesCr;
            const decimal fairPe = 15.0m;

            return Math.Round(eps * fairPe, 2);
        }

        private static decimal CalculateNavPerShare(DeepFinancial data)
        {
            return data.BookValuePerShare <= 0 ? 0m : Math.Round(data.BookValuePerShare, 2);
        }

        private static decimal CalculatePbIntrinsicValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal growth = 0.05m;
            decimal roe = data.ReportedRoePercent / 100m;

            decimal denominator = Math.Max(0.005m, costOfEquity - growth);
            decimal justifiedPb = (roe - growth) / denominator;
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 12.0m);

            return Math.Round(data.BookValuePerShare * justifiedPb, 2);
        }

        #endregion
    }
}