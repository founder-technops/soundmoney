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

            decimal roicPercent = data.RoicPercent;

            decimal opmPercent = (data.SalesCr > 0m && !data.IsFinancialSector)
                ? data.OperatingProfitMargin
                : 0m;

            // Evaluate Dividend Health Rating from historical financials
            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            DividendAnalysisResult dividendAnalysis = DividendEvaluator.Evaluate(data, historyList ?? new List<HistoricalFinancial>());

            // -------------------------------------------------------------
            // 1. MARGIN OF SAFETY (Max 25 Pts - Scaled to ROE/ROIC Quality)
            // -------------------------------------------------------------
            // Weak ROE (<12%) or weak ROIC (<10%) prevents cheap valuation from driving a high score
            decimal maxMosContribution = ((roePercent < 12.0m || roicPercent < 10.0m) && !data.IsFinancialSector) ? 12m : 25m;

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
            // 2. CAPITAL EFFICIENCY: ROE & ROIC BLEND (Max 25 Pts)
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
                // ROE Allocation (Shareholder Return - Max 15 Pts)
                if (roePercent >= 20m) score += 15m;
                else if (roePercent >= 15m) score += 12m;
                else if (roePercent >= 12m) score += 8m;
                else if (roePercent >= 8m) score += 4m;

                // ROIC Allocation (Operational Return on Capital - Max 10 Pts)
                if (roicPercent >= 20m) score += 10m;
                else if (roicPercent >= 15m) score += 8m;
                else if (roicPercent >= 12m) score += 5m;
                else if (roicPercent >= 8m) score += 2m;
            }

            // -------------------------------------------------------------
            // 3. SOLVENCY & LEVERAGE (Max 20 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                decimal leverageCr = data.IsCashEstimateReliable ? -data.NetCashCr : data.TotalBorrowingsCr;

                if (leverageCr <= 0m)
                {
                    // Require capital efficiency to grant full solvency points
                    score += (roePercent >= 12.0m || roicPercent >= 10.0m) ? 20m : 10m;
                }
                else if (data.EbitCr > 0m)
                {
                    decimal debtToEbit = leverageCr / data.EbitCr;

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
            // Blends Revenue Growth (Top-Line) & Net Profit Growth (Bottom-Line)
            // -------------------------------------------------------------
            decimal salesGrowth = 0m;
            decimal profitGrowth = 0m;
            bool hasValidHistory = historyList != null && historyList.Count >= 3;
            bool hasValidProfitGrowth = false;

            if (hasValidHistory)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                // 1. Revenue Growth (Top-Line)
                if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr > 0m)
                {
                    double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr);
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                }

                decimal peakRevenue = historyList.Max(h => h.HistoricalRevenueCr);
                if (newest.HistoricalRevenueCr < (peakRevenue * 0.85m))
                {
                    salesGrowth = -0.10m; // Penalize structural top-line drop (>15% from peak)
                }

                // 2. Net Profit / PAT Growth (Bottom-Line)
                if (oldest.HistoricalNetProfitCr > 0m && newest.HistoricalNetProfitCr > 0m)
                {
                    double patRatio = (double)(newest.HistoricalNetProfitCr / oldest.HistoricalNetProfitCr);
                    profitGrowth = (decimal)(Math.Pow(patRatio, 1.0 / periods) - 1.0);
                    hasValidProfitGrowth = true;
                }

                decimal peakProfit = historyList.Max(h => h.HistoricalNetProfitCr);
                if (peakProfit > 0m && newest.HistoricalNetProfitCr < (peakProfit * 0.70m))
                {
                    profitGrowth = -0.10m; // Penalize severe bottom-line drop (>30% from peak)
                }

                // 3. Score Allocation
                if (data.IsFinancialSector)
                {
                    decimal patPoints = (hasValidProfitGrowth && profitGrowth > 0m) ? Math.Min(10m, (profitGrowth / 0.15m) * 10m) : 0m;
                    decimal revPoints = (salesGrowth > 0m) ? Math.Min(5m, (salesGrowth / 0.15m) * 5m) : 0m;
                    score += (patPoints + revPoints);
                }
                else
                {
                    decimal revPoints = (salesGrowth > 0m) ? Math.Min(7.5m, (salesGrowth / 0.15m) * 7.5m) : 0m;
                    decimal patPoints = 0m;
                    if (hasValidProfitGrowth && profitGrowth > 0m)
                    {
                        decimal cfoPatRatio = (data.NetProfitCr > 0m && data.CashFromOperationsCr > 0m)
                            ? (data.CashFromOperationsCr / data.NetProfitCr)
                            : 0m;
                        decimal maxPatPts = (cfoPatRatio < 0.50m) ? 3.75m : 7.5m;
                        patPoints = Math.Min(maxPatPts, (profitGrowth / 0.15m) * maxPatPts);
                    }
                    score += (revPoints + patPoints);
                }
            }

            // -------------------------------------------------------------
            // 6. DIVIDEND HEALTH RATING INTEGRATION
            // Bonus / Penalty based on DividendEvaluator rating
            // -------------------------------------------------------------
            switch (dividendAnalysis.HealthRating)
            {
                case "Elite (Dividend Champion)": score += 5m; break;
                case "Reliable": score += 3m; break;
                case "Moderate": score += 1m; break;
                case "Unstable":
                    if (dividendAnalysis.ConsecutiveYearsPaid > 0 && !dividendAnalysis.IsFcfSupported) score -= 5m;
                    break;
            }

            // -------------------------------------------------------------
            // 7. GOVERNANCE, MARGIN & ROIC DEDUCTIONS
            // -------------------------------------------------------------
            decimal pledgePercent = (data.PromoterPledgePercent <= 1.0m && data.PromoterPledgePercent > 0m)
                ? data.PromoterPledgePercent * 100m
                : data.PromoterPledgePercent;

            if (pledgePercent >= 25.0m) score -= 15m;
            else if (pledgePercent >= 10.0m) score -= 8m;

            if (!data.IsFinancialSector)
            {
                // Thin Operating Margin Penalty (< 8%)
                if (opmPercent > 0m && opmPercent < 8.0m) score -= 10m;

                // Working Capital Stress Check
                bool hasNetDebt = data.IsCashEstimateReliable ? data.NetCashCr < 0m : data.TotalBorrowingsCr > 0m;
                if (data.WorkingCapitalCr < 0m && hasNetDebt) score -= 8m;

                // Low ROE Penalty (< 10%)
                if (roePercent < 10.0m) score -= 12m;

                // Poor Operational ROIC Penalty (< 8% Cost of Capital Benchmark)
                if (roicPercent < 8.0m) score -= 8m;
            }

            if (salesGrowth < 0m && hasValidHistory) score -= 5m;
            if (profitGrowth < 0m && hasValidHistory && hasValidProfitGrowth) score -= 5m;

            // -------------------------------------------------------------
            // 8. VALUE TRAP INTERCEPTOR & CAP
            // -------------------------------------------------------------
            bool hasHeavyNetDebt = data.IsCashEstimateReliable ? data.NetCashCr < -300m : data.TotalBorrowingsCr > 300m;
            bool isCapitalDestroyer = !data.IsFinancialSector && (roePercent < 8.0m || roicPercent < 5.0m) && salesGrowth < 0.05m;
            bool isHighDebtCommodity = !data.IsFinancialSector && opmPercent < 8.0m && hasHeavyNetDebt;
            bool isDeclining = (salesGrowth < 0m || (hasValidProfitGrowth && profitGrowth < 0m)) && hasValidHistory;
            bool isSeverePledge = pledgePercent >= 35.0m;

            bool isPaperProfitTrap = !data.IsFinancialSector
                && data.NetProfitCr > 0m
                && (data.CashFromOperationsCr <= 0m || (data.CashFromOperationsCr / data.NetProfitCr) < 0.20m);

            // FCF Drain Trap: Positive net profit but negative FCF due to excessive Capex
            bool isFcfDrainTrap = !data.IsFinancialSector
                && data.NetProfitCr > 0m
                && data.FreeCashFlowCr < 0m
                && data.CashFromOperationsCr / data.NetProfitCr < 0.50m;

            bool isValueTrap = roePercent < 5.0m
                || (!data.IsFinancialSector && roicPercent < 5.0m)
                || isDeclining
                || data.NetProfitCr <= 0m
                || isCapitalDestroyer
                || isHighDebtCommodity
                || isSeverePledge
                || isPaperProfitTrap
                || isFcfDrainTrap; // Hard cap score at 40 if company burns cash after Capex

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            // Hard Cap at 40 for Value Traps / Governance Risk / Paper Profits
            return isValueTrap ? Math.Min(finalScore, 40) : finalScore;
        }
    }
}