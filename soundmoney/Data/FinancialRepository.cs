using Microsoft.EntityFrameworkCore;
using SoundMoney.Models;

namespace SoundMoney.Data
{
    public interface IFinancialRepository
    {
        // Save / Upsert Methods
        Task SaveValuationAsync(StockValuation valuation, CancellationToken ct = default);
        
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

        #endregion

        #region Retrieval Operations

        public async Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Symbol == symbol.ToUpperInvariant(), ct);
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
