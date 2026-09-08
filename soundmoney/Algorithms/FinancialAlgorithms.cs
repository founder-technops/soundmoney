using Microsoft.AspNetCore.Components.Forms;
using SoundMoney.Models;
using SoundMoney.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SoundMoney.Algorithms
{
    public static class FinancialAlgorithms
    {
        public static decimal CalculateOperatingProfitMargin(Financial current) =>
            current.SalesCr > 0m ? Math.Round((current.OperatingProfitCr / current.SalesCr) * 100m, 2) : 0m;

        public static decimal CalculateEbit(Financial current) =>
           (current.OperatingProfitCr + current.OtherIncomeCr) - current.DepreciationCr;

        public static decimal CalculateEbitda(Financial current) =>
            current.OperatingProfitCr + current.OtherIncomeCr;

        public static decimal CalculateTotalEquity(Financial current) =>
            current.ShareCapitalCr + current.ReservesCr ;

        public static decimal CalculateTotalAssets(Financial current) =>
            current.FixedAssetsCr + current.CwipCr + current.InvestmentsCr + current.OtherAssetsCr;

        public static decimal CalculateTotalLiabilities(Financial current) =>
            current.ShareCapitalCr + current.ReservesCr + current.TotalBorrowingsCr + current.OtherLiabilitiesCr;

        public static decimal CalculateNetCash(Financial current) =>
            current.CashAndEquivalentsCr - current.TotalBorrowingsCr;

        public static decimal CalculateNonCurrentAssets(Financial current) =>
            current.FixedAssetsCr + current.CwipCr + current.InvestmentsCr;

        public static decimal CalculateCurrentAssets(Financial current) =>
            Math.Max(0m, CalculateTotalAssets(current) - CalculateNonCurrentAssets(current));

        public static decimal CalculateWorkingCapital(Financial current) =>
            CalculateCurrentAssets(current) - current.OtherLiabilitiesCr;

        public static decimal CalculateCurrentLiabilities(Financial current) =>
            CalculateCurrentAssets(current) - CalculateWorkingCapital(current);

        public static decimal CalculateGrossCapex(Financial current) =>
           current.CashFromOperationsCr - current.FreeCashFlowCr;

        public static decimal CalculateNetCashFlow(Financial current) =>
            current.CashFromOperationsCr + current.CashFromInvestmentCr + current.CashFromFinanceCr;

        public static decimal CalculateCfoToOpRatio(Financial current) =>
            current.OperatingProfitCr > 0m ? Math.Round(current.CashFromOperationsCr/ current.OperatingProfitCr, 4) : 0m;

        public static decimal CalculateCashConversionRatio(Financial current, decimal cfo, decimal netProfit) =>
            current.NetProfitCr > 0m ? Math.Round(current.CashFromOperationsCr / current.NetProfitCr, 2) : 0m;

        public static decimal CalculateCapitalAdequacy(Financial current) =>
            current.IsFinancialSector && CalculateTotalEquity(current) > 0m && CalculateTotalAssets(current) > 0m ? Math.Round((CalculateTotalEquity(current) / CalculateTotalAssets(current)) * 100m, 2) : 0m;

        public static decimal CalculateRoa(Financial current) =>
            CalculateTotalAssets(current) > 0m ? Math.Round((current.NetProfitCr / CalculateTotalAssets(current)) * 100m, 2) : (current.IsFinancialSector ? 1.0m : 0m);

        public static decimal CalculateEffectiveTaxRate(Financial current) =>
            current.TaxPercent > 0m ? Math.Clamp(current.TaxPercent / 100m, 0.0m, 0.35m) : 0.25m;

        public static decimal CalculateCostOfDebt(Financial current) =>
            current.TotalBorrowingsCr > 0m && current.InterestExpenseCr > 0m ? Math.Clamp(current.InterestExpenseCr / current.TotalBorrowingsCr, 0.03m, 0.18m) : 0.08m;

        public static decimal CalculateInvestmentAssetsRatio(Financial current) =>
            CalculateTotalAssets(current) > 0m ? Math.Clamp(current.InvestmentsCr / CalculateTotalAssets(current), 0m, 1m) : 0m;

        public static decimal CalculateInterestIncomeRatio(Financial current) =>
            current.SalesCr > 0m ? Math.Clamp(current.IntrestIncomeCr / current.SalesCr, 0m, 1m) : 0m;

        public static bool CheckCoreInvestmentCompany(Financial current) =>
            current.IsCoreInvestmentCompanyExplicit || (CalculateInvestmentAssetsRatio(current) >= 0.70m && CalculateInterestIncomeRatio(current) < 0.30m);

        public static decimal CalculateRoic(Financial current)
        {
            decimal investedCapital = CalculateTotalEquity(current) + current.TotalBorrowingsCr - current.CashAndEquivalentsCr;
            if (investedCapital <= 0m) return 0m;
            decimal nopat = CalculateEbit(current)  * (1m - CalculateEffectiveTaxRate(current));
            return Math.Round((nopat / investedCapital) * 100m, 2);
        }

        public static decimal CalculateTotalShares(Financial current) =>
            current.CurrentPrice > 0m && current.MarketCapCr > 0m ? Math.Round(current.MarketCapCr / current.CurrentPrice, 4) : 0m;

        public static decimal CalculateCroic(Financial current) =>
            (current.InvestmentsCr > 0m && !current.IsFinancialSector) ? (current.FreeCashFlowCr / current.InvestmentsCr) * 100m : 0m;

        public static decimal CalculateSloanRatio(Financial current) => (CalculateTotalAssets(current) > 0m && !current.IsFinancialSector)
                ? ((current.NetProfitCr - current.CashFromOperationsCr) / CalculateTotalAssets(current)) * 100m
                : 0m;

        public static bool CheckCashPredictable(Financial current) => 
            CalculateFreeCashFlow(current) > 0m && CalculateOcfToNetProfit(current) >= 0.8m && CalculateFcfToNetProfit(current) >= 0.50m
                && CalculateSloanRatio(current) <= 10.0m;

        public static decimal CalculateInterestCoverage(Financial current) => (current.TotalBorrowingsCr > 0m && current.InterestExpenseCr > 0m && !current.IsFinancialSector)
                ? (CalculateEbit(current) / current.InterestExpenseCr)
                : (current.TotalBorrowingsCr <= 0m ? 999m : 0m);

        // =========================================================================
        // 1. CAPEX-TO-DEPRECIATION RATIO
        // =========================================================================
        /// <summary>
        /// Measures capital reinvestment intensity.
        /// Ratio > 1.0 indicates capital expansion/growth reinvestment.
        /// Ratio < 1.0 indicates potential under-investment or asset harvesting.
        /// </summary>
        public static decimal CalculateCapexToDepreciationRatio(Financial current)
        {
            if (current == null || current.DepreciationCr <= 0m) return 0m;

            decimal capex = Math.Abs(CalculateGrossCapex(current));
            return Math.Round(capex / current.DepreciationCr, 2);
        }

        // =========================================================================
        // 2. ALTMAN Z-SCORE (Distress & Bankruptcy Model)
        // =========================================================================
        /// <summary>
        /// Calculates standard Altman Z-Score for Non-Manufacturing / General Companies.
        /// Z > 2.99 = Safe Zone | 1.81 <= Z <= 2.99 = Grey Zone | Z < 1.81 = Distress Zone
        /// </summary>
        public static decimal CalculateAltmanZScore(Financial current)
        {
            if (current == null || CalculateTotalAssets(current) <= 0m || current.IsFinancialSector) return 0m;

            decimal totalAssets = CalculateTotalAssets(current);

            // X1: Working Capital / Total Assets
            decimal x1 = CalculateWorkingCapital(current) / totalAssets;

            // X2: Retained Earnings / Total Assets
            decimal x2 = current.ReservesCr / totalAssets;

            // X3: EBIT / Total Assets
            decimal x3 = CalculateEbit(current) / totalAssets;

            // X4: Market Value of Equity / Total Liabilities
            decimal totalLiabilities = current.TotalBorrowingsCr + current.OtherLiabilitiesCr;
            decimal x4 = totalLiabilities > 0m ? current.MarketCapCr / totalLiabilities : 10m;

            // X5: Sales / Total Assets
            decimal x5 = current.SalesCr / totalAssets;

            // Z = 1.2*X1 + 1.4*X2 + 3.3*X3 + 0.6*X4 + 0.999*X5
            decimal zScore = (1.2m * x1) + (1.4m * x2) + (3.3m * x3) + (0.6m * x4) + (0.999m * x5);

            return Math.Round(zScore, 2);
        }

        // =========================================================================
        // 3. PIOTROSKI F-SCORE (0 to 9 Financial Health Scale)
        // =========================================================================
        /// <summary>
        /// Calculates Piotroski F-Score across 9 fundamental criteria across Profitability, Leverage, and Operating Efficiency.
        /// Requires at least 2 historical periods (T and T-1).
        /// </summary>
        public static int CalculatePiotroskiFScore(Financial current, IEnumerable<Financial> historicals)
        {
            if (current == null || historicals == null || current.IsFinancialSector) return 0;

            var historyList = historicals.OrderByDescending(h => h.Year).ToList();
            if (historyList.Count < 2) return 0;

            var t = historyList[0];     // Most recent completed year
            var tPrev = historyList[1]; // Previous year (T-1)

            int fScore = 0;

            // --- Profitability Criteria (Max 4 Points) ---

            // 1. Positive Return on Assets (ROA > 0)
            decimal roaT = CalculateTotalAssets(t) > 0m ? t.NetProfitCr / CalculateTotalAssets(t) : 0m;
            decimal roaPrev = CalculateTotalAssets(tPrev) > 0m ? tPrev.NetProfitCr / CalculateTotalAssets(tPrev) : 0m;
            if (roaT > 0m) fScore++;

            // 2. Positive Operating Cash Flow (CFO > 0)
            if (t.CashFromOperationsCr > 0m) fScore++;

            // 3. Quality of Earnings (CFO > Net Income)
            if (t.CashFromOperationsCr > t.NetProfitCr) fScore++;

            // 4. ROA Trend (ROA(t) > ROA(t-1))
            if (roaT > roaPrev) fScore++;

            // --- Leverage, Liquidity & Source of Funds (Max 3 Points) ---

            // 5. Debt Decrease (Long-Term Debt Ratio Decrease)
            decimal leverageT = CalculateTotalAssets(t) > 0m ? t.TotalBorrowingsCr / CalculateTotalAssets(t) : 0m;
            decimal leveragePrev = CalculateTotalAssets(tPrev) > 0m ? tPrev.TotalBorrowingsCr / CalculateTotalAssets(tPrev) : 0m;
            if (leverageT < leveragePrev) fScore++;

            // 6. Current Ratio Increase
            decimal currentRatioT =  CalculateCurrentLiabilities(t) > 0m ? CalculateCurrentAssets(t)  / CalculateCurrentLiabilities(t) : 0m;
            decimal currentRatioPrev = CalculateCurrentLiabilities(tPrev) > 0m ?  CalculateCurrentAssets(tPrev)  / CalculateCurrentLiabilities(tPrev) : 0m;
            if (currentRatioT > currentRatioPrev) fScore++;

            // 7. No Equity Dilution (Shares outstanding in T <= T-1)
            if (t.ShareCapitalCr <= tPrev.ShareCapitalCr) fScore++;

            // --- Operating Efficiency (Max 2 Points) ---

            // 8. Gross Margin Improvement
            decimal grossMarginT = t.SalesCr > 0m ? (t.SalesCr - (t.SalesCr - t.OperatingProfitCr)) / t.SalesCr : 0m;
            decimal grossMarginPrev = tPrev.SalesCr > 0m ? (tPrev.SalesCr - (tPrev.SalesCr - tPrev.OperatingProfitCr)) / tPrev.SalesCr : 0m;
            if (grossMarginT > grossMarginPrev) fScore++;

            // 9. Asset Turnover Improvement (Sales / Total Assets)
            decimal assetTurnoverT = CalculateTotalAssets(t) > 0m ? t.SalesCr / CalculateTotalAssets(t) : 0m;
            decimal assetTurnoverPrev = CalculateTotalAssets(tPrev) > 0m ? tPrev.SalesCr / CalculateTotalAssets(tPrev) : 0m;
            if (assetTurnoverT > assetTurnoverPrev) fScore++;

            return fScore;
        }

        // =========================================================================
        // 4. BENEISH M-SCORE (Earnings Manipulation Detection)
        // =========================================================================
        /// <summary>
        /// Calculates Beneish M-Score to detect financial statement manipulation.
        /// M-Score > -1.78 suggests high probability of earnings manipulation.
        /// Requires at least 2 historical periods (T and T-1).
        /// </summary>
        public static decimal CalculateBeneishMScore(Financial current, IEnumerable<Financial> historicals)
        {
            if (current == null || historicals == null || current.IsFinancialSector) return 0m;

            var historyList = historicals.OrderByDescending(h => h.Year).ToList();
            if (historyList.Count < 2) return 0m;

            var t = historyList[0];     // Period T
            var tPrev = historyList[1]; // Period T-1

            if (tPrev.SalesCr <= 0m || t.SalesCr <= 0m ||
                CalculateTotalAssets(tPrev) <= 0m || CalculateTotalAssets(t) <= 0m)
                return 0m;

            // 1. DSRI: Days Sales in Receivables Index
            // Formula approximation assuming working capital receivables proxy
            decimal recT = Math.Max(0m, CalculateWorkingCapital(t));
            decimal recPrev = Math.Max(0m, CalculateWorkingCapital(tPrev));
            decimal dsri = (t.SalesCr > 0m && tPrev.SalesCr > 0m && recPrev > 0m)
                ? (recT / t.SalesCr) / (recPrev / tPrev.SalesCr)
                : 1.0m;

            // 2. GMI: Gross Margin Index
            decimal gmPrev = tPrev.SalesCr > 0m ? tPrev.OperatingProfitCr / tPrev.SalesCr : 1.0m;
            decimal gmT = t.SalesCr > 0m ? t.OperatingProfitCr / t.SalesCr : 1.0m;
            decimal gmi = gmT > 0m ? gmPrev / gmT : 1.0m;

            // 3. AQI: Asset Quality Index
            decimal nonCurrentAssetsT = CalculateTotalAssets(t) -  CalculateCurrentAssets(t)  - t.FixedAssetsCr;
            decimal nonCurrentAssetsPrev = CalculateTotalAssets(tPrev) -  CalculateCurrentAssets(tPrev)  - tPrev.FixedAssetsCr;
            decimal aqiT = CalculateTotalAssets(t) > 0m ? 1m - (nonCurrentAssetsT / CalculateTotalAssets(t)) : 1m;
            decimal aqiPrev = CalculateTotalAssets(tPrev) > 0m ? 1m - (nonCurrentAssetsPrev / CalculateTotalAssets(tPrev)) : 1m;
            decimal aqi = aqiPrev > 0m ? aqiT / aqiPrev : 1.0m;

            // 4. SGI: Sales Growth Index
            decimal sgi = t.SalesCr / tPrev.SalesCr;

            // 5. DEPI: Depreciation Index
            decimal depRatePrev = tPrev.FixedAssetsCr > 0m ? tPrev.DepreciationCr / (tPrev.FixedAssetsCr + tPrev.DepreciationCr) : 0.1m;
            decimal depRateT = t.FixedAssetsCr > 0m ? t.DepreciationCr / (t.FixedAssetsCr + t.DepreciationCr) : 0.1m;
            decimal depi = depRateT > 0m ? depRatePrev / depRateT : 1.0m;

            // 6. SGAI: Sales, General & Administrative Expenses Index
            decimal sgaiT = t.SalesCr > 0m ? t.ExpenseCr / t.SalesCr : 1.0m;
            decimal sgaiPrev = tPrev.SalesCr > 0m ? tPrev.ExpenseCr / tPrev.SalesCr : 1.0m;
            decimal sgai = sgaiPrev > 0m ? sgaiT / sgaiPrev : 1.0m;

            // 7. LVGI: Leverage Index
            decimal levT = CalculateTotalAssets(t) > 0m ? t.TotalBorrowingsCr / CalculateTotalAssets(t) : 1.0m;
            decimal levPrev = CalculateTotalAssets(tPrev) > 0m ? tPrev.TotalBorrowingsCr / CalculateTotalAssets(tPrev) : 1.0m;
            decimal lvgi = levPrev > 0m ? levT / levPrev : 1.0m;

            // 8. TATA: Total Accruals to Total Assets
            decimal totalAccruals = t.NetProfitCr - t.CashFromOperationsCr;
            decimal tata = CalculateTotalAssets(t) > 0m ? totalAccruals / CalculateTotalAssets(t) : 0m;

            // Beneish M-Score Formula (8-variable model):
            double mScore = -4.84
                + (0.92 * (double)dsri)
                + (0.528 * (double)gmi)
                + (0.404 * (double)aqi)
                + (0.892 * (double)sgi)
                + (0.115 * (double)depi)
                - (0.172 * (double)sgai)
                + (4.679 * (double)tata)
                + (0.327 * (double)lvgi);

            return Math.Round((decimal)mScore, 2);
        }

        /// <summary>
        /// Evaluates historical dividend performance and safety metrics.
        /// </summary>
        /// <param name="historical">List of historical financial records.</param>
        /// <param name="current">Current Financial metrics (for NetProfit, CapitalAdequacy, etc.).</param>
        /// <returns>DividendAnalysisResult containing consistency checks and safety rating.</returns>
        public static DividendAnalysisResult CalculateDividend(Financial current, List<Financial> historical)
        {
            if (historical == null || !historical.Any() || current == null)
                return new DividendAnalysisResult();

            bool isFinancialSector = current.IsFinancialSector;

            // Sort historical records by year descending (newest first)
            var sortedHistory = historical.OrderByDescending(h => h.Year).ToList();

            int paidStreak = 0;
            int growthStreak = 0;

            for (int i = 0; i < sortedHistory.Count; i++)
            {
                var currentH = sortedHistory[i];

                // 1. Uninterrupted Payment Streak Check
                if (currentH.DividendPayoutPercent > 0m)
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
                    var previousH = sortedHistory[i + 1];

                    // Maintain growth/stability streak if payout ratio stays within 10% tolerance
                    if (currentH.DividendPayoutPercent >= previousH.DividendPayoutPercent * 0.9m)
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
            bool isFcfSupported = recentYears.All(x => x.FreeCashFlowCr > 0m || x.CashFromOperationsCr > 0m);

            // -------------------------------------------------------------
            // DYNAMIC DIVIDEND SAFETY CHECK
            // Uses CAR & Profitability for Financials; FCF & Payout for Non-Financials
            // -------------------------------------------------------------
            bool isDividendSafe = isFinancialSector
                ? (current.NetProfitCr > 0m && averagePayoutRatio <= 60m && CalculateCapitalAdequacy(current) >= 13m)
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

        public static int CalculateSoundScore(decimal marginOfSafety, Financial current, IEnumerable<Financial> historicals)
        {
            if (current == null) return 0;

            decimal score = 0m;

            // -------------------------------------------------------------
            // 0. UNIT NORMALIZATION & DERIVED ADVANCED METRICS
            // Standardizes percentages so 15% is represented as 15.0m
            // -------------------------------------------------------------
            decimal roePercent = (current.ReportedRoePercent <= 1.0m && current.ReportedRoePercent > -1.0m)
                ? current.ReportedRoePercent * 100m
                : current.ReportedRoePercent;

            decimal roaPercent = (CalculateRoa(current)   <= 1.0m && CalculateRoa(current) > -1.0m)
                ? CalculateRoa(current) * 100m
                : CalculateRoa(current);

            decimal roicPercent = CalculateRoic(current);

            decimal opmPercent = (current.SalesCr > 0m && !current.IsFinancialSector)
                ? CalculateOperatingProfitMargin(current)
                : 0m;

            // Derived Free Cash Flow (FCF = CFO - Capex)
            decimal fcfCr = CalculateFreeCashFlow(current);

            // Derived CROIC (Cash Return on Invested Capital = FCF / Invested Capital)
            decimal croicPercent = CalculateCroic(current);

            // Derived Sloan Ratio (Accrual & Earnings Quality Index)
            decimal sloanRatio = CalculateSloanRatio(current);  

            // Evaluate Dividend Health Rating from historical financials
            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            DividendAnalysisResult dividendAnalysis = CalculateDividend(current, historyList ?? new List<Financial>());

            // -------------------------------------------------------------
            // 1. MARGIN OF SAFETY (Max 25 Pts - Scaled to ROE/ROIC Quality)
            // -------------------------------------------------------------
            decimal maxMosContribution = ((roePercent < 12.0m || roicPercent < 10.0m) && !current.IsFinancialSector) ? 12m : 25m;

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
            if (current.IsFinancialSector)
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
            if (!current.IsFinancialSector)
            {
                decimal leverageCr = current.IsCashEstimateReliable ? -  CalculateNetCash(current) : current.TotalBorrowingsCr;

                if (leverageCr <= 0m)
                {
                    score += (roePercent >= 12.0m || roicPercent >= 10.0m) ? 15m : 8m;
                }
                else if (CalculateEbit(current) > 0m)
                {
                    decimal debtToEbit = leverageCr / CalculateEbit(current);

                    if (debtToEbit <= 1.5m) score += 12m;
                    else if (debtToEbit <= 3.0m) score += 6m;
                    else if (debtToEbit <= 4.5m) score += 3m;
                }

                // Interest Coverage Buffer (Max 5 Pts)
                if (current.TotalBorrowingsCr > 0m && current.InterestExpenseCr > 0m)
                {
                    decimal interestCoverage = CalculateEbit(current) / current.InterestExpenseCr;
                    if (interestCoverage >= 8.0m) score += 5m;
                    else if (interestCoverage >= 4.0m) score += 3m;
                    else if (interestCoverage < 2.0m) score -= 5m; // Debt servicing strain penalty
                }
                else if (current.TotalBorrowingsCr <= 0m)
                {
                    score += 5m; // Net debt free bonus
                }
            }
            else
            {
                if ( CalculateCapitalAdequacy(current) >= 16m) score += 20m;
                else if (CalculateCapitalAdequacy(current) >= 13m) score += 12m;
                else if (CalculateCapitalAdequacy(current) >= 11m) score += 5m;
            }

            // -------------------------------------------------------------
            // 4. CASH FLOW QUALITY & FCF CONVERSION (Max 15 Pts)
            // -------------------------------------------------------------
            if (!current.IsFinancialSector)
            {
                if (current.NetProfitCr > 0m)
                {
                    decimal cfoConversion = current.CashFromOperationsCr / current.NetProfitCr;
                    decimal fcfConversion = fcfCr / current.NetProfitCr;

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

                if (oldest.SalesCr > 0m && newest.SalesCr > 0m)
                {
                    double revRatio = (double)(newest.SalesCr / oldest.SalesCr);
                    salesGrowth = (decimal)(Math.Pow(revRatio, 1.0 / periods) - 1.0);
                }

                decimal peakRevenue = historyList.Max(h => h.SalesCr);
                if (newest.SalesCr < (peakRevenue * 0.85m))
                {
                    salesGrowth = -0.10m;
                }

                if (oldest.SalesCr > 0m && newest.SalesCr > 0m)
                {
                    double patRatio = (double)(newest.NetProfitCr / oldest.NetProfitCr);
                    profitGrowth = (decimal)(Math.Pow(patRatio, 1.0 / periods) - 1.0);
                    hasValidProfitGrowth = true;
                }

                decimal peakProfit = historyList.Max(h => h.NetProfitCr);
                if (peakProfit > 0m && newest.NetProfitCr < (peakProfit * 0.70m))
                {
                    profitGrowth = -0.10m;
                }

                if (current.IsFinancialSector)
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
                        decimal cfoPatRatio = (current.NetProfitCr > 0m && current.CashFromOperationsCr > 0m)
                            ? (current.CashFromOperationsCr / current.NetProfitCr)
                            : 0m;
                        decimal maxPatPts = (cfoPatRatio < 0.50m) ? 3m : 6m;
                        patPoints = Math.Min(maxPatPts, (profitGrowth / 0.15m) * maxPatPts);
                    }

                    // Pricing Power & Margin Stability (Max 3 Pts)
                    decimal avgHistoricalOpm = historyList.Average(h => CalculateOperatingProfitMargin(h));
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
            decimal pledgePercent = (current.PromoterPledgePercent <= 1.0m && current.PromoterPledgePercent > 0m)
                ? current.PromoterPledgePercent * 100m
                : current.PromoterPledgePercent;

            if (pledgePercent >= 25.0m) score -= 15m;
            else if (pledgePercent >= 10.0m) score -= 8m;

            if (!current.IsFinancialSector)
            {
                if (opmPercent > 0m && opmPercent < 8.0m) score -= 10m;

                bool hasNetDebt = current.IsCashEstimateReliable ? CalculateNetCash(current) < 0m : current.TotalBorrowingsCr > 0m;
                if (CalculateWorkingCapital(current) < 0m && hasNetDebt) score -= 8m;

                if (roePercent < 10.0m) score -= 12m;
                if (roicPercent < 8.0m) score -= 8m;

                // Sloan Ratio Deduction (High Accrual Risk > 10%)
                if (sloanRatio > 10.0m) score -= 8m;

                // Cash Conversion Cycle Efficiency Adjustments
                if (current.CashConversionCycleDays < 0m) score += 3m; // Negative CCC bargaining power
                else if (current.CashConversionCycleDays > 120m) score -= 5m; // Excessively tied up capital
            }

            if (salesGrowth < 0m && hasValidHistory) score -= 5m;
            if (profitGrowth < 0m && hasValidHistory && hasValidProfitGrowth) score -= 5m;

            // -------------------------------------------------------------
            // 8. VALUE TRAP INTERCEPTOR & HARD SCORE CAP
            // -------------------------------------------------------------
            bool hasHeavyNetDebt = current.IsCashEstimateReliable ? CalculateNetCash(current) < -300m : current.TotalBorrowingsCr > 300m;
            bool isCapitalDestroyer = !current.IsFinancialSector && (roePercent < 8.0m || roicPercent < 5.0m) && salesGrowth < 0.05m;
            bool isHighDebtCommodity = !current.IsFinancialSector && opmPercent < 8.0m && hasHeavyNetDebt;
            bool isDeclining = (salesGrowth < 0m || (hasValidProfitGrowth && profitGrowth < 0m)) && hasValidHistory;
            bool isSeverePledge = pledgePercent >= 35.0m;

            bool isPaperProfitTrap = !current.IsFinancialSector
                && current.NetProfitCr > 0m
                && (current.CashFromOperationsCr <= 0m || (current.CashFromOperationsCr / current.NetProfitCr) < 0.20m);

            bool isFcfDrainTrap = !current.IsFinancialSector
                && current.NetProfitCr > 0m
                && fcfCr < 0m
                && (current.CashFromOperationsCr / current.NetProfitCr) < 0.50m;

            bool isAggressiveAccrualTrap = !current.IsFinancialSector && sloanRatio > 18.0m;

            // -------------------------------------------------------------
            // 9. ADVANCED FORENSIC SCORES
            // -------------------------------------------------------------
            decimal capexToDepRatio = CalculateCapexToDepreciationRatio(current);
            decimal altmanZ = CalculateAltmanZScore(current);
            int piotroskiF = CalculatePiotroskiFScore(current, historicals);
            decimal beneishM = CalculateBeneishMScore(current, historicals);

            // Add Piotroski F-Score Quality Boost (Max 5 Pts)
            if (piotroskiF >= 7) score += 5m;
            else if (piotroskiF <= 3 && !current.IsFinancialSector) score -= 5m;

            // Altman Z-Score Distress Penalty
            if (!current.IsFinancialSector)
            {
                if (altmanZ < 1.81m) score -= 15m; // Distress Zone
                else if (altmanZ > 2.99m) score += 3m; // Safe Zone
            }

            // Beneish M-Score Earnings Manipulation Interceptor
            bool isBeneishManipulator = beneishM > -1.78m && !current.IsFinancialSector;
            if (isBeneishManipulator) score -= 20m;

            // Update Value Trap Interceptor condition with Beneish M-Score
            bool isValueTrap = roePercent < 5.0m
                || (!current.IsFinancialSector && roicPercent < 5.0m)
                || isDeclining
                || current.NetProfitCr <= 0m
                || isCapitalDestroyer
                || isHighDebtCommodity
                || isSeverePledge
                || isPaperProfitTrap
                || isFcfDrainTrap
                || isAggressiveAccrualTrap
                || isBeneishManipulator; // Intercepts financial statement manipulators

            int finalScore = (int)Math.Clamp(Math.Round(score), 0, 100);

            // Hard Cap at 40 for Value Traps / Governance Risk / Paper Profits / Accrual Manipulation
            return isValueTrap ? Math.Min(finalScore, 40) : finalScore;
        }

        public static decimal CalculateStandardDcf(Financial current, IEnumerable<Financial> historicals)
        {
            if (CalculateTotalShares(current) <= 0) return 0m;

            decimal fcfCr = current.FreeCashFlowCr != 0
                ? current.FreeCashFlowCr
                : (current.CashFromOperationsCr -  CalculateGrossCapex(current));

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(current, historicals, defaultFallback: 0.08m);
            decimal discountRate = CalculateWacc(current);

            decimal terminalRate = 0.03m;

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + growthRate);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = CalculateNetDebt(current);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / CalculateTotalShares(current), 2));
        }

        public static decimal CalculateTwoStageDcf(Financial current, IEnumerable<Financial> historicals)
        {
            if (CalculateTotalShares(current) <= 0) return 0m;

            decimal fcfCr = current.FreeCashFlowCr != 0
                ? current.FreeCashFlowCr
                : (current.CashFromOperationsCr - CalculateGrossCapex(current));

            if (fcfCr <= 0) return 0m;

            decimal stage1Growth = ResolveDynamicGrowthRate(current, historicals, defaultFallback: 0.10m);
            decimal stage2Growth = stage1Growth * 0.5m;
            decimal discountRate = CalculateWacc(current);
            decimal terminalRate = Math.Min(0.03m, stage2Growth);

            decimal cumulativePv = 0m;
            decimal projectedFcf = fcfCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcf *= (1m + stage1Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            for (int yr = 6; yr <= 10; yr++)
            {
                projectedFcf *= (1m + stage2Growth);
                cumulativePv += projectedFcf / (decimal)Math.Pow((double)(1m + discountRate), yr);
            }

            decimal denominator = Math.Max(0.005m, discountRate - terminalRate);
            decimal terminalValue = (projectedFcf * (1m + terminalRate)) / denominator;
            decimal pvTerminal = terminalValue / (decimal)Math.Pow((double)(1m + discountRate), 10);

            decimal equityValueCr = cumulativePv + pvTerminal;
            return Math.Max(0m, Math.Round(equityValueCr / CalculateTotalShares(current), 2));
        }

        public static decimal CalculateExitMultipleDcf(Financial current, IEnumerable<Financial> historicals)
        {
            if (CalculateTotalShares(current) <= 0m || CalculateEbit(current) <= 0m) return 0m;

            decimal taxRate = CalculateEffectiveTaxRate(current);
            // Unlevered Operating Cash Flow (FCFF approximation)
            decimal ebitAfterTax = CalculateEbit(current) * (1m - taxRate);
            decimal fcffCr = ebitAfterTax + current.DepreciationCr - CalculateGrossCapex(current);

            if (fcffCr <= 0m) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(current, historicals, defaultFallback: 0.08m);
            decimal wacc = CalculateWacc(current);
            decimal evEbitdaMultiple = current.ReportedRoePercent >= 18.0m ? 14.0m : 10.0m;

            decimal cumulativePv = 0m;
            decimal projectedFcff = fcffCr;
            decimal projectedEbitda = CalculateEbitda(current);

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcff *= (1m + growthRate);
                projectedEbitda *= (1m + growthRate);
                cumulativePv += projectedFcff / (decimal)Math.Pow((double)(1m + wacc), yr);
            }

            decimal terminalEv = projectedEbitda * evEbitdaMultiple;
            decimal pvTerminal = terminalEv / (decimal)Math.Pow((double)(1m + wacc), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = CalculateNetDebt(current);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / CalculateTotalShares(current), 2));
        }

        public static decimal CalculateExcessReturns(Financial current)
        {
            if (current.BookValuePerShare <= 0m || current.ReportedRoePercent <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            decimal roe = current.ReportedRoePercent / 100m;

            if (roe <= costOfEquity) return Math.Round(current.BookValuePerShare, 2);

            decimal payoutRatio = Math.Clamp(current.DividendPayoutPercent / 100m, 0m, 0.80m);
            decimal retentionRatio = 1m - payoutRatio;

            decimal currentBookValue = current.BookValuePerShare;
            decimal pvExcessReturns = 0m;

            // 5-Year Explicit Forecast Horizon with Compounding Book Value
            for (int yr = 1; yr <= 5; yr++)
            {
                decimal excessReturnPerShare = (roe - costOfEquity) * currentBookValue;
                pvExcessReturns += excessReturnPerShare / (decimal)Math.Pow((double)(1m + costOfEquity), yr);
                currentBookValue += excessReturnPerShare * retentionRatio; // Compound equity base
            }

            // Terminal Excess Return Value
            const decimal terminalGrowth = 0.03m;
            decimal denominator = Math.Max(0.01m, costOfEquity - terminalGrowth);
            decimal terminalExcessReturn = ((roe - costOfEquity) * currentBookValue) / denominator;
            decimal pvTerminalExcess = terminalExcessReturn / (decimal)Math.Pow((double)(1m + costOfEquity), 5);

            decimal totalIntrinsicValue = current.BookValuePerShare + pvExcessReturns + pvTerminalExcess;
            return Math.Round(totalIntrinsicValue, 2);
        }

        public static decimal CalculateDdm(Financial current)
        {
            if (current.BookValuePerShare <= 0 || current.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            const decimal dividendGrowth = 0.05m;

            decimal payoutRatio = current.DividendPayoutPercent > 0 ? current.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = current.BookValuePerShare * (current.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal denominator = Math.Max(0.005m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        public static decimal CalculateDdmPassThroughYield(Financial current)
        {
            if (CalculateTotalShares(current) <= 0) return 0m;

            // Compute realized dividend per share (d0) received by parent shareholders
            decimal d0 = 0m;
            if (current.CurrentPrice > 0 && current.DividendYieldPercent > 0)
            {
                d0 = current.CurrentPrice * (current.DividendYieldPercent / 100m);
            }
            else if (current.BookValuePerShare > 0 && current.ReportedRoePercent > 0 && current.DividendPayoutPercent > 0)
            {
                decimal eps = current.BookValuePerShare * (current.ReportedRoePercent / 100m);
                decimal rawDividend = eps * (current.DividendPayoutPercent / 100m);

                // Apply a 50% pass-through friction haircut for investment/holding entities
                d0 = rawDividend * 0.50m;
            }

            if (d0 <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            // Conservative growth cap for holding entity pass-through cash flow
            const decimal dividendGrowth = 0.035m;

            if (costOfEquity <= dividendGrowth)
            {
                costOfEquity = dividendGrowth + 0.05m;
            }

            decimal denominator = Math.Max(0.02m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);

            return Math.Round(d1 / denominator, 2);
        }

        public static decimal CalculateGordonGrowthDdm(Financial current)
        {
            if (current.BookValuePerShare <= 0 || current.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            decimal payoutRatio = current.DividendPayoutPercent > 0 ? current.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = current.BookValuePerShare * (current.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal payoutGrowth = Math.Min((current.ReportedRoePercent / 100m) * (1m - payoutRatio), 0.06m);
            if (payoutGrowth >= costOfEquity) payoutGrowth = costOfEquity - 0.01m;

            decimal denominator = Math.Max(0.005m, costOfEquity - payoutGrowth);
            decimal d1 = d0 * (1m + payoutGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        public static decimal CalculateOwnerEarnings(Financial current, IEnumerable<Financial> historicals)
        {
            if (CalculateTotalShares(current) <= 0) return 0m;

            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            decimal ownerEarningsCr;

            if (historyList != null && historyList.Count >= 3)
            {
                decimal weightedOcfSum = 0m;
                decimal weightedCapexSum = 0m;
                decimal weightTotal = 0m;

                for (int i = 0; i < historyList.Count; i++)
                {
                    decimal weight = i + 1m;
                    weightedOcfSum += historyList[i].CashFromOperationsCr * weight;
                    weightedCapexSum += CalculateGrossCapex(historyList[i]) * weight;
                    weightTotal += weight;
                }

                ownerEarningsCr = (weightedOcfSum / weightTotal) - (weightedCapexSum / weightTotal);
            }
            else
            {
                ownerEarningsCr = current.CashFromOperationsCr - CalculateGrossCapex(current);
            }

            if (ownerEarningsCr <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            decimal terminalGrowth = Math.Min(0.04m, costOfEquity - 0.02m);
            decimal capRateDenominator = Math.Max(0.02m, costOfEquity - terminalGrowth);
            decimal capMultiple = 1m / capRateDenominator;

            return Math.Round((ownerEarningsCr * capMultiple) / CalculateTotalShares(current), 2);
        }

        public static decimal CalculateNormalizedPe(Financial current, IEnumerable<Financial> historicals)
        {
            if (CalculateTotalShares(current) <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgNetProfitCr = historicals.Average(h => h.NetProfitCr);
            if (avgNetProfitCr <= 0) return 0m;

            decimal normalizedEps = avgNetProfitCr / CalculateTotalShares(current);
            decimal targetPe = ResolveDynamicTargetPe(current);

            return Math.Round(normalizedEps * targetPe, 2);
        }

        public static decimal CalculatePegRatioValue(Financial current, IEnumerable<Financial> historicals)
        {
            if (current.BookValuePerShare <= 0 || current.ReportedRoePercent <= 0) return 0m;

            decimal eps = current.BookValuePerShare * (current.ReportedRoePercent / 100m);
            decimal growthRate = ResolveDynamicGrowthRate(current, historicals, 0.10m) * 100m;

            if (eps <= 0 || growthRate <= 0) return 0m;

            decimal fairPe = Math.Clamp(growthRate, 4.0m, 30.0m);
            return Math.Round(eps * fairPe, 2);
        }

        public static decimal CalculateEvSalesMultiple(Financial current)
        {
            if (CalculateTotalShares(current) <= 0 || current.SalesCr <= 0) return 0m;

            decimal targetEvSales = 2.5m;
            decimal revenueCr = current.SalesCr;
            decimal netDebtCr = CalculateNetDebt(current);

            decimal targetEquityValueCr = (revenueCr * targetEvSales) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / CalculateTotalShares(current), 2));
        }

        public static decimal CalculatePriceToSales(Financial current)
        {
            if (CalculateTotalShares(current) <= 0 || current.SalesCr <= 0) return 0m;

            decimal salesPerShare = current.SalesCr / CalculateTotalShares(current);
            const decimal targetPs = 1.5m;
            return Math.Round(salesPerShare * targetPs, 2);
        }

        public static decimal CalculateEvEbitdaMultiple(Financial current)
        {
            if (CalculateTotalShares(current) <= 0 || CalculateEbit(current) <= 0) return 0m;

            decimal estimatedEbitdaCr = CalculateEbit(current) * 1.2m;
            decimal targetEvEbitda = current.ReportedRoePercent >= 18.0m ? 12.0m : 8.5m;
            decimal netDebtCr = CalculateNetDebt(current);

            decimal targetEquityValueCr = (estimatedEbitdaCr * targetEvEbitda) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / CalculateTotalShares(current), 2));
        }

        public static decimal CalculatePriceToEarnings(Financial current)
        {
            if (current.NetProfitCr <= 0 || CalculateTotalShares(current) <= 0) return 0m;

            decimal eps = current.NetProfitCr / CalculateTotalShares(current);
            decimal fairPe = ResolveDynamicTargetPe(current);

            if (!current.IsFinancialSector)
            {
                decimal cashConversion = current.NetProfitCr > 0
                    ? Math.Clamp(current.CashFromOperationsCr / current.NetProfitCr, 0m, 1m)
                    : 0m;

                if (cashConversion < 0.50m)
                {
                    fairPe *= Math.Max(0.20m, cashConversion);
                }
            }

            return Math.Round(eps * fairPe, 2);
        }

        public static decimal CalculateNavPerShare(Financial current)
        {
            return current.BookValuePerShare <= 0 ? 0m : Math.Round(current.BookValuePerShare, 2);
        }

        public static decimal CalculatePbIntrinsicValue(Financial current)
        {
            if (current.BookValuePerShare <= 0 || current.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(current);
            const decimal growth = 0.05m;
            decimal roe = current.ReportedRoePercent / 100m;

            decimal denominator = Math.Max(0.005m, costOfEquity - growth);
            decimal justifiedPb = (roe - growth) / denominator;
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 12.0m);

            return Math.Round(current.BookValuePerShare * justifiedPb, 2);
        }

        public static decimal CalculateHoldingCompanyValue(Financial current)
        {
            if (current.BookValuePerShare <= 0) return 0m;

            decimal rawNavPerShare = current.BookValuePerShare;
            decimal holdCoDiscount = 0.50m;

            if (current.DividendYieldPercent < 1.0m)
            {
                holdCoDiscount += 0.10m;
            }

            decimal adjustedNav = rawNavPerShare * (1.0m - holdCoDiscount);
            return Math.Round(adjustedNav, 2);
        }

        public static decimal ResolveDynamicGrowthRate(Financial current, IEnumerable<Financial> historicals, decimal defaultFallback = 0.08m)
        {
            if (current.ReportedRoePercent > 0)
            {
                decimal roe = current.ReportedRoePercent / 100m;
                decimal payoutRatio = Math.Max(0m, Math.Min(current.DividendPayoutPercent / 100m, 1m));
                decimal retentionRatio = 1m - payoutRatio;

                decimal fundamentalGrowth = roe * retentionRatio;

                if (fundamentalGrowth > 0)
                {
                    return Math.Clamp(fundamentalGrowth, 0.02m, 0.15m);
                }
            }

            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            if (historyList != null && historyList.Count >= 3)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (oldest.CashFromOperationsCr > 0 && newest.CashFromOperationsCr > 0)
                {
                    decimal ocfCagr = CalculateCagr(oldest.CashFromOperationsCr, newest.CashFromOperationsCr, periods);
                    if (ocfCagr > 0)
                    {
                        return Math.Clamp(ocfCagr, 0.02m, 0.15m);
                    }
                }
            }

            return defaultFallback;
        }

        public static decimal CalculateNetDebt(Financial current) 
                => current.IsCashEstimateReliable ? current.TotalBorrowingsCr - current.CashAndEquivalentsCr
                : current.TotalBorrowingsCr;
        public static decimal CalculateDebtToEbit(Financial current) 
                => CalculateNetDebt(current) > 0 && CalculateEbit(current) > 0 ? Math.Max(0m, CalculateNetDebt(current) / CalculateEbit(current)) : 0m;
        public static decimal CalculateCapexToOcf(Financial current) 
                => current.CashFromOperationsCr > 0 ? Math.Max(0m, CalculateGrossCapex(current) / current.CashFromOperationsCr) : 0m;
        public static decimal CalculateFreeCashFlow(Financial current) 
                => current.FreeCashFlowCr != 0 ? current.FreeCashFlowCr : current.CashFromOperationsCr - CalculateGrossCapex(current);
        public static decimal CalculateOcfToNetProfit(Financial current) 
                => current.NetProfitCr > 0 ? Math.Max(0m, current.CashFromOperationsCr / current.NetProfitCr) : 0m;
        public static decimal CalculateFcfToNetProfit(Financial current) 
                => current.NetProfitCr > 0 ? Math.Max(0m, CalculateFreeCashFlow(current) / current.NetProfitCr) : 0m;

        public static decimal CalculateWacc(Financial current, decimal riskFreeRate = 0.07m, decimal equityRiskPremium = 0.055m)
        {
            decimal equityValueCr = current.MarketCapCr;
            decimal debtValueCr = Math.Max(0m, current.TotalBorrowingsCr);
            decimal totalCapitalCr = equityValueCr + debtValueCr;

            if (totalCapitalCr <= 0m) return 0.11m;

            decimal beta = current.Beta > 0 ? Math.Clamp(current.Beta, 0.5m, 2.5m) : 1.0m;
            decimal costOfEquity = riskFreeRate + (beta * equityRiskPremium);

            decimal costOfDebt = CalculateCostOfDebt(current);
            decimal taxRate = CalculateEffectiveTaxRate(current);

            decimal weightEquity = equityValueCr / totalCapitalCr;
            decimal weightDebt = debtValueCr / totalCapitalCr;

            decimal wacc = (weightEquity * costOfEquity) + (weightDebt * costOfDebt * (1m - taxRate));
            return Math.Clamp(wacc, 0.085m, 0.18m);
        }

        public static decimal ResolveDynamicTargetPe(Financial current)
        {
            decimal basePe = 15.0m;
            if (current.ReportedRoePercent >= 25.0m) return 25.0m;
            if (current.ReportedRoePercent >= 18.0m) return 20.0m;
            if (current.ReportedRoePercent <= 8.0m) return 10.0m;
            return basePe;
        }

        public static decimal CalculateCagr(decimal initialValue, decimal finalValue, int periods)
        {
            if (initialValue <= 0 || finalValue <= 0 || periods <= 0)
                return 0m;

            double ratio = (double)(finalValue / initialValue);
            double cagr = Math.Pow(ratio, 1.0 / periods) - 1.0;

            return (decimal)cagr;
        }

        
    }
}
