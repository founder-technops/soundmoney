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

        public async Task<StockValuation> EvaluateDataAsync(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalData)
        {
            // Guarantees safe operations downstream if historicals are missing
            historicalData ??= new List<HistoricalFinancial>();

            if (valuationData == null || deepData == null)
            {
                _logger.LogWarning("Cannot calculate valuation: Missing core dataset input.");
                return null;
            }

            SectorCategory sector = SectorMapper.Map(valuationData.Sector);

            ValuationMethodology methodology = sector == SectorCategory.Biotech
                ? IntrinsicMapper.ResolveBiotechMethod(deepData.RevenueCr, deepData.NetProfitCr)
                : IntrinsicMapper.GetValuationMethodology(sector);

            _logger.LogInformation("Valuing {Symbol} under Sector [{Sector}]. Primary: {Primary}, Secondary: {Secondary}",
                valuationData.Symbol, sector, methodology.PrimaryMethod, methodology.SecondaryMethod);

            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, deepData, historicalData);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, deepData, historicalData);

            // Calculate Blended Intrinsic Value with safe fallback checking
            decimal blendedIntrinsicValue = (primaryValue > 0 && secondaryValue > 0)
                ? Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2)
                : Math.Max(primaryValue, secondaryValue);

            decimal cmp = deepData.CurrentPrice;

            // Derive margin and verdict cleanly
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
                UpdatedAt = DateTime.UtcNow
            });
        }

        #region Method Execution Router

        private decimal ComputeValueByMethod(string methodName, DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            return methodName switch
            {
                "Excess Returns Model" => CalculateExcessReturns(data),
                "Dividend Discount Model (DDM)" => CalculateDdm(data),
                "Gordon Growth DDM" => CalculateGordonGrowthDdm(data),
                "Buffett Owner Earnings Model" => CalculateOwnerEarnings(data, historicals),
                "2-Stage Discounted Cash Flow (DCF)" => CalculateTwoStageDcf(data),
                "Normalized DCF (Cycle-Adjusted)" => CalculateNormalizedDcf(data, historicals),
                "Net Asset Value (NAV)" => CalculateNavPerShare(data),
                "Price-to-Book (P/B) Intrinsic Multiples" or "Price-to-Book (P/B)" => CalculatePbIntrinsicValue(data),
                "Capitalized Free Cash Flow Yield" => CalculateCapFcfYield(data),
                _ => CalculateTwoStageDcf(data)
            };
        }

        #endregion

        #region Valuation Algorithms

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
            if (data.DividendYieldPercent <= 0 || data.CurrentPrice <= 0) return 0m;

            const decimal costOfEquity = 0.11m;
            const decimal dividendGrowth = 0.06m;

            decimal d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            decimal d1 = d0 * (1m + dividendGrowth);

            return Math.Round(d1 / (costOfEquity - dividendGrowth), 2);
        }

        private static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.DividendYieldPercent <= 0 || data.CurrentPrice <= 0) return 0m;

            const decimal costOfEquity = 0.10m;
            decimal d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * 0.5m, 0.08m);

            if (payoutGrowth >= costOfEquity) payoutGrowth = 0.09m; // Guard against division by zero/negative

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

        private static decimal CalculateTwoStageDcf(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            const decimal discountRate = 0.11m;
            const decimal growthRate = 0.10m;
            const decimal terminalRate = 0.04m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;
            decimal discountFactor = 1m + discountRate;
            decimal currentDiscount = discountFactor;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / currentDiscount;
                currentDiscount *= discountFactor; // Avoids double-precision loss from Math.Pow
            }

            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / (discountRate - terminalRate);
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            return Math.Round((cumulativePv + pvTerminal) / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNormalizedDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgOcf = historicals.Average(h => h.HistoricalOcfCr);
            decimal avgCapex = historicals.Average(h => h.HistoricalCapexCr);
            decimal normalizedFcfCr = avgOcf - avgCapex;

            if (normalizedFcfCr <= 0) return 0m;

            const decimal discountRate = 0.12m;
            const decimal cycleGrowth = 0.05m;

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

            decimal terminalValue = (fcf * 1.03m) / (discountRate - 0.03m);
            decimal pvTerminal = terminalValue / Math.Max(currentDiscount / discountFactor, 1m);

            return Math.Round((cumulativePv + pvTerminal) / data.TotalSharesCr, 2);
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
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 4.0m);

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