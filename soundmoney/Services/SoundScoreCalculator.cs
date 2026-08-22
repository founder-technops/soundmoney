using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public static class SoundScoreCalculator
    {
        public static int CalculateSoundScore(
            decimal marginOfSafety,
            DeepFinancial data,
            IEnumerable<HistoricalFinancial> historicals)
        {
            if (data == null) return 0;

            decimal score = 0m;

            // -------------------------------------------------------------
            // 0. UNIT NORMALIZATION
            // Standardizes percentages so 15% is represented as 15.0m
            // -------------------------------------------------------------
            decimal roePercent = data.ReportedRoePercent;

            decimal roaPercent = data.ReportedRoaPercent;

            // -------------------------------------------------------------
            // 1. MARGIN OF SAFETY (Max 30 Pts)
            // -------------------------------------------------------------
            if (marginOfSafety >= 30m)
                score += 30m;
            else if (marginOfSafety > 0m)
                score += 15m + (marginOfSafety / 30m * 14m);
            else if (marginOfSafety >= -20m)
                score += Math.Max(0m, 14m + (marginOfSafety / 20m * 14m));

            // -------------------------------------------------------------
            // 2. CAPITAL EFFICIENCY: ROE / ROA (Max 20 Pts)
            // -------------------------------------------------------------
            if (data.IsFinancialSector)
            {
                if (roaPercent >= 1.8m) score += 20m;
                else if (roaPercent >= 1.2m) score += 15m;
                else if (roaPercent >= 0.8m) score += 10m;
            }
            else
            {
                if (roePercent >= 20m) score += 20m;
                else if (roePercent >= 15m) score += 15m;
                else if (roePercent >= 10m) score += 10m;
            }

            // -------------------------------------------------------------
            // 3. SOLVENCY & DEBT HEALTH (Max 20 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                if (data.NetCashCr >= 0m)
                {
                    score += 20m;
                }
                else if (data.EbitCr > 0m)
                {
                    decimal netDebt = Math.Abs(data.NetCashCr);
                    decimal debtToEbit = netDebt / data.EbitCr;

                    if (debtToEbit <= 2.0m) score += 12m;
                    else if (debtToEbit <= 3.5m) score += 6m;
                }
            }
            else
            {
                if (data.CapitalAdequacyPercent >= 15m) score += 20m;
                else if (data.CapitalAdequacyPercent >= 12m) score += 12m;
            }

            // -------------------------------------------------------------
            // 4. CASH FLOW QUALITY: OCF / NET PROFIT (Max 15 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector && data.NetProfitCr > 0m && data.CashFromOperationsCr > 0m)
            {
                decimal cashConversion = data.CashFromOperationsCr / data.NetProfitCr;
                if (cashConversion >= 1.0m) score += 15m;
                else if (cashConversion >= 0.7m) score += 10m;
            }
            else if (data.IsFinancialSector)
            {
                score += 12m;
            }

            // -------------------------------------------------------------
            // 5. HISTORICAL GROWTH & ACCRUAL QUALITY (Max 15 Pts)
            // -------------------------------------------------------------
            var historyList = historicals?.OrderBy(h => h.Year).ToList();

            decimal salesGrowth = 0m;
            bool hasValidHistory = historyList != null && historyList.Count >= 3;

            if (hasValidHistory)
            {
                var oldest = historyList.First(); // Correctly gets oldest (e.g. 2021)
                var newest = historyList.Last();  // Correctly gets newest (e.g. 2024)
                int periods = historyList.Count - 1;

                // Revenue Growth CAGR
                if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr > 0m)
                {
                    double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr);
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                }

                // Peak Revenue Check: Detects structural decay even if baseline CAGR looks positive
                decimal peakRevenue = historyList.Max(h => h.HistoricalRevenueCr);
                bool isBelowPeak = newest.HistoricalRevenueCr < (peakRevenue * 0.85m); // 15%+ drop from peak

                if (isBelowPeak)
                {
                    salesGrowth = -0.10m; // Override artificially positive CAGR
                }

                if (salesGrowth >= 0.15m)
                    score += 15m;
                else if (salesGrowth > 0m)
                    score += (salesGrowth / 0.15m) * 15m;
            }

            // -------------------------------------------------------------
            // 6. WORKING CAPITAL & DIVIDEND PENALTIES
            // -------------------------------------------------------------
            if (!data.IsFinancialSector && data.WorkingCapitalCr < 0m && data.NetCashCr < 0m)
            {
                score -= 8m;
            }

            decimal dividendPayoutPercent = data.DividendPayoutPercent;

            if (data.NetCashCr >= 0m && data.NetProfitCr > 0m && dividendPayoutPercent == 0m)
            {
                score -= 5m;
            }

            // Standard Penalty Adjustments
            if (roePercent < 8.0m)
            {
                score -= 15m;
            }

            if (salesGrowth < 0m && hasValidHistory)
            {
                score -= 15m;
            }

            // -------------------------------------------------------------
            // 7. HARD VALUE TRAP INTERCEPTOR
            // -------------------------------------------------------------

            // 1. Small absolute net profit (< 10 Cr) makes 11% ROE volatile and unreliable
            bool isLowAbsoluteProfit = data.NetProfitCr < 10.0m;

            // 2. High Price-to-Book discount on declining retail models
            bool isDeepDiscountTrap = (data.NetProfitCr < 10.0m) && (roePercent < 12.0m);

            bool isValueTrap = roePercent < 5.0m
                || (salesGrowth < 0m && hasValidHistory)
                || data.NetProfitCr <= 0m
                || isDeepDiscountTrap
                || isLowAbsoluteProfit;

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            if (isValueTrap)
            {
                // Caps score at 35 (UNSOUND)
                return Math.Min(finalScore, 35);
            }

            return finalScore;
        }
    }
}