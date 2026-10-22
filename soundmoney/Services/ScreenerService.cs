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

        // ErrorMessage is non-null only when Model is null - carries the specific reason
        // scraping/valuation failed (e.g. a network error reaching Screener.in) up to the
        // controller, instead of the caller only ever seeing a bare null.
        Task<(StockDetailsViewModel? Model, string? ErrorMessage)> RunScreenDetailsAsync(string symbol);
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

        public async Task<(StockDetailsViewModel? Model, string? ErrorMessage)> RunScreenDetailsAsync(string symbol)
        {
            string cleanSymbol = symbol.ToUpper();

            StockValuation? stockValuation;
            Financial? current;
            List<Financial>? historical;
            string? scrapeError = null;
            DateTime dataAsOf = DateTime.Now; // overwritten below when data actually came from cache
            bool needsCacheWrite = false; // set once fresh data is selected; written after Evaluate() below

            // Serve today's cached scrape if we have one, rather than hitting Screener.in
            // again - this is the "once per stock per day" cache. Calendar-day boundary
            // (not a rolling 24h window), matching the convention GetPendingValuationsAsync
            // already uses (UpdatedAt < DateTime.Today) elsewhere in this codebase.
            var cached = await _repo.GetCachedScrapeAsync(cleanSymbol);
            bool cacheIsFreshToday = cached.ScrapedAt.HasValue && cached.ScrapedAt.Value.Date == DateTime.Today
                && cached.Current != null && cached.Historical != null && cached.Summary != null;

            if (cacheIsFreshToday)
            {
                stockValuation = cached.Summary;
                current = cached.Current;
                historical = cached.Historical;
                dataAsOf = cached.ScrapedAt!.Value;
            }
            else
            {
                var (freshValuation, freshCurrent, freshHistorical, freshError) =
                    await _scraperService.ScrapeStockAsync(cleanSymbol);

                if (freshValuation != null && freshCurrent != null && freshHistorical != null)
                {
                    stockValuation = freshValuation;
                    current = freshCurrent;
                    historical = freshHistorical;
                    dataAsOf = DateTime.Now;

                    // Deliberately NOT cached here yet: freshValuation is the raw scrape
                    // result, which only ever has Symbol/CompanyName/Sector/CurrentPrice
                    // populated (see ScraperService.ScrapeStockAsync) - IntrinsicValue/
                    // MarginOfSafety/Verdict/SoundScore/etc. are all still at their C#
                    // defaults (0/""/false) at this point. The actual cache write happens
                    // below, once valuationResult (the Evaluate() output) exists, so the
                    // persisted StockValuationJson reflects real computed numbers instead
                    // of a half-populated stub - the same mistake this is avoiding was
                    // originally made (and then fixed) in AutomationService's own cache
                    // write for exactly this reason.
                    needsCacheWrite = true;
                }
                else if (cached.Current != null && cached.Historical != null && cached.Summary != null)
                {
                    // Live scrape failed (e.g. the transient network timeouts Screener.in
                    // sometimes returns) but we have a stale cache from an earlier day -
                    // better to show slightly-stale data than fail the page outright.
                    stockValuation = cached.Summary;
                    current = cached.Current;
                    historical = cached.Historical;
                    dataAsOf = cached.ScrapedAt!.Value;
                    _logger.LogWarning("Live scrape failed for {Symbol}, serving cache from {ScrapedAt}: {Error}", cleanSymbol, cached.ScrapedAt, freshError);
                }
                else
                {
                    stockValuation = null;
                    current = null;
                    historical = null;
                    scrapeError = freshError;
                }
            }

            if (stockValuation is null || current is null || historical is null)
            {
                return (null, scrapeError ?? $"Could not retrieve or analyze data for symbol '{symbol}'.");
            }

            // Calculate intrinsic value & score rating
            var valuationResult = _valuationService.Evaluate(stockValuation, current, historical);

            if (needsCacheWrite)
            {
                // Fire-and-forget would risk a failed write going unnoticed and unretried;
                // awaiting it keeps this simple and correct at the cost of a little request
                // latency on cache-miss days - acceptable since this only happens once per
                // symbol per day instead of every page view. Caches valuationResult (the
                // fully-evaluated StockValuation), not the raw scrape stub - see the
                // comment where needsCacheWrite is set, above.
                await _repo.SaveScrapedCacheAsync(cleanSymbol, valuationResult, current, historical);
            }

            DividendAnalysisResult dividendAnalysis = FinancialAlgorithms.CalculateDividend(current, historical);

            // Prepare historical data for trend analysis
            var historicalList = historical.OrderBy(h => h.Year).ToList();

            // Calculate yearly metrics for trend display
            var revenueByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.SalesCr, isPercentageMetric: false);
            var profitByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.NetProfitCr, isPercentageMetric: false);
            var roeByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateRoe(f), isPercentageMetric: true);
            var roceByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateRoce(f), isPercentageMetric: true);
            var debtToEquityByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => FinancialAlgorithms.CalculateDebtToEquity(f), isPercentageMetric: false);
            var fcfByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.FreeCashFlowCr, isPercentageMetric: false);
            // Promoter/FII/DII Holding move far more slowly year-to-year than ROE/ROCE, so
            // they need a much tighter Up/Down threshold than those metrics' default 5
            // points - otherwise a real, worth-flagging change reads as "Flat" (see
            // FinancialAlgorithms.CalculateYearlyMetrics).
            var promoterHoldingByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.PromoterHoldingPercent, isPercentageMetric: true, upDownThresholdPercent: 0.5m);
            var fiiHoldingByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.FiiHoldingPercent, isPercentageMetric: true, upDownThresholdPercent: 0.5m);
            var diiHoldingByYear = FinancialAlgorithms.CalculateYearlyMetrics(current, historicalList, f => f.DiiHoldingPercent, isPercentageMetric: true, upDownThresholdPercent: 0.5m);

            // Determine trend directions
            string revenueTrend = FinancialAlgorithms.DetermineTrendDirection(revenueByYear);
            string profitTrend = FinancialAlgorithms.DetermineTrendDirection(profitByYear);
            string roeTrend = FinancialAlgorithms.DetermineTrendDirection(roeByYear);
            string roceTrend = FinancialAlgorithms.DetermineTrendDirection(roceByYear);
            string debtTrend = FinancialAlgorithms.DetermineTrendDirection(debtToEquityByYear) == "Improving" ? "Declining" : FinancialAlgorithms.DetermineTrendDirection(debtToEquityByYear) == "Declining" ? "Improving" : "Stable";
            string cashFlowTrend = FinancialAlgorithms.DetermineTrendDirection(fcfByYear);
            string promoterHoldingTrend = FinancialAlgorithms.DetermineTrendDirection(promoterHoldingByYear);
            string fiiHoldingTrend = FinancialAlgorithms.DetermineTrendDirection(fiiHoldingByYear);
            string diiHoldingTrend = FinancialAlgorithms.DetermineTrendDirection(diiHoldingByYear);

            var soundScoreDetails = valuationResult.Verdict == "INSUFFICIENT DATA"
                ? new SoundScoreResult(0, 0, Array.Empty<string>())
                : FinancialAlgorithms.CalculateSoundScoreDetailed(valuationResult.MarginOfSafety, current, historicalList);

            // Map scraped metrics to Details ViewModel
            var model = new StockDetailsViewModel
            {
                // 1. Basic Stock Information
                Symbol = stockValuation.Symbol,
                CompanyName = stockValuation.CompanyName,
                Sector = stockValuation.Sector,
                CurrentPrice = stockValuation.CurrentPrice,
                LastAnalyzed = dataAsOf,
                IsFinancialSector = current.IsFinancialSector,

                // 2. Core Valuation Output
                IntrinsicValue = valuationResult.IntrinsicValue,
                MarginOfSafetyPercent = valuationResult.MarginOfSafety,
                Verdict = valuationResult.Verdict,
                SoundScoreRating = valuationResult.SoundScoreRating,
                SoundScore = (int)valuationResult.SoundScore,
                SoundScoreRaw = soundScoreDetails.RawScore,
                ScoreCapReasons = soundScoreDetails.CapReasons.ToList(),
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
                FiiHoldingPercent = current.FiiHoldingPercent,
                FiiHoldingTrend = fiiHoldingTrend,
                DiiHoldingPercent = current.DiiHoldingPercent,
                DiiHoldingTrend = diiHoldingTrend,
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
                FiiHoldingByYear = fiiHoldingByYear,
                DiiHoldingByYear = diiHoldingByYear,

                // 6. Trend Summary Indicators
                RevenueTrend = revenueTrend,
                ProfitTrend = profitTrend,
                RoeTrend = roeTrend,
                RoceTrend = roceTrend,
                DebtTrend = debtTrend,
                CashFlowTrend = cashFlowTrend
            };
            return (model, null);
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