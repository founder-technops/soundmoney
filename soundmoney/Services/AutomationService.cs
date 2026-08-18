using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundMoney.Data;

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
            // Set timer tick interval to 1 minute
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1440));

            _logger.LogInformation("Stock scraper background service started. Executing every 1 minute.");

            do
            {
                try
                {
                    _logger.LogInformation("Starting stock scraping execution loop at: {Time}", DateTimeOffset.Now);

                    await ProcessSymbolsAsync(stoppingToken);

                    _logger.LogInformation("Finished stock scraping batch successfully.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Background service execution was canceled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during symbol processing execution.");
                }

            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task ProcessSymbolsAsync(CancellationToken cancellationToken)
        {
            // Create a dedicated scope per execution cycle
            using var scope = _serviceProvider.CreateScope();

            var valuationRepo = scope.ServiceProvider.GetRequiredService<IFinancialRepository>();
            var scraperService = scope.ServiceProvider.GetRequiredService<IScraperService>();
            var valuationService = scope.ServiceProvider.GetRequiredService<IValuationService>();

            var symbols = await valuationRepo.GetPendingValuationsAsync();

            foreach (var symbol in symbols)
            {
                // Ensure cancellation is respected before picking up the next symbol
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogInformation("Scraping data for symbol: {Symbol}", symbol.Symbol);

                    var (stockValuation, deepFinancials, historicalFinancials) = await scraperService.ScrapeStockAsync(symbol.Symbol);

                    if (stockValuation is not null && deepFinancials is not null && historicalFinancials is not null)
                    {
                        symbol.Sector = stockValuation.Sector;
                        //await valuationRepo.SaveCompleteFinancialDataAsync(deepFinancials, historicalFinancials);
                        _logger.LogInformation("Saved scraped financial data for {Symbol}", symbol.Symbol);
                        var valuation = await valuationService.EvaluateDataAsync(symbol, deepFinancials, historicalFinancials);
                        await valuationRepo.SaveValuationAsync(valuation);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing symbol: {Symbol}", symbol.Symbol);
                }

                // 1 minute throttle delay between individual stock scrapes to prevent rate limiting
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
}