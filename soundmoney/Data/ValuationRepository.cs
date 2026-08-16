using Microsoft.EntityFrameworkCore;
using SoundMoney.Models;

namespace SoundMoney.Data;

/// <summary>
/// Repository service for StockValuation database operations.
/// Handles CRUD operations and queries for the screener.
/// </summary>
public interface IValuationRepository
{
    Task<StockValuation?> GetBySymbolAsync(string symbol);
    Task<List<StockValuation>> GetAllAsync();
    Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter);
    Task AddOrUpdateAsync(StockValuation stock);
    Task DeleteAsync(string symbol);
    Task DeleteAllAsync();
    Task<int> SaveChangesAsync();
}

public class ValuationRepository : IValuationRepository
{
    private readonly DataContext _context;
    private readonly ILogger<ValuationRepository> _logger;

    public ValuationRepository(DataContext context, ILogger<ValuationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<string>> GetAllSymbolsAsync()
    {
        try
        {
            return await _context.StockValuations
                .AsNoTracking()
                .OrderBy(s => s.Symbol)
                .Select(s => s.Symbol)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all symbols from database");
            return new List<string>();
        }
    }

    /// <summary>
    /// Retrieve a single stock by symbol.
    /// </summary>
    public async Task<StockValuation?> GetBySymbolAsync(string symbol)
    {
        try
        {
            return await _context.StockValuations
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Symbol.ToUpper() == symbol.ToUpper());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stock {Symbol} from database", symbol);
            return null;
        }
    }

    /// <summary>
    /// Retrieve all stocks from the database.
    /// </summary>
    public async Task<List<StockValuation>> GetAllAsync()
    {
        try
        {
            return await _context.StockValuations
                .AsNoTracking()
                .OrderByDescending(s => s.MarginOfSafety)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all stocks from database");
            return new List<StockValuation>();
        }
    }

    /// <summary>
    /// Retrieve stocks filtered by margin of safety and optional sector.
    /// </summary>
    public async Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter)
    {
        try
        {
            var query = _context.StockValuations
                .Where(s => s.MarginOfSafety >= minMarginOfSafety);

            if (sectorFilter is not null)
            {
                var sectorName = sectorFilter.ToString();
                query = query.Where(s => s.Sector == sectorName);
            }

            return await query
                .AsNoTracking()
                .OrderByDescending(s => s.MarginOfSafety)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filtering stocks from database");
            return new List<StockValuation>();
        }
    }

    /// <summary>
    /// Add or update a stock record.
    /// </summary>
    public async Task AddOrUpdateAsync(StockValuation stock)
    {
        try
        {
            var existing = await _context.StockValuations
                .FirstOrDefaultAsync(s => s.Symbol.ToUpper() == stock.Symbol.ToUpper());

            if (existing is not null)
            {
                // Update existing record
                existing.CompanyName = stock.CompanyName;
                existing.CurrentPrice = stock.CurrentPrice;
                existing.Sector = stock.Sector;
                existing.IntrinsicMethod = stock.IntrinsicMethod;
                existing.IntrinsicValue = stock.IntrinsicValue;
                existing.MarginOfSafety = stock.MarginOfSafety;
                existing.Verdict = stock.Verdict;
                existing.FetchedAt = stock.FetchedAt;
                existing.UpdatedAt = DateTime.Now;

                _context.StockValuations.Update(existing);
                _logger.LogInformation("Updated stock record for {Symbol}", stock.Symbol);
            }
            else
            {
                // Add new record
                stock.FetchedAt = DateTime.Now;
                await _context.StockValuations.AddAsync(stock);
                _logger.LogInformation("Added new stock record for {Symbol}", stock.Symbol);
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding/updating stock {Symbol} in database", stock.Symbol);
            throw;
        }
    }

    /// <summary>
    /// Delete a single stock record by symbol.
    /// </summary>
    public async Task DeleteAsync(string symbol)
    {
        try
        {
            var stock = await _context.StockValuations
                .FirstOrDefaultAsync(s => s.Symbol.ToUpper() == symbol.ToUpper());

            if (stock is not null)
            {
                _context.StockValuations.Remove(stock);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted stock record for {Symbol}", symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stock {Symbol} from database", symbol);
            throw;
        }
    }

    /// <summary>
    /// Delete all stock records from the database.
    /// </summary>
    public async Task DeleteAllAsync()
    {
        try
        {
            await _context.StockValuations.ExecuteDeleteAsync();
            _logger.LogInformation("Deleted all stock records from database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all stocks from database");
            throw;
        }
    }

    /// <summary>
    /// Save all changes to the database.
    /// </summary>
    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }
}
