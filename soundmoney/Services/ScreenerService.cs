using SoundMoney.Algorithms;
using SoundMoney.Data;
using SoundMoney.Models;
using System.Net.Http;
using System.Threading;

namespace SoundMoney.Services
{
    public interface IScreenerService
    {
        Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score);
        Task<StockDetailsViewModel> RunScreenDetailsAsync(string symbol);
    }
    public class ScreenerService : IScreenerService
    {
        private readonly ILogger<ScreenerService> _logger;
        private readonly IScraperService _scraperService;
        private readonly IValuationService _valuationService;
        private readonly IFinancialRepository _repo;
        public ScreenerService(
        IFinancialRepository repo,
        IScraperService scraperService,
        IValuationService valuationService,
        ILogger<ScreenerService> logger)
        {
            _repo = repo;
            _scraperService = scraperService;
            _valuationService = valuationService;
            _logger = logger;
        }
        public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score)
        {
            var symbols = await _repo.GetByFilterAsync(minMarginOfSafety, searchQuery, score);
            var screenRows = symbols.Select(s => StockValuationToScreenResultRow(s)).ToList();
            return screenRows;
        }

        public async Task<StockDetailsViewModel> RunScreenDetailsAsync(string symbol)
        {
            // Scrape live financial records
            var (stockValuation, current, historical) =
                await _scraperService.ScrapeStockAsync(symbol.ToUpper());

            if (stockValuation is null || current is null || historical is null)
            {
                return null;
            }

            // Calculate intrinsic value & score rating
            var valuationResult = _valuationService.Evaluate(stockValuation, current, historical);

            DividendAnalysisResult dividendAnalysis = FinancialAlgorithms.CalculateDividend(current, historical);

            // Same inputs ValuationService used for the score, re-run only to get the
            // breakdown (raw score + why it was capped). Skipped when there's no valuation.
            var scoreDetail = valuationResult.Verdict == "INSUFFICIENT DATA"
                ? null
                : FinancialAlgorithms.CalculateSoundScoreDetailed(valuationResult.MarginOfSafety, current, historical);

            // Prepare historical data for trend analysis
            var historicalList = historical.OrderBy(h => h.Year).ToList();

            // Calculate yearly metrics for trend display
            var revenueByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.SalesCr, isPercentageMetric: false);
            var profitByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.NetProfitCr, isPercentageMetric: false);
            var roeByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateRoe(f), isPercentageMetric: true);
            var roceByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateRoce(f), isPercentageMetric: true);
            var debtToEquityByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateDebtToEquity(f), isPercentageMetric: false);
            var fcfByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.FreeCashFlowCr, isPercentageMetric: false);
            // Promoter Holding moves far more slowly year-to-year than ROE/ROCE, so it
            // needs a much tighter Up/Down threshold than those metrics' default 5 points -
            // otherwise a real, worth-flagging decline reads as "Flat" (see FinancialAlgorithms.CalculateYearlyMetrics).
            var promoterHoldingByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.PromoterHoldingPercent, isPercentageMetric: true, upDownThresholdPercent: 0.5m);

            // Determine trend directions
            string revenueTrend = FinancialAlgorithms.DetermineTrendDirection(revenueByYear);
            string profitTrend = FinancialAlgorithms.DetermineTrendDirection(profitByYear);
            string roeTrend = FinancialAlgorithms.DetermineTrendDirection(roeByYear);
            string roceTrend = FinancialAlgorithms.DetermineTrendDirection(roceByYear);
            string debtTrend = FinancialAlgorithms.DetermineTrendDirection(debtToEquityByYear) == "Improving" ? "Declining" : FinancialAlgorithms.DetermineTrendDirection(debtToEquityByYear) == "Declining" ? "Improving" : "Stable";
            string cashFlowTrend = FinancialAlgorithms.DetermineTrendDirection(fcfByYear);
            string promoterHoldingTrend = FinancialAlgorithms.DetermineTrendDirection(promoterHoldingByYear);

            // Map scraped metrics to Details ViewModel
            var model = new StockDetailsViewModel
            {
                // 1. Basic Stock Information
                Symbol = stockValuation.Symbol,
                CompanyName = stockValuation.CompanyName,
                Sector = stockValuation.Sector,
                CurrentPrice = stockValuation.CurrentPrice,
                LastAnalyzed = DateTime.Now,
                IsFinancialSector = current.IsFinancialSector,

                // 2. Core Valuation Output
                IntrinsicValue = valuationResult.IntrinsicValue,
                MarginOfSafetyPercent = valuationResult.MarginOfSafety,
                Verdict = valuationResult.Verdict,
                SoundScoreRating = valuationResult.SoundScoreRating,
                SoundScore = (int)valuationResult.SoundScore,
                SoundScoreRaw = scoreDetail?.RawScore ?? (int)valuationResult.SoundScore,
                ScoreCapReasons = scoreDetail?.CapReasons.ToList() ?? new List<string>(),
                PrimaryMethod = valuationResult.PrimaryMethod,
                SecondaryMethod = valuationResult.SecondaryMethod,
                MarketCapCr = current.MarketCapCr,
                Eps = current.Eps,
                BookValuePerShareAmount = FinancialAlgorithms.CalculateBookValuePerShare(current),
                FaceValue = FinancialAlgorithms.CalculateFaceValue(current),
                Beta = current.Beta,
                CashConversionCycleDays = current.CashConversionCycleDays,
                PromoterHoldingPercent = current.PromoterHoldingPercent,
                PromoterHoldingTrend = promoterHoldingTrend,
                PromoterPledgePercent = FinancialAlgorithms.NormalizePromoterPledgePercent(current),

                // 3. Deep Financial Indicators
                PE = FinancialAlgorithms.CalculatePeRatio(current),
                PB = FinancialAlgorithms.CalculatePbRatio(current),
                EvToEbitda = FinancialAlgorithms.CalculateEvToEbitda(current),
                PegRatio = FinancialAlgorithms.CalculatePegRatio(current, historicalList),
                ROEPercent = FinancialAlgorithms.CalculateRoe(current),
                ROCEPercent = FinancialAlgorithms.CalculateRoce(current),
                ROICPercent = FinancialAlgorithms.CalculateRoic(current),
                NetProfitMarginPercent = FinancialAlgorithms.CalculateNetProfitMarginPercent(current),
                OperatingProfitMarginPercent = FinancialAlgorithms.CalculateOperatingProfitMargin(current),
                SloanRatio = FinancialAlgorithms.CalculateSloanRatio(current),
                DebtToEquity = FinancialAlgorithms.CalculateDebtToEquity(current),
                DebtToEbitda = FinancialAlgorithms.CalculateDebtToEbitda(current),
                InterestCoverageRatio = FinancialAlgorithms.CalculateInterestCoverage(current),
                CurrentRatio = FinancialAlgorithms.CalculateCurrentRatio(current),
                FreeCashFlowCr = current.FreeCashFlowCr,
                FcfConversionPercent = FinancialAlgorithms.CalculateFcfToNetProfit(current) * 100m,
                DividendYieldPercent = current.DividendYieldPercent,
                DividendPayoutPercent = current.DividendPayoutPercent,
                IsDividendConsistent = dividendAnalysis.IsConsistent,

                // Health & Solvency Risk Scores
                AltmanZScore = FinancialAlgorithms.CalculateAltmanZScore(current),
                PiotroskiFScore = FinancialAlgorithms.CalculatePiotroskiFScore(current, historicalList),
                MScore = FinancialAlgorithms.CalculateBeneishMScore(current, historicalList),

                // 4. Historical Trends (Simple CAGR)
                RevenueCagr3Yr = FinancialAlgorithms.CalculateCagrPercent(current, historicalList, 3, f => f.SalesCr),
                RevenueCagr5Yr = FinancialAlgorithms.CalculateCagrPercent(current, historicalList, 5, f => f.SalesCr),
                ProfitCagr3Yr = FinancialAlgorithms.CalculateCagrPercent(current, historicalList, 3, f => f.NetProfitCr),
                ProfitCagr5Yr = FinancialAlgorithms.CalculateCagrPercent(current, historicalList, 5, f => f.NetProfitCr),
                AverageRoe3Yr = FinancialAlgorithms.CalculateAverageRoePercent(current, historicalList, 3),
                AverageRoe5Yr = FinancialAlgorithms.CalculateAverageRoePercent(current, historicalList, 5),
                ConsecutiveDividendYears = dividendAnalysis.ConsecutiveYearsPaid,

                // 5. Detailed Trend Analysis (Year-by-Year)
                RevenueByYear = revenueByYear,
                ProfitByYear = profitByYear,
                RoeByYear = roeByYear,
                RoceByYear = roceByYear,
                DebtToEquityByYear = debtToEquityByYear,
                FreeCashFlowByYear = fcfByYear,
                PromoterHoldingByYear = promoterHoldingByYear,

                // 6. Trend Summary Indicators
                RevenueTrend = revenueTrend,
                ProfitTrend = profitTrend,
                RoeTrend = roeTrend,
                RoceTrend = roceTrend,
                DebtTrend = debtTrend,
                CashFlowTrend = cashFlowTrend
            };
            return model;
        }



        /// <summary>
        /// Convert ScreenerResultRow to StockValuation for database storage.
        /// </summary>
        private static ScreenerResultRow StockValuationToScreenResultRow(StockValuation value)
        {
            return new ScreenerResultRow
            {
                Symbol = value.Symbol,
                CompanyName = value.CompanyName,
                Sector = SectorClassifier.GetMacroSector(value.Sector),
                CurrentPrice = value.CurrentPrice,
                IntrinsicValue = value.IntrinsicValue,
                DividendYieldPercent = value.DividendYieldPercent,
                IsDividendConsistent = value.IsDividendConsistent,
                MarginOfSafetyPercent = value.MarginOfSafety,
                Verdict = value.Verdict,
                SoundScoreRating = value.SoundScoreRating,
                LastAnalyzed = value.UpdatedAt
            };
        }
    }
}