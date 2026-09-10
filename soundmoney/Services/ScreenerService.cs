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

            // Map scraped metrics to Details ViewModel
            var model = new StockDetailsViewModel
            {
                // 1. Basic Stock Information
                Symbol = valuationResult.Symbol,
                CompanyName = valuationResult.CompanyName,
                Sector = valuationResult.Sector,
                CurrentPrice = valuationResult.CurrentPrice,
                LastAnalyzed = DateTime.Now,

                // 2. Core Valuation Output
                IntrinsicValue = valuationResult.IntrinsicValue,
                MarginOfSafetyPercent = valuationResult.MarginOfSafety,
                Verdict = valuationResult.Verdict,
                SoundScoreRating = valuationResult.SoundScoreRating,

                // 3. Deep Financial Indicators
                // Screener's own trailing P/E when it scraped one; otherwise derive it
                // from price / EPS rather than leaving it at a hardcoded 0.
                PE = FinancialAlgorithms.CalculatePeRatio(current),
                PB = FinancialAlgorithms.CalculatePbRatio(current),
                EvToEbitda = FinancialAlgorithms.CalculateEvToEbitda(current),
                ROEPercent = current.ReportedRoePercent,
                ROCEPercent = current.ReportedRocePercent,
                NetProfitMarginPercent = FinancialAlgorithms.CalculateNetProfitMarginPercent(current),
                DebtToEquity = FinancialAlgorithms.CalculateDebtToEquity(current),
                InterestCoverageRatio = FinancialAlgorithms.CalculateInterestCoverage(current),
                CurrentRatio = FinancialAlgorithms.CalculateCurrentRatio(current),
                FreeCashFlowCr = current.FreeCashFlowCr,
                DividendYieldPercent = current.DividendYieldPercent,
                IsDividendConsistent = dividendAnalysis.IsConsistent,

                // 4. Historical Trends
                RevenueCagr3Yr = FinancialAlgorithms.CalculateCagrPercent(current, historical, 3, f => f.SalesCr),
                RevenueCagr5Yr = FinancialAlgorithms.CalculateCagrPercent(current, historical, 5, f => f.SalesCr),
                ProfitCagr3Yr = FinancialAlgorithms.CalculateCagrPercent(current, historical, 3, f => f.NetProfitCr),
                ProfitCagr5Yr = FinancialAlgorithms.CalculateCagrPercent(current, historical, 5, f => f.NetProfitCr),
                AverageRoe3Yr = FinancialAlgorithms.CalculateAverageRoePercent(current, historical, 3),
                AverageRoe5Yr = FinancialAlgorithms.CalculateAverageRoePercent(current, historical, 5),
                ConsecutiveDividendYears = dividendAnalysis.ConsecutiveYearsPaid
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