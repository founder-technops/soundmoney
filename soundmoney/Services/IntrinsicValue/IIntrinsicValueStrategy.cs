using SoundMoney.Models;

namespace SoundMoney.Services.IntrinsicValue;

/// <summary>
/// Each sector gets its own valuation approach because a single formula
/// (e.g. plain P/E based Graham) misprices sectors with very different
/// economics -- a bank's value comes from book value + ROE spread, not
/// free-cash-flow growth; an IT services firm is asset-light and
/// earnings/growth driven, etc.
/// </summary>
public interface IIntrinsicValueStrategy
{
    SectorCategory Sector { get; }
    decimal Calculate(StockFundamentals fundamentals);
}
