using Google.GenAI;
using Google.GenAI.Types;
using HtmlAgilityPack;
using Microsoft.Extensions.AI;
using Microsoft.VisualBasic;
using PuppeteerSharp;
using SoundMoney.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;

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
        var prompt = await BuildPrompt(symbol);
        //var response = await CallGeminiApiClientAsync(prompt);
        var response = await CallGeminiAsync(prompt);
        //var response = await CallGeminiWebScrapperAsync(prompt);

        if (response is null)
            return null;

        return ParseGeminiResponse(response, symbol.Symbol);
    }

    private async Task<string> BuildPrompt(StockValuation symbol)
    {
        var html =  await _httpClient.GetStringAsync(@$"https://www.screener.in/company/{symbol.Symbol}/consolidated/");
        return $@"

Act as a financial analyst.use the above data Return a JSON object with the following fields:
Symbol: {symbol.Symbol},
CompanyName: Full legal company name,
CurrentPrice: Latest stock price (as a decimal),
Sector: {symbol.Sector},
IntrinsicMethod: ""Sum of the Parts (SOTP)"",
IntrinsicValue: Estimated intrinsic value (as a numeric decimal)
Base all figures on the most recent financial data available.";

        return $@"
You are a financial analyst. For the NSE-listed Indian stock symbol '{symbol.Symbol}', provide the following data in JSON format:

{{
  ""Symbol"": ""{symbol.Symbol}"",
  ""CompanyName"": ""{(!string.IsNullOrEmpty(symbol.CompanyName) ? symbol.CompanyName : "<official company name>")}"" ,
  ""CurrentPrice"": ""<current price in INR as decimal>"",
  ""Sector"": ""{(!string.IsNullOrEmpty(symbol.Sector) ? symbol.Sector : "<sector category: Banking, IT, Energy, FMCG, Pharma, Automobile, Metals, Infrastructure, or FinancialServices>")}"",
  ""IntrinsicMethod"": ""{(!string.IsNullOrEmpty(symbol.PrimaryMethod) ? symbol.PrimaryMethod : "<choose the intrinsic method based on the sector>")}"",
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

    private async Task<string?> CallGeminiWebScrapperAsync(string prompt)
    {
        string response = null;
        var url = @$"https://www.google.com/search?q={prompt}";


        string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

        var launchOptions = new LaunchOptions
        {
            Headless = true,
            ExecutablePath = System.IO.File.Exists(chromePath) ? chromePath : null,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-blink-features=AutomationControlled", // Prevents bot detection
                "--window-size=1920,1080"
            }
        };

        // Download fallback only if local Chrome doesn't exist
        if (launchOptions.ExecutablePath == null)
        {
            var fetcher = new BrowserFetcher();
            await fetcher.DownloadAsync();
        }

        await using var browser = await Puppeteer.LaunchAsync(launchOptions);
        await using var page = await browser.NewPageAsync();

        // 1. Set realistic User-Agent & Viewport to avoid bot blocking
        await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

        Console.WriteLine("Navigating...");
        await page.GoToAsync(url, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
        });

        try
        {
            // 2. Use a broader selector strategy instead of highly specific class names
            // Searches for pre, code, or any container holding text
            string targetSelector = "pre, code, div[data-attr]";

            Console.WriteLine("Waiting for element...");
            var element = await page.WaitForSelectorAsync(targetSelector, new WaitForSelectorOptions
            {
                Timeout = 15000 // 15 seconds timeout
            });

            response = await page.EvaluateFunctionAsync<string>("el => el.textContent", element);
            Console.WriteLine("Found Content:\n" + response);
        }
        catch (WaitTaskTimeoutException)
        {
            Console.WriteLine("\n[ERROR] Element timed out! Inspecting returned page state...");

            // Check if Google showed a CAPTCHA or altered layout
            string pageTitle = await page.GetTitleAsync();
            Console.WriteLine($"Page Title: {pageTitle}");

            response = await page.GetContentAsync();
            if (response.Contains("captcha") || response.Contains("Before you continue"))
            {
                Console.WriteLine("CAUSE: Google triggered a CAPTCHA / Consent page.");
            }
            else
            {
                Console.WriteLine("CAUSE: Element missing or class names changed. First 500 chars of HTML:");
                Console.WriteLine(response.Substring(0, Math.Min(500, response.Length)));
            }
        }

        // Pass fully rendered HTML to HtmlAgilityPack
        var doc = new HtmlDocument();
        doc.LoadHtml(response);

        var codeNode = doc.DocumentNode.SelectSingleNode("//pre/code");
        if (codeNode != null)
        {
            response = HttpUtility.HtmlDecode(codeNode.InnerText).Trim();
        }

        return response;
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
           
            result.Sector = SectorClassifier.GetMacroSector(result.Sector).ToString();
            
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
