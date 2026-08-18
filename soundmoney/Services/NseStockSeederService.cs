using CsvHelper;
using CsvHelper.Configuration;
using Google;
using Microsoft.EntityFrameworkCore;
 
using SoundMoney.Data;
using SoundMoney.Models;
using System.ComponentModel.DataAnnotations;
using System.Formats.Asn1;
using System.Globalization;

namespace SoundMoney.Services
{
    public class CsvStock
    {
        [Key]
        public string Symbol { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string Series { get; set; } = null!;
        public string Isin { get; set; } = null!;
        public DateTime UpdatedAt { get; set; }
    }
    public sealed class NseStockCsvMap : ClassMap<CsvStock>
    {
        public NseStockCsvMap()
        {
            // Map CSV headers to the Stock entity properties
            Map(m => m.Symbol).Name("SYMBOL");
            Map(m => m.CompanyName).Name("NAME OF COMPANY");
            Map(m => m.Series).Name("SERIES");
            Map(m => m.Isin).Name("ISIN NUMBER");
        }
    }
    public class NseStockSeederService
    {
        private readonly DataContext _dbContext;
        private readonly HttpClient _httpClient;

        public NseStockSeederService(DataContext dbContext, HttpClient httpClient)
        {
            _dbContext = dbContext;
            _httpClient = httpClient;
        }

        public async Task SeedFromNseCsvAsync()
        {
            Stream csvStream;

            // Download directly from official NSE Archives
            const string nseCsvUrl = "https://archives.nseindia.com/content/equities/EQUITY_L.csv";

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            );

            var response = await _httpClient.GetAsync(nseCsvUrl);
            response.EnsureSuccessStatusCode();

            csvStream = await response.Content.ReadAsStreamAsync();


            using var reader = new StreamReader(csvStream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null // Ignore non-mapped fields gracefully
            });

            csv.Context.RegisterClassMap<NseStockCsvMap>();

            // Read all records from the CSV file
            var records = csv.GetRecords<CsvStock>()
                .Where(s => s.Series == "EQ") // Filter for standard equity shares only
                .ToList();

            var now = DateTime.UtcNow;

            // Fetch existing symbols from the DB to separate INSERT vs UPDATE operations
            var existingSymbols = await _dbContext.StockValuations
                .Select(s => s.Symbol)
                .ToListAsync();

            var newStocks = new List<StockValuation>();
            var updatedStocks = new List<StockValuation>();

            foreach (var stock in records)
            {
                stock.UpdatedAt = now;

                if (!existingSymbols.Contains(stock.Symbol))
                {
                    newStocks.Add(new StockValuation()
                    {
                        Symbol = stock.Symbol,
                        CompanyName = stock.CompanyName,
                        Sector = string.Empty,
                        CurrentPrice = 0m,
                        FetchedAt = DateTime.Now,
                        IntrinsicValue = 0m,
                        MarginOfSafety = 0m,
                        PrimaryMethod = string.Empty,
                        SecondaryMethod = string.Empty,
                        Verdict = string.Empty,
                        SoundScore = 0m,
                        SoundScoreRating = string.Empty,
                        UpdatedAt = DateTime.Now
                    });
                }

            }

            // Batch insert new symbols
            if (newStocks.Count > 0)
            {
                await _dbContext.StockValuations.AddRangeAsync(newStocks);
            }

            await _dbContext.SaveChangesAsync();
            await csvStream.DisposeAsync();
        }
    }
}
