using SoundMoney.Data;
using SoundMoney.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundMoney.Services
{
    public interface IValuationService
    {
        StockValuation EvaluateData(StockValuation valuationData, DeepFinancial deepData, List<HistoricalFinancial> historicalData);
    }

    public class ValuationService : IValuationService
    {
        private readonly IFinancialRepository _repository;
        private readonly ILogger<ValuationService> _logger;

        public ValuationService(IFinancialRepository repository, ILogger<ValuationService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public StockValuation EvaluateData(
            StockValuation valuationData,
            DeepFinancial deepData,
            List<HistoricalFinancial> historicalData)
        {
            historicalData ??= new List<HistoricalFinancial>();

            if (valuationData == null || deepData == null)
            {
                _logger.LogWarning("Cannot calculate valuation: Missing core dataset input.");
                return null;
            }

            // 1. Resolve strategy dynamically using context rules
            ValuationMethodology methodology = ValuationStrategyResolver.ResolveMethodology(deepData, historicalData);

            _logger.LogInformation("Valuing {Symbol}. Dynamic Strategy Primary: {Primary}, Secondary: {Secondary} | Rationale: {Rationale}",
                valuationData.Symbol, methodology.PrimaryMethod, methodology.SecondaryMethod, methodology.Rationale);

            // 2. Compute Intrinsic Values based on resolved methods
            decimal primaryValue = ComputeValueByMethod(methodology.PrimaryMethod, deepData, historicalData);
            decimal secondaryValue = ComputeValueByMethod(methodology.SecondaryMethod, deepData, historicalData);

            // 3. Blend intrinsic values
            decimal blendedIntrinsicValue = (primaryValue > 0 && secondaryValue > 0)
                ? Math.Round((primaryValue * 0.6m) + (secondaryValue * 0.4m), 2)
                : Math.Max(primaryValue, secondaryValue);

            decimal cmp = deepData.CurrentPrice;
            decimal marginOfSafety = 0m;
            string verdict;

            if (blendedIntrinsicValue <= 0 || cmp <= 0)
            {
                verdict = "INSUFFICIENT DATA";
            }
            else
            {
                marginOfSafety = Math.Round(((blendedIntrinsicValue - cmp) / cmp) * 100m, 2);
                verdict = cmp switch
                {
                    var p when p <= blendedIntrinsicValue * 0.70m => "STRONG BUY",
                    var p when p <= blendedIntrinsicValue => "BUY",
                    var p when p <= blendedIntrinsicValue * 1.20m => "HOLD",
                    _ => "OVERVALUED"
                };
            }

            int soundScore = SoundScoreCalculator.CalculateSoundScore(marginOfSafety, deepData, historicalData);

            if (verdict == "INSUFFICIENT DATA")
            {
                soundScore = 0;
            }

            string soundRating = soundScore switch
            {
                >= 80 => "STRONG SOUND",
                >= 60 => "SOUND",
                >= 40 => "NEUTRAL",
                _ => "UNSOUND"
            };

            DividendAnalysisResult dividendAnalysis = DividendEvaluator.Evaluate(deepData, historicalData);

            var result = new StockValuation
            {
                Symbol = valuationData.Symbol,
                Sector = valuationData.Sector,
                CompanyName = valuationData.CompanyName,
                PrimaryMethod = methodology.PrimaryMethod,
                SecondaryMethod = methodology.SecondaryMethod,
                CurrentPrice = cmp,
                IntrinsicValue = blendedIntrinsicValue,
                MarginOfSafety = marginOfSafety,
                DividendYieldPercent = deepData.DividendYieldPercent,
                IsDividendConsistent = dividendAnalysis.IsConsistent,
                Verdict = verdict,
                SoundScore = soundScore,
                SoundScoreRating = soundRating,
                UpdatedAt = DateTime.Now
            };

            return result;
        }

        #region Method Execution Router

        private decimal ComputeValueByMethod(string methodName, DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            return methodName switch
            {
                "Excess Returns Model" => CalculateExcessReturns(data),
                "Price-to-TBV (Tangible Book Value)" or "Price-to-Book (P/B)" or "Price-to-Book (P/B) Intrinsic Multiples" => CalculatePbIntrinsicValue(data),

                "EV/Sales Relative Multiple" => CalculateEvSalesMultiple(data),
                "Price-to-Sales (P/S)" => CalculatePriceToSales(data),

                "Net Asset Value (NAV)" => CalculateNavPerShare(data),
                "Normalized Mid-Cycle P/E" => CalculateNormalizedPe(data, historicals),

                "Exit Multiple DCF (FCFF)" or "Exit Multiple DCF" => CalculateExitMultipleDcf(data, historicals),
                "EV/EBITDA Relative Multiple" => CalculateEvEbitdaMultiple(data),

                "Dividend Discount Model (DDM)" => CalculateDdm(data),
                "Dividend Discount Model (Pass-Through Yield)" => CalculateDdmPassThroughYield(data),
                "Gordon Growth Model" or "Gordon Growth DDM" => CalculateGordonGrowthDdm(data),

                "Buffett Owner Earnings Model" => CalculateOwnerEarnings(data, historicals),
                "2-Stage FCFE DCF" or "2-Stage Discounted Cash Flow (DCF)" => CalculateTwoStageDcf(data, historicals),
                "Price-to-Earnings-to-Growth (PEG)" => CalculatePegRatioValue(data, historicals),

                "Price-to-Earnings (P/E) Multiple" => CalculatePriceToEarnings(data),
                "Discounted Cash Flow (DCF)" or "Standard DCF" => CalculateStandardDcf(data, historicals),
                "Adjusted Net Asset Value (SOTP with HoldCo Discount)" => CalculateHoldingCompanyValue(data),
                _ => CalculateStandardDcf(data, historicals)
            };
        }

        #endregion

        #region Dynamic Utilities & Target Multiples

        private static decimal GetNetDebtCr(DeepFinancial data) =>
            data.IsCashEstimateReliable
                ? data.TotalBorrowingsCr - data.CashAndEquivalentsCr
                : data.TotalBorrowingsCr;

        private static decimal CalculateWacc(DeepFinancial data, decimal riskFreeRate = 0.07m, decimal equityRiskPremium = 0.055m)
        {
            decimal equityValueCr = data.MarketCapCr;
            decimal debtValueCr = Math.Max(0m, data.TotalBorrowingsCr);
            decimal totalCapitalCr = equityValueCr + debtValueCr;

            if (totalCapitalCr <= 0m) return 0.11m;

            decimal beta = data.Beta > 0 ? Math.Clamp(data.Beta, 0.5m, 2.5m) : 1.0m;
            decimal costOfEquity = riskFreeRate + (beta * equityRiskPremium);

            decimal costOfDebt = data.CostOfDebt;
            decimal taxRate = data.EffectiveTaxRate;

            decimal weightEquity = equityValueCr / totalCapitalCr;
            decimal weightDebt = debtValueCr / totalCapitalCr;

            decimal wacc = (weightEquity * costOfEquity) + (weightDebt * costOfDebt * (1m - taxRate));
            return Math.Clamp(wacc, 0.085m, 0.18m);
        }

        private static decimal ResolveDynamicTargetPe(DeepFinancial data)
        {
            decimal basePe = 15.0m;
            if (data.ReportedRoePercent >= 25.0m) return 25.0m;
            if (data.ReportedRoePercent >= 18.0m) return 20.0m;
            if (data.ReportedRoePercent <= 8.0m) return 10.0m;
            return basePe;
        }

        private static decimal CalculateCagr(decimal initialValue, decimal finalValue, int periods)
        {
            if (initialValue <= 0 || finalValue <= 0 || periods <= 0)
                return 0m;

            double ratio = (double)(finalValue / initialValue);
            double cagr = Math.Pow(ratio, 1.0 / periods) - 1.0;

            return (decimal)cagr;
        }

        private static decimal ResolveDynamicGrowthRate(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals, decimal defaultFallback = 0.08m)
        {
            if (data.ReportedRoePercent > 0)
            {
                decimal roe = data.ReportedRoePercent / 100m;
                decimal payoutRatio = Math.Max(0m, Math.Min(data.DividendPayoutPercent / 100m, 1m));
                decimal retentionRatio = 1m - payoutRatio;

                decimal fundamentalGrowth = roe * retentionRatio;

                if (fundamentalGrowth > 0)
                {
                    return Math.Clamp(fundamentalGrowth, 0.02m, 0.15m);
                }
            }

            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            if (historyList != null && historyList.Count >= 3)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (oldest.HistoricalOcfCr > 0 && newest.HistoricalOcfCr > 0)
                {
                    decimal ocfCagr = CalculateCagr(oldest.HistoricalOcfCr, newest.HistoricalOcfCr, periods);
                    if (ocfCagr > 0)
                    {
                        return Math.Clamp(ocfCagr, 0.02m, 0.15m);
                    }
                }
            }

            return defaultFallback;
        }

        #endregion

        #region Valuation Algorithms

        private static decimal CalculateStandardDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal discountRate = CalculateWacc(data);

            decimal terminalRate = 0.03m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = GetNetDebtCr(data);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateTwoStageDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal stage1Growth = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.10m);
            decimal stage2Growth = stage1Growth * 0.5m;
            decimal discountRate = CalculateWacc(data);
            decimal terminalRate = Math.Min(0.03m, stage2Growth);

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + stage1Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            for (int yr = 6; yr <= 10; yr++)
            {
                projectedFcf *= (1m + stage2Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 10);

            decimal equityValueCr = cumulativePv + pvTerminal;
            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateExitMultipleDcf(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0m || data.EbitCr <= 0m) return 0m;

            decimal taxRate = data.EffectiveTaxRate;
            // Unlevered Operating Cash Flow (FCFF approximation)
            decimal ebitAfterTax = data.EbitCr * (1m - taxRate);
            decimal fcffCr = ebitAfterTax + data.DepreciationCr - data.GrossCapexCr;

            if (fcffCr <= 0m) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal wacc = CalculateWacc(data);
            decimal evEbitdaMultiple = data.ReportedRoePercent >= 18.0m ? 14.0m : 10.0m;

            decimal cumulativePv = 0m;
            decimal projectedFcff = fcffCr;
            decimal projectedEbitda = data.EbitdaCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcff *= (1m + growthRate);
                projectedEbitda *= (1m + growthRate);
                cumulativePv += projectedFcff / (decimal)Math.Pow((double)(1m + wacc), yr);
            }

            decimal terminalEv = projectedEbitda * evEbitdaMultiple;
            decimal pvTerminal = terminalEv / (decimal)Math.Pow((double)(1m + wacc), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = GetNetDebtCr(data);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculateExcessReturns(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0m || data.ReportedRoePercent <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal roe = data.ReportedRoePercent / 100m;

            if (roe <= costOfEquity) return Math.Round(data.BookValuePerShare, 2);

            decimal payoutRatio = Math.Clamp(data.DividendPayoutPercent / 100m, 0m, 0.80m);
            decimal retentionRatio = 1m - payoutRatio;

            decimal currentBookValue = data.BookValuePerShare;
            decimal pvExcessReturns = 0m;

            // 5-Year Explicit Forecast Horizon with Compounding Book Value
            for (int yr = 1; yr <= 5; yr++)
            {
                decimal excessReturnPerShare = (roe - costOfEquity) * currentBookValue;
                pvExcessReturns += excessReturnPerShare / (decimal)Math.Pow((double)(1m + costOfEquity), yr);
                currentBookValue += excessReturnPerShare * retentionRatio; // Compound equity base
            }

            // Terminal Excess Return Value
            const decimal terminalGrowth = 0.03m;
            decimal denominator = Math.Max(0.01m, costOfEquity - terminalGrowth);
            decimal terminalExcessReturn = ((roe - costOfEquity) * currentBookValue) / denominator;
            decimal pvTerminalExcess = terminalExcessReturn / (decimal)Math.Pow((double)(1m + costOfEquity), 5);

            decimal totalIntrinsicValue = data.BookValuePerShare + pvExcessReturns + pvTerminalExcess;
            return Math.Round(totalIntrinsicValue, 2);
        }

        private static decimal CalculateDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal dividendGrowth = 0.05m;

            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal denominator = Math.Max(0.005m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        private static decimal CalculateDdmPassThroughYield(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            // Compute realized dividend per share (d0) received by parent shareholders
            decimal d0 = 0m;
            if (data.CurrentPrice > 0 && data.DividendYieldPercent > 0)
            {
                d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            }
            else if (data.BookValuePerShare > 0 && data.ReportedRoePercent > 0 && data.DividendPayoutPercent > 0)
            {
                decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
                decimal rawDividend = eps * (data.DividendPayoutPercent / 100m);

                // Apply a 50% pass-through friction haircut for investment/holding entities
                d0 = rawDividend * 0.50m;
            }

            if (d0 <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            // Conservative growth cap for holding entity pass-through cash flow
            const decimal dividendGrowth = 0.035m;

            if (costOfEquity <= dividendGrowth)
            {
                costOfEquity = dividendGrowth + 0.05m;
            }

            decimal denominator = Math.Max(0.02m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);

            return Math.Round(d1 / denominator, 2);
        }

        private static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * (1m - payoutRatio), 0.06m);
            if (payoutGrowth >= costOfEquity) payoutGrowth = costOfEquity - 0.01m;

            decimal denominator = Math.Max(0.005m, costOfEquity - payoutGrowth);
            decimal d1 = d0 * (1m + payoutGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        private static decimal CalculateOwnerEarnings(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            decimal ownerEarningsCr;

            if (historyList != null && historyList.Count >= 3)
            {
                decimal weightedOcfSum = 0m;
                decimal weightedCapexSum = 0m;
                decimal weightTotal = 0m;

                for (int i = 0; i < historyList.Count; i++)
                {
                    decimal weight = i + 1m;
                    weightedOcfSum += historyList[i].HistoricalOcfCr * weight;
                    weightedCapexSum += historyList[i].HistoricalCapexCr * weight;
                    weightTotal += weight;
                }

                ownerEarningsCr = (weightedOcfSum / weightTotal) - (weightedCapexSum / weightTotal);
            }
            else
            {
                ownerEarningsCr = data.CashFromOperationsCr - data.GrossCapexCr;
            }

            if (ownerEarningsCr <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal terminalGrowth = Math.Min(0.04m, costOfEquity - 0.02m);
            decimal capRateDenominator = Math.Max(0.02m, costOfEquity - terminalGrowth);
            decimal capMultiple = 1m / capRateDenominator;

            return Math.Round((ownerEarningsCr * capMultiple) / data.TotalSharesCr, 2);
        }

        private static decimal CalculateNormalizedPe(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.TotalSharesCr <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgNetProfitCr = historicals.Average(h => h.HistoricalNetProfitCr);
            if (avgNetProfitCr <= 0) return 0m;

            decimal normalizedEps = avgNetProfitCr / data.TotalSharesCr;
            decimal targetPe = ResolveDynamicTargetPe(data);

            return Math.Round(normalizedEps * targetPe, 2);
        }

        private static decimal CalculatePegRatioValue(DeepFinancial data, IEnumerable<HistoricalFinancial> historicals)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, 0.10m) * 100m;

            if (eps <= 0 || growthRate <= 0) return 0m;

            decimal fairPe = Math.Clamp(growthRate, 4.0m, 30.0m);
            return Math.Round(eps * fairPe, 2);
        }

        private static decimal CalculateEvSalesMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.SalesCr <= 0) return 0m;

            decimal targetEvSales = 2.5m;
            decimal revenueCr = data.SalesCr;
            decimal netDebtCr = GetNetDebtCr(data);

            decimal targetEquityValueCr = (revenueCr * targetEvSales) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculatePriceToSales(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.SalesCr <= 0) return 0m;

            decimal salesPerShare = data.SalesCr / data.TotalSharesCr;
            const decimal targetPs = 1.5m;
            return Math.Round(salesPerShare * targetPs, 2);
        }

        private static decimal CalculateEvEbitdaMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.EbitCr <= 0) return 0m;

            decimal estimatedEbitdaCr = data.EbitCr * 1.2m;
            decimal targetEvEbitda = data.ReportedRoePercent >= 18.0m ? 12.0m : 8.5m;
            decimal netDebtCr = GetNetDebtCr(data);

            decimal targetEquityValueCr = (estimatedEbitdaCr * targetEvEbitda) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        private static decimal CalculatePriceToEarnings(DeepFinancial data)
        {
            if (data.NetProfitCr <= 0 || data.TotalSharesCr <= 0) return 0m;

            decimal eps = data.NetProfitCr / data.TotalSharesCr;
            decimal fairPe = ResolveDynamicTargetPe(data);

            if (!data.IsFinancialSector)
            {
                decimal cashConversion = data.NetProfitCr > 0
                    ? Math.Clamp(data.CashFromOperationsCr / data.NetProfitCr, 0m, 1m)
                    : 0m;

                if (cashConversion < 0.50m)
                {
                    fairPe *= Math.Max(0.20m, cashConversion);
                }
            }

            return Math.Round(eps * fairPe, 2);
        }

        private static decimal CalculateNavPerShare(DeepFinancial data)
        {
            return data.BookValuePerShare <= 0 ? 0m : Math.Round(data.BookValuePerShare, 2);
        }

        private static decimal CalculatePbIntrinsicValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal growth = 0.05m;
            decimal roe = data.ReportedRoePercent / 100m;

            decimal denominator = Math.Max(0.005m, costOfEquity - growth);
            decimal justifiedPb = (roe - growth) / denominator;
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 12.0m);

            return Math.Round(data.BookValuePerShare * justifiedPb, 2);
        }

        private static decimal CalculateHoldingCompanyValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0) return 0m;

            decimal rawNavPerShare = data.BookValuePerShare;
            decimal holdCoDiscount = 0.50m;

            if (data.DividendYieldPercent < 1.0m)
            {
                holdCoDiscount += 0.10m;
            }

            decimal adjustedNav = rawNavPerShare * (1.0m - holdCoDiscount);
            return Math.Round(adjustedNav, 2);
        }

        #endregion
    }
}