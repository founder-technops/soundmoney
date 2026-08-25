using HtmlAgilityPack;
using SoundMoney.Data;
using SoundMoney.Models;
using System.Globalization;

namespace SoundMoney.Services
{
    public interface IScraperService
    {
        Task<(StockValuation?, DeepFinancial?, List<HistoricalFinancial>?)> ScrapeStockAsync(string symbol, CancellationToken ct = default);
    }

    public class ScraperService : IScraperService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(HttpClient httpClient, ILogger<ScraperService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            }
        }

        public async Task<(StockValuation?, DeepFinancial?, List<HistoricalFinancial>?)> ScrapeStockAsync(string symbol, CancellationToken ct = default)
        {
            string cleanSymbol = symbol.Trim().ToUpperInvariant();
            _logger.LogInformation("Starting scraping for symbol: {Symbol}", cleanSymbol);

            try
            {
                string html = await FetchHtmlContentAsync(cleanSymbol, ct);
                if (string.IsNullOrWhiteSpace(html))
                {
                    _logger.LogWarning("Failed to retrieve HTML for symbol: {Symbol}", cleanSymbol);
                    return (null, null, null);
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var companyName = ExtractCompanyName(doc, cleanSymbol);
                var sector = ExtractSector(doc);

                var stockValuation = new StockValuation
                {
                    Symbol = cleanSymbol,
                    CompanyName = companyName,
                    Sector = sector,
                };

                var deepFinancial = ExtractDeepFinancial(doc, cleanSymbol);
                var historicalFinancials = ExtractHistoricalFinancials(doc, deepFinancial, cleanSymbol);

                SectorCategory sectormap = SectorMapper.Map(sector);
                deepFinancial.IsFinancialSector = sectormap == SectorCategory.Banking || sectormap == SectorCategory.FinancialServices;

                _logger.LogInformation("Successfully scraped financial data for {Symbol}. Extracted {Count} historical records.", cleanSymbol, historicalFinancials.Count);
                return (stockValuation, deepFinancial, historicalFinancials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while scraping data for symbol: {Symbol}", cleanSymbol);
                return (null, null, null);
            }
        }

        #region HTTP Parsing Logic

        private async Task<string> FetchHtmlContentAsync(string symbol, CancellationToken ct)
        {
            string consolidatedUrl = $"https://www.screener.in/company/{symbol}/consolidated/";
            var response = await _httpClient.GetAsync(consolidatedUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                string standaloneUrl = $"https://www.screener.in/company/{symbol}/";
                response = await _httpClient.GetAsync(standaloneUrl, ct);
            }

            if (!response.IsSuccessStatusCode) return string.Empty;
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
            var subSectorNode = doc.DocumentNode
                .SelectSingleNode("//a[contains(@href, '/market/') and (@title='Industry' or @title='Sub Sector')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/company/compare/')]");

            if (subSectorNode != null)
                return System.Net.WebUtility.HtmlDecode(subSectorNode.InnerText.Trim());

            var broadNode = doc.DocumentNode
                .SelectSingleNode("//a[contains(@href, '/market/') and @title='Broad Sector']")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/market/IN') and not(contains(@href, '/IN'))]");

            if (broadNode != null)
                return System.Net.WebUtility.HtmlDecode(broadNode.InnerText.Trim());

            return "General";
        }

        #endregion

        #region Financial Extraction Logic

        private DeepFinancial ExtractDeepFinancial(HtmlDocument doc, string symbol)
        {
            var df = new DeepFinancial { Symbol = symbol };

            // A. Top Ratios
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
                    else if (name.Contains("ROE", StringComparison.OrdinalIgnoreCase)) df.ReportedRoePercent = val;
                    else if (name.Contains("Face Value", StringComparison.OrdinalIgnoreCase)) df.FaceValue = val;
                    else if (name.Contains("Dividend Yield", StringComparison.OrdinalIgnoreCase)) df.DividendYieldPercent = val;
                    else if (name.Contains("Pledged", StringComparison.OrdinalIgnoreCase)) df.PromoterPledgePercent = val;
                }
            }

            // Fallback: If Promoter Pledge wasn't in top ratios, extract from Shareholding Pattern
            if (df.PromoterPledgePercent == 0m)
            {
                df.PromoterPledgePercent = ExtractPromoterPledgeFromShareholding(doc);
            }

            // Fallback 2: Extract from Shareholding section if Pros & Cons didn't mention it
            if (df.PromoterPledgePercent == 0m)
            {
                df.PromoterPledgePercent = ExtractPromoterPledgeFromProsAndCons(doc);
            }

            if (df.CurrentPrice > 0m && df.MarketCapCr > 0m)
            {
                df.TotalSharesCr = df.MarketCapCr / df.CurrentPrice;
            }

            // B. Profit & Loss Section
            var pnlSection = doc.DocumentNode.SelectSingleNode("//section[@id='profit-loss']");
            if (pnlSection != null)
            {
                df.RevenueCr = GetLastCellRowValue(pnlSection, "Sales");
                df.OperatingProfitEbitdaCr = GetLastCellRowValue(pnlSection, "Operating Profit");
                if (df.OperatingProfitEbitdaCr == 0m)
                    df.OperatingProfitEbitdaCr = GetLastCellRowValue(pnlSection, "OP");

                df.InterestExpenseCr = Math.Abs(GetLastCellRowValue(pnlSection, "Interest"));
                df.DepreciationCr = Math.Abs(GetLastCellRowValue(pnlSection, "Depreciation"));
                df.ProfitBeforeTaxCr = GetLastCellRowValue(pnlSection, "Profit before tax");
                df.TaxPercent = GetLastCellRowValue(pnlSection, "Tax %");
                df.NetProfitCr = GetLastCellRowValue(pnlSection, "Net Profit");
                df.DividendPayoutPercent = GetLastCellRowValue(pnlSection, "Dividend Payout");

                df.EbitCr = df.OperatingProfitEbitdaCr - df.DepreciationCr;
            }

            // C. Balance Sheet Section
            var bsSection = doc.DocumentNode.SelectSingleNode("//section[@id='balance-sheet']");
            if (bsSection != null)
            {
                df.ShareCapitalCr = GetLastCellRowValue(bsSection, "Equity Capital");
                df.ReservesCr = GetLastCellRowValue(bsSection, "Reserves");
                df.TotalBorrowingsCr = Math.Abs(GetLastCellRowValue(bsSection, "Borrowings"));
                df.CurrentLiabilitiesCr = GetLastCellRowValue(bsSection, "Other Liabilities");
                df.TotalLiabilitiesCr = df.TotalBorrowingsCr + df.CurrentLiabilitiesCr;

                df.NetFixedAssetsCr = GetLastCellRowValue(bsSection, "Fixed Assets");
                df.CwipCr = GetLastCellRowValue(bsSection, "CWIP");
                df.InvestmentsCr = GetLastCellRowValue(bsSection, "Investments");
                df.IntangibleAssetsCr = GetLastCellRowValue(bsSection, "Intangible Assets");
                df.TotalAssetsCr = GetLastCellRowValue(bsSection, "Total Assets");

                decimal nonCurrentAssets = df.NetFixedAssetsCr + df.CwipCr + df.InvestmentsCr + df.IntangibleAssetsCr;
                df.CurrentAssetsCr = Math.Max(0m, df.TotalAssetsCr - nonCurrentAssets);
                df.TotalEquityCr = df.ShareCapitalCr + df.ReservesCr;
                df.WorkingCapitalCr = df.CurrentAssetsCr - df.CurrentLiabilitiesCr;

                // Actual cash component extraction or fallback
                decimal scrapedCash = GetLastCellRowValue(bsSection, "Cash & Equivalents");
                df.CashAndEquivalentsCr = scrapedCash > 0m ? scrapedCash : Math.Max(0m, df.CurrentAssetsCr * 0.20m);
                df.NetCashCr = df.CashAndEquivalentsCr - df.TotalBorrowingsCr;
            }

            // D. Cash Flow Section
            var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
            if (cfSection != null)
            {
                df.CashFromOperationsCr = GetLastCellRowValue(cfSection, "Cash from Operating Activity");
                df.CashFromInvestmentCr = GetLastCellRowValue(cfSection, "Cash from Investing Activity");
                df.CashFromFinanceCr = GetLastCellRowValue(cfSection, "Cash from Financing Activity");
                df.FreeCashFlowCr = GetLastCellRowValue(cfSection, "Free Cash Flow");
                df.GrossCapexCr = df.CashFromOperationsCr - df.FreeCashFlowCr;
            }

            return df;
        }

        /// <summary>
        /// Helper to extract Promoter Pledging from the Shareholding Pattern table if top ratios do not contain it.
        /// </summary>
        private decimal ExtractPromoterPledgeFromShareholding(HtmlDocument doc)
        {
            var shareholdingSection = doc.DocumentNode.SelectSingleNode("//section[@id='shareholding']");
            if (shareholdingSection == null) return 0m;

            // Search for rows containing "Pledged" or "Pledged percentage" inside shareholding tables
            var pledgeRow = shareholdingSection.SelectSingleNode(".//tr[td[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'pledged')]]");
            var lastCell = pledgeRow?.SelectNodes("td")?.LastOrDefault();

            if (lastCell != null)
            {
                string text = lastCell.InnerText.Trim().Replace(",", "").Replace("%", "");
                return ParseDecimal(text);
            }

            return 0m;
        }

        private decimal ExtractPromoterPledgeFromProsAndCons(HtmlDocument doc)
        {
            // Find the Cons container
            var consNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'cons')]");
            if (consNode == null) return 0m;

            // Look through all list items under Cons
            var bulletNodes = consNode.SelectNodes(".//ul/li");
            if (bulletNodes == null) return 0m;

            foreach (var li in bulletNodes)
            {
                string text = li.InnerText.Trim();

                // Screener typically writes: "Promoter pledge is 12.5%" or "Company has pledged 12.5% of its shares"
                if (text.Contains("pledge", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+(\.\d+)?)%");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal pledgedPct))
                    {
                        return pledgedPct;
                    }
                }
            }

            return 0m;
        }

        private List<HistoricalFinancial> ExtractHistoricalFinancials(HtmlDocument doc, DeepFinancial deepFinancial, string symbol)
        {
            var historyList = new List<HistoricalFinancial>();

            var pnlSection = doc.DocumentNode.SelectSingleNode("//section[@id='profit-loss']");
            var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
            var balanceSheetSection = doc.DocumentNode.SelectSingleNode("//section[@id='balance-sheet']");
            if (pnlSection == null && cfSection == null) return historyList;

            var headerCells = pnlSection?.SelectNodes(".//table[contains(@class, 'ranges-table') or contains(@class, 'table')]//thead//th");
            var yearHeaderList = new List<(int ColumnIndex, int Year)>();

            if (headerCells != null)
            {
                for (int i = 1; i < headerCells.Count; i++)
                {
                    string headerText = headerCells[i].InnerText.Trim();
                    int year = ExtractYearFromHeader(headerText);
                    if (year > 0)
                    {
                        yearHeaderList.Add((i, year));
                    }
                }
            }

            if (!yearHeaderList.Any())
            {
                int currentYear = DateTime.UtcNow.Year;
                for (int i = 0; i < 5; i++)
                {
                    yearHeaderList.Add((i + 1, currentYear - 4 + i));
                }
            }

            var revenueDict = GetRowValuesByColumn(pnlSection, "Sales");
            var netProfitDict = GetRowValuesByColumn(pnlSection, "Net Profit");
            var ocfDict = GetRowValuesByColumn(cfSection, "Cash from Operating Activity");
            var fcfDict = GetRowValuesByColumn(cfSection, "Free Cash Flow");
            var equityCapitalDict = GetRowValuesByColumn(balanceSheetSection, "Equity Capital");
            var DividendPayoutPercentDict = GetRowValuesByColumn(pnlSection, "Dividend Payout %");

            foreach (var header in yearHeaderList)
            {
                revenueDict.TryGetValue(header.ColumnIndex, out decimal rev);
                netProfitDict.TryGetValue(header.ColumnIndex, out decimal netProfit);
                ocfDict.TryGetValue(header.ColumnIndex, out decimal ocf);
                fcfDict.TryGetValue(header.ColumnIndex, out decimal fcf);
                equityCapitalDict.TryGetValue(header.ColumnIndex, out decimal equityCap);
                DividendPayoutPercentDict.TryGetValue(header.ColumnIndex, out decimal dividendPayoutPer);
                historyList.Add(new HistoricalFinancial
                {
                    Symbol = symbol,
                    Year = header.Year,
                    HistoricalRevenueCr = rev,
                    HistoricalNetProfitCr = netProfit,
                    HistoricalOcfCr = ocf,
                    HistoricalFcfCr = fcf,
                    HistoricalCapexCr = ocf - fcf,
                    EquityCapitalCr = equityCap,
                    DividendPayoutPercent = dividendPayoutPer,
                    HistoricalSharesCr = deepFinancial.FaceValue > 0m ? equityCap / deepFinancial.FaceValue : 0m,
                    HistoricalPatCr = netProfit
                });
            }

            return historyList;
        }

        #endregion

        #region Helper Methods

        private decimal GetLastCellRowValue(HtmlNode? sectionNode, string rowName)
        {
            if (sectionNode == null) return 0m;

            var rowNode = sectionNode.SelectSingleNode($".//tr[td[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{rowName.ToLowerInvariant()}')]]");
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

            var rowNode = sectionNode.SelectSingleNode($".//tr[td[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{rowName.ToLowerInvariant()}')]]");
            var cells = rowNode?.SelectNodes("td");

            if (cells != null)
            {
                for (int colIndex = 1; colIndex < cells.Count; colIndex++)
                {
                    string text = cells[colIndex].InnerText.Trim().Replace(",", "").Replace("%","");
                    result[colIndex] = ParseDecimal(text);
                }
            }

            return result;
        }

        private int ExtractYearFromHeader(string headerText)
        {
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