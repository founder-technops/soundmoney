using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public static class DividendEvaluator
    {
        /// <summary>
        /// Evaluates historical dividend performance and safety metrics.
        /// </summary>
        /// <param name="historicalData">List of historical financial records.</param>
        /// <param name="data">Current DeepFinancial metrics (for NetProfit, CapitalAdequacy, etc.).</param>
        /// <returns>DividendAnalysisResult containing consistency checks and safety rating.</returns>
        public static DividendAnalysisResult Evaluate(DeepFinancial data, List<HistoricalFinancial> historicalData)
        {
            if (historicalData == null || !historicalData.Any() || data == null)
                return new DividendAnalysisResult();

            bool isFinancialSector = data.IsFinancialSector;

            // Sort historical records by year descending (newest first)
            var sortedHistory = historicalData.OrderByDescending(h => h.Year).ToList();

            int paidStreak = 0;
            int growthStreak = 0;

            for (int i = 0; i < sortedHistory.Count; i++)
            {
                var current = sortedHistory[i];

                // 1. Uninterrupted Payment Streak Check
                if (current.DividendPayoutPercent > 0m)
                {
                    paidStreak++;
                }
                else
                {
                    break; // Streak breaks on non-payment years
                }

                // 2. Growth / Consistency Streak
                if (i < sortedHistory.Count - 1)
                {
                    var previous = sortedHistory[i + 1];

                    // Maintain growth/stability streak if payout ratio stays within 10% tolerance
                    if (current.DividendPayoutPercent >= previous.DividendPayoutPercent * 0.9m)
                    {
                        growthStreak++;
                    }
                    else
                    {
                        growthStreak = 0; // Reset streak on major dividend cuts
                    }
                }
            }

            // 3. 5-Year Payout CAGR
            decimal cagr = 0m;
            if (sortedHistory.Count >= 5 && sortedHistory[4].DividendPayoutPercent > 0m)
            {
                double startVal = (double)sortedHistory[4].DividendPayoutPercent;
                double endVal = (double)sortedHistory[0].DividendPayoutPercent;
                cagr = (decimal)(Math.Pow(endVal / startVal, 1.0 / 5.0) - 1.0) * 100m;
            }

            // 4. Safety & Coverage Metrics (Last 5 Years)
            var recentYears = sortedHistory.Take(5).ToList();
            decimal averagePayoutRatio = recentYears.Any() ? recentYears.Average(x => x.DividendPayoutPercent) : 0m;

            // FCF Support check for traditional non-financial sectors
            bool isFcfSupported = recentYears.All(x => x.HistoricalFcfCr > 0m || x.HistoricalOcfCr > 0m);

            // -------------------------------------------------------------
            // DYNAMIC DIVIDEND SAFETY CHECK
            // Uses CAR & Profitability for Financials; FCF & Payout for Non-Financials
            // -------------------------------------------------------------
            bool isDividendSafe = isFinancialSector
                ? (data.NetProfitCr > 0m && averagePayoutRatio <= 60m && data.CapitalAdequacyPercent >= 13m)
                : (averagePayoutRatio <= 75m && isFcfSupported);

            // 5. Final Consistency & Classification Logic
            bool isConsistent = paidStreak >= 3 && isDividendSafe;

            string rating = "Unstable";
            if (paidStreak >= 5 && growthStreak >= 3 && isDividendSafe && averagePayoutRatio <= 60m)
                rating = "Elite (Dividend Champion)";
            else if (paidStreak >= 5 && isDividendSafe)
                rating = "Reliable";
            else if (paidStreak >= 3 && isDividendSafe)
                rating = "Moderate";

            return new DividendAnalysisResult
            {
                ConsecutiveYearsPaid = paidStreak,
                ConsecutiveYearsGrown = growthStreak,
                FiveYearCagr = Math.Round(cagr, 2),
                AveragePayoutRatio = Math.Round(averagePayoutRatio, 2),
                IsFcfSupported = isFinancialSector ? true : isFcfSupported,
                IsConsistent = isConsistent,
                HealthRating = rating
            };
        }
    }
}