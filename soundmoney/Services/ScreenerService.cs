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
        private readonly IValuationRepository _valuationRepo;
        private readonly ILogger<ScreenerService> _logger;
        public ScreenerService(
        IValuationRepository valuationRepo,
        ILogger<ScreenerService> logger)
        {
            _valuationRepo = valuationRepo;
            _logger = logger;
        }
        public async Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter)
        {
            var symbols = await _valuationRepo.GetByFilterAsync(minMarginOfSafety, sectorFilter);
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
