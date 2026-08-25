using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundMoney.Data;
using SoundMoney.Models;
using System.Security.Cryptography;

namespace SoundMoney.Services
{
    public class AutomationService : BackgroundService
    {
        private readonly ILogger<AutomationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AutomationService(
            ILogger<AutomationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stock Automation Service initialized with Screener rate-limiting (5-10s delay).");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Calculate time remaining until market close (4:00 PM IST)
                TimeSpan delayUntilMarketClose = GetDelayUntilMarketClose();
                _logger.LogInformation("Next daily scraping batch scheduled to start in {Hours}h {Minutes}m.",
                    delayUntilMarketClose.Hours, delayUntilMarketClose.Minutes);

                await Task.Delay(delayUntilMarketClose, stoppingToken);

                try
                {
                    _logger.LogInformation("Market closed. Starting rate-limited sequential scraping for all pending stocks...");
                    await ProcessStocksSequentiallyAsync(stoppingToken);
                    _logger.LogInformation("Daily batch completed successfully.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Stock scraping background task was canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during daily batch scraping execution.");
                }
            }
        }

        private async Task ProcessStocksSequentiallyAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var valuationRepo = scope.ServiceProvider.GetRequiredService<IFinancialRepository>();
            var scraperService = scope.ServiceProvider.GetRequiredService<IScraperService>();
            var valuationService = scope.ServiceProvider.GetRequiredService<IValuationService>();

            var pendingSymbols = (await valuationRepo.GetPendingValuationsAsync()).ToList();

            if (!pendingSymbols.Any())
            {
                _logger.LogInformation("No pending stocks found for today.");
                return;
            }

            _logger.LogInformation("Processing {Count} stocks sequentially. Estimated completion time: ~5.5 hours.", pendingSymbols.Count);

            int processedCount = 0;
            int totalCount = pendingSymbols.Count;

            foreach (var symbol in pendingSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedCount++;
                _logger.LogInformation("[{Current}/{Total}] Fetching data for symbol: {Symbol}",
                    processedCount, totalCount, symbol.Symbol);

                try
                {
                    var (stockValuation, deepFinancials, historicalFinancials) =
                        await scraperService.ScrapeStockAsync(symbol.Symbol, cancellationToken);

                    if (stockValuation is not null && deepFinancials is not null && historicalFinancials is not null)
                    {
                        symbol.Sector = stockValuation.Sector;
                        var valuation = valuationService.EvaluateData(symbol, deepFinancials, historicalFinancials);

                        // Save individually so progress is preserved if interrupted
                        await valuationRepo.SaveValuationAsync(valuation);
                        _logger.LogInformation("Successfully analyzed and saved: {Symbol}", symbol.Symbol);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing stock: {Symbol}", symbol.Symbol);
                }

                // Enforce 5 to 10 seconds random delay to prevent IP block
                int delaySeconds = RandomNumberGenerator.GetInt32(5, 11);
                _logger.LogDebug("Throttling request. Waiting {Seconds} seconds...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        private static TimeSpan GetDelayUntilMarketClose()
        {
            var nowUtc = DateTime.UtcNow;
            var nowIst = nowUtc.AddHours(5).AddMinutes(30); // IST Offset

            var targetRunTime = new DateTime(nowIst.Year, nowIst.Month, nowIst.Day, 16, 0, 0); // 4:00 PM IST

            if (nowIst >= targetRunTime)
            {
                targetRunTime = targetRunTime.AddDays(1);
            }

            return targetRunTime - nowIst;
        }
    }
}