using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundMoney.Data;
using SoundMoney.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;

namespace SoundMoney.Services
{
    public sealed class AutomationService : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RestartDelayOnCancellation = TimeSpan.FromMinutes(5);

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
            _logger.LogInformation("Stock Automation Service initialized.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextPendingValuationAsync(stoppingToken);
                    await DelayWithCancellationAsync(TimeSpan.FromSeconds(RandomNumberGenerator.GetInt32(5, 10)), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Stock Automation Service host cancellation requested. Exiting.");
                    //break;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Processing task was cancelled internally. Automatic restart scheduled in 5 minutes...");
                    if (!await DelayWithCancellationAsync(RestartDelayOnCancellation, stoppingToken))
                    {
                        //break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception occurred during processing loop. Retrying automatically in 5 minutes...");
                    if (!await DelayWithCancellationAsync(RestartDelayOnCancellation, stoppingToken))
                    {
                        //break;
                    }
                }
            }

            _logger.LogInformation("Stock Automation Service fully stopped.");
        }

        private async Task ProcessNextPendingValuationAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var valuationRepo = scope.ServiceProvider.GetRequiredService<IFinancialRepository>();
            var scraperService = scope.ServiceProvider.GetRequiredService<IScraperService>();
            var valuationService = scope.ServiceProvider.GetRequiredService<IValuationService>();

            StockValuation? pendingSymbol;
            try
            {
                pendingSymbol = await valuationRepo.GetPendingValuationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve next pending valuation from repository.");
                return;
            }

            if (pendingSymbol is null)
            {
                _logger.LogDebug("No pending stock valuations found. Waiting for next interval.");
                return;
            }

            _logger.LogInformation("Processing symbol: {Symbol}", pendingSymbol.Symbol);

            var valuation = new StockValuation
            {
                Symbol = pendingSymbol.Symbol,
                CompanyName = pendingSymbol.CompanyName ?? string.Empty,
                CurrentPrice = pendingSymbol.CurrentPrice,
                Sector = pendingSymbol.Sector ?? string.Empty,
                FetchedAt = DateTime.UtcNow
            };

            // Set when ScrapeStockAsync reports a connectivity-level failure (Screener.in
            // unreachable, DNS failure, connection timeout - the "connected host has failed
            // to respond" case) rather than a genuine data problem (bad/delisted symbol,
            // page structure Screener.in itself returned but this app couldn't parse). A
            // transient failure shouldn't cost this symbol a full day's wait before the next
            // attempt (see the finally block below) the way a real data problem should.
            bool isTransientNetworkError = false;

            try
            {
                var (stockValuation, current, historical, scrapeError) =
                    await scraperService.ScrapeStockAsync(pendingSymbol.Symbol, cancellationToken);

                isTransientNetworkError = scrapeError != null
                    && scrapeError.StartsWith("Could not reach Screener.in", StringComparison.OrdinalIgnoreCase);

                if (stockValuation is not null && current is not null && historical is not null)
                {
                    pendingSymbol.Sector = stockValuation.Sector;
                    valuation = valuationService.Evaluate(pendingSymbol, current, historical);
                    valuation.ErrorMessage = $"Successfully analyzed and persisted: {pendingSymbol.Symbol}";
                    _logger.LogInformation("Successfully processed symbol: {Symbol}", pendingSymbol.Symbol);

                    // Also populate the full-scrape cache ScreenerService.RunScreenDetailsAsync
                    // reads from, so a user opening Details/Summary for this symbol later
                    // today gets an instant cache hit instead of a second, redundant scrape -
                    // the two pipelines (this background pass and on-demand page views) end
                    // up sharing one "once per stock per day" guarantee instead of each
                    // separately hitting Screener.in for the same symbol on the same day.
                    //
                    // Deliberately caches `valuation` (the Evaluate() result), NOT the raw
                    // `stockValuation` from the scraper - ScraperService only ever sets
                    // Symbol/CompanyName/Sector/CurrentPrice on that raw object, so
                    // IntrinsicValue/MarginOfSafety/Verdict/SoundScore/SoundScoreRating/
                    // DividendYieldPercent/IsDividendConsistent would all be cached as their
                    // C# defaults (0 / "" / false) if `stockValuation` were cached instead.
                    try
                    {
                        await valuationRepo.SaveScrapedCacheAsync(pendingSymbol.Symbol, valuation, current, historical, cancellationToken);
                    }
                    catch (Exception cacheEx)
                    {
                        // Don't let a cache-write failure undo an otherwise-successful
                        // valuation - ScreenerService will just fall back to its own live
                        // scrape later today if this write didn't land.
                        _logger.LogError(cacheEx, "Failed to write scrape cache for symbol: {Symbol}", pendingSymbol.Symbol);
                    }
                }
                else
                {
                    valuation.ErrorMessage = scrapeError ?? $"Incomplete data retrieved for symbol: {pendingSymbol.Symbol}. Skipping valuation calculation.";

                    if (isTransientNetworkError)
                    {
                        _logger.LogWarning("Transient network error scraping {Symbol}, will retry on the next cycle: {Error}", pendingSymbol.Symbol, scrapeError);
                    }
                    else
                    {
                        _logger.LogWarning("Incomplete data for symbol: {Symbol}. Reason: {Error}", pendingSymbol.Symbol, scrapeError);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                valuation.ErrorMessage = "Automation background task received cancellation signal.";
                _logger.LogWarning("Processing canceled for symbol: {Symbol}", pendingSymbol.Symbol);
                throw;
            }
            catch (Exception ex)
            {
                valuation.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error occurred while processing symbol: {Symbol}", pendingSymbol.Symbol);
            }
            finally
            {
                try
                {
                    // On a transient network failure, deliberately do NOT bump UpdatedAt:
                    // GetPendingValuationsAsync selects rows where UpdatedAt is null or
                    // before today, so touching it here would push this symbol's next
                    // attempt to tomorrow even though the failure had nothing to do with
                    // this symbol's data - preserving the prior value keeps it eligible for
                    // the very next polling cycle (every 5-10 seconds) instead.
                    valuation.UpdatedAt = isTransientNetworkError ? pendingSymbol.UpdatedAt : DateTime.Now;
                    await valuationRepo.SaveValuationAsync(valuation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save valuation state for symbol: {Symbol}", pendingSymbol.Symbol);
                }
            }
        }

        private static async Task<bool> DelayWithCancellationAsync(TimeSpan delay, CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(delay, stoppingToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}