using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundMoney.Data;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public sealed class AutomationService : BackgroundService
    {
        private static readonly TimeSpan TargetMarketCloseUtcOffset = new(5, 30, 0); // IST Offset (+05:30)
        private const int TargetRunHourIst = 16; // 4:00 PM IST

        private readonly ILogger<AutomationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public AutomationService(
            ILogger<AutomationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Stock Automation Service initialized with rate-limiting throttling.");

            bool hasRunInCurrentSession = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DateTime nowIst = GetCurrentIstTime();
                    DateTime targetRunTimeIst = nowIst.Date.AddHours(TargetRunHourIst);

                    // If starting after 4:00 PM IST and hasn't executed during this host uptime, run catch-up
                    if (nowIst >= targetRunTimeIst && !hasRunInCurrentSession)
                    {
                        _logger.LogInformation("Startup detected post-market hours ({CurrentTime} IST). Triggering immediate catch-up batch execution.",
                            nowIst.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else
                    {
                        if (nowIst >= targetRunTimeIst)
                        {
                            targetRunTimeIst = targetRunTimeIst.AddDays(1);
                        }

                        TimeSpan delayUntilNextRun = targetRunTimeIst - nowIst;
                        _logger.LogInformation("Next daily scraping batch scheduled in {TotalHours:F1}h ({Minutes}m) at {TargetTime} IST.",
                            delayUntilNextRun.TotalHours, delayUntilNextRun.Minutes, targetRunTimeIst.ToString("yyyy-MM-dd HH:mm:ss"));

                        await Task.Delay(delayUntilNextRun, stoppingToken);
                    }

                    _logger.LogInformation("Executing market close processing sequence...");
                    await ProcessStocksSequentiallyAsync(stoppingToken);

                    hasRunInCurrentSession = true;
                    _logger.LogInformation("Daily batch completed successfully.");

                    // Enforce delay boundary so continuous processing doesn't re-trigger immediately
                    await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Automation background task received cancellation signal. Gracefully exiting.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception occurred during daily batch scraping loop. Retrying in 5 minutes.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ProcessStocksSequentiallyAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var valuationRepo = scope.ServiceProvider.GetRequiredService<IFinancialRepository>();
            var scraperService = scope.ServiceProvider.GetRequiredService<IScraperService>();
            var valuationService = scope.ServiceProvider.GetRequiredService<IValuationService>();

            IReadOnlyList<StockValuation> pendingSymbols;
            try
            {
                var symbols = await valuationRepo.GetPendingValuationsAsync();
                pendingSymbols = symbols?.ToList() ?? new List<StockValuation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve pending valuation symbols from database.");
                return;
            }

            if (pendingSymbols.Count == 0)
            {
                _logger.LogInformation("No pending stock valuations found for today's queue.");
                return;
            }

            _logger.LogInformation("Processing {Count} stocks sequentially. Estimated completion time: ~{Hours:F1} hours.",
                pendingSymbols.Count, (pendingSymbols.Count * 7.5) / 3600.0);

            var stopwatch = Stopwatch.StartNew();
            int processedCount = 0;
            int successCount = 0;
            int totalCount = pendingSymbols.Count;

            foreach (var symbol in pendingSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedCount++;
                _logger.LogInformation("[{Current}/{Total}] Scraping symbol: {Symbol}",
                    processedCount, totalCount, symbol.Symbol);

                try
                {
                    var (stockValuation, deepFinancials, historicalFinancials) =
                        await scraperService.ScrapeStockAsync(symbol.Symbol, cancellationToken);

                    if (stockValuation is not null && deepFinancials is not null && historicalFinancials is not null)
                    {
                        symbol.Sector = stockValuation.Sector;
                        var valuation = valuationService.EvaluateData(symbol, deepFinancials, historicalFinancials);

                        await valuationRepo.SaveValuationAsync(valuation);
                        successCount++;
                        _logger.LogInformation("Successfully analyzed and persisted: {Symbol}", symbol.Symbol);
                    }
                    else
                    {
                        _logger.LogWarning("Incomplete data retrieved for symbol: {Symbol}. Skipping valuation save.", symbol.Symbol);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Execution canceled while processing symbol: {Symbol}", symbol.Symbol);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing stock symbol: {Symbol}", symbol.Symbol);
                }

                // Random jitter delay (5–10 seconds) for rate-limiting compliance
                int delaySeconds = RandomNumberGenerator.GetInt32(5, 11);
                _logger.LogDebug("Throttling request. Sleeping for {Seconds}s...", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }

            stopwatch.Stop();
            _logger.LogInformation("Batch execution finished. Processed {Success}/{Total} successfully in {ElapsedMinutes:F1} minutes.",
                successCount, totalCount, stopwatch.Elapsed.TotalMinutes);
        }

        private static DateTime GetCurrentIstTime()
        {
            // Cross-platform time zone conversion (Linux/Docker/Windows)
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
                DateTime.UtcNow,
                OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata"
            );
        }
    }
}