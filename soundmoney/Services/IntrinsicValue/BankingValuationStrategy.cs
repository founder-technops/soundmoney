using SoundMoney.Models;

namespace SoundMoney.Services.IntrinsicValue;

/// <summary>
/// Banks/NBFCs are levered balance sheets whose earnings quality is hard
/// to read from EPS alone (provisioning, NPA cycles, leverage). The
/// standard practitioner approach is a "justified Price/Book" multiple
/// derived from the excess-return model:
///     Justified P/B = (ROE - g) / (CostOfEquity - g)
///     Intrinsic Value = BookValuePerShare x Justified P/B
/// A bank earning above its cost of equity deserves a premium to book;
/// one earning below it deserves a discount.
/// </summary>
public class BankingValuationStrategy : IIntrinsicValueStrategy
{
    private const decimal DefaultCostOfEquity = 13m; // percent; tune to current risk-free + equity risk premium
    private const decimal GrowthCap = 12m;            // percent; sustainable growth ceiling for lenders

    public SectorCategory Sector => SectorCategory.Banking;

    public decimal Calculate(StockFundamentals f)
    {
        decimal roe = f.ROE / 100m;
        decimal g = Math.Min(f.EstimatedGrowthRate, GrowthCap) / 100m;
        decimal costOfEquity = (f.RequiredRateOfReturn > 0 ? f.RequiredRateOfReturn : DefaultCostOfEquity) / 100m;

        if (costOfEquity <= g) costOfEquity = g + 0.02m; // guard against divide-by-zero/negative spread

        decimal justifiedPB = (roe - g) / (costOfEquity - g);
        if (justifiedPB < 0) justifiedPB = 0.5m; // floor for a distressed/loss-making lender

        return Math.Round(f.BookValuePerShare * justifiedPB, 2);
    }
}
