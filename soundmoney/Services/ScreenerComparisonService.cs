using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using HtmlAgilityPack;

namespace SoundMoney.Services;

public interface IScreenerComparisonService
{
    Task<List<ScreenerCsvComparisonResult>> CompareCsvToLiveAsync(string csvPath, IEnumerable<string>? symbols = null, CancellationToken ct = default);
}

public sealed class ScreenerCsvComparisonResult
{
    public string Symbol { get; set; } = string.Empty;
    public string? CsvSector { get; set; }
    public string? LiveSector { get; set; }
    public decimal CsvCurrentPrice { get; set; }
    public decimal LiveCurrentPrice { get; set; }
    public decimal CsvIntrinsicValue { get; set; }
    public decimal CsvMarginOfSafety { get; set; }
    public string? CsvVerdict { get; set; }
    public decimal LivePe { get; set; }
    public decimal LiveRoe { get; set; }
    public decimal LiveRoce { get; set; }
    public decimal LiveDividendYield { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class ScreenerComparisonService : IScreenerComparisonService
{
    private readonly HttpClient _httpClient;

    public ScreenerComparisonService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        }
    }

    public async Task<List<ScreenerCsvComparisonResult>> CompareCsvToLiveAsync(string csvPath, IEnumerable<string>? symbols = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            throw new ArgumentException("CSV path is required.", nameof(csvPath));
        }

        var rows = ReadCsvRows(csvPath);
        var selectedSymbols = symbols is null
            ? rows.Select(r => r.Symbol).Distinct(StringComparer.OrdinalIgnoreCase)
            : symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

        var comparison = new List<ScreenerCsvComparisonResult>();

        foreach (var symbol in selectedSymbols)
        {
            var csvRow = rows.FirstOrDefault(r => string.Equals(r.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (csvRow is null)
            {
                continue;
            }

            var live = await FetchLiveMetricsAsync(symbol, ct);
            var result = new ScreenerCsvComparisonResult
            {
                Symbol = symbol,
                CsvSector = csvRow.Sector,
                LiveSector = live.Sector,
                CsvCurrentPrice = csvRow.CurrentPrice,
                LiveCurrentPrice = live.CurrentPrice,
                CsvIntrinsicValue = csvRow.IntrinsicValue,
                CsvMarginOfSafety = csvRow.MarginOfSafety,
                CsvVerdict = csvRow.Verdict,
                LivePe = live.Pe,
                LiveRoe = live.Roe,
                LiveRoce = live.Roce,
                LiveDividendYield = live.DividendYield,
                Notes = BuildComparisonNote(csvRow, live)
            };

            comparison.Add(result);
        }

        return comparison;
    }

    private static List<CsvValuationRow> ReadCsvRows(string csvPath)
    {
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();

        var rows = new List<CsvValuationRow>();
        while (csv.Read())
        {
            var row = new CsvValuationRow
            {
                Symbol = csv.GetField("Symbol") ?? string.Empty,
                CompanyName = csv.GetField("CompanyName") ?? string.Empty,
                Sector = csv.GetField("Sector") ?? string.Empty,
                PrimaryMethod = csv.GetField("PrimaryMethod") ?? string.Empty,
                SecondaryMethod = csv.GetField("SecondaryMethod") ?? string.Empty,
                CurrentPrice = ParseDecimal(csv.GetField("CurrentPrice")),
                IntrinsicValue = ParseDecimal(csv.GetField("IntrinsicValue")),
                MarginOfSafety = ParseDecimal(csv.GetField("MarginOfSafety")),
                DividendYieldPercent = ParseDecimal(csv.GetField("DividendYieldPercent")),
                Verdict = csv.GetField("Verdict") ?? string.Empty,
                SoundScore = ParseDecimal(csv.GetField("SoundScore")),
                SoundScoreRating = csv.GetField("SoundScoreRating") ?? string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(row.Symbol))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private async Task<LiveScreenerMetrics> FetchLiveMetricsAsync(string symbol, CancellationToken ct)
    {
        var url = $"https://www.screener.in/company/{Uri.EscapeDataString(symbol)}/consolidated/";
        var html = await _httpClient.GetStringAsync(url, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        string sector = ExtractSector(doc);
        decimal currentPrice = ExtractMetricValue(doc, "Current Price") ?? 0m;
        decimal pe = ExtractMetricValue(doc, "Stock P/E") ?? 0m;
        decimal roe = ExtractMetricValue(doc, "ROE") ?? 0m;
        decimal roce = ExtractMetricValue(doc, "ROCE") ?? 0m;
        decimal dividendYield = ExtractMetricValue(doc, "Dividend Yield") ?? 0m;

        return new LiveScreenerMetrics
        {
            Sector = sector,
            CurrentPrice = currentPrice,
            Pe = pe,
            Roe = roe,
            Roce = roce,
            DividendYield = dividendYield
        };
    }

    private static string ExtractSector(HtmlDocument doc)
    {
        var sectorNode = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/market/') and not(contains(@href, 'company'))]")
            ?? doc.DocumentNode.SelectSingleNode("//span[contains(normalize-space(.), 'Information Technology')]");

        if (sectorNode != null)
        {
            var text = HtmlEntity.DeEntitize(sectorNode.InnerText).Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1") ?? doc.DocumentNode.SelectSingleNode("//title");
        return titleNode is null ? string.Empty : HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
    }

    private static decimal? ExtractMetricValue(HtmlDocument doc, string metricName)
    {
        var pageText = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        var pattern = $"{Regex.Escape(metricName)}\\s*([0-9,]+(?:\\.[0-9]+)?)";
        var match = Regex.Match(pageText, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups[1].Value.Trim();
        return ParseDecimal(raw);
    }

    private static string BuildComparisonNote(CsvValuationRow csvRow, LiveScreenerMetrics live)
    {
        var differences = new List<string>();

        if (!string.Equals(csvRow.Sector, live.Sector, StringComparison.OrdinalIgnoreCase))
        {
            differences.Add($"sector mismatch: csv={csvRow.Sector}, live={live.Sector}");
        }

        if (csvRow.CurrentPrice > 0m && live.CurrentPrice > 0m)
        {
            var delta = ((live.CurrentPrice - csvRow.CurrentPrice) / csvRow.CurrentPrice) * 100m;
            differences.Add($"price diff={delta:F2}%");
        }

        if (csvRow.Verdict is not null && csvRow.Verdict.Length > 0 && live.Pe > 0m)
        {
            differences.Add($"live PE={live.Pe:F2}, live ROE={live.Roe:F2}%");
        }

        return differences.Count == 0 ? "live metrics aligned" : string.Join(" | ", differences);
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var normalized = value
            .Replace(",", string.Empty)
            .Replace("₹", string.Empty)
            .Replace("%", string.Empty)
            .Trim();

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return 0m;
    }

    private sealed class CsvValuationRow
    {
        public string Symbol { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string Sector { get; set; } = string.Empty;
        public string PrimaryMethod { get; set; } = string.Empty;
        public string SecondaryMethod { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal IntrinsicValue { get; set; }
        public decimal MarginOfSafety { get; set; }
        public decimal DividendYieldPercent { get; set; }
        public string Verdict { get; set; } = string.Empty;
        public decimal SoundScore { get; set; }
        public string SoundScoreRating { get; set; } = string.Empty;
    }

    private sealed class LiveScreenerMetrics
    {
        public string Sector { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal Pe { get; set; }
        public decimal Roe { get; set; }
        public decimal Roce { get; set; }
        public decimal DividendYield { get; set; }
    }
}
