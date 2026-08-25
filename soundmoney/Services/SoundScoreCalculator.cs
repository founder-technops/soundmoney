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
            decimal roePercent = (data.ReportedRoePercent <= 1.0m && data.ReportedRoePercent > -1.0m)
                ? data.ReportedRoePercent * 100m
                : data.ReportedRoePercent;

            decimal roaPercent = (data.ReportedRoaPercent <= 1.0m && data.ReportedRoaPercent > -1.0m)
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
            {
                score += maxMosContribution;
            }
            else if (marginOfSafety > 0m)
            {
                // Smooth interpolation between 10 pts and maxMosContribution
                score += 10m + ((marginOfSafety / 30m) * (maxMosContribution - 10m));
            }
            else if (marginOfSafety >= -20m)
            {
                // Smooth reduction down from 10 pts to 0 pts
                score += Math.Max(0m, 10m * (1m + (marginOfSafety / 20m)));
            }

            // -------------------------------------------------------------
            // 2. CAPITAL EFFICIENCY: ROE / ROA (Max 25 Pts)
            // -------------------------------------------------------------
            if (data.IsFinancialSector)
            {
                if (roaPercent >= 2.0m) score += 25m;
                else if (roaPercent >= 1.5m) score += 18m;
                else if (roaPercent >= 1.0m) score += 10m;
                else if (roaPercent >= 0.5m) score += 5m;
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
                    else if (debtToEbit <= 4.5m) score += 4m;
                }
            }
            else
            {
                if (data.CapitalAdequacyPercent >= 16m) score += 20m;
                else if (data.CapitalAdequacyPercent >= 13m) score += 12m;
                else if (data.CapitalAdequacyPercent >= 11m) score += 5m;
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
                    else if (cashConversion >= 0.4m) score += 5m;
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
            // 6. GOVERNANCE & MARGIN DEDUCTIONS
            // -------------------------------------------------------------
            // Promoter Share Pledging Penalty (Crucial for stocks like ASHOKLEY)
            decimal pledgePercent = (data.PromoterPledgePercent <= 1.0m && data.PromoterPledgePercent > 0m)
                ? data.PromoterPledgePercent * 100m
                : data.PromoterPledgePercent;

            if (pledgePercent >= 25.0m)
            {
                score -= 15m;
            }
            else if (pledgePercent >= 10.0m)
            {
                score -= 8m;
            }

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
                    score -= 12m;
                }
            }

            if (salesGrowth < 0m && hasValidHistory)
            {
                score -= 10m;
            }

            // -------------------------------------------------------------
            // 7. VALUE TRAP INTERCEPTOR & CAP
            // -------------------------------------------------------------
            bool isCapitalDestroyer = !data.IsFinancialSector && roePercent < 8.0m && salesGrowth < 0.05m;
            bool isHighDebtCommodity = !data.IsFinancialSector && opmPercent < 8.0m && data.NetCashCr < -300m;
            bool isDeclining = salesGrowth < 0m && hasValidHistory;
            bool isSeverePledge = pledgePercent >= 35.0m; // High promoter pledge cap

            bool isValueTrap = roePercent < 5.0m
                || isDeclining
                || data.NetProfitCr <= 0m
                || isCapitalDestroyer
                || isHighDebtCommodity
                || isSeverePledge;

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            // Hard Cap at 40 for Value Traps / Governance Risk
            return isValueTrap ? Math.Min(finalScore, 40) : finalScore;
        }
    }
}