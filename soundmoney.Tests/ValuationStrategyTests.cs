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

    [Fact]
    public void SoundScore_OneValueTrapWarning_ShouldNotCrushAnOtherwiseStrongCompany()
    {
        var current = new Financial
        {
            Symbol = "GOODCO",
            Sector = "Capital Goods",
            IsFinancialSector = false,
            CurrentPrice = 120m,
            MarketCapCr = 350m,
            SalesCr = 1200m,
            OperatingProfitCr = 260m,
            OtherIncomeCr = 30m,
            DepreciationCr = 35m,
            NetProfitCr = 180m,
            InterestExpenseCr = 18m,
            CashFromOperationsCr = 200m,
            FreeCashFlowCr = 150m,
            ShareCapitalCr = 120m,
            ReservesCr = 150m,
            TotalBorrowingsCr = 280m,
            OtherLiabilitiesCr = 60m,
            FixedAssetsCr = 180m,
            CwipCr = 22m,
            InvestmentsCr = 70m,
            OtherAssetsCr = 50m,
            CashAndEquivalentsCr = 25m,
            DividendPayoutPercent = 30m,
            FaceValue = 1m,
            Beta = 1.1m,
            IsCashEstimateReliable = true
        };

        var history = new List<Financial>
        {
            new() { Year = 2021, SalesCr = 980m, NetProfitCr = 130m, CashFromOperationsCr = 145m, FreeCashFlowCr = 100m, ShareCapitalCr = 100m, ReservesCr = 130m, TotalBorrowingsCr = 220m, OtherLiabilitiesCr = 50m, FixedAssetsCr = 170m, CwipCr = 20m, InvestmentsCr = 62m, OtherAssetsCr = 46m, CashAndEquivalentsCr = 22m, DepreciationCr = 28m, InterestExpenseCr = 15m, OperatingProfitCr = 200m, OtherIncomeCr = 22m, DividendPayoutPercent = 28m },
            new() { Year = 2022, SalesCr = 1040m, NetProfitCr = 145m, CashFromOperationsCr = 160m, FreeCashFlowCr = 110m, ShareCapitalCr = 105m, ReservesCr = 137m, TotalBorrowingsCr = 245m, OtherLiabilitiesCr = 55m, FixedAssetsCr = 173m, CwipCr = 21m, InvestmentsCr = 64m, OtherAssetsCr = 48m, CashAndEquivalentsCr = 23m, DepreciationCr = 30m, InterestExpenseCr = 16m, OperatingProfitCr = 220m, OtherIncomeCr = 24m, DividendPayoutPercent = 29m },
            new() { Year = 2023, SalesCr = 1100m, NetProfitCr = 160m, CashFromOperationsCr = 175m, FreeCashFlowCr = 120m, ShareCapitalCr = 110m, ReservesCr = 145m, TotalBorrowingsCr = 260m, OtherLiabilitiesCr = 58m, FixedAssetsCr = 176m, CwipCr = 21m, InvestmentsCr = 66m, OtherAssetsCr = 49m, CashAndEquivalentsCr = 24m, DepreciationCr = 32m, InterestExpenseCr = 17m, OperatingProfitCr = 235m, OtherIncomeCr = 27m, DividendPayoutPercent = 29m },
            new() { Year = 2024, SalesCr = 1200m, NetProfitCr = 180m, CashFromOperationsCr = 200m, FreeCashFlowCr = 150m, ShareCapitalCr = 120m, ReservesCr = 150m, TotalBorrowingsCr = 280m, OtherLiabilitiesCr = 60m, FixedAssetsCr = 180m, CwipCr = 22m, InvestmentsCr = 70m, OtherAssetsCr = 50m, CashAndEquivalentsCr = 25m, DepreciationCr = 35m, InterestExpenseCr = 18m, OperatingProfitCr = 260m, OtherIncomeCr = 30m, DividendPayoutPercent = 30m }
        };

        var result = FinancialAlgorithms.CalculateSoundScoreDetailed(18m, current, history);

        Assert.True(result.Score >= 60, $"Expected a strong score, but got {result.Score} with reasons: {string.Join(", ", result.CapReasons)}");
        Assert.True(result.CapReasons.Count <= 1, $"A single warning should not crush the score: {string.Join(", ", result.CapReasons)}");
    }

    [Fact]
    public void BuyTodaySellTomorrow_UsesRatioGateInsteadOfVerdictOnly()
    {
        var signal = FinancialAlgorithms.EvaluateBuyTodaySellTomorrow(
            verdict: "STRONG BUY",
            marginOfSafetyPercent: 27m,
            soundScoreRating: "STRONG SOUND",
            pe: 12.5m,
            pb: 1.5m,
            roePercent: 18m,
            rocePercent: 17m,
            netProfitMarginPercent: 12m,
            fcfConversionPercent: 68m,
            debtToEquity: 0.7m,
            interestCoverage: 7m,
            currentPrice: 120m,
            intrinsicValue: 180m);

        Assert.True(signal.IsBuyTodaySellTomorrow, $"Expected a valid buy-today/sell-tomorrow setup, but got: {signal.Reason}");
        Assert.Contains("buy today", signal.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CombinedSetup_PassesWhenFundamentalsAndTrendBothPass()
    {
        var bars = Enumerable.Range(0, 220)
            .Select(day => new DailyMarketBar(
                DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)),
                day == 219 ? 110m : 100m,
                day == 219 ? 1600m : 1000m))
            .ToList();
        var current = CreateStrongFundamentalCurrent();
        var historical = CreateStrongFundamentalHistory();

        var technical = BtstTechnicalService.Evaluate("TEST", current.MarketCapCr, bars, DateTimeOffset.UtcNow);
        var signal = BtstTechnicalService.EvaluateCombined(technical, current, historical, intrinsicValue: 150m);

        Assert.Equal("Available", signal.Status);
        Assert.True(signal.IsSetup);
        Assert.True(signal.IsFundamentallyStrong);
        Assert.True(signal.IsTechnicalSetup);
        Assert.All(signal.Checks, check => Assert.True(check.Passed, $"Expected {check.Name} to pass."));
        Assert.Equal(10m, signal.OneDayReturnPercent);
        Assert.Equal(1.6m, signal.VolumeRatio);
        Assert.Equal(143m, signal.TargetPrice30Percent);
        Assert.Equal(154m, signal.TargetPrice40Percent);
        Assert.Contains(signal.Checks, check => check.Name == "DMA 50 trend" && check.Passed);
        Assert.Contains(signal.Checks, check => check.Name == "20-session price trend" && check.Passed);
        Assert.Contains(signal.Checks, check => check.Name == "40-session price trend" && check.Passed);
        Assert.Contains(signal.Checks, check => check.Name == "DMA 50/200 alignment" && check.Category == "Confirmation");

        var stricterEntry = BtstTechnicalService.EvaluateCombined(
            technical,
            current,
            historical,
            intrinsicValue: 150m,
            minimumMarginOfSafety: 30m);
        Assert.True(stricterEntry.IsSetup);
        Assert.Contains(stricterEntry.Checks, check => check.Name == "Entry margin of safety" && !check.Passed && check.Category == "Context");
    }

    [Fact]
    public void CombinedSetup_FundamentalMissesDoNotBlockTechnicalTrendSetup()
    {
        var bars = Enumerable.Range(0, 220)
            .Select(day => new DailyMarketBar(
                DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)),
                day == 219 ? 110m : 100m,
                day == 219 ? 1600m : 1000m))
            .ToList();
        var weakCurrent = CreateStrongFundamentalCurrent();
        weakCurrent.NetProfitCr = 20m;
        weakCurrent.ReportedRoePercent = 4m;
        weakCurrent.ReportedRocePercent = 5m;
        var weakHistory = CreateStrongFundamentalHistory();

        var technical = BtstTechnicalService.Evaluate("TEST", weakCurrent.MarketCapCr, bars, DateTimeOffset.UtcNow);
        var signal = BtstTechnicalService.EvaluateCombined(technical, weakCurrent, weakHistory, intrinsicValue: 150m);

        Assert.True(signal.IsTechnicalSetup);
        Assert.False(signal.IsFundamentallyStrong);
        Assert.True(signal.IsSetup);
        Assert.Contains(signal.Checks, check => check.Name == "Current ROE" && !check.Passed && check.Category == "Fundamental");
    }

    [Fact]
    public void CombinedSetup_DoesNotRequireOneDayVolumeSpikeForMediumTermTrend()
    {
        var bars = Enumerable.Range(0, 220)
            .Select(day => new DailyMarketBar(
                DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)),
                day < 199 ? 100m : 100m + ((day - 198) * 0.5m),
                1000m))
            .ToList();
        var current = CreateStrongFundamentalCurrent();
        var historical = CreateStrongFundamentalHistory();

        var technical = BtstTechnicalService.Evaluate("TEST", current.MarketCapCr, bars, DateTimeOffset.UtcNow);
        var signal = BtstTechnicalService.EvaluateCombined(technical, current, historical, intrinsicValue: 150m);

        Assert.False(technical.Checks.Single(check => check.Name == "Volume-price confirmation").Passed);
        Assert.True(signal.IsTechnicalSetup);
        Assert.True(signal.IsSetup);
    }

    [Theory]
    [InlineData(102d, true, "demand confirmation")]
    [InlineData(98d, false, "selling pressure")]
    [InlineData(100d, false, "disagreement")]
    public void VolumePriceConfirmation_RequiresHighVolumeAndPositivePriceMove(
        double latestClose,
        bool expectedConfirmation,
        string expectedInterpretation)
    {
        decimal close = (decimal)latestClose;
        var bars = Enumerable.Range(0, 220)
            .Select(day => new DailyMarketBar(
                DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)),
                day == 219 ? close : 100m,
                day == 219 ? 2000m : 1000m))
            .ToList();

        var signal = BtstTechnicalService.Evaluate("VOLUME", 2000m, bars, DateTimeOffset.UtcNow);
        var confirmation = signal.Checks.Single(check => check.Name == "Volume-price confirmation");

        Assert.Equal(expectedConfirmation, confirmation.Passed);
        Assert.Contains(expectedInterpretation, confirmation.Observed, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Confirmation", confirmation.Category);
    }

    [Fact]
    public void CombinedSetup_DoesNotRequireDma200ForTwentyToFortySessionHorizon()
    {
        var bars = Enumerable.Range(0, 70)
            .Select(day => new DailyMarketBar(
                DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)),
                100m + (day * 0.2m),
                1000m))
            .ToList();
        var current = CreateStrongFundamentalCurrent();
        var historical = CreateStrongFundamentalHistory();

        var technical = BtstTechnicalService.Evaluate("TEST", current.MarketCapCr, bars, DateTimeOffset.UtcNow);
        var signal = BtstTechnicalService.EvaluateCombined(technical, current, historical, intrinsicValue: 200m);

        Assert.Null(technical.Dma200);
        Assert.True(signal.IsTechnicalSetup);
        Assert.True(signal.IsSetup);
        Assert.Contains(signal.Checks, check => check.Name == "Close above DMA 200" && check.Category == "Confirmation" && !check.Passed);
    }

    [Fact]
    public void TechnicalBtstSignal_ReportsInsufficientHistoryWithoutFailingSignal()
    {
        var bars = Enumerable.Range(0, 50)
            .Select(day => new DailyMarketBar(DateOnly.FromDateTime(new DateTime(2025, 1, 1).AddDays(day)), 100m, 1000m))
            .ToList();

        var signal = BtstTechnicalService.Evaluate("TEST", 2000m, bars, DateTimeOffset.UtcNow);

        Assert.Equal("InsufficientData", signal.Status);
        Assert.Null(signal.IsSetup);
        Assert.Empty(signal.Checks);
    }

    [Fact]
    public void FundamentalWatchlistRanking_KeepsNearMissCandidatesVisible()
    {
        var strong = CreateStrongFundamentalCurrent();
        var nearMiss = CreateStrongFundamentalCurrent();
        nearMiss.ReportedRoePercent = 18m;
        var strongChecks = BtstTechnicalService.EvaluateFundamentalChecks(strong, CreateStrongFundamentalHistory());
        var nearMissChecks = BtstTechnicalService.EvaluateFundamentalChecks(nearMiss, CreateStrongFundamentalHistory());

        Assert.All(strongChecks.Where(check => check.Category == "Fundamental"), check => Assert.True(check.Passed, check.Name));
        Assert.Contains(nearMissChecks, check => check.Name == "Current ROE" && !check.Passed);
        Assert.True(strongChecks.Count(check => check.Passed) > nearMissChecks.Count(check => check.Passed));
    }

    private static Financial CreateStrongFundamentalCurrent() => new()
    {
        Symbol = "TEST",
        Year = 2025,
        CurrentPrice = 110m,
        MarketCapCr = 2000m,
        IsFinancialSector = false,
        ReportedRoePercent = 36m,
        ReportedRocePercent = 30m,
        PromoterHoldingPercent = 55m,
        ShareCapitalCr = 100m,
        ReservesCr = 400m,
        TotalBorrowingsCr = 50m,
        NetProfitCr = 180m,
        SalesCr = 1200m,
        OperatingProfitCr = 180m,
        OtherIncomeCr = 10m,
        DepreciationCr = 10m,
        CashFromOperationsCr = 150m
    };

    private static List<Financial> CreateStrongFundamentalHistory() => Enumerable.Range(0, 5)
        .Select(offset => new Financial
        {
            Year = 2020 + offset,
            NetProfitCr = 100m + (offset * 12m),
            SalesCr = 800m + (offset * 80m),
            ShareCapitalCr = 100m,
            ReservesCr = 350m + (offset * 10m),
            TotalBorrowingsCr = 50m,
            OperatingProfitCr = 150m + (offset * 5m),
            OtherIncomeCr = 10m,
            DepreciationCr = 10m
        })
        .ToList();

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
        public Task<List<CachedFinancialEntry>> GetRecentCachedFinancialEntriesAsync(DateTime scrapedSince, CancellationToken ct = default) => Task.FromResult(new List<CachedFinancialEntry>());
        public Task SaveScrapedCacheAsync(string symbol, StockValuation stockValuation, Financial current, List<Financial> historical, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StockValuation?> GetValuationBySymbolAsync(string symbol, CancellationToken ct = default) => Task.FromResult<StockValuation?>(null);
        public Task<List<StockValuation>> GetValuationsBySectorAsync(string sector, CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<List<StockValuation>> GetValuationsByVerdictAsync(string verdict, CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<List<StockValuation>> GetAllValuationsAsync(CancellationToken ct = default) => Task.FromResult(new List<StockValuation>());
        public Task<StockValuation> GetPendingValuationsAsync(CancellationToken ct = default) => Task.FromResult(new StockValuation());
        public Task<List<StockValuation>> GetByFilterAsync(decimal minMarginOfSafety, string? searchQuery, List<string>? score) => Task.FromResult(new List<StockValuation>());
    }
}
