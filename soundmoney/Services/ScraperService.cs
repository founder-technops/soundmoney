using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
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
        // Screener typically exposes ~10-12 years of P&L/cash-flow history. Below this we
        // treat the cumulative-cash-flow-derived balance as too short a window to trust as
        // an absolute figure, especially for companies far older than the scraped window.
        private const int MinReliableCashHistoryYears = 10;

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
                var sectorCategory = SectorClassifier.GetMacroSector(sector);

                var stockValuation = new StockValuation
                {
                    Symbol = cleanSymbol,
                    CompanyName = companyName,
                    Sector = sector,
                };
                
                var deepFinancial = ExtractDeepFinancial(doc, cleanSymbol);

                deepFinancial.IsFinancialSector = sectorCategory == MacroSector.FinancialServices;
                deepFinancial.IsCoreInvestmentCompanyExplicit = sectorCategory == MacroSector.FinancialServices
                    && (sector.Equals("Holding", StringComparison.OrdinalIgnoreCase) ||
                        sector.Equals("Investment", StringComparison.OrdinalIgnoreCase));

                // Extract Cash & Cash Equivalents time-series (API Schedule Primary, CF Roll-Forward Fallback)
                var cashTimeSeries = await ExtractCashAndEquivalentsAsync(cleanSymbol, doc, ct);

                // Map cash balance into DeepFinancial latest reporting period
                if (cashTimeSeries.Count > 0)
                {
                    deepFinancial.CashAndEquivalentsCr = cashTimeSeries.Values.LastOrDefault();
                }

                // Screener's balance sheet has no dedicated Cash & Equivalents line (it's
                // folded into Other Assets), so the figure above is a roll-forward of net
                // cash flow starting from an assumed zero balance in the earliest scraped
                // year. That's a reasonable estimate for a company whose full operating
                // history fits in the scraped window, but it systematically understates
                // cash for older companies where the scraped window starts well after
                // incorporation/IPO. Flag low-confidence years so downstream leverage and
                // valuation logic can fall back to gross-debt-based checks instead of
                // trusting NetCashCr outright.
                deepFinancial.CashHistoryYears = cashTimeSeries.Count;
                deepFinancial.IsCashEstimateReliable = cashTimeSeries.Count >= MinReliableCashHistoryYears;

                var historicalFinancials = ExtractHistoricalFinancials(doc, deepFinancial, cleanSymbol, cashTimeSeries);

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

        #region Cash & Schedule Parsing Engine

        public async Task<Dictionary<int, decimal>> ExtractCashAndEquivalentsAsync(
            string symbol,
            HtmlDocument companyDoc,
            CancellationToken ct = default)
        {
            // Method 2: Fallback Cash Flow Statement Roll-Forward
            return DeriveCashFromCashFlowRollForward(companyDoc);
        }

        private Dictionary<int, decimal> DeriveCashFromCashFlowRollForward(HtmlDocument doc)
        {
            var result = new Dictionary<int, decimal>();

            try
            {
                var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
                if (cfSection == null) return result;

                var headerCells = cfSection.SelectNodes(".//thead//th");
                if (headerCells == null || headerCells.Count <= 1) return result;

                var years = new List<int>();
                for (int i = 1; i < headerCells.Count; i++)
                {
                    years.Add(ExtractYearFromHeader(headerCells[i].InnerText));
                }

                var ocfDict = GetRowValuesByColumn(cfSection, "Cash from Operating Activity");
                var icfDict = GetRowValuesByColumn(cfSection, "Cash from Investing Activity");
                var fcfDict = GetRowValuesByColumn(cfSection, "Cash from Financing Activity");

                decimal runningCash = 0m;
                for (int i = 0; i < years.Count; i++)
                {
                    int colIdx = i + 1;
                    int year = years[i];

                    ocfDict.TryGetValue(colIdx, out decimal ocf);
                    icfDict.TryGetValue(colIdx, out decimal icf);
                    fcfDict.TryGetValue(colIdx, out decimal fcf);

                    decimal netCashFlow = ocf + icf + fcf;
                    runningCash += netCashFlow;

                    if (year > 0)
                    {
                        result[year] = runningCash;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Cash Flow Roll-Forward logic.");
            }

            return result;
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
                    else if (name.Contains("Stock P/E", StringComparison.OrdinalIgnoreCase)) df.ReportedPePercent = val;
                    else if (name.Contains("Book Value", StringComparison.OrdinalIgnoreCase)) df.BookValuePerShare = val;
                    else if (name.Contains("Dividend Yield", StringComparison.OrdinalIgnoreCase)) df.DividendYieldPercent = val;
                    else if (name.Contains("ROCE", StringComparison.OrdinalIgnoreCase)) df.ReportedRocePercent = val;
                    else if (name.Contains("ROE", StringComparison.OrdinalIgnoreCase)) df.ReportedRoePercent = val;
                    else if (name.Contains("Face Value", StringComparison.OrdinalIgnoreCase)) df.FaceValue = val;
                    else if (name.Contains("Pledged", StringComparison.OrdinalIgnoreCase)) df.PromoterPledgePercent = val;
                }
            }

            if (df.PromoterPledgePercent == 0m)
            {
                df.PromoterPledgePercent = ExtractPromoterPledgeFromShareholding(doc);
            }

            if (df.PromoterPledgePercent == 0m)
            {
                df.PromoterPledgePercent = ExtractPromoterPledgeFromProsAndCons(doc);
            }

            // B. Profit & Loss Section
            var pnlSection = doc.DocumentNode.SelectSingleNode("//section[@id='profit-loss']");
            if (pnlSection != null)
            {
                df.SalesCr = GetLastCellRowValue(pnlSection, "Sales");
                df.ExpenseCr = GetLastCellRowValue(pnlSection, "Expenses");
                df.OperatingProfitCr = GetLastCellRowValue(pnlSection, "Operating Profit");
                df.OtherIncomeCr = GetLastCellRowValue(pnlSection, "Other Income");
                df.InterestExpenseCr = Math.Abs(GetLastCellRowValue(pnlSection, "Interest"));
                df.DepreciationCr = Math.Abs(GetLastCellRowValue(pnlSection, "Depreciation"));
                df.ProfitBeforeTaxCr = GetLastCellRowValue(pnlSection, "Profit before tax");
                df.TaxPercent = GetLastCellRowValue(pnlSection, "Tax %");
                df.NetProfitCr = GetLastCellRowValue(pnlSection, "Net Profit");
                df.Eps = GetLastCellRowValue(pnlSection, "EPS in Rs");
                df.DividendPayoutPercent = GetLastCellRowValue(pnlSection, "Dividend Payout");
            }

            // C. Balance Sheet Section
            var bsSection = doc.DocumentNode.SelectSingleNode("//section[@id='balance-sheet']");
            if (bsSection != null)
            {
                df.ShareCapitalCr = GetLastCellRowValue(bsSection, "Equity Capital");
                df.ReservesCr = GetLastCellRowValue(bsSection, "Reserves");
                df.TotalBorrowingsCr = Math.Abs(GetLastCellRowValue(bsSection, "Borrowings"));
                df.OtherLiabilitiesCr = GetLastCellRowValue(bsSection, "Other Liabilities");

                df.NetFixedAssetsCr = GetLastCellRowValue(bsSection, "Fixed Assets");
                df.CwipCr = GetLastCellRowValue(bsSection, "CWIP");
                df.InvestmentsCr = GetLastCellRowValue(bsSection, "Investments");
                df.OtherAssetsCr = GetLastCellRowValue(bsSection, "Other Assets");
            }

            // D. Cash Flow Section
            var cfSection = doc.DocumentNode.SelectSingleNode("//section[@id='cash-flow']");
            if (cfSection != null)
            {
                df.CashFromOperationsCr = GetLastCellRowValue(cfSection, "Cash from Operating Activity");
                df.CashFromInvestmentCr = GetLastCellRowValue(cfSection, "Cash from Investing Activity");
                df.CashFromFinanceCr = GetLastCellRowValue(cfSection, "Cash from Financing Activity");
                df.FreeCashFlowCr = GetLastCellRowValue(cfSection, "Free Cash Flow");
            }

            return df;
        }

        private decimal ExtractPromoterPledgeFromShareholding(HtmlDocument doc)
        {
            var shareholdingSection = doc.DocumentNode.SelectSingleNode("//section[@id='shareholding']");
            if (shareholdingSection == null) return 0m;

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
            var consNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'cons')]");
            if (consNode == null) return 0m;

            var bulletNodes = consNode.SelectNodes(".//ul/li");
            if (bulletNodes == null) return 0m;

            foreach (var li in bulletNodes)
            {
                string text = li.InnerText.Trim();
                if (text.Contains("pledge", StringComparison.OrdinalIgnoreCase))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+(\.\d+)?)%");
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal pledgedPct))
                    {
                        return pledgedPct;
                    }
                }
            }

            return 0m;
        }

        private List<HistoricalFinancial> ExtractHistoricalFinancials(
            HtmlDocument doc,
            DeepFinancial deepFinancial,
            string symbol,
            Dictionary<int, decimal> cashTimeSeries)
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
            var opDict = GetRowValuesByColumn(pnlSection, "Operating Profit");
            var netProfitDict = GetRowValuesByColumn(pnlSection, "Net Profit");
            var ocfDict = GetRowValuesByColumn(cfSection, "Cash from Operating Activity");
            var fcfDict = GetRowValuesByColumn(cfSection, "Free Cash Flow");
            var equityCapitalDict = GetRowValuesByColumn(balanceSheetSection, "Equity Capital");
            var dividendPayoutPercentDict = GetRowValuesByColumn(pnlSection, "Dividend Payout %");

            foreach (var header in yearHeaderList)
            {
                revenueDict.TryGetValue(header.ColumnIndex, out decimal rev);
                opDict.TryGetValue(header.ColumnIndex, out decimal op);
                netProfitDict.TryGetValue(header.ColumnIndex, out decimal netProfit);
                ocfDict.TryGetValue(header.ColumnIndex, out decimal ocf);
                fcfDict.TryGetValue(header.ColumnIndex, out decimal fcf);
                equityCapitalDict.TryGetValue(header.ColumnIndex, out decimal equityCap);
                dividendPayoutPercentDict.TryGetValue(header.ColumnIndex, out decimal dividendPayoutPer);

                cashTimeSeries.TryGetValue(header.Year, out decimal cashAndEquiv);

                historyList.Add(new HistoricalFinancial
                {
                    Symbol = symbol,
                    Year = header.Year,
                    HistoricalRevenueCr = rev,
                    HistoricalOperatingProfitCr = op,
                    HistoricalNetProfitCr = netProfit,
                    HistoricalOcfCr = ocf,
                    HistoricalFcfCr = fcf,
                    HistoricalCapexCr = ocf - fcf,
                    EquityCapitalCr = equityCap,
                    DividendPayoutPercent = dividendPayoutPer,
                    HistoricalSharesCr = deepFinancial.FaceValue > 0m ? equityCap / deepFinancial.FaceValue : 0m,
                    HistoricalPatCr = netProfit,
                    HistoricalCashAndEquivalentsCr = cashAndEquiv
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
                    string text = cells[colIndex].InnerText.Trim().Replace(",", "").Replace("%", "");
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