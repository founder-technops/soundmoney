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

            // Standardize ROE consistently as a percentage (e.g., 15.0 = 15%)
            decimal roePercent = data.ReportedRoePercent <= 1.0m
                ? data.ReportedRoePercent * 100m
                : data.ReportedRoePercent;

            // Standardize ROA as a percentage
            decimal roaPercent = data.ReportedRoaPercent <= 1.0m && !data.IsFinancialSector
                ? data.ReportedRoaPercent * 100m
                : data.ReportedRoaPercent;

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
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                // OCF CAGR Check (Handles negative numbers gracefully without throwing NaN)
                if (!data.IsFinancialSector && oldest.HistoricalOcfCr > 0m && newest.HistoricalOcfCr > 0m)
                {
                    double ratio = (double)(newest.HistoricalOcfCr / oldest.HistoricalOcfCr);
                    decimal ocfCagr = (decimal)(Math.Pow(ratio, 1.0 / periods) - 1.0);

                    if (ocfCagr >= 0.12m) score += 15m;
                    else if (ocfCagr >= 0.05m) score += 10m;
                }
                else if (data.IsFinancialSector)
                {
                    score += 10m;
                }

                // Multi-Year Sales Growth
                if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr > 0m)
                {
                    double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr);
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                }
                else if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr <= 0m)
                {
                    salesGrowth = -1.0m;
                }

                // Share Dilution Penalty
                if (oldest.HistoricalSharesCr > 0m && newest.HistoricalSharesCr > oldest.HistoricalSharesCr * 1.02m)
                {
                    score -= 5m;
                }

                // Accrual Quality Penalty
                decimal totalOcf = historyList.Sum(h => h.HistoricalOcfCr);
                decimal totalPat = historyList.Sum(h => h.HistoricalPatCr);
                if (!data.IsFinancialSector && totalPat > 0m && totalOcf < (0.5m * totalPat))
                {
                    score -= 10m;
                }
            }
            else
            {
                score += 7m; // Default allocation for insufficient history
            }

            // -------------------------------------------------------------
            // 6. WORKING CAPITAL & DIVIDEND PENALTIES
            // -------------------------------------------------------------
            if (!data.IsFinancialSector && data.WorkingCapitalCr < 0m && data.NetCashCr < 0m)
            {
                score -= 8m;
            }

            decimal dividendPayoutPercent = data.DividendPayoutPercent <= 1.0m
                ? data.DividendPayoutPercent * 100m
                : data.DividendPayoutPercent;

            if (data.NetCashCr >= 0m && data.NetProfitCr > 0m && dividendPayoutPercent == 0m)
            {
                score -= 5m;
            }

            // -------------------------------------------------------------
            // 7. STRUCTURAL GATEKEEPERS & VALUE-TRAP PROTECTION
            // -------------------------------------------------------------

            // Standard Penalties
            if (roePercent < 8.0m)
            {
                score -= 15m;
            }

            if (salesGrowth < 0m && hasValidHistory)
            {
                score -= 15m;
            }

            // VALUE TRAP HARD CAP: 
            // If a company fails basic profitability (ROE < 5%) OR has structurally declining revenue, 
            // cap its maximum score to 35 (UNSOUND) regardless of how "cheap" the margin of safety looks.
            bool isValueTrap = roePercent < 5.0m || (salesGrowth < 0m && hasValidHistory) || data.NetProfitCr <= 0m;

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            if (isValueTrap)
            {
                return Math.Min(finalScore, 35); 
            }

            return finalScore;
        }
    }
}