using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SoundMoney.Services
{
    public class SelfAwakenService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SelfAwakenService> _logger;
        private readonly IConfiguration _configuration;

        public SelfAwakenService(
            IHttpClientFactory httpClientFactory,
            ILogger<SelfAwakenService> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Set ping interval to 10 minutes (to beat the 15-minute sleep threshold)
            TimeSpan pingInterval = TimeSpan.FromMinutes(10);

            // Allow the Web API / MVC app to fully startup before making the first call
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Retrieve site URL from configuration or default fallback
                    string siteUrl = _configuration["AppConfig:SiteUrl"] ?? "http://www.soundmoney.somee.com/";

                    var client = _httpClientFactory.CreateClient("SelfAwakenClient");

                    _logger.LogInformation("SelfAwakenService sending keep-alive ping to {SiteUrl} at {Time}", siteUrl, DateTimeOffset.Now);

                    HttpResponseMessage response = await client.GetAsync(siteUrl, stoppingToken);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("SelfAwakenService keep-alive ping succeeded with status code {StatusCode}", response.StatusCode);
                    }
                    else
                    {
                        _logger.LogWarning("SelfAwakenService keep-alive ping returned status code {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "An error occurred while executing the SelfAwakenService keep-alive ping.");
                }

                // Wait 10 minutes before sending the next ping
                await Task.Delay(pingInterval, stoppingToken);
            }
        }
    }
}