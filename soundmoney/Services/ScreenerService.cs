using SoundMoney.Data;
using SoundMoney.Models;
using System.Net.Http;

namespace SoundMoney.Services
{
    public interface IScreenerService
    {
        Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter);
    }
    public class ScreenerService :IScreenerService
    {
        private readonly IStockRepository _stockRepository;
        private readonly ILogger<ScreenerService> _logger;
        public ScreenerService(
        IStockRepository stockRepository,
        ILogger<ScreenerService> logger)
        {
            _stockRepository = stockRepository;
            _logger = logger;
        }
        public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter)
        {
            var symbols = await _stockRepository.GetByFilterAsync(minMarginOfSafety, sectorFilter);
            var screenRows = symbols.Select(s => StockValuationToScreenResultRow(s)).ToList();
            return screenRows;
        }

        /// <summary>
        /// Convert ScreenerResultRow to StockValuation for database storage.
        /// </summary>
        private static ScreenerResultRow StockValuationToScreenResultRow(StockValuation value)
        {
            return new ScreenerResultRow
            {
                Symbol = value.Symbol,
                CompanyName = value.CompanyName,
                Sector = SectorMapper.Map(value.Sector),
                CurrentPrice = value.CurrentPrice,
                IntrinsicValue = value.IntrinsicValue,
                MarginOfSafetyPercent = value.MarginOfSafety,
                Verdict = value.Verdict
            };
        }
    }
}
