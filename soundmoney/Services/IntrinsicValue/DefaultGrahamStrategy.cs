using SoundMoney.Models;

namespace SoundMoney.Services.IntrinsicValue;

/// <summary>
/// Used for sectors that are earnings-driven but don't warrant IT's
/// aggressive growth assumptions and aren't balance-sheet driven like
/// banking (FMCG, Pharma, Auto, Metals, Energy, Infrastructure, and any
/// unclassified "Other"). Blends two classic Graham approaches:
///   - Revised earnings formula:  EPS x (8.5 + 2g) x 4.4 / Y
///   - Graham Number (conservative, book-value anchored): sqrt(22.5 x EPS x BVPS)
/// Averaging the two means an overly optimistic growth input alone can't
/// blow out the valuation -- the book-value anchor pulls it back down.
/// Growth is capped per-sector to reflect realistic ceilings.
/// </summary>
public class DefaultGrahamStrategy : IIntrinsicValueStrategy
{
    private const decimal AaaBondYield = 7.5m; // percent; update periodically

    private static readonly Dictionary<SectorCategory, decimal> GrowthCaps = new()
    {
        [SectorCategory.FMCG] = 10m,
        [SectorCategory.Pharma] = 12m,
        [SectorCategory.Automobile] = 10m,
        [SectorCategory.Metals] = 8m,
        [SectorCategory.Energy] = 8m,
        [SectorCategory.Infrastructure] = 8m,
        [SectorCategory.FinancialServices] = 10m,
        [SectorCategory.Other] = 8m,
    };

    public SectorCategory Sector { get; }

    public DefaultGrahamStrategy(SectorCategory sector) => Sector = sector;

    public decimal Calculate(StockFundamentals f)
    {
        decimal cap = GrowthCaps.TryGetValue(Sector, out var c) ? c : 8m;
        decimal g = Math.Min(f.EstimatedGrowthRate, cap);

        decimal grahamRevised = f.EPS * (8.5m + 2m * g) * 4.4m / AaaBondYield;

        decimal grahamNumber = 0m;
        if (f.EPS > 0 && f.BookValuePerShare > 0)
            grahamNumber = (decimal)Math.Sqrt((double)(22.5m * f.EPS * f.BookValuePerShare));

        decimal blended = grahamNumber > 0 ? (grahamRevised + grahamNumber) / 2m : grahamRevised;
        return Math.Round(Math.Max(blended, 0), 2);
    }
}
