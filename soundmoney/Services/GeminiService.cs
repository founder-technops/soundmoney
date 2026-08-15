using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using Microsoft.VisualBasic;
using SoundMoney.Models;
using SoundMoney.Services.IntrinsicValue;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SoundMoney.Services;

public interface IGeminiService
{
    Task<StockValuation> Evaluate(StockValuation symbol);

}
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<GeminiService> _logger;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Fetch data from Gemini, calculate valuation, and store in database.
    /// </summary>
    public async Task<StockValuation> Evaluate(StockValuation symbol)
    {
        var prompt = BuildPrompt(symbol);
        //var response = await CallGeminiApiClientAsync(prompt);
        var response = await CallGeminiAsync(prompt);

        if (response is null)
            return null;

        return ParseGeminiResponse(response, symbol.Symbol);
    }

    private string BuildPrompt(StockValuation symbol)
    {
        return $@"
You are a financial analyst. For the NSE-listed Indian stock symbol '{symbol.Symbol}', provide the following data in JSON format:

{{
  ""Symbol"": ""{symbol.Symbol}"",
  ""CompanyName"": ""{(!string.IsNullOrEmpty(symbol.CompanyName) ? symbol.CompanyName : "<official company name>")}"" ,
  ""CurrentPrice"": ""<current price in INR as decimal>"",
  ""Sector"": ""{(!string.IsNullOrEmpty(symbol.Sector) ? symbol.Sector : "<sector category: Banking, IT, Energy, FMCG, Pharma, Automobile, Metals, Infrastructure, or FinancialServices>")}"",
  ""IntrinsicMethod"": ""{(!string.IsNullOrEmpty(symbol.IntrinsicMethod) ? symbol.IntrinsicMethod : "<choose the intrinsic method based on the sector>")}"",
  ""IntrinsicValue"": <calculate the intrinsic value based on the IntrinsicMethod>
}}

Base your data on the most recent available information. Return ONLY valid JSON, no additional text.";
    }

    private async Task<string?> CallGeminiApiClientAsync(string prompt)
    {
        try
        {
            var model = _config.GetValue<string>("Gemini:Model") ?? "gemini-2.0-flash";
            var apiKey = _config.GetValue<string>("Gemini:ApiKey");
            var client = new Client(apiKey: apiKey);
            var searchTool = new Tool
            {
                GoogleSearch = new GoogleSearch()
            };

            // 2. Configure the request
            var config = new GenerateContentConfig
            {
                Tools = new List<Tool>() { searchTool },
                ResponseMimeType = "application/json",
                Temperature = 1.0f // Recommended temperature when using Search
            };

            var response = await client.Models.GenerateContentAsync(
            model: model,
            contents: prompt,
            config: config
            );

            return response?.Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API client");
            return null;
        }
    }

    private async Task<string?> CallGeminiAsync(string prompt)
    {
        try
        {
            var apiKey = _config.GetValue<string>("Gemini:ApiKey");
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
                },
                generationConfig = new
                {
                    maxOutputTokens = 2048
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

            var result = JsonSerializer.Deserialize<StockValuation>(jsonString);
           
            result.Sector = SectorMapper.Map(result.Sector).ToString();
            
            result.MarginOfSafety = result.IntrinsicValue > 0 ? Math.Round((result.IntrinsicValue - result.CurrentPrice) / result.IntrinsicValue * 100m, 2) : -100m;
            result.Verdict = result.MarginOfSafety >= 1 ? "Undervalued"
                        : result.MarginOfSafety <= -1 ? "Overvalued"
                        : "Fair value";
             
            _logger.LogInformation("Parsed screener data for {Symbol}: Price={Price}, IV={IV}, MOS={MOS}%",
                symbol, result.CurrentPrice, result.IntrinsicValue, result.MarginOfSafety);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Gemini response for {Symbol}", symbol);
            return null;
        }
    }

    
}
