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
           

            // Normalize ROE to decimal fraction
            decimal roeDecimal = data.ReportedRoePercent < 1m ? data.ReportedRoePercent : data.ReportedRoePercent / 100m;

            // 1. Margin of Safety (Max 30 Pts)
            if (marginOfSafety >= 30m)
                score += 30m;
            else if (marginOfSafety > 0m)
                score += 15m + (marginOfSafety / 30m * 14m);
            else if (marginOfSafety >= -20m)
                score += Math.Max(0m, 14m + (marginOfSafety / 20m * 14m));

            // 2. Capital Efficiency: ROE / ROA (Max 20 Pts)
            if (data.IsFinancialSector)
            {
                decimal roaDecimal = data.ReportedRoaPercent < 1m ? data.ReportedRoaPercent : data.ReportedRoaPercent / 100m;
                if (roaDecimal >= 0.018m) score += 20m;
                else if (roaDecimal >= 0.012m) score += 15m;
                else if (roaDecimal >= 0.008m) score += 10m;
            }
            else
            {
                if (roeDecimal >= 0.20m) score += 20m;
                else if (roeDecimal >= 0.15m) score += 15m;
                else if (roeDecimal >= 0.10m) score += 10m;
            }

            // 3. Solvency & Debt Health (Max 20 Pts)
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

            // 4. Cash Flow Quality: OCF / Net Profit (Max 15 Pts)
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

            // 5. OCF Growth Stability & Multi-Year Sales Growth Analysis (Max 15 Pts)
            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            decimal salesGrowth = 0m;

            if (historyList != null && historyList.Count >= 3)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (!data.IsFinancialSector && oldest.HistoricalOcfCr > 0 && newest.HistoricalOcfCr > 0)
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

                // Robust Sales Growth calculation supporting negative trends
                if (oldest.HistoricalRevenueCr > 0m)
                {
                    if (newest.HistoricalRevenueCr > 0m)
                    {
                        double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr);
                        salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                    }
                    else
                    {
                        salesGrowth = -1.0m; // Complete revenue loss flag
                    }
                }

                // --- Additional Institutional Quality Checks ---

                // A. Share Dilution Penalty (Piotroski Criterion)
                if (oldest.HistoricalSharesCr > 0m && newest.HistoricalSharesCr > oldest.HistoricalSharesCr * 1.02m)
                {
                    score -= 5m;
                }

                // B. Accrual Quality Penalty
                decimal totalOcf = historyList.Sum(h => h.HistoricalOcfCr);
                decimal totalPat = historyList.Sum(h => h.HistoricalPatCr);
                if (!data.IsFinancialSector && totalPat > 0m && totalOcf < (0.5m * totalPat))
                {
                    score -= 10m;
                }
            }
            else
            {
                score += 7m;
            }

            // --- Working Capital & Capital Allocation Penalties ---

            // C. Financial Distress Check
            if (!data.IsFinancialSector && data.CwipCr < 0m && data.NetCashCr < 0m)
            {
                score -= 8m;
            }

            // D. Capital Allocation Penalty
            decimal dividendPayout = data.DividendPayoutPercent < 1m ? data.DividendPayoutPercent : data.DividendPayoutPercent / 100m;
            if (data.NetCashCr >= 0m && data.NetProfitCr > 0m && dividendPayout == 0m)
            {
                score -= 5m;
            }

            // --- Quality & Growth Gatekeeper Penalties (Value Trap Protection) ---
            if (roeDecimal < 0.08m)
            {
                score -= 15m;
            }

            if (salesGrowth < 0m)
            {
                score -= 15m;
            }

            // Hard cap to prevent clean balance sheets with declining fundamentals from scoring "Strong"
            if (roeDecimal < 0.08m && salesGrowth < 0m)
            {
                score = Math.Min(score, 55m);
            }

            return (int)Math.Clamp(Math.Round(score), 0, 100);
        }
    }
}