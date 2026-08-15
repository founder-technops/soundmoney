using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SoundMoney.Data;
using static System.Formats.Asn1.AsnWriter;

namespace SoundMoney.Services
{   
    public class DailyBackgroundService : BackgroundService
    {
        private readonly ILogger<DailyBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IGeminiService _geminiService;
        // Set your target daily execution time (e.g., 02:00 AM)
        private readonly TimeSpan _scheduledTime = new TimeSpan(0, 0, 10);

        public DailyBackgroundService(ILogger<DailyBackgroundService> logger, IServiceProvider serviceProvider, IGeminiService geminiService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _geminiService = geminiService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 1. Calculate delay until the next occurrence of the target time
            TimeSpan initialDelay = GetInitialDelay(_scheduledTime);
            _logger.LogInformation("Next daily execution scheduled in {Delay}", initialDelay);

            try
            {
                // Wait until the first scheduled time
                await Task.Delay(_scheduledTime, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return; // Graceful shutdown before first run
            }

            // 2. Setup a 24-hour timer for subsequent daily runs
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

            do
            {
                try
                {
                    _logger.LogInformation("Daily background task started at: {Time}", DateTimeOffset.Now);

                    // --- YOUR DAILY WORK HERE ---
                    await DoDailyWorkAsync(stoppingToken);

                    _logger.LogInformation("Daily background task completed successfully.");
                }
                catch (Exception ex)
                {
                    // Catch exceptions so errors don't crash the background service worker loop
                    _logger.LogError(ex, "An error occurred during daily task execution.");
                }

            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task DoDailyWorkAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var stockRepository = scope.ServiceProvider.GetRequiredService<IStockRepository>();
                var symbols = await stockRepository.GetAllAsync();
                foreach (var symbol in symbols)
                {
                    // Example: Re-run the screener for each stock symbol
                    var newSymbol = await _geminiService.Evaluate(symbol); // Adjust parameters as needed
                    if(newSymbol is not null)
                        await stockRepository.AddOrUpdateAsync(newSymbol);
                    await Task.Delay(100, cancellationToken); // Small delay to avoid overwhelming the service
                }
                await stockRepository.SaveChangesAsync();
            }
            // Simulate work (e.g., database cleanup, sending daily emails)
            await Task.Delay(1000, cancellationToken);
        }

        private static TimeSpan GetInitialDelay(TimeSpan targetTime)
        {
            DateTime now = DateTime.Now;
            DateTime nextRun = now.Date.Add(targetTime);

            // If today's target time has already passed, schedule for tomorrow
            if (now > nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            return nextRun - now;
        }
    }
}
