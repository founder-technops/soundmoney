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
            // 0. UNIT NORMALIZATION & DERIVED ADVANCED METRICS
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

            // Derived Free Cash Flow (FCF = CFO - Capex)
            decimal fcfCr = data.FreeCashFlowCr != 0m
                ? data.FreeCashFlowCr
                : data.CashFromOperationsCr - Math.Abs(data.GrossCapexCr); 

            // Derived CROIC (Cash Return on Invested Capital = FCF / Invested Capital)
            decimal croicPercent = (data.InvestmentsCr > 0m && !data.IsFinancialSector)
                ? (fcfCr / data.InvestmentsCr) * 100m
                : 0m;

            // Derived Sloan Ratio (Accrual & Earnings Quality Index)
            decimal sloanRatio = (data.TotalAssetsCr > 0m && !data.IsFinancialSector)
                ? ((data.NetProfitCr - data.CashFromOperationsCr) / data.TotalAssetsCr) * 100m
                : 0m;

            // Evaluate Dividend Health Rating from historical financials
            var historyList = historicals?.OrderBy(h => h.Year).ToList(); 
            DividendAnalysisResult dividendAnalysis = DividendEvaluator.Evaluate(data, historyList ?? new List<HistoricalFinancial>()); 

            // -------------------------------------------------------------
            // 1. MARGIN OF SAFETY (Max 25 Pts - Scaled to ROE/ROIC Quality)
            // -------------------------------------------------------------
            decimal maxMosContribution = ((roePercent < 12.0m || roicPercent < 10.0m) && !data.IsFinancialSector) ? 12m : 25m; 

            if (marginOfSafety >= 30m)
            {
                score += maxMosContribution; 
            }
            else if (marginOfSafety > 0m)
            {
                score += 10m + ((marginOfSafety / 30m) * (maxMosContribution - 10m)); 
            }
            else if (marginOfSafety >= -20m)
            {
                score += Math.Max(0m, 10m * (1m + (marginOfSafety / 20m))); 
            }

            // -------------------------------------------------------------
            // 2. CAPITAL EFFICIENCY: ROE, ROIC & CROIC BLEND (Max 25 Pts)
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
                // ROE Allocation (Shareholder Return - Max 12 Pts)
                if (roePercent >= 20m) score += 12m;
                else if (roePercent >= 15m) score += 9m;
                else if (roePercent >= 12m) score += 6m;
                else if (roePercent >= 8m) score += 3m;

                // ROIC Allocation (Operational Return - Max 8 Pts)
                if (roicPercent >= 20m) score += 8m;
                else if (roicPercent >= 15m) score += 6m;
                else if (roicPercent >= 12m) score += 4m;
                else if (roicPercent >= 8m) score += 2m;

                // CROIC Allocation (Cash Return on Capital - Max 5 Pts)
                if (croicPercent >= 15m) score += 5m;
                else if (croicPercent >= 10m) score += 3m;
                else if (croicPercent >= 5m) score += 1m;
            }

            // -------------------------------------------------------------
            // 3. SOLVENCY, LEVERAGE & INTEREST COVERAGE (Max 20 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                decimal leverageCr = data.IsCashEstimateReliable ? -data.NetCashCr : data.TotalBorrowingsCr; 

                if (leverageCr <= 0m)
                {
                    score += (roePercent >= 12.0m || roicPercent >= 10.0m) ? 15m : 8m;
                }
                else if (data.EbitCr > 0m)
                {
                    decimal debtToEbit = leverageCr / data.EbitCr; 

                    if (debtToEbit <= 1.5m) score += 12m;
                    else if (debtToEbit <= 3.0m) score += 6m;
                    else if (debtToEbit <= 4.5m) score += 3m;
                }

                // Interest Coverage Buffer (Max 5 Pts)
                if (data.TotalBorrowingsCr > 0m && data.InterestExpenseCr > 0m)
                {
                    decimal interestCoverage = data.EbitCr / data.InterestExpenseCr;
                    if (interestCoverage >= 8.0m) score += 5m;
                    else if (interestCoverage >= 4.0m) score += 3m;
                    else if (interestCoverage < 2.0m) score -= 5m; // Debt servicing strain penalty
                }
                else if (data.TotalBorrowingsCr <= 0m)
                {
                    score += 5m; // Net debt free bonus
                }
            }
            else
            {
                if (data.CapitalAdequacyPercent >= 16m) score += 20m; 
                else if (data.CapitalAdequacyPercent >= 13m) score += 12m; 
                else if (data.CapitalAdequacyPercent >= 11m) score += 5m; 
            }

            // -------------------------------------------------------------
            // 4. CASH FLOW QUALITY & FCF CONVERSION (Max 15 Pts)
            // -------------------------------------------------------------
            if (!data.IsFinancialSector)
            {
                if (data.NetProfitCr > 0m)
                {
                    decimal cfoConversion = data.CashFromOperationsCr / data.NetProfitCr; 
                    decimal fcfConversion = fcfCr / data.NetProfitCr;

                    // FCF Conversion Component (Max 10 Pts)
                    if (fcfConversion >= 0.80m) score += 10m;
                    else if (fcfConversion >= 0.50m) score += 7m;
                    else if (fcfConversion >= 0.20m) score += 4m;

                    // CFO Conversion Component (Max 5 Pts)
                    if (cfoConversion >= 1.0m) score += 5m;
                    else if (cfoConversion >= 0.70m) score += 3m;
                    else if (cfoConversion >= 0.40m) score += 1m;
                }
            }
            else
            {
                score += 10m; 
            }

            // -------------------------------------------------------------
            // 5. HISTORICAL GROWTH & MARGIN STABILITY (Max 15 Pts)
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

                if (oldest.HistoricalRevenueCr > 0m && newest.HistoricalRevenueCr > 0m)
                {
                    double revRatio = (double)(newest.HistoricalRevenueCr / oldest.HistoricalRevenueCr); 
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0); 
                }

                decimal peakRevenue = historyList.Max(h => h.HistoricalRevenueCr); 
                if (newest.HistoricalRevenueCr < (peakRevenue * 0.85m))
                {
                    salesGrowth = -0.10m; 
                }

                if (oldest.HistoricalNetProfitCr > 0m && newest.HistoricalNetProfitCr > 0m)
                {
                    double patRatio = (double)(newest.HistoricalNetProfitCr / oldest.HistoricalNetProfitCr); 
                    profitGrowth = (decimal)(Math.Pow(patRatio, 1.0 / periods) - 1.0); 
                    hasValidProfitGrowth = true; 
                }

                decimal peakProfit = historyList.Max(h => h.HistoricalNetProfitCr); 
                if (peakProfit > 0m && newest.HistoricalNetProfitCr < (peakProfit * 0.70m))
                {
                    profitGrowth = -0.10m; 
                }

                if (data.IsFinancialSector)
                {
                    decimal patPoints = (hasValidProfitGrowth && profitGrowth > 0m) ? Math.Min(10m, (profitGrowth / 0.15m) * 10m) : 0m; 
                    decimal revPoints = (salesGrowth > 0m) ? Math.Min(5m, (salesGrowth / 0.15m) * 5m) : 0m; 
                    score += (patPoints + revPoints); 
                }
                else
                {
                    decimal revPoints = (salesGrowth > 0m) ? Math.Min(6m, (salesGrowth / 0.15m) * 6m) : 0m;
                    decimal patPoints = 0m;
                    if (hasValidProfitGrowth && profitGrowth > 0m)
                    {
                        decimal cfoPatRatio = (data.NetProfitCr > 0m && data.CashFromOperationsCr > 0m)
                            ? (data.CashFromOperationsCr / data.NetProfitCr)
                            : 0m; 
                        decimal maxPatPts = (cfoPatRatio < 0.50m) ? 3m : 6m;
                        patPoints = Math.Min(maxPatPts, (profitGrowth / 0.15m) * maxPatPts);
                    }

                    // Pricing Power & Margin Stability (Max 3 Pts)
                    decimal avgHistoricalOpm = historyList.Average(h => h.HistoricalOpmPercent);
                    decimal marginTrendPoints = (opmPercent >= avgHistoricalOpm) ? 3m : 0m;

                    score += (revPoints + patPoints + marginTrendPoints);
                }
            }

            // -------------------------------------------------------------
            // 6. DIVIDEND HEALTH RATING INTEGRATION
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
            // 7. GOVERNANCE, ACCRUAL & WORKING CAPITAL DEDUCTIONS
            // -------------------------------------------------------------
            decimal pledgePercent = (data.PromoterPledgePercent <= 1.0m && data.PromoterPledgePercent > 0m)
                ? data.PromoterPledgePercent * 100m
                : data.PromoterPledgePercent; 

            if (pledgePercent >= 25.0m) score -= 15m; 
            else if (pledgePercent >= 10.0m) score -= 8m; 

            if (!data.IsFinancialSector)
            {
                if (opmPercent > 0m && opmPercent < 8.0m) score -= 10m; 

                bool hasNetDebt = data.IsCashEstimateReliable ? data.NetCashCr < 0m : data.TotalBorrowingsCr > 0m; 
                if (data.WorkingCapitalCr < 0m && hasNetDebt) score -= 8m; 

                if (roePercent < 10.0m) score -= 12m; 
                if (roicPercent < 8.0m) score -= 8m; 

                // Sloan Ratio Deduction (High Accrual Risk > 10%)
                if (sloanRatio > 10.0m) score -= 8m;

                // Cash Conversion Cycle Efficiency Adjustments
                if (data.CashConversionCycleDays < 0m) score += 3m; // Negative CCC bargaining power
                else if (data.CashConversionCycleDays > 120m) score -= 5m; // Excessively tied up capital
            }

            if (salesGrowth < 0m && hasValidHistory) score -= 5m; 
            if (profitGrowth < 0m && hasValidHistory && hasValidProfitGrowth) score -= 5m; 

            // -------------------------------------------------------------
            // 8. VALUE TRAP INTERCEPTOR & HARD SCORE CAP
            // -------------------------------------------------------------
            bool hasHeavyNetDebt = data.IsCashEstimateReliable ? data.NetCashCr < -300m : data.TotalBorrowingsCr > 300m; 
            bool isCapitalDestroyer = !data.IsFinancialSector && (roePercent < 8.0m || roicPercent < 5.0m) && salesGrowth < 0.05m; 
            bool isHighDebtCommodity = !data.IsFinancialSector && opmPercent < 8.0m && hasHeavyNetDebt; 
            bool isDeclining = (salesGrowth < 0m || (hasValidProfitGrowth && profitGrowth < 0m)) && hasValidHistory; 
            bool isSeverePledge = pledgePercent >= 35.0m; 

            bool isPaperProfitTrap = !data.IsFinancialSector
                && data.NetProfitCr > 0m
                && (data.CashFromOperationsCr <= 0m || (data.CashFromOperationsCr / data.NetProfitCr) < 0.20m); 

            bool isFcfDrainTrap = !data.IsFinancialSector
                && data.NetProfitCr > 0m
                && fcfCr < 0m
                && (data.CashFromOperationsCr / data.NetProfitCr) < 0.50m; 

            bool isAggressiveAccrualTrap = !data.IsFinancialSector && sloanRatio > 18.0m;

            bool isValueTrap = roePercent < 5.0m
                || (!data.IsFinancialSector && roicPercent < 5.0m)
                || isDeclining
                || data.NetProfitCr <= 0m
                || isCapitalDestroyer
                || isHighDebtCommodity
                || isSeverePledge
                || isPaperProfitTrap
                || isFcfDrainTrap
                || isAggressiveAccrualTrap;

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100); 

            // Hard Cap at 40 for Value Traps / Governance Risk / Paper Profits / Accrual Manipulation
            return isValueTrap ? Math.Min(finalScore, 40) : finalScore; 
        }
    }
}