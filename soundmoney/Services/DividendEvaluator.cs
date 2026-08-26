using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public static class DividendEvaluator
    {
        public static DividendAnalysisResult Evaluate(List<HistoricalFinancial> historicalData)
        {
            if (historicalData == null || !historicalData.Any())
                return new DividendAnalysisResult();

            // Sort historical records by year descending (newest first)
            var sortedHistory = historicalData.OrderByDescending(h => h.Year).ToList();

            int paidStreak = 0;
            int growthStreak = 0;

            for (int i = 0; i < sortedHistory.Count; i++)
            {
                var current = sortedHistory[i];

                // 1. Uninterrupted Payment Streak
                if (current.DividendPayoutPercent > 0m)
                {
                    paidStreak++;
                }
                else
                {
                    break; // Payment streak breaks on zero-payout years
                }

                // 2. Growth / Non-Reduction Streak
                if (i < sortedHistory.Count - 1)
                {
                    var previous = sortedHistory[i + 1];
                    if (current.DividendPayoutPercent >= previous.DividendPayoutPercent)
                    {
                        growthStreak++;
                    }
                    else
                    {
                        growthStreak = 0; // Reset growth streak on dividend reduction
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

            // 4. Safety Metrics (Last 5 Years)
            var recentYears = sortedHistory.Take(5).ToList();
            decimal avgPayout = recentYears.Any() ? recentYears.Average(x => x.DividendPayoutPercent) : 0m;

            // Validate that Operating Cash Flow covers CapEx and Dividend commitments
            bool fcfSupported = recentYears.All(x => x.HistoricalFcfCr > 0m || x.HistoricalOcfCr > 0m);

            // 5. Final Classification
            bool isConsistent = paidStreak >= 3 && avgPayout <= 75m && fcfSupported;

            string rating = "Unstable";
            if (paidStreak >= 5 && growthStreak >= 3 && avgPayout <= 60m && fcfSupported)
                rating = "Elite (Dividend Champion)";
            else if (paidStreak >= 5 && avgPayout <= 75m)
                rating = "Reliable";
            else if (paidStreak >= 3)
                rating = "Moderate";

            return new DividendAnalysisResult
            {
                ConsecutiveYearsPaid = paidStreak,
                ConsecutiveYearsGrown = growthStreak,
                FiveYearCagr = Math.Round(cagr, 2),
                AveragePayoutRatio = Math.Round(avgPayout, 2),
                IsFcfSupported = fcfSupported,
                IsConsistent = isConsistent,
                HealthRating = rating
            };
        }
    }
}