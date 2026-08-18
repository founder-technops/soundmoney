using HtmlAgilityPack;
using SoundMoney.Data;
using SoundMoney.Models;
using System.Globalization;

namespace SoundMoney.Services
{
    public interface IScraperService
    {
        /// <summary>
        /// Scrapes stock data from Screener.in and saves/updates it in the database.
        /// </summary>
        /// <param name="symbol">Stock ticker symbol (e.g., RELIANCE, TCS, INFY)</param>
        /// <returns>True if scraping and persistence succeeded.</returns>
        Task<(StockValuation, DeepFinancial, List<HistoricalFinancial>)> ScrapeStockAsync(string symbol, CancellationToken ct = default);
    }
    public class ScraperService : IScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(
            HttpClient httpClient,
            ILogger<ScraperService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Set up realistic headers to prevent request blocking
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            }
        }

        public async Task<(StockValuation, DeepFinancial, List<HistoricalFinancial>)> ScrapeStockAsync(string symbol, CancellationToken ct = default)
        {
            string cleanSymbol = symbol.Trim().ToUpperInvariant();
            _logger.LogInformation("Starting scraping for symbol: {Symbol}", cleanSymbol);

            try
            {
                // 1. Fetch HTML from Screener.in (fallback to non-consolidated if needed)
                string html = await FetchHtmlContentAsync(cleanSymbol, ct);
                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogWarning("Failed to retrieve HTML for symbol: {Symbol}", cleanSymbol);
                    return (null, null, null);
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // 2. Extract Data into EF Core Entities
                var companyName = ExtractCompanyName(doc, cleanSymbol);
                var sector = ExtractSector(doc);

                var stockValuation = new StockValuation()
                {
                    Symbol = symbol,
                    CompanyName = companyName,
                    Sector = sector,
                };

                var deepFinancial = ExtractDeepFinancial(doc, cleanSymbol);
                var historicalFinancials = ExtractHistoricalFinancials(doc, cleanSymbol);

                // 3. Persist to Database atomically via Repository
                //await _repository.SaveCompleteValuationDataAsync(valuation, deepFinancial, historicalFinancials, ct);

                _logger.LogInformation("Successfully scraped and saved financial data for {Symbol}", cleanSymbol);
                return (stockValuation, deepFinancial, historicalFinancials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while scraping and saving data for symbol: {Symbol}", cleanSymbol);
                return (null, null,null);
            }
        }

        #region HTTP Parsing Logic

        private async Task<string> FetchHtmlContentAsync(string symbol, CancellationToken ct)
        {
            string consolidatedUrl = $"https://www.screener.in/company/{symbol}/consolidated/";
            var response = await _httpClient.GetAsync(consolidatedUrl, ct);

            // Fallback to standalone company page if consolidated view doesn't exist
            if (!response.IsSuccessStatusCode)
            {
                string standaloneUrl = $"https://www.screener.in/company/{symbol}/";
                response = await _httpClient.GetAsync(standaloneUrl, ct);
            }

            if (!response.IsSuccessStatusCode)
                return string.Empty;

            return await response.Content.ReadAsStringAsync(ct);
        }

        private string ExtractCompanyName(HtmlDocument doc, string fallbackSymbol)
        {
            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'show-from-tablet') or contains(@class, 'margin-0')]")
                         ?? doc.DocumentNode.SelectSingleNode("//h1");
            return titleNode?.InnerText?.Trim() ?? fallbackSymbol;
        }

        private string ExtractSector(HtmlDocument doc)
        {
            // 2. Sub-Sector / Industry: Links explicitly tagged with title="Industry" or title="Sub Sector",
            // or nested market paths (/market/INxx/INxxxx/...)
            var subSectorNode = doc.DocumentNode
                .SelectSingleNode("//a[contains(@href, '/market/') and (@title='Industry' or @title='Sub Sector')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/company/compare/')]");

            if (subSectorNode != null)
            {
               return System.Net.WebUtility.HtmlDecode(subSectorNode.InnerText.Trim());
            }

            // 1. Broad Sector: Links with title="Broad Sector" or single-level market links (/market/INxx/)
            var broadNode = doc.DocumentNode
                .SelectSingleNode("//a[contains(@href, '/market/') and @title='Broad Sector']")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/market/IN') and not(contains(@href, '/IN'))]");

            if (broadNode != null)
            {
                return System.Net.WebUtility.HtmlDecode(broadNode.InnerText.Trim());
            }

            return "General";
        }

        #endregion

        #region Financial Extraction Logic

        private DeepFinancial ExtractDeepFinancial(HtmlDocument doc, string symbol)
        {
            var df = new DeepFinancial { Symbol = symbol };

            // A. Top Ratios List
            var ratioNodes = doc.DocumentNode.SelectNodes("//ul[@id='top-ratios']/li");
            if (ratioNodes != null)
            {
                foreach (var li in ratioNodes)
                {
                    string name = li.SelectSingleNode(".//span[@class='name']")?.InnerText?.Trim() ?? "";
                    string valStr = li.SelectSingleNode(".//span[@class='number']")?.InnerText?.Trim().Replace(",", "") ?? "0";
                    decimal val = ParseDecimal(valStr);

                    if (name.Contains("Market Cap", StringComparison.OrdinalIgnoreCase)) df.MarketCapCr = val;
                    else if (name.Contains("Current Price", StringComparison.OrdinalIgnoreCase)) df.CurrentPrice = val;
                    else if (name.Contains("Book Value", StringComparison.OrdinalIgnoreCase)) df.BookValuePerShare = val;
                    else if (name.Contains("ROE", StringComparison.OrdinalIgnoreCase)) df.ReportedRoePercent = val / 100m; // Store as decimal fraction
                    else if (name.Contains("Dividend Yield", StringComparison.OrdinalIgnoreCase)) df.DividendYieldPercent = val / 100m;
                }
            }

            // Compute Total Shares Outstanding (in Crores)
            if (df.CurrentPrice > 0 && df.MarketCapCr > 0)
            {
                df.TotalSharesCr = df.MarketCapCr / df.CurrentPrice;
            }

            // B. Profit & Loss Section
            var pnlSection = doc.DocumentNode.SelectSingleNode("//section[@id='profit-loss']");
            if (pnlSection != null)
            {
                df.RevenueCr = GetLastCellRowValue(pnlSection, "Sales");

                // 1. Extract Operating Profit (EBITDA)
                decimal operatingProfit = GetLastCellRowValue(pnlSection, "Operating Profit");
                if (operatingProfit == 0m)
                {
                    // Fallback check if Screener abbreviates it as OP
                    operatingProfit = GetLastCellRowValue(pnlSection, "OP");
                }

                df.OperatingProfitEbitdaCr = operatingProfit;
                df.DepreciationCr = GetLastCellRowValue(pnlSection, "Depreciation");
                df.NetProfitCr = GetLastCellRowValue(pnlSection, "Net Profit");
                df.DividendPayoutPercent = GetLastCellRowValue(pnlSection, "Dividend Payout") / 100m;

                // 2. Derive EBIT (Operating Profit - Depreciation)
                df.EbitCr = df.OperatingProfitEbitdaCr - df.DepreciationCr;
            }

            // C. Balance Sheet Section
            var bsSection = doc.DocumentNode.SelectSingleNode("//section[@id='balance-sheet']");
            if (bsSection != null)
            {
                df.ShareCapitalCr = GetLastCellRowValue(bsSection, "Share Capital");
                df.ReservesCr = GetLastCellRowValue(bsSection, "Reserves");
                df.TotalBorrowingsCr = GetLastCellRowValue(bsSection, "Borrowings");

                decimal otherLiabilities = GetLastCellRowValue(bsSection, "Other Liabilities");
                df.TotalLiabilitiesCr = df.TotalBorrowingsCr + otherLiabilities;

                df.NetFixedAssetsCr = GetLastCellRowValue(bsSection, "Fixed Assets");
                df.CwipCr = GetLastCellRowValue(bsSection, "CWIP");
                df.CashAndEquivalentsCr = GetLastCellRowValue(bsSection, "Other Assets"); // Screener groups cash & investments under other assets
                df.IntangibleAssetsCr = GetLastCellRowValue(bsSection, "Intangible Assets");
                df.TotalAssetsCr = GetLastCellRowValue(bsSection, "Total Assets");

                df.TotalEquityCr = df.ShareCapitalCr + df.ReservesCr;

                // Calculate Net Cash for valuation algorithms (Cash - Debt)
                df.NetCashCr = df.CashAndEquivalentsCr - df.TotalBorrowingsCr;
            }

            // D. Cash Flow Section
            var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
            if (cfSection != null)
            {
                df.CashFromOperationsCr = GetLastCellRowValue(cfSection, "Cash from Operating Activity");
                df.GrossCapexCr = Math.Abs(GetLastCellRowValue(cfSection, "Fixed assets purchased"));
                df.FreeCashFlowCr = df.CashFromOperationsCr - df.GrossCapexCr;
            }

            return df;
        }

        private List<HistoricalFinancial> ExtractHistoricalFinancials(HtmlDocument doc, string symbol)
        {
            var historyList = new List<HistoricalFinancial>();

            var pnlSection = doc.DocumentNode.SelectSingleNode("//section[@id='profit-loss']");
            var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
            if (pnlSection == null && cfSection == null) return historyList;

            // 1. Parse Years from table header (e.g. Mar 2020, Mar 2021, Mar 2022...)
            var headerCells = pnlSection?.SelectNodes(".//table[contains(@class, 'ranges-table') or contains(@class, 'table')]//thead//th");
            var yearHeaderList = new List<(int ColumnIndex, int Year)>();

            if (headerCells != null)
            {
                for (int i = 1; i < headerCells.Count; i++) // Skip row label column (index 0)
                {
                    string headerText = headerCells[i].InnerText.Trim();
                    int year = ExtractYearFromHeader(headerText);
                    if (year > 0)
                    {
                        yearHeaderList.Add((i, year));
                    }
                }
            }

            // Fallback if the header parsing could not find dates: generate recent 5 consecutive years ending with current year
            if (!yearHeaderList.Any())
            {
                int currentYear = DateTime.UtcNow.Year;
                for (int i = 0; i < 5; i++)
                {
                    yearHeaderList.Add((i + 1, currentYear - 4 + i));
                }
            }

            // 2. Extract historical rows indexed by year
            var revenueDict = GetRowValuesByColumn(pnlSection, "Sales");
            var ocfDict = GetRowValuesByColumn(cfSection, "Cash from Operating Activity");
            var capexDict = GetRowValuesByColumn(cfSection, "Fixed assets purchased");

            foreach (var header in yearHeaderList)
            {
                revenueDict.TryGetValue(header.ColumnIndex, out decimal rev);
                ocfDict.TryGetValue(header.ColumnIndex, out decimal ocf);
                capexDict.TryGetValue(header.ColumnIndex, out decimal capex);

                historyList.Add(new HistoricalFinancial
                {
                    Symbol = symbol,
                    Year = header.Year,
                    HistoricalRevenueCr = rev,
                    HistoricalOcfCr = ocf,
                    HistoricalCapexCr = Math.Abs(capex)
                });
            }

            return historyList;
        }

        #endregion

        #region Helper Methods

        private decimal GetLastCellRowValue(HtmlNode sectionNode, string rowName)
        {
            if (sectionNode == null) return 0m;

            // Matches row containing the target label (case-insensitive substring match)
            var rowNode = sectionNode.SelectSingleNode($".//tr[td[contains(translate(normalize-space(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{rowName.ToLowerInvariant()}')]]");
            var lastCell = rowNode?.SelectNodes("td")?.LastOrDefault();

            if (lastCell != null)
            {
                string text = lastCell.InnerText.Trim().Replace(",", "").Replace("%", "");
                return ParseDecimal(text);
            }

            return 0m;
        }

        private Dictionary<int, decimal> GetRowValuesByColumn(HtmlNode? sectionNode, string rowName)
        {
            var result = new Dictionary<int, decimal>();
            if (sectionNode == null) return result;

            var rowNode = sectionNode.SelectSingleNode($".//tr[td[contains(normalize-space(), '{rowName}')]]");
            var cells = rowNode?.SelectNodes("td");

            if (cells != null)
            {
                for (int colIndex = 1; colIndex < cells.Count; colIndex++)
                {
                    string text = cells[colIndex].InnerText.Trim().Replace(",", "");
                    result[colIndex] = ParseDecimal(text);
                }
            }

            return result;
        }

        private int ExtractYearFromHeader(string headerText)
        {
            // Handles "Mar 2024", "TTM", "2024", etc.
            var match = System.Text.RegularExpressions.Regex.Match(headerText, @"\b(20\d{2})\b");
            if (match.Success && int.TryParse(match.Value, out int yr))
            {
                return yr;
            }
            return 0;
        }

        private decimal ParseDecimal(string text)
        {
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }
            return 0m;
        }

        #endregion
    }
}
