using Microsoft.Extensions.Caching.Memory;
using SoundMoney.Algorithms;
using SoundMoney.Models;
using System.Net;
using System.Text.Json;

namespace SoundMoney.Services;

public sealed record DailyMarketBar(DateOnly Date, decimal Close, decimal Volume);

public interface IBtstTechnicalService
{
    Task<BtstTechnicalSignal> GetSignalAsync(string symbol, Financial current, List<Financial> historical, decimal intrinsicValue, decimal minimumMarginOfSafety = 0m, CancellationToken cancellationToken = default);
}

public sealed class BtstTechnicalService : IBtstTechnicalService
{
    private const string Source = "Yahoo Finance chart data (unofficial)";
    // Enough history for the 50-session average measured 20 sessions ago, a 40-session
    // return, and volume context. DMA200 is displayed as context when available.
    private const int RequiredPriceBars = 70;
    private const int AverageVolumeLookback = 20;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(8);
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BtstTechnicalService> _logger;
    private readonly string _diskCacheDirectory;

    private sealed record PersistedDailyBars(DateTimeOffset RetrievedAtUtc, List<DailyMarketBar> Bars);

    public BtstTechnicalService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<BtstTechnicalService> logger,
        IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _diskCacheDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "TechnicalMarketCache");
    }

    public async Task<BtstTechnicalSignal> GetSignalAsync(string symbol, Financial current, List<Financial> historical, decimal intrinsicValue, decimal minimumMarginOfSafety = 0m, CancellationToken cancellationToken = default)
    {
        string normalizedSymbol = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSymbol))
        {
            return Unavailable("No symbol was supplied.");
        }

        string cacheKey = $"btst-daily-bars:{normalizedSymbol}";
        PersistedDailyBars? persisted = null;
        if (_cache.TryGetValue(cacheKey, out PersistedDailyBars? memoryCacheEntry))
        {
            persisted = memoryCacheEntry;
        }
        else
        {
            persisted = await TryReadDiskCacheAsync(normalizedSymbol, cancellationToken);
            if (persisted is not null)
            {
                _cache.Set(cacheKey, persisted, CacheDuration);
            }
        }

        if (persisted is not null && IsCacheFresh(persisted))
        {
            var cachedSignal = Evaluate(normalizedSymbol, current.MarketCapCr, persisted.Bars, persisted.RetrievedAtUtc);
            return EvaluateCombined(cachedSignal, current, historical, intrinsicValue, minimumMarginOfSafety);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var bars = await FetchDailyBarsAsync(normalizedSymbol, timeout.Token);
            var freshCache = new PersistedDailyBars(DateTimeOffset.UtcNow, bars.ToList());
            _cache.Set(cacheKey, freshCache, CacheDuration);
            await TryWriteDiskCacheAsync(normalizedSymbol, freshCache, cancellationToken);
            var freshSignal = Evaluate(normalizedSymbol, current.MarketCapCr, bars, freshCache.RetrievedAtUtc);
            return EvaluateCombined(freshSignal, current, historical, intrinsicValue, minimumMarginOfSafety);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            if (persisted is not null)
            {
                return EvaluateStaleCache(normalizedSymbol, current, historical, intrinsicValue, minimumMarginOfSafety, persisted);
            }
            return EvaluateCombined(Unavailable("Market-data request timed out. Try again later."), current, historical, intrinsicValue, minimumMarginOfSafety);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "BTST market-data request failed for {Symbol}", normalizedSymbol);
            if (persisted is not null)
            {
                return EvaluateStaleCache(normalizedSymbol, current, historical, intrinsicValue, minimumMarginOfSafety, persisted);
            }
            return EvaluateCombined(Unavailable("Market data is temporarily unavailable."), current, historical, intrinsicValue, minimumMarginOfSafety);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse BTST market data for {Symbol}", normalizedSymbol);
            if (persisted is not null)
            {
                return EvaluateStaleCache(normalizedSymbol, current, historical, intrinsicValue, minimumMarginOfSafety, persisted);
            }
            return EvaluateCombined(Unavailable("The market-data provider returned an unreadable response."), current, historical, intrinsicValue, minimumMarginOfSafety);
        }
    }

    private BtstTechnicalSignal EvaluateStaleCache(
        string symbol,
        Financial current,
        List<Financial> historical,
        decimal intrinsicValue,
        decimal minimumMarginOfSafety,
        PersistedDailyBars staleCache)
    {
        var stale = Evaluate(symbol, current.MarketCapCr, staleCache.Bars, staleCache.RetrievedAtUtc);
        stale = stale with
        {
            Status = "Stale",
            DataIsStale = true,
            Message = "Live market data could not be refreshed; showing the cached technical values, which may be stale. " + stale.Message
        };
        return EvaluateCombined(stale, current, historical, intrinsicValue, minimumMarginOfSafety);
    }

    private async Task<PersistedDailyBars?> TryReadDiskCacheAsync(string symbol, CancellationToken cancellationToken)
    {
        string path = GetDiskCachePath(symbol);
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<PersistedDailyBars>(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Could not read technical market-data cache for {Symbol}", symbol);
            return null;
        }
    }

    private async Task TryWriteDiskCacheAsync(string symbol, PersistedDailyBars value, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_diskCacheDirectory);
            string path = GetDiskCachePath(symbol);
            string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, value, cancellationToken: cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The in-memory cache remains available if the deployment filesystem is read-only.
            _logger.LogWarning(ex, "Could not persist technical market-data cache for {Symbol}", symbol);
        }
    }

    private string GetDiskCachePath(string symbol)
    {
        string safeSymbol = new(symbol.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '^').ToArray());
        return Path.Combine(_diskCacheDirectory, $"{safeSymbol}.json");
    }

    private static bool IsCacheFresh(PersistedDailyBars cache)
    {
        if (cache.Bars is null || cache.Bars.Count == 0)
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - cache.RetrievedAtUtc >= CacheDuration)
        {
            return false;
        }

        var indiaNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5.5));
        bool isWeekday = indiaNow.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        bool sessionClosed = indiaNow.TimeOfDay >= TimeSpan.FromHours(16);
        DateOnly latestCachedDate = cache.Bars.Count == 0 ? DateOnly.MinValue : cache.Bars.Max(bar => bar.Date);

        // Once today's session is complete, refresh so the final daily close is used.
        return !isWeekday || !sessionClosed || latestCachedDate >= DateOnly.FromDateTime(indiaNow.DateTime);
    }

    public static BtstTechnicalSignal Evaluate(
        string symbol,
        decimal marketCapCr,
        IReadOnlyList<DailyMarketBar> inputBars,
        DateTimeOffset retrievedAtUtc)
    {
        var indiaNow = retrievedAtUtc.ToOffset(TimeSpan.FromHours(5.5));
        var currentIndiaDate = DateOnly.FromDateTime(indiaNow.DateTime);
        var bars = inputBars
            .Where(bar => bar.Close > 0m && bar.Volume >= 0m)
            .GroupBy(bar => bar.Date)
            .Select(group => group.Last())
            // A provider may expose today's still-forming daily candle. Do not use it
            // until the NSE cash session has had time to close and the final bar settles.
            .Where(bar => bar.Date < currentIndiaDate || (bar.Date == currentIndiaDate && indiaNow.TimeOfDay >= TimeSpan.FromHours(16)))
            .OrderBy(bar => bar.Date)
            .ToList();

        if (bars.Count < RequiredPriceBars || bars.Count <= AverageVolumeLookback)
        {
            return new BtstTechnicalSignal
            {
                Status = "InsufficientData",
                IsSetup = null,
                Message = $"At least {RequiredPriceBars} valid daily closes are needed to evaluate the 20–40-session trend and DMA 50.",
                SessionDate = bars.LastOrDefault()?.Date,
                RetrievedAtUtc = retrievedAtUtc,
                DataSource = Source
            };
        }

        var latest = bars[^1];
        var previous = bars[^2];
        var twentySessionsAgo = bars[^21];
        var fortySessionsAgo = bars[^41];
        decimal dma50 = bars.TakeLast(50).Average(bar => bar.Close);
        decimal? dma200 = bars.Count >= 200 ? bars.TakeLast(200).Average(bar => bar.Close) : null;
        decimal dma50TwentySessionsAgo = bars.Skip(bars.Count - 70).Take(50).Average(bar => bar.Close);
        decimal averageVolume20 = bars.Skip(bars.Count - AverageVolumeLookback - 1)
            .Take(AverageVolumeLookback)
            .Average(bar => bar.Volume);

        decimal? dmaSpreadPercent = dma200 is > 0m ? ((dma50 - dma200.Value) / dma200.Value) * 100m : null;
        decimal priceAboveDma50Percent = dma50 > 0m ? ((latest.Close - dma50) / dma50) * 100m : 0m;
        decimal twentySessionReturnPercent = twentySessionsAgo.Close > 0m ? ((latest.Close - twentySessionsAgo.Close) / twentySessionsAgo.Close) * 100m : 0m;
        decimal fortySessionReturnPercent = fortySessionsAgo.Close > 0m ? ((latest.Close - fortySessionsAgo.Close) / fortySessionsAgo.Close) * 100m : 0m;
        decimal oneDayReturnPercent = previous.Close > 0m ? ((latest.Close - previous.Close) / previous.Close) * 100m : 0m;
        decimal? volumeRatio = averageVolume20 > 0m ? latest.Volume / averageVolume20 : null;
        bool hasVolumeSurge = volumeRatio.HasValue && volumeRatio.Value > 1.5m;
        string volumePriceRead = !hasVolumeSurge
            ? "No unusual volume confirmation"
            : oneDayReturnPercent > 0m
                ? "High volume on an up day — demand confirmation, not proof of buyer dominance"
                : oneDayReturnPercent < 0m
                    ? "High volume on a down day — possible selling pressure/distribution"
                    : "High volume with a flat price — possible disagreement or absorption";

        var checks = new List<BtstRuleCheck>
        {
            Check("Close above DMA 50", latest.Close.ToString("N2"), $"> {dma50:N2}", latest.Close > dma50),
            Check("DMA 50 trend", $"{dma50TwentySessionsAgo:N2} → {dma50:N2}", "rising over 20 sessions", dma50 > dma50TwentySessionsAgo),
            Check("Price above DMA 50", $"{priceAboveDma50Percent:N2}%", "above 0% and no more than 15%", priceAboveDma50Percent > 0m && priceAboveDma50Percent <= 15m),
            Check("20-session price trend", $"{twentySessionReturnPercent:N2}%", "> 0%", twentySessionReturnPercent > 0m),
            Check("40-session price trend", $"{fortySessionReturnPercent:N2}%", "> 0%", fortySessionReturnPercent > 0m),
            Check("Close above DMA 200", dma200.HasValue ? latest.Close.ToString("N2") : "N/A (less than 200 sessions)", dma200.HasValue ? $"> {dma200.Value:N2}" : "long-term context only", dma200.HasValue && latest.Close > dma200.Value, "Confirmation"),
            Check("DMA 50/200 alignment", dma200.HasValue ? $"DMA 50 {dma50:N2}; DMA 200 {dma200.Value:N2}" : "N/A (less than 200 sessions)", dma200.HasValue ? "long-term context; not required for 20–40-session setup" : "long-term context only", dma200.HasValue && dma50 > dma200.Value, "Confirmation"),
            Check("DMA 50/200 spread", dmaSpreadPercent.HasValue ? $"{dmaSpreadPercent.Value:N2}%" : "N/A (less than 200 sessions)", "long-term context; not a gate", dmaSpreadPercent > 0m, "Confirmation"),
            Check("Volume-price confirmation", $"{(volumeRatio.HasValue ? $"{volumeRatio.Value:N2}x average; " : "N/A; ")}{volumePriceRead}", ">1.5x average volume with a positive daily close for demand confirmation; context only", hasVolumeSurge && oneDayReturnPercent > 0m, "Confirmation")
        };

        bool passes = checks.Where(check => check.Category != "Confirmation").All(check => check.Passed);
        return new BtstTechnicalSignal
        {
            Status = "Available",
            IsSetup = passes,
            Message = passes
                ? "The 20–40-session technical trend conditions passed. Long-term DMA200 and volume are context only; this does not predict future returns."
                : $"{checks.Count(check => check.Passed && check.Category != "Confirmation")} of {checks.Count(check => check.Category != "Confirmation")} 20–40-session trend conditions passed.",
            SessionDate = latest.Date,
            RetrievedAtUtc = retrievedAtUtc,
            LatestClose = latest.Close,
            Dma50 = dma50,
            Dma200 = dma200,
            DmaSpreadPercent = dmaSpreadPercent,
            PriceAboveDma50Percent = priceAboveDma50Percent,
            TwentySessionReturnPercent = twentySessionReturnPercent,
            FortySessionReturnPercent = fortySessionReturnPercent,
            OneDayReturnPercent = oneDayReturnPercent,
            LatestVolume = latest.Volume,
            AverageVolume20Sessions = averageVolume20,
            VolumeRatio = volumeRatio,
            IsTechnicalSetup = passes,
            Checks = checks,
            DataSource = Source
        };
    }

    public static IReadOnlyList<BtstRuleCheck> EvaluateFundamentalChecks(Financial current, List<Financial> historical)
    {
        var history = historical ?? new List<Financial>();
        decimal averageRoe5Yr = FinancialAlgorithms.CalculateAverageRoePercent(current, history, 5);
        var fiveYearWindow = history
            .Where(item => item.Year > current.Year - 5 && item.Year <= current.Year && item.Year != current.Year)
            .Append(current)
            .ToList();
        decimal averageRoce5Yr = fiveYearWindow.Count > 0
            ? fiveYearWindow.Average(FinancialAlgorithms.CalculateRoce)
            : 0m;
        decimal profitGrowth5Yr = FinancialAlgorithms.CalculateCagrPercent(current, history, 5, item => item.NetProfitCr);
        decimal debtToEquity = FinancialAlgorithms.CalculateDebtToEquity(current);
        decimal cashConversionPercent = current.NetProfitCr > 0m
            ? (current.CashFromOperationsCr / current.NetProfitCr) * 100m
            : 0m;

        return new List<BtstRuleCheck>
        {
            Check("Market capitalization", $"₹{current.MarketCapCr:N0} Cr", "between ₹500 Cr and ₹20,000 Cr", current.MarketCapCr > 500m && current.MarketCapCr < 20000m, "Fundamental"),
            Check("Current ROE", $"{FinancialAlgorithms.CalculateRoe(current):N1}%", "> 20%", FinancialAlgorithms.CalculateRoe(current) > 20m, "Fundamental"),
            Check("5-year average ROE", $"{averageRoe5Yr:N1}%", "> 15%", averageRoe5Yr > 15m, "Fundamental"),
            Check("Promoter holding", $"{current.PromoterHoldingPercent:N1}%", "> 50%", current.PromoterHoldingPercent > 50m, "Fundamental"),
            Check("Current ROCE", current.IsFinancialSector ? "N/A (financial sector)" : $"{FinancialAlgorithms.CalculateRoce(current):N1}%", current.IsFinancialSector ? "Not used for financial sector" : "> 20%", current.IsFinancialSector || FinancialAlgorithms.CalculateRoce(current) > 20m, "Fundamental"),
            Check("5-year average ROCE", current.IsFinancialSector ? "N/A (financial sector)" : $"{averageRoce5Yr:N1}%", current.IsFinancialSector ? "Not used for financial sector" : "> 15%", current.IsFinancialSector || averageRoce5Yr > 15m, "Fundamental"),
            Check("Debt-to-equity", current.IsFinancialSector ? "N/A (financial sector)" : $"{debtToEquity:N2}", current.IsFinancialSector ? "Not used for financial sector" : "< 0.20", current.IsFinancialSector || debtToEquity < 0.20m, "Fundamental"),
            Check("5-year profit growth (CAGR)", $"{profitGrowth5Yr:N1}%", "> 10%", profitGrowth5Yr > 10m, "Fundamental"),
            Check("Cash conversion", $"{cashConversionPercent:N1}%", "> 70% of net profit", current.IsFinancialSector || cashConversionPercent > 70m, "Fundamental"),
            Check("FII holding (context only)", $"{current.FiiHoldingPercent:N1}%", "informational; not a pass/fail gate", true, "Context"),
            Check("DII holding (context only)", $"{current.DiiHoldingPercent:N1}%", "informational; not a pass/fail gate", true, "Context")
        };
    }

    public static BtstTechnicalSignal EvaluateCombined(
        BtstTechnicalSignal technicalSignal,
        Financial current,
        List<Financial> historical,
        decimal intrinsicValue = 0m,
        decimal minimumMarginOfSafety = 0m)
    {
        var fundamentalChecks = EvaluateFundamentalChecks(current, historical).ToList();
        decimal marketClose = technicalSignal.LatestClose ?? 0m;
        decimal marginOfSafety = intrinsicValue > 0m && marketClose > 0m
            ? ((intrinsicValue - marketClose) / intrinsicValue) * 100m
            : decimal.MinValue;
        fundamentalChecks.Add(Check(
            "Entry margin of safety",
            marginOfSafety == decimal.MinValue ? "N/A" : $"{marginOfSafety:N1}%",
            intrinsicValue > 0m ? $"Ranking preference: >= {minimumMarginOfSafety:N1}%" : "Intrinsic value unavailable; trend can still be assessed",
            intrinsicValue > 0m && marketClose > 0m && marginOfSafety >= minimumMarginOfSafety,
            "Context"));

        bool fundamentalsPass = fundamentalChecks
            .Where(check => check.Category == "Fundamental")
            .All(check => check.Passed);
        var allChecks = fundamentalChecks.Concat(technicalSignal.Checks).ToList();
        bool? combinedSetup = technicalSignal.IsTechnicalSetup.HasValue
            ? technicalSignal.IsTechnicalSetup.Value && !technicalSignal.DataIsStale
            : null;
        decimal? referencePrice = technicalSignal.LatestClose;
        var decisionChecks = technicalSignal.Checks.Where(check => check.Category == "Technical").ToList();
        int checksPassed = decisionChecks.Count(check => check.Passed);
        string checksSummary = $"{checksPassed}/{decisionChecks.Count} price-trend gates passed";
        var trendChecks = technicalSignal.Checks.Where(check => check.Category == "Technical").ToList();
        int trendChecksPassed = trendChecks.Count(check => check.Passed);

        return new BtstTechnicalSignal
        {
            Status = technicalSignal.Status,
            IsSetup = combinedSetup,
            IsTechnicalSetup = technicalSignal.IsTechnicalSetup,
            IsFundamentallyStrong = fundamentalsPass,
            ChecksPassed = checksPassed,
            ChecksTotal = decisionChecks.Count,
            TrendChecksPassed = trendChecksPassed,
            TrendChecksTotal = trendChecks.Count,
            ChecksSummary = checksSummary,
            Message = !technicalSignal.IsTechnicalSetup.HasValue
                ? $"Price trend could not be checked because technical data is {technicalSignal.Status.ToLowerInvariant()}. Fundamentals and valuation remain available as context."
                : combinedSetup == true
                    ? "Price-trend gates pass. Review the fundamental quality and entry valuation context before making your own decision; this is not a promised return."
                    : "One or more price-trend gates are not met. Fundamental quality and valuation are shown as context, not mandatory gates.",
            DataSource = technicalSignal.DataSource,
            SessionDate = technicalSignal.SessionDate,
            RetrievedAtUtc = technicalSignal.RetrievedAtUtc,
            LatestClose = referencePrice,
            Dma50 = technicalSignal.Dma50,
            Dma200 = technicalSignal.Dma200,
            DmaSpreadPercent = technicalSignal.DmaSpreadPercent,
            PriceAboveDma50Percent = technicalSignal.PriceAboveDma50Percent,
            TwentySessionReturnPercent = technicalSignal.TwentySessionReturnPercent,
            FortySessionReturnPercent = technicalSignal.FortySessionReturnPercent,
            OneDayReturnPercent = technicalSignal.OneDayReturnPercent,
            LatestVolume = technicalSignal.LatestVolume,
            AverageVolume20Sessions = technicalSignal.AverageVolume20Sessions,
            VolumeRatio = technicalSignal.VolumeRatio,
            DataIsStale = technicalSignal.DataIsStale,
            TargetPrice30Percent = referencePrice.HasValue ? Math.Round(referencePrice.Value * 1.30m, 2) : null,
            TargetPrice40Percent = referencePrice.HasValue ? Math.Round(referencePrice.Value * 1.40m, 2) : null,
            PotentialEntryMarginOfSafety = marginOfSafety == decimal.MinValue ? null : marginOfSafety,
            Checks = allChecks
        };
    }

    private async Task<IReadOnlyList<DailyMarketBar>> FetchDailyBarsAsync(string symbol, CancellationToken cancellationToken)
    {
        string ticker = Uri.EscapeDataString($"{symbol}.NS");
        string requestUri = $"https://query1.finance.yahoo.com/v8/finance/chart/{ticker}?range=2y&interval=1d&events=div%2Csplits";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.UserAgent.ParseAdd("SoundMoney/1.0 (daily technical screening)");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new HttpRequestException("Market-data symbol was not found.", null, response.StatusCode);
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("chart", out var chart)
            || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            throw new JsonException("Market-data response has no chart result.");
        }

        var result = results[0];
        var timestamps = result.GetProperty("timestamp");
        var indicators = result.GetProperty("indicators");
        var quote = indicators.GetProperty("quote")[0];
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");
        int length = Math.Min(timestamps.GetArrayLength(), Math.Min(closes.GetArrayLength(), volumes.GetArrayLength()));
        var bars = new List<DailyMarketBar>(length);
        for (int index = 0; index < length; index++)
        {
            // Keep the provider's quoted close for price levels/targets. Dividend-adjusted
            // closes are unsuitable for displaying a rupee target because they are not the
            // actual traded price; the provider's Close series is already split-adjusted.
            var closeElement = closes[index];
            if (timestamps[index].ValueKind != JsonValueKind.Number
                || closeElement.ValueKind != JsonValueKind.Number
                || volumes[index].ValueKind != JsonValueKind.Number
                || !timestamps[index].TryGetInt64(out long unixSeconds)
                || !closeElement.TryGetDecimal(out decimal close)
                || !volumes[index].TryGetDecimal(out decimal volume))
            {
                continue;
            }

            var marketDate = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.AddHours(5.5));
            bars.Add(new DailyMarketBar(marketDate, close, volume));
        }

        return bars;
    }

    private static BtstRuleCheck Check(string name, string observed, string requirement, bool passed, string category = "Technical") =>
        new(name, observed, requirement, passed, category);

    private static BtstTechnicalSignal Unavailable(string message) => new()
    {
        Status = "Unavailable",
        IsSetup = null,
        Message = message,
        DataSource = Source
    };
}
