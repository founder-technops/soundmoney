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
        Task<(List<ScreenerResultRow> Results, int Scanned, string Notice)> RunPotentialEntryScreenAsync(
            decimal minimumMarginOfSafety,
            string? searchQuery,
            CancellationToken cancellationToken = default);

        // ErrorMessage is non-null only when Model is null - carries the specific reason
        // scraping/valuation failed (e.g. a network error reaching Screener.in) up to the
        // controller, instead of the caller only ever seeing a bare null.
        Task<(StockDetailsViewModel? Model, string? ErrorMessage)> RunScreenDetailsAsync(string symbol, CancellationToken cancellationToken = default);
    }
    public class ScreenerService : IScreenerService
    {
        private readonly ILogger<ScreenerService> _logger;
        private readonly IScraperService _scraperService;
        private readonly IBtstTechnicalService _btstTechnicalService;
        private readonly IValuationService _valuationService;
        private readonly IFinancialRepository _repo;
        public ScreenerService(
        IFinancialRepository repo,
        IScraperService scraperService,
        IBtstTechnicalService btstTechnicalService,
        IValuationService valuationService,
        ILogger<ScreenerService> logger)
        {
            _repo = repo;
            _scraperService = scraperService;
            _btstTechnicalService = btstTechnicalService;
            _valuationService = valuationService;
            _logger = logger;
        }
        public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score)
        {
            var symbols = await _repo.GetByFilterAsync(minMarginOfSafety, searchQuery, score);
            var screenRows = symbols.Select(s => StockValuationToScreenResultRow(s)).ToList();
            return screenRows;
        }

        public async Task<(List<ScreenerResultRow> Results, int Scanned, string Notice)> RunPotentialEntryScreenAsync(
            decimal minimumMarginOfSafety,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            const int maxMarketDataCandidates = 500;
            const int fundamentalCacheDays = 90;
            var cachedEntries = await _repo.GetRecentCachedFinancialEntriesAsync(DateTime.Today.AddDays(-fundamentalCacheDays), cancellationToken);
            string? normalizedSearch = string.IsNullOrWhiteSpace(searchQuery) ? null : searchQuery.Trim();
            bool exactSymbolSearch = normalizedSearch is not null
                && cachedEntries.Any(entry => entry.Symbol.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase));

            if (normalizedSearch is not null && !exactSymbolSearch)
            {
                var cachedMatch = await _repo.GetCachedScrapeAsync(normalizedSearch, cancellationToken);
                if (cachedMatch.Current is not null && cachedMatch.Historical is not null && cachedMatch.Summary is not null)
                {
                    cachedEntries.Add(new CachedFinancialEntry(
                        normalizedSearch.ToUpperInvariant(),
                        cachedMatch.Current,
                        cachedMatch.Historical,
                        cachedMatch.Summary,
                        cachedMatch.ScrapedAt ?? DateTime.MinValue));
                    exactSymbolSearch = true;
                }
            }

            var fundamentalCandidates = cachedEntries
                .Where(entry => normalizedSearch is null
                    || (exactSymbolSearch
                        ? entry.Symbol.Equals(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                        : entry.Symbol.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                            || entry.Summary.CompanyName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(entry => BtstTechnicalService.EvaluateFundamentalChecks(entry.Current, entry.Historical).Count(check => check.Passed))
                .ThenByDescending(entry => entry.Summary.SoundScore)
                .ThenByDescending(entry => entry.Summary.MarginOfSafety)
                .Take(exactSymbolSearch ? 1 : maxMarketDataCandidates)
                .ToList();

            var screenedRows = new System.Collections.Concurrent.ConcurrentBag<ScreenerResultRow>();
            using var concurrencyLimit = new SemaphoreSlim(3, 3);
            await Task.WhenAll(fundamentalCandidates.Select(async entry =>
            {
                await concurrencyLimit.WaitAsync(cancellationToken);
                try
                {
                    var signal = await _btstTechnicalService.GetSignalAsync(
                        entry.Symbol,
                        entry.Current,
                        entry.Historical,
                        entry.Summary.IntrinsicValue,
                        minimumMarginOfSafety,
                        cancellationToken);

                    screenedRows.Add(new ScreenerResultRow
                    {
                            Symbol = entry.Symbol,
                            CompanyName = entry.Summary.CompanyName,
                            Sector = SectorClassifier.GetMacroSector(entry.Summary.Sector),
                            CurrentPrice = signal.LatestClose ?? entry.Summary.CurrentPrice,
                            IntrinsicValue = entry.Summary.IntrinsicValue,
                            SoundScore = entry.Summary.SoundScore,
                            MarginOfSafetyPercent = signal.PotentialEntryMarginOfSafety ?? 0m,
                            Verdict = entry.Summary.Verdict,
                            SoundScoreRating = entry.Summary.SoundScoreRating,
                            LastAnalyzed = entry.ScrapedAt,
                            DividendYieldPercent = entry.Summary.DividendYieldPercent,
                            IsDividendConsistent = entry.Summary.IsDividendConsistent,
                            IsPotentialEntry = signal.IsSetup == true,
                            PotentialEntryStatus = signal.IsSetup == true ? "Trend setup" : signal.IsSetup is null ? "Trend check unavailable" : "Trend gates not met",
                            EntryChecksPassed = signal.ChecksPassed,
                            EntryChecksTotal = signal.ChecksTotal,
                            TrendChecksPassed = signal.TrendChecksPassed,
                            TrendChecksTotal = signal.TrendChecksTotal,
                            PriceTrendStatus = signal.IsTechnicalSetup == true
                                ? "Uptrend confirmed"
                                : signal.TwentySessionReturnPercent < 0m
                                    ? "Downtrend — not a trend entry"
                                    : signal.IsTechnicalSetup is null
                                        ? signal.Status == "Unavailable" ? "Trend data unavailable" : "Not enough trend data"
                                        : "Trend not fully confirmed",
                            EntryChecksSummary = signal.ChecksSummary,
                            IsTechnicalDataStale = signal.DataIsStale,
                            TechnicalClose = signal.LatestClose,
                            Dma50 = signal.Dma50,
                            Dma200 = signal.Dma200,
                            PriceAboveDma50Percent = signal.PriceAboveDma50Percent,
                            TwentySessionReturnPercent = signal.TwentySessionReturnPercent,
                            FortySessionReturnPercent = signal.FortySessionReturnPercent,
                            VolumeRatio = signal.VolumeRatio,
                            TechnicalDataStatus = signal.Status,
                            PotentialEntryMarginOfSafety = signal.PotentialEntryMarginOfSafety,
                            TargetPrice30Percent = signal.TargetPrice30Percent,
                            TargetPrice40Percent = signal.TargetPrice40Percent,
                            TechnicalSessionDate = signal.SessionDate
                    });
                }
                finally
                {
                    concurrencyLimit.Release();
                }
            }));

            var results = screenedRows
                .OrderByDescending(row => row.TrendChecksPassed)
                .ThenByDescending(row => row.FortySessionReturnPercent ?? decimal.MinValue)
                .ThenByDescending(row => row.TwentySessionReturnPercent ?? decimal.MinValue)
                .ThenByDescending(row => row.PriceAboveDma50Percent ?? decimal.MinValue)
                .ThenByDescending(row => row.IsPotentialEntry)
                .ThenByDescending(row => row.SoundScore)
                .ThenByDescending(row => row.MarginOfSafetyPercent)
                .ToList();
            string notice = cachedEntries.Count == 0
                ? "No recent cached fundamental data is available yet. Analyze stocks first, then rerun this screen."
                : fundamentalCandidates.Count == 0
                    ? "No cached fundamental snapshots matched this search. Analyze the stock first, then rerun this screen."
                    : exactSymbolSearch
                        ? "Showing the requested symbol without applying fundamental-quality or margin-of-safety gates. Price-trend gates still determine its trend status; fundamentals and valuation are shown as context."
                        : $"Showing trend setups and near-miss watch candidates. Technical trend rules are the only setup gates; fundamentals, Sound Score, and margin of safety rank candidates only. Technical history refreshes every 8 hours; fundamental snapshots use SQL cache up to {fundamentalCacheDays} days old. At most {maxMarketDataCandidates} candidates are evaluated per run.";

            return (results, fundamentalCandidates.Count, notice);
        }

        public async Task<(StockDetailsViewModel? Model, string? ErrorMessage)> RunScreenDetailsAsync(string symbol, CancellationToken cancellationToken = default)
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

            // Calculate intrinsic value before combining it with the latest market close.
            var valuationResult = _valuationService.Evaluate(stockValuation, current, historical);

            // Technical BTST data is an independent, optional signal. A provider failure
            // must not prevent the fundamental valuation page from rendering.
            BtstTechnicalSignal technicalBtstSignal;
            try
            {
                technicalBtstSignal = await _btstTechnicalService.GetSignalAsync(
                    cleanSymbol,
                    current,
                    historical,
                    valuationResult.IntrinsicValue,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Combined fundamental/technical setup analysis failed for {Symbol}", cleanSymbol);
                technicalBtstSignal = new BtstTechnicalSignal
                {
                    Status = "Unavailable",
                    IsSetup = null,
                    Message = "Daily market data is unavailable; the technical BTST screen could not be evaluated.",
                    DataSource = "Yahoo Finance chart data (unofficial)"
                };
            }

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
                TechnicalBtstSignal = technicalBtstSignal,

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