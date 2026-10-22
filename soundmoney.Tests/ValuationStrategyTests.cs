using SoundMoney.Algorithms;
using SoundMoney.Data;
using SoundMoney.Models;
using SoundMoney.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace soundmoney.Tests;

public class ValuationStrategyTests
{
    [Fact]
    public void WealthManagementAndAMC_UsesCashFlowDrivenMethodology()
    {
        var current = new Financial
        {
            Symbol = "360ONE",
            Sector = "Stockbroking & Allied",
            IsFinancialSector = true,
            CurrentPrice = 1084m,
            MarketCapCr = 405.22m,
            DividendYieldPercent = 1.11m,
            ReportedRocePercent = 26m,
            ReportedRoePercent = 18m,
            ShareCapitalCr = 40m,
            ReservesCr = 200m,
            TotalBorrowingsCr = 10m,
            OtherLiabilitiesCr = 50m,
            FixedAssetsCr = 30m,
            CwipCr = 5m,
            InvestmentsCr = 120m,
            OtherAssetsCr = 180m,
            CashAndEquivalentsCr = 60m,
            SalesCr = 500m,
            OperatingProfitCr = 120m,
            OtherIncomeCr = 20m,
            DepreciationCr = 15m,
            NetProfitCr = 90m,
            InterestExpenseCr = 5m,
            CashFromOperationsCr = 140m,
            CashFromInvestmentCr = -10m,
            CashFromFinanceCr = -30m,
            FreeCashFlowCr = 95m,
            DividendPayoutPercent = 35m,
            FaceValue = 1m
        };

        var historical = new List<Financial>
        {
            new() { Year = 2022, SalesCr = 420m, NetProfitCr = 72m, CashFromOperationsCr = 110m, FreeCashFlowCr = 70m, ShareCapitalCr = 35m, ReservesCr = 170m, TotalBorrowingsCr = 15m, OtherLiabilitiesCr = 45m, FixedAssetsCr = 25m, CwipCr = 4m, InvestmentsCr = 110m, OtherAssetsCr = 170m, CashAndEquivalentsCr = 62m, DepreciationCr = 12m, InterestExpenseCr = 6m, OperatingProfitCr = 96m, OtherIncomeCr = 18m, DividendPayoutPercent = 32m },
            new() { Year = 2023, SalesCr = 455m, NetProfitCr = 80m, CashFromOperationsCr = 128m, FreeCashFlowCr = 82m, ShareCapitalCr = 38m, ReservesCr = 180m, TotalBorrowingsCr = 12m, OtherLiabilitiesCr = 48m, FixedAssetsCr = 27m, CwipCr = 5m, InvestmentsCr = 112m, OtherAssetsCr = 175m, CashAndEquivalentsCr = 58m, DepreciationCr = 14m, InterestExpenseCr = 5m, OperatingProfitCr = 106m, OtherIncomeCr = 19m, DividendPayoutPercent = 33m },
            new() { Year = 2024, SalesCr = 490m, NetProfitCr = 88m, CashFromOperationsCr = 136m, FreeCashFlowCr = 90m, ShareCapitalCr = 40m, ReservesCr = 194m, TotalBorrowingsCr = 11m, OtherLiabilitiesCr = 50m, FixedAssetsCr = 28m, CwipCr = 4m, InvestmentsCr = 118m, OtherAssetsCr = 180m, CashAndEquivalentsCr = 60m, DepreciationCr = 15m, InterestExpenseCr = 5m, OperatingProfitCr = 118m, OtherIncomeCr = 20m, DividendPayoutPercent = 34m }
        };

        var methodology = ValuationStrategyResolver.ResolveMethodology(current, historical);

        Assert.Equal("Discounted Cash Flow (DCF)", methodology.PrimaryMethod);
        Assert.Equal("Price-to-Earnings (P/E) Multiple", methodology.SecondaryMethod);
    }

