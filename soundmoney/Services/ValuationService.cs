using SoundMoney.Data;
using SoundMoney.Models;
using Microsoft.Extensions.Logging;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        Task<StockValuation> EvaluateDataAsync(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalFinancial);
    }

    public class ValuationService : IValuationService
    {
        private readonly IFinancialRepository _repository;
        private readonly ILogger<ValuationService> _logger;

        public ValuationService(IFinancialRepository repository, ILogger<ValuationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<StockValuation> EvaluateDataAsync(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalData)
        {
            // 1. Fetch raw underlying data
            if (valuationData== null || deepData == null)
            {
                _logger.LogWarning("Cannot calculate valuation: Missing DeepFinancial data");
                return null;
            }

            // 2. Map industry text to SectorCategory (Assumes DeepFinancial has Industry property or fallback)
            SectorCategory sector = SectorMapper.Map(valuationData.Sector);

            // Resolve exact methodology
            ValuationMethodology methodology;
            if (sector == SectorCategory.Biotech)
            {
                methodology = IntrinsicMapper.ResolveBiotechMethod(deepData.RevenueCr, deepData.NetProfitCr);
            }
            else
            {
                methodology = IntrinsicMapper.GetValuationMethodology(sector);
            }

            _logger.LogInformation("Valuing {Symbol} under Sector [{Sector}]. Primary: {Primary}, Secondary: {Secondary}",
                valuationData.Symbol, sector, methodology.PrimaryMethod, methodology.SecondaryMethod);

            // 3. Compute Primary and Secondary Intrinsic Values
            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, deepData, historicalData);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, deepData, historicalData);

            // Blended Intrinsic Value (60/40 Weighting)
            decimal blendedIntrinsicValue;
            if (primaryValue > 0 && secondaryValue > 0)
                blendedIntrinsicValue = Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2);
            else
                blendedIntrinsicValue = Math.Max(primaryValue, secondaryValue);

            // 4. Calculate Margin of Safety and Verdict
            decimal cmp = deepData.CurrentPrice;
            decimal marginOfSafety = cmp > 0 ? Math.Round(((blendedIntrinsicValue - cmp) / cmp) * 100m, 2) : 0m;

            string verdict = cmp switch
            {
                var price when price <= 0 => "NO DATA",
                var price when price <= blendedIntrinsicValue * 0.70m => "STRONG BUY",
                var price when price <= blendedIntrinsicValue => "BUY",
                var price when price <= blendedIntrinsicValue * 1.20m => "HOLD",
                _ => "OVERVALUED"
            };

            _logger.LogInformation("Saved valuation for {Symbol}. Intrinsic Value: {IV}, Verdict: {Verdict}",
               valuationData.Symbol, blendedIntrinsicValue, verdict);
            // 5. Construct Valuation Record
            return new StockValuation
            {
                Symbol = valuationData.Symbol,
                CurrentPrice = cmp,
                IntrinsicValue = blendedIntrinsicValue,
                MarginOfSafety = marginOfSafety,
                Verdict = verdict,
                UpdatedAt = DateTime.Now
            };
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
                "2-Stage Discounted Cash Flow (DCF)" => CalculateTwoStageDcf(data, historicals),
                "Normalized DCF (Cycle-Adjusted)" => CalculateNormalizedDcf(data, historicals),
                "Net Asset Value (NAV)" => CalculateNavPerShare(data),
                "Price-to-Book (P/B) Intrinsic Multiples" or "Price-to-Book (P/B)" => CalculatePbIntrinsicValue(data),
                "Capitalized Free Cash Flow Yield" => CalculateCapFcfYield(data, historicals),
                _ => CalculateTwoStageDcf(data, historicals) // Fallback default
            };
        }

        #endregion

        #region Valuation Algorithms

        private static decimal CalculateExcessReturns(DeepFinancial data)
        {
            // Intrinsic Value = BVPS + PV(Excess Returns)
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = 0.12m; // 12% Cost of Equity
            decimal roeDecimal = data.ReportedRoePercent / 100m;

            if (roeDecimal <= costOfEquity) return Math.Round(data.BookValuePerShare, 2);

            decimal excessReturnRate = roeDecimal - costOfEquity;
            decimal terminalGrowth = 0.05m; // 5% long-term growth

            decimal excessValue = (data.BookValuePerShare * excessReturnRate) / (costOfEquity - terminalGrowth);
            return Math.Round(data.BookValuePerShare + excessValue, 2);
        }

        private static decimal CalculateDdm(DeepFinancial data)
        {
            if (data.DividendYieldPercent <= 0 || data.CurrentPrice <= 0) return 0m;

            decimal d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            decimal costOfEquity = 0.11m;
            decimal dividendGrowth = 0.06m;

            decimal d1 = d0 * (1 + dividendGrowth);
            return Math.Round(d1 / (costOfEquity - dividendGrowth), 2);
        }

        private static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.DividendYieldPercent <= 0 || data.CurrentPrice <= 0) return 0m;

            decimal d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            decimal costOfEquity = 0.10m;
            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * 0.5m, 0.08m); // Capped at 8%

            decimal d1 = d0 * (1 + payoutGrowth);
            return Math.Round(d1 / (costOfEquity - payoutGrowth), 2);
        }

        private static decimal CalculateOwnerEarnings(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal avgOcf = historicals.Any() ? historicals.Average(h => h.HistoricalOcfCr) : data.CashFromOperationsCr;
            decimal avgCapex = historicals.Any() ? historicals.Average(h => h.HistoricalCapexCr) : data.GrossCapexCr;

            decimal ownerEarningsCr = avgOcf - avgCapex;
            if (ownerEarningsCr <= 0) return 0m;

            // Apply 15x Owner Earnings multiple
            decimal totalCapCr = ownerEarningsCr * 15m;
            return Math.Round(totalCapCr / data.TotalSharesCr, 2);
        }

        private static decimal CalculateTwoStageDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal discountRate = 0.11m;
            decimal growthRate = 0.10m;
            decimal terminalRate = 0.04m;
            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1 + growthRate);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1 + discountRate), yr);
            }

            decimal terminalValue = (projectedFcf * (1 + terminalRate)) / (discountRate - terminalRate);
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1 + discountRate), 5);

            decimal totalEquityCr = cumulativePv + pvTerminal;
            return Math.Round(totalEquityCr / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNormalizedDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0 || !historicals.Any()) return 0m;

            decimal avgOcf = historicals.Average(h => h.HistoricalOcfCr);
            decimal avgCapex = historicals.Average(h => h.HistoricalCapexCr);
            decimal normalizedFcfCr = avgOcf - avgCapex;

            if (normalizedFcfCr <= 0) return 0m;

            decimal discountRate = 0.12m;
            decimal cycleGrowth = 0.05m;
            decimal cumulativePv = 0m;
            decimal fcf = normalizedFcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                fcf *= (1 + cycleGrowth);
                cumulativePv += fcf / (decimal)Math.Pow((double)(1 + discountRate), yr);
            }

            decimal terminalValue = (fcf * 1.03m) / (discountRate - 0.03m);
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1 + discountRate), 5);

            return Math.Round((cumulativePv + pvTerminal) / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNavPerShare(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0) return 0m;
            return Math.Round(data.BookValuePerShare, 2);
        }

        private static decimal CalculatePbIntrinsicValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = 0.12m;
            decimal growth = 0.05m;
            decimal roe = data.ReportedRoePercent / 100m;

            decimal justifiedPb = (roe - growth) / (costOfEquity - growth);
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 4.0m);

            return Math.Round(data.BookValuePerShare * justifiedPb, 2);
        }

        private static decimal CalculateCapFcfYield(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr > 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            // Capitalize Free Cash Flow at a target 7% Yield
            decimal totalValuationCr = fcfCr / 0.07m;
            return Math.Round(totalValuationCr / data.TotalSharesCr, 2);
        }

        #endregion
    }
}