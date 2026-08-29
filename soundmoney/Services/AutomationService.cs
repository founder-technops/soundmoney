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

            try
            {
                var (stockValuation, deepFinancials, historicalFinancials) =
                    await scraperService.ScrapeStockAsync(pendingSymbol.Symbol, cancellationToken);

                if (stockValuation is not null && deepFinancials is not null && historicalFinancials is not null)
                {
                    pendingSymbol.Sector = stockValuation.Sector;
                    valuation = valuationService.EvaluateData(pendingSymbol, deepFinancials, historicalFinancials);
                    valuation.ErrorMessage = $"Successfully analyzed and persisted: {pendingSymbol.Symbol}";
                    _logger.LogInformation("Successfully processed symbol: {Symbol}", pendingSymbol.Symbol);
                }
                else
                {
                    valuation.ErrorMessage = $"Incomplete data retrieved for symbol: {pendingSymbol.Symbol}. Skipping valuation calculation.";
                    _logger.LogWarning("Incomplete data for symbol: {Symbol}", pendingSymbol.Symbol);
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