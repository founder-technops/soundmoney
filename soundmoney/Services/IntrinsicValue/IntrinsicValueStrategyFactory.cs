using SoundMoney.Models;

namespace SoundMoney.Services.IntrinsicValue;

public interface IIntrinsicValueStrategyFactory
{
    IIntrinsicValueStrategy GetStrategy(SectorCategory sector);
}

public class IntrinsicValueStrategyFactory : IIntrinsicValueStrategyFactory
{
    private readonly Dictionary<SectorCategory, IIntrinsicValueStrategy> _strategies;

    public IntrinsicValueStrategyFactory()
    {
        // Sectors not listed here fall through to DefaultGrahamStrategy,
        // parameterised per-sector (see its GrowthCaps dictionary).
        _strategies = new Dictionary<SectorCategory, IIntrinsicValueStrategy>
        {
            [SectorCategory.Banking] = new BankingValuationStrategy(),
            [SectorCategory.InformationTechnology] = new ItServicesValuationStrategy(),
        };
    }

    public IIntrinsicValueStrategy GetStrategy(SectorCategory sector) =>
        _strategies.TryGetValue(sector, out var strategy) ? strategy : new DefaultGrahamStrategy(sector);
}
