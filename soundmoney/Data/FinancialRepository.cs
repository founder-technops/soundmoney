using Microsoft.EntityFrameworkCore;
using SoundMoney.Models;
using System.Text.Json;

namespace SoundMoney.Data
{
    /// <summary>
    /// A cached scrape result, deserialized back into usable objects, plus when it was
    /// scraped - the caller decides what counts as "fresh" from ScrapedAt. All four are
    /// null when there is no cache row for the symbol yet.
    /// </summary>
    public record CachedScrape(Financial? Current, List<Financial>? Historical, StockValuation? Summary, DateTime? ScrapedAt);

    public interface IFinancialRepository
    {
        // Save / Upsert Methods
        Task SaveValuationAsync(StockValuation valuation, CancellationToken ct = default);

        // Full-scrape cache (current + historical Financial, not just the lightweight
        // StockValuation summary above) - see ScrapedFinancialCache for why this is a
        // separate table/pipeline from SaveValuationAsync/StockValuations.
        Task<CachedScrape> GetCachedScrapeAsync(string symbol, CancellationToken ct = default);
        Task SaveScrapedCacheAsync(string symbol, StockValuation stockValuation, Financial current, List<Financial> historical, CancellationToken ct = default);

        // Retrieval Methods - Single Entities
        Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default);

        // Retrieval Methods - Bulk / Querying
        Task<List<StockValuation>> GetValuationsBySectorAsync(string sector, CancellationToken ct = default);
        Task<List<StockValuation>> GetValuationsByVerdictAsync(string verdict, CancellationToken ct = default);
        Task<List<StockValuation>> GetAllValuationsAsync(CancellationToken ct = default);
        Task<StockValuation> GetPendingValuationsAsync(CancellationToken ct = default);
        // Delete Methods
        Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score);

    }
    public class FinancialRepository : IFinancialRepository
    {
        private readonly DataContext _context;

        public FinancialRepository(DataContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region Save / Upsert Operations

        public async Task SaveValuationAsync(StockValuation valuation, CancellationToken ct = default)
        {
            var existing = await _context.StockValuations
                .FirstOrDefaultAsync(v => v.Symbol == valuation.Symbol, ct);

            if (existing == null)
            {
                await _context.StockValuations.AddAsync(valuation, ct);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(valuation);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task SaveScrapedCacheAsync(string symbol, StockValuation stockValuation, Financial current, List<Financial> historical, CancellationToken ct = default)
        {
            string cleanSymbol = symbol.ToUpperInvariant();

            var entity = new ScrapedFinancialCache
            {
                Symbol = cleanSymbol,
                StockValuationJson = JsonSerializer.Serialize(stockValuation),
                CurrentFinancialJson = JsonSerializer.Serialize(current),
                HistoricalFinancialsJson = JsonSerializer.Serialize(historical),
                ScrapedAt = DateTime.Now
            };

            var existing = await _context.ScrapedFinancialCaches
                .FirstOrDefaultAsync(c => c.Symbol == cleanSymbol, ct);

            if (existing == null)
            {
                await _context.ScrapedFinancialCaches.AddAsync(entity, ct);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(entity);
            }

            await _context.SaveChangesAsync(ct);
        }

        #endregion

        #region Retrieval Operations

        public async Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Symbol == symbol.ToUpperInvariant(), ct);
        }

        public async Task<CachedScrape> GetCachedScrapeAsync(string symbol, CancellationToken ct = default)
        {
            var entity = await _context.ScrapedFinancialCaches
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Symbol == symbol.ToUpperInvariant(), ct);

            if (entity == null)
            {
                return new CachedScrape(null, null, null, null);
            }

            try
            {
                var stockValuation = JsonSerializer.Deserialize<StockValuation>(entity.StockValuationJson);
                var current = JsonSerializer.Deserialize<Financial>(entity.CurrentFinancialJson);
                var historical = JsonSerializer.Deserialize<List<Financial>>(entity.HistoricalFinancialsJson);
                return new CachedScrape(current, historical, stockValuation, entity.ScrapedAt);
            }
            catch (JsonException)
            {
                // A cache row that doesn't deserialize cleanly (e.g. written by an older
                // version of Financial/StockValuation before a field was added/renamed)
                // should be treated as a cache miss, not a crash - the caller falls back
                // to a live scrape exactly as if this symbol had never been cached.
                return new CachedScrape(null, null, null, null);
            }
        }



        public async Task<List<StockValuation>> GetValuationsBySectorAsync(string sector, CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .Where(v => v.Sector.Equals(sector, StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.Symbol)
                .ToListAsync(ct);
        }

        public async Task<List<StockValuation>> GetValuationsByVerdictAsync(string verdict, CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .Where(v => v.Verdict.Equals(verdict, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.MarginOfSafety)
                .ToListAsync(ct);
        }

        public async Task<List<StockValuation>> GetAllValuationsAsync(CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .OrderBy(v => v.Symbol)
                .ToListAsync(ct);
        }

        public async Task<StockValuation> GetPendingValuationsAsync(CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                //.Where(v => v.Symbol == "MILKYMIST").FirstOrDefaultAsync(cancellationToken: ct);
                .FirstOrDefaultAsync(v => (v.UpdatedAt == null || v.UpdatedAt < DateTime.Today), cancellationToken: ct);
        }

        /// <summary>
        /// Retrieve stocks filtered by margin of safety and optional sector.
        /// </summary>
        public async Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score)
        {
            try
            {
                var query = _context.StockValuations
                    .Where(v => v.FetchedAt != null);

                if (minMarginOfSafety != 0)
                {
                    query = query.Where(s => s.MarginOfSafety >= minMarginOfSafety);
                }

                if (searchQuery is not null)
                {
                    query = query.Where(s => s.Symbol.Contains(searchQuery) || s.CompanyName.Contains(searchQuery));
                }

                if (score is not null)
                {
                    query = query.Where(s => score.Contains(s.SoundScoreRating));
                }

                return await query
                    .AsNoTracking()
                    .OrderByDescending(s => s.SoundScore)
                    .ThenByDescending(s => s.MarginOfSafety)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                return new List<StockValuation>();
            }
        }


        #endregion


    }
}