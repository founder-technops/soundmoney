using Microsoft.EntityFrameworkCore;
using SoundMoney.Models;

namespace SoundMoney.Data
{
    public interface IFinancialRepository
    {
        // Save / Upsert Methods
        Task SaveValuationAsync(StockValuation valuation, CancellationToken ct = default);
        Task SaveDeepFinancialAsync(DeepFinancial financial, CancellationToken ct = default);
        Task SaveHistoricalFinancialsAsync(IEnumerable<HistoricalFinancial> historicalList, CancellationToken ct = default);
        Task SaveCompleteFinancialDataAsync(DeepFinancial deepFinancial, IEnumerable<HistoricalFinancial> historicalList, CancellationToken ct = default);

        // Retrieval Methods - Single Entities
        Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default);
        Task<DeepFinancial?> GetDeepFinancialBySymbolAsync(string symbol, CancellationToken ct = default);
        Task<List<HistoricalFinancial>> GetHistoricalFinancialsBySymbolAsync(string symbol, CancellationToken ct = default);

        // Retrieval Methods - Bulk / Querying
        Task<List<StockValuation>> GetValuationsBySectorAsync(string sector, CancellationToken ct = default);
        Task<List<StockValuation>> GetValuationsByVerdictAsync(string verdict, CancellationToken ct = default);
        Task<List<StockValuation>> GetAllValuationsAsync(CancellationToken ct = default);

        // Delete Methods
        Task<bool> DeleteFinancialDataBySymbolAsync(string symbol, CancellationToken ct = default);
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

        public async Task SaveDeepFinancialAsync(DeepFinancial financial, CancellationToken ct = default)
        {
            var existing = await _context.DeepFinancials
                .FirstOrDefaultAsync(df => df.Symbol == financial.Symbol, ct);

            if (existing == null)
            {
                await _context.DeepFinancials.AddAsync(financial, ct);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(financial);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task SaveHistoricalFinancialsAsync(IEnumerable<HistoricalFinancial> historicalList, CancellationToken ct = default)
        {
            foreach (var item in historicalList)
            {
                var existing = await _context.HistoricalFinancials
                    .FirstOrDefaultAsync(h => h.Symbol == item.Symbol && h.Year == item.Year, ct);

                if (existing == null)
                {
                    await _context.HistoricalFinancials.AddAsync(item, ct);
                }
                else
                {
                    _context.Entry(existing).CurrentValues.SetValues(item);
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Saves valuation metrics, deep financials, and historical entries atomically inside a single transaction.
        /// </summary>
        public async Task SaveCompleteFinancialDataAsync(
            DeepFinancial deepFinancial,
            IEnumerable<HistoricalFinancial> historicalList,
            CancellationToken ct = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                // 1. Upsert DeepFinancial
                var existingDeep = await _context.DeepFinancials
                    .FirstOrDefaultAsync(df => df.Symbol == deepFinancial.Symbol, ct);
                if (existingDeep == null)
                    await _context.DeepFinancials.AddAsync(deepFinancial, ct);
                else
                    _context.Entry(existingDeep).CurrentValues.SetValues(deepFinancial);

                // 2. Upsert HistoricalFinancials
                foreach (var hItem in historicalList)
                {
                    var existingHist = await _context.HistoricalFinancials
                        .FirstOrDefaultAsync(h => h.Symbol == hItem.Symbol && h.Year == hItem.Year, ct);
                    if (existingHist == null)
                        await _context.HistoricalFinancials.AddAsync(hItem, ct);
                    else
                        _context.Entry(existingHist).CurrentValues.SetValues(hItem);
                }

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        #endregion

        #region Retrieval Operations

        public async Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            return await _context.StockValuations
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Symbol == symbol.ToUpperInvariant(), ct);
        }

        public async Task<DeepFinancial?> GetDeepFinancialBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            return await _context.DeepFinancials
                .AsNoTracking()
                .FirstOrDefaultAsync(df => df.Symbol == symbol.ToUpperInvariant(), ct);
        }

        public async Task<List<HistoricalFinancial>> GetHistoricalFinancialsBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            return await _context.HistoricalFinancials
                .AsNoTracking()
                .Where(h => h.Symbol == symbol.ToUpperInvariant())
                .OrderByDescending(h => h.Year)
                .ToListAsync(ct);
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

        #endregion

        #region Delete Operations

        public async Task<bool> DeleteFinancialDataBySymbolAsync(string symbol, CancellationToken ct = default)
        {
            string upperSymbol = symbol.ToUpperInvariant();

            var deep = await _context.DeepFinancials.FirstOrDefaultAsync(df => df.Symbol == upperSymbol, ct);
            var historicals = await _context.HistoricalFinancials.Where(h => h.Symbol == upperSymbol).ToListAsync(ct);

            if (deep == null && !historicals.Any())
                return false;

            if (deep != null) _context.DeepFinancials.Remove(deep);
            if (historicals.Any()) _context.HistoricalFinancials.RemoveRange(historicals);

            await _context.SaveChangesAsync(ct);
            return true;
        }

        #endregion
    }
}