    [Fact]
    public void DistressTurnaroundRule_IgnoresOneOffLowInterestCoverage_WhenRecentCashQualityIsStrong()
    {
        var current = new Financial
        {
            Symbol = "HEALTHY",
            Sector = "Capital Goods",
            IsFinancialSector = false,
            NetProfitCr = 30m,
            InterestExpenseCr = 20m,
            CashFromOperationsCr = 35m,
            FreeCashFlowCr = 22m,
            ShareCapitalCr = 80m,
            ReservesCr = 90m,
            TotalBorrowingsCr = 120m,
            OtherLiabilitiesCr = 50m,
            FixedAssetsCr = 120m,
            CwipCr = 20m,
            InvestmentsCr = 40m,
            OtherAssetsCr = 30m,
            CashAndEquivalentsCr = 15m,
            SalesCr = 300m,
            OperatingProfitCr = 55m,
            OtherIncomeCr = 10m,
            DepreciationCr = 16m,
            DividendPayoutPercent = 25m,
            FaceValue = 1m
        };

        var history = new List<Financial>
        {
            new() { Year = 2021, NetProfitCr = 26m, CashFromOperationsCr = 30m, FreeCashFlowCr = 20m, InterestExpenseCr = 14m },
            new() { Year = 2022, NetProfitCr = 28m, CashFromOperationsCr = 34m, FreeCashFlowCr = 21m, InterestExpenseCr = 15m },
            new() { Year = 2023, NetProfitCr = 33m, CashFromOperationsCr = 39m, FreeCashFlowCr = 25m, InterestExpenseCr = 16m },
            new() { Year = 2024, NetProfitCr = 35m, CashFromOperationsCr = 42m, FreeCashFlowCr = 27m, InterestExpenseCr = 17m }
        };

        var ctx = new EvaluationContext
        {
            Current = current,
            Historicals = history,
            OcfToNetProfit = 35m / 30m,
            FcfToNetProfit = 22m / 30m,
            InterestCoverage = 2.5m,
            IsCashPredictable = true,
            IsCyclical = false
        };

        var rule = new DistressTurnaroundRule();
        Assert.False(rule.IsMatch(ctx));
    }

    [Theory]
    [InlineData("ITC")]
    [InlineData("TCS")]
    [InlineData("RELIANCE")]
    [InlineData("HDFCBANK")]
    [InlineData("360ONE")]
    [InlineData("NTPC")]
    [InlineData("ASIANPAINT")]
    [InlineData("TATASTEEL")]
    public async Task LiveScreenerData_ProducesValidValuationOutcome(string symbol)
    {
        var scraper = new ScraperService(new HttpClient(), NullLogger<ScraperService>.Instance);
        var valuationService = new ValuationService(new NoopFinancialRepository(), NullLogger<ValuationService>.Instance);

        var (stockValuation, current, historical, error) = await scraper.ScrapeStockAsync(symbol);

        Assert.True(error == null, $"Scrape failed for {symbol}: {error}");
        Assert.NotNull(stockValuation);
        Assert.NotNull(current);
        Assert.NotEmpty(historical!);

        var result = valuationService.Evaluate(stockValuation!, current!, historical!);

        Console.WriteLine($"Symbol: {result.Symbol}");
        Console.WriteLine($"Primary: {result.PrimaryMethod}");
        Console.WriteLine($"Secondary: {result.SecondaryMethod}");
        Console.WriteLine($"Intrinsic: {result.IntrinsicValue}");
        Console.WriteLine($"MarginOfSafety: {result.MarginOfSafety}");
        Console.WriteLine($"Verdict: {result.Verdict}");

        Assert.Equal(symbol, result.Symbol);
        Assert.True(result.IntrinsicValue > 0m, $"Intrinsic value should be > 0 for live symbol {symbol}.");
        Assert.False(string.IsNullOrWhiteSpace(result.PrimaryMethod));
        Assert.False(string.IsNullOrWhiteSpace(result.SecondaryMethod));
    }

    [Fact]
    public void HighLeverageGrowthCompany_UsesCashFlowDcfOverPeMultiple()
    {
        var current = new Financial
        {
            Symbol = "ABANSENT",
            Sector = "Trading - Metals",
            IsFinancialSector = false,
            CurrentPrice = 27.5m,
            MarketCapCr = 40.15m,
            ReportedRocePercent = 18m,
            ReportedRoePercent = 17m,
            ShareCapitalCr = 15m,
            ReservesCr = 25m,
            TotalBorrowingsCr = 35m,
            OtherLiabilitiesCr = 20m,
            FixedAssetsCr = 40m,
            CwipCr = 10m,
            InvestmentsCr = 20m,
            OtherAssetsCr = 30m,
            CashAndEquivalentsCr = 10m,
            SalesCr = 250m,
            OperatingProfitCr = 35m,
            OtherIncomeCr = 5m,
            DepreciationCr = 12m,
            NetProfitCr = 24m,
            InterestExpenseCr = 4m,
            CashFromOperationsCr = 38m,
            CashFromInvestmentCr = -10m,
            CashFromFinanceCr = -14m,
            FreeCashFlowCr = 26m,
            DividendPayoutPercent = 20m,
            FaceValue = 1m,
            Beta = 1.1m
        };

        var historical = new List<Financial>
        {
            new() { Year = 2022, SalesCr = 180m, NetProfitCr = 10m, CashFromOperationsCr = 18m, FreeCashFlowCr = 12m, ShareCapitalCr = 12m, ReservesCr = 18m, TotalBorrowingsCr = 32m, OtherLiabilitiesCr = 17m, FixedAssetsCr = 35m, CwipCr = 8m, InvestmentsCr = 18m, OtherAssetsCr = 20m, CashAndEquivalentsCr = 8m, DepreciationCr = 9m, InterestExpenseCr = 5m, OperatingProfitCr = 18m, OtherIncomeCr = 5m, DividendPayoutPercent = 15m },
            new() { Year = 2023, SalesCr = 210m, NetProfitCr = 16m, CashFromOperationsCr = 26m, FreeCashFlowCr = 17m, ShareCapitalCr = 13m, ReservesCr = 20m, TotalBorrowingsCr = 30m, OtherLiabilitiesCr = 18m, FixedAssetsCr = 36m, CwipCr = 9m, InvestmentsCr = 19m, OtherAssetsCr = 22m, CashAndEquivalentsCr = 9m, DepreciationCr = 9m, InterestExpenseCr = 4m, OperatingProfitCr = 22m, OtherIncomeCr = 6m, DividendPayoutPercent = 18m },
            new() { Year = 2024, SalesCr = 240m, NetProfitCr = 20m, CashFromOperationsCr = 34m, FreeCashFlowCr = 24m, ShareCapitalCr = 14m, ReservesCr = 22m, TotalBorrowingsCr = 29m, OtherLiabilitiesCr = 19m, FixedAssetsCr = 38m, CwipCr = 10m, InvestmentsCr = 20m, OtherAssetsCr = 25m, CashAndEquivalentsCr = 10m, DepreciationCr = 10m, InterestExpenseCr = 4m, OperatingProfitCr = 28m, OtherIncomeCr = 5m, DividendPayoutPercent = 20m }
        };

        var methodology = ValuationStrategyResolver.ResolveMethodology(current, historical);

        Assert.Equal("Exit Multiple DCF (FCFF)", methodology.PrimaryMethod);
        Assert.Contains("FCFF", methodology.Rationale);
    }

