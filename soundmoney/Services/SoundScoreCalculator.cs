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
            // 0. UNIT NORMALIZATION & DERIVED METRICS
            // Standardizes percentages so 15% is represented as 15.0m
            // -------------------------------------------------------------
            decimal roePercent = data.ReportedRoePercent <= 1.0m && data.ReportedRoePercent > -1.0m
                ? data.ReportedRoePercent * 100m
                : data.ReportedRoePercent;

            decimal roaPercent = data.ReportedRoaPercent <= 1.0m && data.ReportedRoaPercent > -1.0m
                ? data.ReportedRoaPercent * 100m
                : data.ReportedRoaPercent;

            decimal opmPercent = (data.RevenueCr > 0m && !data.IsFinancialSector)
                ? (data.EbitCr / data.RevenueCr) * 100m
                : 0m;

            // -------------------------------------------------------------
            // 1. MARGIN OF SAFETY (Max 25 Pts - Scaled to ROE Quality)
            // -------------------------------------------------------------
            // Weak ROE (<12%) prevents cheap valuation from driving a high score
            decimal maxMosContribution = (roePercent < 12.0m && !data.IsFinancialSector) ? 12m : 25m;

            if (marginOfSafety >= 30m)
                score += maxMosContribution;
            else if (marginOfSafety > 0m)
                score += Math.Min(maxMosContribution, 10m + (marginOfSafety / 30m * 15m));
            else if (marginOfSafety >= -20m)
                score += Math.Max(0m, 10m + (marginOfSafety / 20m * 10m));

            // -------------------------------------------------------------
            // 2. CAPITAL EFFICIENCY: ROE / ROA (Max 25 Pts)
            // -------------------------------------------------------------
            if (data.IsFinancialSector)
            {
                if (roaPercent >= 2.0m) score += 25m;
                else if (roaPercent >= 1.5m) score += 18m;
                else if (roaPercent >= 1.0m) score += 10m;
            }
            else
            {
                if (roePercent >= 20m) score += 25m;
                else if (roePercent >= 15m) score += 20m;
                else if (roePercent >= 12m) score += 12m;
                else if (roePercent >= 8m) score += 5m;
            }

            // -------------------------------------------------------------
            // 3. SOLVENCY & LEVERAGE (Max 20 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                if (data.NetCashCr >= 0m)
                {
                    // Require capital efficiency to grant full solvency points
                    score += (roePercent >= 12.0m) ? 20m : 10m;
                }
                else if (data.EbitCr > 0m)
                {
                    decimal netDebt = Math.Abs(data.NetCashCr);
                    decimal debtToEbit = netDebt / data.EbitCr;

                    if (debtToEbit <= 1.5m) score += 15m;
                    else if (debtToEbit <= 3.0m) score += 8m;
                }
            }
            else
            {
                if (data.CapitalAdequacyPercent >= 16m) score += 20m;
                else if (data.CapitalAdequacyPercent >= 13m) score += 12m;
            }

            // -------------------------------------------------------------
            // 4. CASH FLOW QUALITY & OPERATING MARGINS (Max 15 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                if (data.NetProfitCr > 0m && data.CashFromOperationsCr > 0m)
                {
                    decimal cashConversion = data.CashFromOperationsCr / data.NetProfitCr;
                    if (cashConversion >= 1.0m) score += 15m;
                    else if (cashConversion >= 0.7m) score += 10m;
                }
            }
            else
            {
                score += 10m; // Standard baseline score for qualified financials
            }

            // -------------------------------------------------------------
            // 5. HISTORICAL GROWTH & PEAK TESTS (Max 15 Pts)
            // -------------------------------------------------------------
            var historyList = historicals?.OrderBy(h => h.Year).ToList();

            decimal salesGrowth = 0m;
            bool hasValidHistory = historyList != null && historyList.Count >= 3;

            if (hasValidHistory)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr > 0m)
                {
                    double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr);
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                }

                decimal peakRevenue = historyList.Max(h => h.HistoricalRevenueCr);
                if (newest.HistoricalRevenueCr < (peakRevenue * 0.85m))
                {
                    salesGrowth = -0.10m; // Penalize structural top-line drop
                }

                if (salesGrowth >= 0.15m)
                    score += 15m;
                else if (salesGrowth > 0m)
                    score += (salesGrowth / 0.15m) * 15m;
            }

            // -------------------------------------------------------------
            // 6. MARGIN & CAPITAL DEDUCTIONS
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                // Thin Operating Margin Penalty (< 8%)
                if (opmPercent > 0m && opmPercent < 8.0m)
                {
                    score -= 10m;
                }

                // Working Capital Stress Check
                if (data.WorkingCapitalCr < 0m && data.NetCashCr < 0m)
                {
                    score -= 8m;
                }

                // Low ROE Penalty
                if (roePercent < 10.0m)
                {
                    score -= 15m;
                }
            }

            if (salesGrowth < 0m && hasValidHistory)
            {
                score -= 15m;
            }

            // -------------------------------------------------------------
            // 7. VALUE TRAP INTERCEPTOR & CAP
            // -------------------------------------------------------------
            bool isMicroProfit = data.NetProfitCr < 10.0m;
            bool isCapitalDestroyer = !data.IsFinancialSector && roePercent < 10.0m && salesGrowth < 0.05m;
            bool isHighDebtCommodity = !data.IsFinancialSector && opmPercent < 10.0m && data.NetCashCr < -200m;
            bool isDeclining = salesGrowth < 0m && hasValidHistory;

            bool isValueTrap = roePercent < 5.0m
                || isDeclining
                || data.NetProfitCr <= 0m
                || isMicroProfit
                || isCapitalDestroyer
                || isHighDebtCommodity;

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            // Hard Cap at 35 for Value Traps
            return isValueTrap ? Math.Min(finalScore, 35) : finalScore;
        }
    }
}