using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public static class ValuationStrategyResolver
    {
        public static ValuationMethodology ResolveMethodology(
            DeepFinancial data,
            IEnumerable<HistoricalFinancial> historicals)
        {
            var historyList = historicals?.OrderBy(h => h.Year).ToList() ?? new List<HistoricalFinancial>();

            // ----------------------------------------------------
            // FILTER 3: Corporate Lifecycle Stage (Turnaround / Distress)
            // ----------------------------------------------------
            if (data.NetProfitCr <= 0 || data.BookValuePerShare <= 0)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Net Asset Value (NAV)",
                    SecondaryMethod = "Price-to-Book (P/B)",
                    Rationale = "Early stage, turnaround, or unprofitable business; valued via asset liquidation/book value foundation."
                };
            }

            // ----------------------------------------------------
            // FILTER 1: Predictability of Cash Flows
            // ----------------------------------------------------
            decimal fcfCr = data.FreeCashFlowCr > 0 ? data.FreeCashFlowCr : (data.CashFromOperationsCr - data.GrossCapexCr);
            bool possessesPositiveFcf = fcfCr > 0;

            decimal ocfToNetProfitRatio = data.NetProfitCr > 0 ? (data.CashFromOperationsCr / data.NetProfitCr) : 0m;
            bool highCashPredictability = possessesPositiveFcf && ocfToNetProfitRatio >= 0.8m;

            // Check OCF volatility across historical periods
            if (historyList.Count >= 3)
            {
                bool hasNegativeOcfYears = historyList.Any(h => h.HistoricalOcfCr <= 0);
                if (hasNegativeOcfYears) highCashPredictability = false;
            }

            // ----------------------------------------------------
            // FILTER 2: Capital Structure & Debt Layout
            // ----------------------------------------------------
            decimal netDebt = Math.Abs(Math.Min(0m, data.NetCashCr));
            decimal debtToEbit = data.EbitCr > 0 ? (netDebt / data.EbitCr) : 99m;
            bool isCapitalIntensiveOrLeveraged = debtToEbit >= 2.5m || data.NetCashCr < 0;

            // ----------------------------------------------------
            // EXECUTION: Select Optimal Valuation Pair
            // ----------------------------------------------------

            // Scenario A: Mature / Dividend-Paying Steady State
            if (data.DividendPayoutPercent >= 40m && highCashPredictability)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Dividend Discount Model (DDM)",
                    SecondaryMethod = "Gordon Growth DDM",
                    Rationale = "Mature, steady-state company distributing substantial capital via dividends."
                };
            }

            // Scenario B: Capital Intensive / High Debt (Enterprise Focus)
            if (isCapitalIntensiveOrLeveraged)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Exit Multiple DCF",
                    SecondaryMethod = "Normalized DCF (Cycle-Adjusted)",
                    Rationale = "Heavy debt layout or capital intensity requires enterprise-level EV models independent of debt structure."
                };
            }

            // Scenario C: Asset Light + Stable Cash Flows (Consumer Monopolies / Software)
            if (highCashPredictability && !isCapitalIntensiveOrLeveraged)
            {
                bool isHighRoeMoat = data.ReportedRoePercent >= 20m;

                return new ValuationMethodology
                {
                    PrimaryMethod = isHighRoeMoat ? "Buffett Owner Earnings Model" : "2-Stage Discounted Cash Flow (DCF)",
                    SecondaryMethod = "Capitalized Free Cash Flow Yield",
                    Rationale = "Asset-light model with high cash predictability and low leverage; ideal for cash-flow discount models."
                };
            }

            // Scenario D: Volatile Cash Flows / Cyclical (Fallback to Multiples)
            return new ValuationMethodology
            {
                PrimaryMethod = "Price-to-Book (P/B) Intrinsic Multiples",
                SecondaryMethod = "Capitalized Free Cash Flow Yield",
                Rationale = "Volatile or low cash flow predictability; avoiding long-term DCF projections in favor of asset multiples."
            };
        }
    }
}