using SoundMoney.Models;

namespace SoundMoney.Services;

/// <summary>
/// Service interface for running stock screener and getting results.
/// </summary>
public interface ISoundMoneyService
{
    Task<List<ScreenerResultRow>> RunScreenAsync(decimal minMarginOfSafety, SectorCategory? sectorFilter);
}
