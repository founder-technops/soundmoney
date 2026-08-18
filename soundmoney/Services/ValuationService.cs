using SoundMoney.Data;
using SoundMoney.Models;
using Microsoft.Extensions.Logging;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        Task<StockValuation> EvaluateDataAsync(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalData);
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

        public async Task<StockValuation> EvaluateDataAsync(
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

            // 1. Resolve strategy dynamically using cash flow, debt, and lifecycle filters
            ValuationMethodology methodology = ValuationStrategyResolver.ResolveMethodology(deepData, historicalData);

            _logger.LogInformation("Valuing {Symbol}. Dynamic Strategy Primary: {Primary}, Secondary: {Secondary} | Rationale: {Rationale}",
                valuationData.Symbol, methodology.PrimaryMethod, methodology.SecondaryMethod, methodology.Rationale);

            // 2. Compute Intrinsic Values based on dynamically selected methods
            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, deepData, historicalData);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, deepData, historicalData);

            // 3. Blend intrinsic value and compute Sound Score...
            decimal blendedIntrinsicValue = (primaryValue > 0 && secondaryValue > 0)
                ? Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2)
                : Math.Max(primaryValue, secondaryValue);

            decimal cmp = deepData.CurrentPrice;
            decimal marginOfSafety = blendedIntrinsicValue > 0 && cmp > 0
                ? Math.Round(((blendedIntrinsicValue - cmp) / cmp) * 100m, 2)
                : 0m;

            
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

            return await Task.FromResult(new StockValuation
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
            });
        }
        #region Method Execution Router

        private decimal ComputeValueByMethod(string methodName, DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            return methodName switch
            {
                // Standard & General DCF Methods
                "Discounted Cash Flow (DCF)" or "Standard DCF" or "DCF" => CalculateStandardDcf(data, historicals),
                "2-Stage Discounted Cash Flow (DCF)" => CalculateTwoStageDcf(data, historicals),
                "Exit Multiple DCF" => CalculateExitMultipleDcf(data, historicals),
                "Normalized DCF (Cycle-Adjusted)" => CalculateNormalizedDcf(data, historicals),

                // Sector Specific DCF Variants
                "Risk-Adjusted DCF (rDCF)" => CalculateRiskAdjustedDcf(data, historicals),
                "Finite Concession DCF (Project Level)" => CalculateFiniteConcessionDcf(data, historicals),

                // Asset & Capital Based Methods
                "Net Asset Value (NAV)" or "Net Asset Value (NAV via Reserve DCF)" or "Asset Replacement Value" => CalculateNavPerShare(data),
                "Regulatory Asset Base (RAB) Valuation" => CalculateRabValuation(data),
                "Price-to-Book (P/B) Intrinsic Multiples" or "Price-to-Book (P/B)" => CalculatePbIntrinsicValue(data),

                // Earnings & Dividend Models
                "Excess Returns Model" => CalculateExcessReturns(data),
                "Dividend Discount Model (DDM)" => CalculateDdm(data),
                "Gordon Growth DDM" => CalculateGordonGrowthDdm(data),
                "Buffett Owner Earnings Model" => CalculateOwnerEarnings(data, historicals),
                "Capitalized Free Cash Flow Yield" => CalculateCapFcfYield(data),

                // Fallback for unmapped or relative valuation placeholders
                _ => CalculateStandardDcf(data, historicals)
            };
        }

        #endregion

        #region Dynamic Growth & Parameter Utilities

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
            // 1. Fundamental Growth: ROE * Retention Ratio
            if (data.ReportedRoePercent > 0)
            {
                decimal roe = data.ReportedRoePercent / 100m;
                decimal payoutRatio = Math.Max(0m, Math.Min(data.DividendPayoutPercent / 100m, 1m));
                decimal retentionRatio = 1m - payoutRatio;

                decimal fundamentalGrowth = roe * retentionRatio;

                if (fundamentalGrowth > 0)
                {
                    return Math.Max(0.02m, Math.Min(fundamentalGrowth, 0.15m));
                }
            }

            // 2. Historical OCF CAGR Fallback
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
                        return Math.Max(0.02m, Math.Min(ocfCagr, 0.15m));
                    }
                }
            }

            return defaultFallback;
        }

        #endregion

        #region Valuation Algorithms

        // Standard 5-Year DCF Model
        private static decimal CalculateStandardDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal discountRate = Math.Max(0.10m, growthRate + 0.02m);
            decimal terminalRate = 0.03m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal discountFactor = 1m + discountRate;
            decimal currentDiscount = discountFactor;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / currentDiscount;
                currentDiscount *= discountFactor;
            }

            decimal denominator = discountRate - terminalRate;
            if (denominator <= 0.005m) denominator = 0.005m;

            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal equityValueCr = enterpriseValueCr + data.NetCashCr;

            return Math.Round(equityValueCr / data.TotalSharesCr, 2);
        }

        // Risk-Adjusted DCF for Pharma/Biotech (applies pipeline survival/approval probability factor)
        private static decimal CalculateRiskAdjustedDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0 ? data.FreeCashFlowCr : (data.CashFromOperationsCr - data.GrossCapexCr);
            if (fcfCr <= 0) return 0m;

            const decimal pipelineSuccessProbability = 0.35m; // Avg Phase II/III FDA success rate
            decimal baseDcfValue = CalculateStandardDcf(data, historicals);

            return Math.Round(baseDcfValue * pipelineSuccessProbability, 2);
        }

        // Infrastructure Finite Concession DCF (No Terminal Value)
        private static decimal CalculateFiniteConcessionDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0 ? data.FreeCashFlowCr : (data.CashFromOperationsCr - data.GrossCapexCr);
            if (fcfCr <= 0) return 0m;

            const int concessionYears = 15; // Typical remaining lifecycle for infrastructure assets
            decimal discountRate = 0.10m;
            decimal growthRate = 0.04m; // Moderate toll/usage inflation rate

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal currentDiscount = 1m + discountRate;

            for (int yr = 1; yr <= concessionYears; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / currentDiscount;
                currentDiscount *= (1m + discountRate);
            }

            decimal equityValueCr = cumulativePv + data.NetCashCr;
            return Math.Round(equityValueCr / data.TotalSharesCr, 2);
        }

        // Regulatory Asset Base (RAB) for Sovereign Utilities
        private static decimal CalculateRabValuation(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0) return 0m;

            const decimal allowedRoe = 0.155m; // Typical sovereign regulated ROE benchmark (~15.5%)
            const decimal costOfCapital = 0.105m;

            decimal rabMultiplier = allowedRoe / costOfCapital;
            return Math.Round(data.BookValuePerShare * rabMultiplier, 2);
        }

        private static decimal CalculateTwoStageDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal discountRate = Math.Max(0.11m, growthRate + 0.02m);
            decimal terminalRate = Math.Min(0.04m, growthRate * 0.4m);

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal discountFactor = 1m + discountRate;
            decimal currentDiscount = discountFactor;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / currentDiscount;
                currentDiscount *= discountFactor;
            }

            decimal denominator = discountRate - terminalRate;
            if (denominator <= 0.005m) denominator = 0.005m;

            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal equityValueCr = enterpriseValueCr + data.NetCashCr;

            return Math.Round(equityValueCr / data.TotalSharesCr, 2);
        }

        private static decimal CalculateExitMultipleDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0 || data.EbitCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.10m);
            decimal discountRate = Math.Max(0.11m, growthRate + 0.02m);
            const decimal evEbitMultiple = 20.0m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal projectedEbit = data.EbitCr;
            decimal discountFactor = 1m + discountRate;
            decimal currentDiscount = discountFactor;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                projectedEbit *= (1m + growthRate);
                cumulativePv += projectedFcf / currentDiscount;
                currentDiscount *= discountFactor;
            }

            decimal terminalValue = projectedEbit * evEbitMultiple;
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal equityValueCr = enterpriseValueCr + data.NetCashCr;

            return Math.Round(equityValueCr / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNormalizedDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgOcf = historicals.Average(h => h.HistoricalOcfCr);
            decimal avgCapex = historicals.Average(h => h.HistoricalCapexCr);
            decimal normalizedFcfCr = avgOcf - avgCapex;

            if (normalizedFcfCr <= 0) return 0m;

            decimal historicalCagr = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.06m);
            decimal cycleGrowth = Math.Min(historicalCagr, 0.12m);

            decimal discountRate = Math.Max(0.12m, cycleGrowth + 0.03m);
            decimal terminalRate = 0.03m;

            decimal cumulativePv = 0m;
            decimal fcf = normalizedFcfCr;
            decimal discountFactor = 1m + discountRate;
            decimal currentDiscount = discountFactor;

            for (int yr = 1; yr <= 5; yr++)
            {
                fcf *= (1m + cycleGrowth);
                cumulativePv += fcf / currentDiscount;
                currentDiscount *= discountFactor;
            }

            decimal terminalValue = (fcf * (1m + terminalRate)) / (discountRate - terminalRate);
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal equityValueCr = enterpriseValueCr + data.NetCashCr;

            return Math.Round(equityValueCr / data.TotalSharesCr, 2);
        }

        private static decimal CalculateExcessReturns(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            const decimal costOfEquity = 0.12m;
            const decimal terminalGrowth = 0.05m;
            decimal roeDecimal = data.ReportedRoePercent / 100m;

            if (roeDecimal <= costOfEquity) return Math.Round(data.BookValuePerShare, 2);

            decimal excessReturnRate = roeDecimal - costOfEquity;
            decimal excessValue = (data.BookValuePerShare * excessReturnRate) / (costOfEquity - terminalGrowth);

            return Math.Round(data.BookValuePerShare + excessValue, 2);
        }

        private static decimal CalculateDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0 || data.DividendPayoutPercent <= 0) return 0m;

            const decimal costOfEquity = 0.11m;
            const decimal dividendGrowth = 0.06m;

            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * (data.DividendPayoutPercent / 100m);
            if (d0 <= 0) return 0m;

            decimal d1 = d0 * (1m + dividendGrowth);
            return Math.Round(d1 / (costOfEquity - dividendGrowth), 2);
        }

        private static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0 || data.DividendPayoutPercent <= 0) return 0m;

            const decimal costOfEquity = 0.10m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * (data.DividendPayoutPercent / 100m);
            if (d0 <= 0) return 0m;

            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * 0.5m, 0.08m);

            if (payoutGrowth >= costOfEquity) payoutGrowth = costOfEquity - 0.01m;

            decimal d1 = d0 * (1m + payoutGrowth);
            return Math.Round(d1 / (costOfEquity - payoutGrowth), 2);
        }

        private static decimal CalculateOwnerEarnings(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal avgOcf = historicals.Any() ? historicals.Average(h => h.HistoricalOcfCr) : data.CashFromOperationsCr;
            decimal avgCapex = historicals.Any() ? historicals.Average(h => h.HistoricalCapexCr) : data.GrossCapexCr;

            decimal ownerEarningsCr = avgOcf - avgCapex;
            if (ownerEarningsCr <= 0) return 0m;

            return Math.Round((ownerEarningsCr * 15m) / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNavPerShare(DeepFinancial data)
        {
            return data.BookValuePerShare <= 0 ? 0m : Math.Round(data.BookValuePerShare, 2);
        }

        private static decimal CalculatePbIntrinsicValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            const decimal costOfEquity = 0.12m;
            const decimal growth = 0.05m;
            decimal roe = data.ReportedRoePercent / 100m;

            decimal justifiedPb = (roe - growth) / (costOfEquity - growth);
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 12.0m);

            return Math.Round(data.BookValuePerShare * justifiedPb, 2);
        }

        private static decimal CalculateCapFcfYield(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal totalValuationCr = fcfCr / 0.07m;
            return Math.Round(totalValuationCr / data.TotalSharesCr, 2);
        }

        #endregion
    }
}