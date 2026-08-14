using SoundMoney.Models;

namespace SoundMoney.Services.IntrinsicValue;

/// <summary>
/// IT/software services businesses are asset-light and earnings/growth
/// driven, so a growth-adjusted earnings multiple fits better than a
/// book-value approach. Uses Benjamin Graham's revised formula:
///     IV = EPS x (base + 2g) x 4.4 / Y
/// where g is expected annual growth (%) and Y is the current AAA
/// corporate bond yield (used to normalise for the prevailing interest
/// rate environment). The growth cap is set higher than for
/// manufacturing/cyclical sectors, reflecting this sector's historically
/// more durable growth.
/// </summary>
public class ItServicesValuationStrategy : IIntrinsicValueStrategy
{
    private const decimal AaaBondYield = 7.5m; // percent; update from RBI/CRISIL data periodically
    private const decimal GrowthCap = 18m;      // percent
    private const decimal BaseMultiple = 7m;    // slightly more conservative than classic Graham's 8.5

    public SectorCategory Sector => SectorCategory.InformationTechnology;

    public decimal Calculate(StockFundamentals f)
    {
        decimal g = Math.Min(f.EstimatedGrowthRate, GrowthCap);
        decimal iv = f.EPS * (BaseMultiple + 2m * g) * 4.4m / AaaBondYield;
        return Math.Round(Math.Max(iv, 0), 2);
    }
}
