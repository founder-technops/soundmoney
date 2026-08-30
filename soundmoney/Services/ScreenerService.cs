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
    public class ScreenerService :IScreenerService
    {
        private readonly IValuationRepository _valuationRepo;
        private readonly ILogger<ScreenerService> _logger;
        private readonly IScraperService _scraperService;
        private readonly IValuationService _valuationService;
        public ScreenerService(
        IValuationRepository valuationRepo,
        IScraperService scraperService,
        IValuationService valuationService,
        ILogger<ScreenerService> logger)
        {
            _valuationRepo = valuationRepo;
            _scraperService = scraperService;
            _valuationService = valuationService;
            _logger = logger;
        }
        public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score)
        {
            var symbols = await _valuationRepo.GetByFilterAsync(minMarginOfSafety, searchQuery, score);
            var screenRows = symbols.Select(s => StockValuationToScreenResultRow(s)).ToList();
            return screenRows;
        }

        public async Task<StockDetailsViewModel> RunScreenDetailsAsync(string symbol)
        {
            // Scrape live financial records
            var (stockValuation, deepFinancials, historicalFinancials) =
                await _scraperService.ScrapeStockAsync(symbol.ToUpper());

            if (stockValuation is null || deepFinancials is null || historicalFinancials is null)
            {
                return null;
            }

            // Calculate intrinsic value & score rating
            var valuationResult = _valuationService.EvaluateData(stockValuation, deepFinancials, historicalFinancials);

            // Map scraped metrics to Details ViewModel
            var model = new StockDetailsViewModel
            {
                // 1. Basic Stock Information
                Symbol = stockValuation.Symbol,
                CompanyName = stockValuation.CompanyName,
                Sector = stockValuation.Sector,
                CurrentPrice = stockValuation.CurrentPrice,
                LastAnalyzed = DateTime.UtcNow,

                // 2. Core Valuation Output
                IntrinsicValue = valuationResult.IntrinsicValue,
                MarginOfSafetyPercent = 0m,
                Verdict = valuationResult.Verdict,
                SoundScoreRating = valuationResult.SoundScoreRating,

                // 3. Deep Financial Indicators
                PE = 0m,
                PB = deepFinancials.BookValuePerShare,
                EvToEbitda = deepFinancials.EbitCr,
                ROEPercent = deepFinancials.ReportedRoePercent,
                ROCEPercent = 0m,
                NetProfitMarginPercent = 0m,
                DebtToEquity = 0m,
                InterestCoverageRatio = 0m,
                CurrentRatio = 0m,
                FreeCashFlowCr = deepFinancials.FreeCashFlowCr,
                DividendYieldPercent = deepFinancials.DividendYieldPercent,
                IsDividendConsistent = true,

                // 4. Historical Trends
                RevenueCagr3Yr = 0m,
                RevenueCagr5Yr = 0m,
                ProfitCagr3Yr = 0m,
                ProfitCagr5Yr = 0m,
                AverageRoe3Yr = 0m,
                AverageRoe5Yr = 0m,
                ConsecutiveDividendYears = 0
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
                Sector = SectorMapper.Map(value.Sector),
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
