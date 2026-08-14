using System.Net.Http.Json;
using System.Text.Json;
using SoundMoney.Models;
using SoundMoney.Services.IntrinsicValue;

namespace SoundMoney.Services;

/// <summary>
/// Unified service that uses Google Gemini API to fetch stock screener data
/// and stores results in SQLite database for filtering and caching.
/// 
/// Workflow:
/// 1. Fetch data from Gemini API
/// 2. Calculate intrinsic value using sector-specific strategies
/// 3. Store results in SQLite database
/// 4. Retrieve and filter from database on subsequent requests
/// </summary>
public class GeminiScreenerService : ISoundMoneyService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly IIntrinsicValueStrategyFactory _strategyFactory;
    private readonly IStockRepository _stockRepository;
    private readonly ILogger<GeminiScreenerService> _logger;

    public GeminiScreenerService(
        HttpClient httpClient,
        IConfiguration config,
        IIntrinsicValueStrategyFactory strategyFactory,
        IStockRepository stockRepository,
        ILogger<GeminiScreenerService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _strategyFactory = strategyFactory;
        _stockRepository = stockRepository;
        _logger = logger;
    }

    public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter)
    {
        var symbols = _config.GetSection("Watchlist").Get<string[]>() ?? Array.Empty<string>();
        var results = new List<ScreenerResultRow>();

        _logger.LogInformation("Starting screen for {SymbolCount} symbols", symbols.Length);

        foreach (var symbol in symbols)
        {
            try
            {
                var row = await FetchAndStoreScreenerRowAsync(symbol);
                if (row is not null)
                {
                    results.Add(row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching data for {Symbol} from Gemini", symbol);
                continue;
            }

            // Be respectful with API rate limiting
            await Task.Delay(1000);
        }

        // Apply filters to database results
        var filtered = results.Where(r => r.MarginOfSafetyPercent >= minMarginOfSafety);
        if (sectorFilter is not null)
            filtered = filtered.Where(r => r.Sector == sectorFilter);

        var finalResults = filtered.OrderByDescending(r => r.MarginOfSafetyPercent).ToList();
        _logger.LogInformation("Screen completed: {ResultCount} stocks meet criteria", finalResults.Count);

        return finalResults;
    }

    /// <summary>
    /// Fetch data from Gemini, calculate valuation, and store in database.
    /// </summary>
    private async Task<ScreenerResultRow?> FetchAndStoreScreenerRowAsync(string symbol)
    {
        var apiKey = _config.GetValue<string>("Gemini:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Gemini API key not configured");
            return null;
        }

        var prompt = BuildPrompt(symbol);
        var response = await CallGeminiAsync(apiKey, prompt);

        if (response is null)
            return null;

        var stockvalue = ParseGeminiResponse(response, symbol);

        ScreenerResultRow row = null; 
        // Store in database for later retrieval
        if (stockvalue is not null)
        {
            row = StockValuationToScreenResultRow(stockvalue);
            await _stockRepository.AddOrUpdateAsync(stockvalue);
        }

        return row ?? new ScreenerResultRow();
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
            MarginOfSafetyPercent = value.MarginOfSafety,
            Verdict = value.Verdict
        };
    }

    private string BuildPrompt(string symbol)
    {
        return $@"
You are a financial analyst. For the NSE-listed Indian stock symbol '{symbol}', provide the following data in JSON format:

{{
  ""symbol"": ""{symbol}"",
  ""companyName"": ""<official company name>"",
  ""currentPrice"": <current price in INR as decimal>,
  ""sector"": ""<sector category: Banking, IT, Energy, FMCG, Pharma, Automobile, Metals, Infrastructure, or FinancialServices>"",
  ""IntrinsicMethod"": <choose the intrinsic method based on the sector>,
  ""IntrinsicValue"": <calculate the intrinsic value based on the IntrinsicMethod>
}}

Base your data on the most recent available information. Return ONLY valid JSON, no additional text.";
    }

    private async Task<string?> CallGeminiAsync(string apiKey, string prompt)
    {
        try
        {
            var baseUrl = _config.GetValue<string>("Gemini:BaseUrl") ?? "https://generativelanguage.googleapis.com/v1beta/models";
            var model = _config.GetValue<string>("Gemini:Model") ?? "gemini-2.0-flash";
            var url = $"{baseUrl}/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var request = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync(url, request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API returned {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
                if (firstCandidate.ValueKind == JsonValueKind.Object &&
                    firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array)
                {
                    var firstPart = parts.EnumerateArray().FirstOrDefault();
                    if (firstPart.ValueKind == JsonValueKind.Object &&
                        firstPart.TryGetProperty("text", out var text))
                    {
                        return text.GetString();
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            return null;
        }
    }

    private StockValuation? ParseGeminiResponse(string response, string symbol)
    {
        try
        {
            // Extract JSON from response (Gemini might include extra text)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd < 0)
            {
                _logger.LogWarning("No JSON found in Gemini response for {Symbol}", symbol);
                return null;
            }

            var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Parse fundamentals
            var companyName = TryGetString(root, "companyName") ?? symbol;
            var currentPrice = TryGetDecimal(root, "currentPrice");
            var sectorStr = TryGetString(root, "sector") ?? "Other";
            var sector = SectorMapper.Map(sectorStr);
            var intrinsicmethod = TryGetString(root, "intrinsicMethod");
            var intrinsicvalue = TryGetDecimal(root, "intrinsicValue");
            var marginofsafety = intrinsicvalue > 0 ? Math.Round((intrinsicvalue - currentPrice) / intrinsicvalue * 100m, 2) : -100m;
            var verdict = marginofsafety >= 1 ? "Undervalued"
                        : marginofsafety <= -1 ? "Overvalued"
                        : "Fair value";
            
            var result = new StockValuation
            {
                Symbol = symbol,
                CompanyName = companyName,
                Sector = sector.ToString(),
                IntrinsicMethod = intrinsicmethod ?? "",
                CurrentPrice = currentPrice,
                IntrinsicValue = intrinsicvalue,
                MarginOfSafety = marginofsafety,
                Verdict = verdict
            };

            _logger.LogInformation("Parsed screener data for {Symbol}: Price={Price}, IV={IV}, MOS={MOS}%",
                symbol, currentPrice, intrinsicvalue, marginofsafety);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Gemini response for {Symbol}", symbol);
            return null;
        }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        return null;
    }

    private static decimal TryGetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop)) return 0m;
        try
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
                return d;
            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (string.IsNullOrWhiteSpace(s)) return 0m;
                s = s.Trim().TrimEnd('%');
                if (decimal.TryParse(s, out var parsed))
                    return parsed;
            }
        }
        catch { }
        return 0m;
    }
}