    [Fact]
    public void ComputeValueByMethod_HandlesLowerCaseAndSpacingVariants()
    {
        var current = new Financial
        {
            Symbol = "TESTCO",
            Sector = "Trading - Metals",
            CurrentPrice = 120m,
            MarketCapCr = 150m,
            ShareCapitalCr = 80m,
            ReservesCr = 60m,
            TotalBorrowingsCr = 50m,
            OtherLiabilitiesCr = 15m,
            FixedAssetsCr = 90m,
            CwipCr = 20m,
            InvestmentsCr = 25m,
            OtherAssetsCr = 40m,
            CashAndEquivalentsCr = 20m,
            SalesCr = 500m,
            OperatingProfitCr = 40m,
            OtherIncomeCr = 8m,
            DepreciationCr = 12m,
            NetProfitCr = 25m,
            InterestExpenseCr = 5m,
            CashFromOperationsCr = 10m,
            CashFromInvestmentCr = -8m,
            CashFromFinanceCr = -7m,
            FreeCashFlowCr = 0m,
            FaceValue = 1m,
            DividendPayoutPercent = 20m,
            ReportedRoePercent = 15m,
            ReportedRocePercent = 16m
        };

        var value = ValuationService.ComputeValueByMethod("ev/sales relative multiple", current, new[] { current });

        Assert.True(value > 0m);
    }

    [Fact]
    public void MethodNames_UsesCanonicalPrimaryAndSecondaryNames()
    {
        Assert.Equal("Excess Returns Model", MethodNames.ExcessReturns.ToDisplayString());
        Assert.Equal("Price-to-TBV (Tangible Book Value)", MethodNames.PriceToTangibleBookValue.ToDisplayString());
        Assert.Equal("Net Asset Value (NAV)", MethodNames.NetAssetValue.ToDisplayString());
        Assert.Equal("Price-to-Book (P/B)", MethodNames.PriceToBook.ToDisplayString());
        Assert.Equal("2-Stage FCFE DCF", MethodNames.TwoStageFcfeDcf.ToDisplayString());
        Assert.Equal("Price-to-Earnings-to-Growth (PEG)", MethodNames.PriceToEarningsToGrowth.ToDisplayString());
    }

    private sealed class NoopFinancialRepository : IFinancialRepository
    {
        public Task SaveValuationAsync(StockValuation valuation, CancellationToken ct = default) => Task.CompletedTask;
        public Task<CachedScrape> GetCachedScrapeAsync(string symbol, CancellationToken ct = default) => Task.FromResult(new CachedScrape(null, null, null, null));
        public Task SaveScrapedCacheAsync(string symbol, StockValuation stockValuation, Financial current, List<Financial> historical, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default) => Task.FromResult<StockValuation?>(null);
        public Task<List<StockValuation>> GetValuationsBySectorAsync(string sector, CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<List<StockValuation>> GetValuationsByVerdictAsync(string verdict, CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<List<StockValuation>> GetAllValuationsAsync(CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<StockValuation> GetPendingValuationsAsync(CancellationToken ct = default) => Task.FromResult(new StockValuation());
        public Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score) => Task.FromResult(new List<StockValuation>());
    }
}
