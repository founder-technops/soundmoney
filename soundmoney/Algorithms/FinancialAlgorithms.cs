using Microsoft.AspNetCore.Components.Forms;
using SoundMoney.Models;
using SoundMoney.Services;

namespace SoundMoney.Algorithms
{
    public static class FinancialAlgorithms
    {
        public static decimal CalculateOperatingProfitMargin(decimal sales, decimal operatingProfit) =>
            sales > 0m ? Math.Round((operatingProfit / sales) * 100m, 2) : 0m;

        public static decimal CalculateEbit(decimal opProfit, decimal otherIncome, decimal depreciation) =>
            (opProfit + otherIncome) - depreciation;

        public static decimal CalculateEbitda(decimal opProfit, decimal otherIncome) =>
            opProfit + otherIncome;

        public static decimal CalculateTotalEquity(decimal shareCapital, decimal reserves) =>
            shareCapital + reserves;

        public static decimal CalculateTotalAssets(decimal fixedAssets, decimal cwip, decimal investments, decimal otherAssets) =>
            fixedAssets + cwip + investments + otherAssets;

        public static decimal CalculateTotalLiabilities(decimal shareCapital, decimal reserves, decimal totalBorrowings, decimal otherLiabilities) =>
            shareCapital + reserves + totalBorrowings + otherLiabilities;

        public static decimal CalculateNetCash(decimal cashAndEquivalents, decimal totalBorrowings) =>
            cashAndEquivalents - totalBorrowings;

        public static decimal CalculateNonCurrentAssets(decimal fixedAssets, decimal cwip, decimal investments) =>
            fixedAssets + cwip + investments;

        public static decimal CalculateCurrentAssets(decimal totalAssets, decimal nonCurrentAssets) =>
            Math.Max(0m, totalAssets - nonCurrentAssets);

        public static decimal CalculateWorkingCapital(decimal currentAssets, decimal otherLiabilities) =>
            currentAssets - otherLiabilities;

        public static decimal CalculateCurrentLiabilities(decimal currentAssets, decimal workingCapital) =>
            currentAssets - workingCapital;

        public static decimal CalculateGrossCapex(decimal cfo, decimal fcf) =>
            cfo - fcf;

        public static decimal CalculateNetCashFlow(decimal cfo, decimal cfi, decimal cff) =>
            cfo + cfi + cff;

        public static decimal CalculateCfoToOpRatio(decimal cfo, decimal operatingProfit) =>
            operatingProfit > 0m ? Math.Round(cfo / operatingProfit, 4) : 0m;

        public static decimal CalculateCashConversionRatio(decimal cfo, decimal netProfit) =>
            netProfit > 0m ? Math.Round(cfo / netProfit, 2) : 0m;

        public static decimal CalculateCapitalAdequacy(bool isFinancial, decimal equity, decimal totalAssets) =>
            isFinancial && equity > 0m && totalAssets > 0m ? Math.Round((equity / totalAssets) * 100m, 2) : 0m;

        public static decimal CalculateRoa(decimal netProfit, decimal totalAssets, bool isFinancial) =>
            totalAssets > 0m ? Math.Round((netProfit / totalAssets) * 100m, 2) : (isFinancial ? 1.0m : 0m);

        public static decimal CalculateEffectiveTaxRate(decimal taxPercent) =>
            taxPercent > 0m ? Math.Clamp(taxPercent / 100m, 0.0m, 0.35m) : 0.25m;

        public static decimal CalculateCostOfDebt(decimal borrowings, decimal interestExpense) =>
            borrowings > 0m && interestExpense > 0m ? Math.Clamp(interestExpense / borrowings, 0.03m, 0.18m) : 0.08m;

        public static decimal CalculateInvestmentAssetsRatio(decimal investments, decimal totalAssets) =>
            totalAssets > 0m ? Math.Clamp(investments / totalAssets, 0m, 1m) : 0m;

        public static decimal CalculateInterestIncomeRatio(decimal interestIncome, decimal sales) =>
            sales > 0m ? Math.Clamp(interestIncome / sales, 0m, 1m) : 0m;

        public static bool CheckCoreInvestmentCompany(bool explicitFlag, decimal investmentRatio, decimal interestRatio) =>
            explicitFlag || (investmentRatio >= 0.70m && interestRatio < 0.30m);

        public static decimal CalculateRoic(DeepFinancial data)
        {
            decimal investedCapital = data.TotalEquityCapitalCr + data.TotalBorrowingsCr - data.CashAndEquivalentsCr;
            if (investedCapital <= 0m) return 0m;
            decimal nopat = data.EbitCr * (1m - data.EffectiveTaxRate);
            return Math.Round((nopat / investedCapital) * 100m, 2);
        }

        public static decimal CalculateTotalShares(decimal currentPrice, decimal marketCap) =>
            currentPrice > 0m && marketCap > 0m ? Math.Round(marketCap / currentPrice, 4) : 0m;

        public static decimal CalculateCroic(DeepFinancial data) =>
            (data.InvestmentsCr > 0m && !data.IsFinancialSector) ? (data.FreeCashFlowCr / data.InvestmentsCr) * 100m : 0m;

        public static decimal CalculateSloanRatio(DeepFinancial data) => (data.TotalAssetsCr > 0m && !data.IsFinancialSector)
                ? ((data.NetProfitCr - data.CashFromOperationsCr) / data.TotalAssetsCr) * 100m
                : 0m;

        public static decimal CalculateInterestCoverage(DeepFinancial data) => (data.TotalBorrowingsCr > 0m && data.InterestExpenseCr > 0m && !data.IsFinancialSector)
                ? (data.EbitCr / data.InterestExpenseCr)
                : (data.TotalBorrowingsCr <= 0m ? 999m : 0m);

        // =========================================================================
        // 1. CAPEX-TO-DEPRECIATION RATIO
        // =========================================================================
        /// <summary>
        /// Measures capital reinvestment intensity.
        /// Ratio > 1.0 indicates capital expansion/growth reinvestment.
        /// Ratio < 1.0 indicates potential under-investment or asset harvesting.
        /// </summary>
        public static decimal CalculateCapexToDepreciationRatio(DeepFinancial data)
        {
            if (data == null || data.DepreciationCr <= 0m) return 0m;

            decimal capex = Math.Abs(data.GrossCapexCr);
            return Math.Round(capex / data.DepreciationCr, 2);
        }

        // =========================================================================
        // 2. ALTMAN Z-SCORE (Distress & Bankruptcy Model)
        // =========================================================================
        /// <summary>
        /// Calculates standard Altman Z-Score for Non-Manufacturing / General Companies.
        /// Z > 2.99 = Safe Zone | 1.81 <= Z <= 2.99 = Grey Zone | Z < 1.81 = Distress Zone
        /// </summary>
        public static decimal CalculateAltmanZScore(DeepFinancial data)
        {
            if (data == null || data.TotalAssetsCr <= 0m || data.IsFinancialSector) return 0m;

            decimal totalAssets = data.TotalAssetsCr;

            // X1: Working Capital / Total Assets
            decimal x1 = data.WorkingCapitalCr / totalAssets;

            // X2: Retained Earnings / Total Assets
            decimal x2 = data.ReservesCr / totalAssets;

            // X3: EBIT / Total Assets
            decimal x3 = data.EbitCr / totalAssets;

            // X4: Market Value of Equity / Total Liabilities
            decimal totalLiabilities = data.TotalBorrowingsCr + data.OtherLiabilitiesCr;
            decimal x4 = totalLiabilities > 0m ? data.MarketCapCr / totalLiabilities : 10m;

            // X5: Sales / Total Assets
            decimal x5 = data.SalesCr / totalAssets;

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
        public static int CalculatePiotroskiFScore(DeepFinancial current, IEnumerable<Financial> historicals)
        {
            if (current == null || historicals == null || current.IsFinancialSector) return 0;

            var historyList = historicals.OrderByDescending(h => h.Year).ToList();
            if (historyList.Count < 2) return 0;

            var t = historyList[0];     // Most recent completed year
            var tPrev = historyList[1]; // Previous year (T-1)

            int fScore = 0;

            // --- Profitability Criteria (Max 4 Points) ---

            // 1. Positive Return on Assets (ROA > 0)
            decimal roaT = t.TotalAssetsCr > 0m ? t.NetProfitCr / t.TotalAssetsCr : 0m;
            decimal roaPrev = tPrev.TotalAssetsCr > 0m ? tPrev.NetProfitCr / tPrev.TotalAssetsCr : 0m;
            if (roaT > 0m) fScore++;

            // 2. Positive Operating Cash Flow (CFO > 0)
            if (t.CashFromOperationsCr > 0m) fScore++;

            // 3. Quality of Earnings (CFO > Net Income)
            if (t.CashFromOperationsCr > t.NetProfitCr) fScore++;

            // 4. ROA Trend (ROA(t) > ROA(t-1))
            if (roaT > roaPrev) fScore++;

            // --- Leverage, Liquidity & Source of Funds (Max 3 Points) ---

            // 5. Debt Decrease (Long-Term Debt Ratio Decrease)
            decimal leverageT = t.TotalAssetsCr > 0m ? t.TotalBorrowingsCr / t.TotalAssetsCr : 0m;
            decimal leveragePrev = tPrev.TotalAssetsCr > 0m ? tPrev.TotalBorrowingsCr / tPrev.TotalAssetsCr : 0m;
            if (leverageT < leveragePrev) fScore++;

            // 6. Current Ratio Increase
            decimal currentRatioT = t.TotalLiabilitiesCr > 0m ? t.CurrentAssetsCr / t.CurrentLiabilitiesCr : 0m;
            decimal currentRatioPrev = tPrev.CurrentLiabilitiesCr > 0m ? tPrev.CurrentAssetsCr / tPrev.CurrentLiabilitiesCr : 0m;
            if (currentRatioT > currentRatioPrev) fScore++;

            // 7. No Equity Dilution (Shares outstanding in T <= T-1)
            if (t.ShareCapitalCr <= tPrev.ShareCapitalCr) fScore++;

            // --- Operating Efficiency (Max 2 Points) ---

            // 8. Gross Margin Improvement
            decimal grossMarginT = t.SalesCr > 0m ? (t.SalesCr - (t.SalesCr - t.OperatingProfitCr)) / t.SalesCr : 0m;
            decimal grossMarginPrev = tPrev.SalesCr > 0m ? (tPrev.SalesCr - (tPrev.SalesCr - tPrev.OperatingProfitCr)) / tPrev.SalesCr : 0m;
            if (grossMarginT > grossMarginPrev) fScore++;

            // 9. Asset Turnover Improvement (Sales / Total Assets)
            decimal assetTurnoverT = t.TotalAssetsCr > 0m ? t.SalesCr / t.TotalAssetsCr : 0m;
            decimal assetTurnoverPrev = tPrev.TotalAssetsCr > 0m ? tPrev.SalesCr / tPrev.TotalAssetsCr : 0m;
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
        public static decimal CalculateBeneishMScore(DeepFinancial current, IEnumerable<Financial> historicals)
        {
            if (current == null || historicals == null || current.IsFinancialSector) return 0m;

            var historyList = historicals.OrderByDescending(h => h.Year).ToList();
            if (historyList.Count < 2) return 0m;

            var t = historyList[0];     // Period T
            var tPrev = historyList[1]; // Period T-1

            if (tPrev.SalesCr <= 0m || t.SalesCr <= 0m ||
                tPrev.TotalAssetsCr <= 0m || t.TotalAssetsCr <= 0m)
                return 0m;

            // 1. DSRI: Days Sales in Receivables Index
            // Formula approximation assuming working capital receivables proxy
            decimal recT = Math.Max(0m, t.WorkingCapitalCr);
            decimal recPrev = Math.Max(0m, tPrev.WorkingCapitalCr);
            decimal dsri = (t.SalesCr > 0m && tPrev.SalesCr > 0m && recPrev > 0m)
                ? (recT / t.SalesCr) / (recPrev / tPrev.SalesCr)
                : 1.0m;

            // 2. GMI: Gross Margin Index
            decimal gmPrev = tPrev.SalesCr > 0m ? tPrev.OperatingProfitCr / tPrev.SalesCr : 1.0m;
            decimal gmT = t.SalesCr > 0m ? t.OperatingProfitCr / t.SalesCr : 1.0m;
            decimal gmi = gmT > 0m ? gmPrev / gmT : 1.0m;

            // 3. AQI: Asset Quality Index
            decimal nonCurrentAssetsT = t.TotalAssetsCr - t.CurrentAssetsCr - t.FixedAssetsCr;
            decimal nonCurrentAssetsPrev = tPrev.TotalAssetsCr - tPrev.CurrentAssetsCr - tPrev.FixedAssetsCr;
            decimal aqiT = t.TotalAssetsCr > 0m ? 1m - (nonCurrentAssetsT / t.TotalAssetsCr) : 1m;
            decimal aqiPrev = tPrev.TotalAssetsCr > 0m ? 1m - (nonCurrentAssetsPrev / tPrev.TotalAssetsCr) : 1m;
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
            decimal levT = t.TotalAssetsCr > 0m ? t.TotalBorrowingsCr / t.TotalAssetsCr : 1.0m;
            decimal levPrev = tPrev.TotalAssetsCr > 0m ? tPrev.TotalBorrowingsCr / tPrev.TotalAssetsCr : 1.0m;
            decimal lvgi = levPrev > 0m ? levT / levPrev : 1.0m;

            // 8. TATA: Total Accruals to Total Assets
            decimal totalAccruals = t.NetProfitCr - t.CashFromOperationsCr;
            decimal tata = t.TotalAssetsCr > 0m ? totalAccruals / t.TotalAssetsCr : 0m;

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
        /// <param name="historicalData">List of historical financial records.</param>
        /// <param name="data">Current DeepFinancial metrics (for NetProfit, CapitalAdequacy, etc.).</param>
        /// <returns>DividendAnalysisResult containing consistency checks and safety rating.</returns>
        public static DividendAnalysisResult CalculateDividend(DeepFinancial data, List<Financial> historicalData)
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
            bool isFcfSupported = recentYears.All(x => x.FreeCashFlowCr > 0m || x.CashFromOperationsCr > 0m);

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

        public static int CalculateSoundScore(decimal marginOfSafety, DeepFinancial data, IEnumerable<Financial> historicals)
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

            decimal roicPercent = FinancialAlgorithms.CalculateRoic(data);

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
            DividendAnalysisResult dividendAnalysis = CalculateDividend(data, historyList ?? new List<Financial>());

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
                    decimal avgHistoricalOpm = historyList.Average(h => h.OperatingProfitMargin);
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

            // -------------------------------------------------------------
            // 9. ADVANCED FORENSIC SCORES
            // -------------------------------------------------------------
            decimal capexToDepRatio = CalculateCapexToDepreciationRatio(data);
            decimal altmanZ = CalculateAltmanZScore(data);
            int piotroskiF = CalculatePiotroskiFScore(data, historicals);
            decimal beneishM = CalculateBeneishMScore(data, historicals);

            // Add Piotroski F-Score Quality Boost (Max 5 Pts)
            if (piotroskiF >= 7) score += 5m;
            else if (piotroskiF <= 3 && !data.IsFinancialSector) score -= 5m;

            // Altman Z-Score Distress Penalty
            if (!data.IsFinancialSector)
            {
                if (altmanZ < 1.81m) score -= 15m; // Distress Zone
                else if (altmanZ > 2.99m) score += 3m; // Safe Zone
            }

            // Beneish M-Score Earnings Manipulation Interceptor
            bool isBeneishManipulator = beneishM > -1.78m && !data.IsFinancialSector;
            if (isBeneishManipulator) score -= 20m;

            // Update Value Trap Interceptor condition with Beneish M-Score
            bool isValueTrap = roePercent < 5.0m
                || (!data.IsFinancialSector && roicPercent < 5.0m)
                || isDeclining
                || data.NetProfitCr <= 0m
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

        public static decimal CalculateStandardDcf(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal discountRate = CalculateWacc(data);

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
            decimal netDebtCr = CalculateNetDebt(data);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        public static decimal CalculateTwoStageDcf(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            decimal fcfCr = data.FreeCashFlowCr != 0
                ? data.FreeCashFlowCr
                : (data.CashFromOperationsCr - data.GrossCapexCr);

            if (fcfCr <= 0) return 0m;

            decimal stage1Growth = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.10m);
            decimal stage2Growth = stage1Growth * 0.5m;
            decimal discountRate = CalculateWacc(data);
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
            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        public static decimal CalculateExitMultipleDcf(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.TotalSharesCr <= 0m || data.EbitCr <= 0m) return 0m;

            decimal taxRate = data.EffectiveTaxRate;
            // Unlevered Operating Cash Flow (FCFF approximation)
            decimal ebitAfterTax = data.EbitCr * (1m - taxRate);
            decimal fcffCr = ebitAfterTax + data.DepreciationCr - data.GrossCapexCr;

            if (fcffCr <= 0m) return 0m;

            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, defaultFallback: 0.08m);
            decimal wacc = CalculateWacc(data);
            decimal evEbitdaMultiple = data.ReportedRoePercent >= 18.0m ? 14.0m : 10.0m;

            decimal cumulativePv = 0m;
            decimal projectedFcff = fcffCr;
            decimal projectedEbitda = data.EbitdaCr;

            for (int yr = 1; yr <= 5; yr++)
            {
                projectedFcff *= (1m + growthRate);
                projectedEbitda *= (1m + growthRate);
                cumulativePv += projectedFcff / (decimal)Math.Pow((double)(1m + wacc), yr);
            }

            decimal terminalEv = projectedEbitda * evEbitdaMultiple;
            decimal pvTerminal = terminalEv / (decimal)Math.Pow((double)(1m + wacc), 5);

            decimal enterpriseValueCr = cumulativePv + pvTerminal;
            decimal netDebtCr = CalculateNetDebt(data);
            decimal equityValueCr = enterpriseValueCr - netDebtCr;

            return Math.Max(0m, Math.Round(equityValueCr / data.TotalSharesCr, 2));
        }

        public static decimal CalculateExcessReturns(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0m || data.ReportedRoePercent <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal roe = data.ReportedRoePercent / 100m;

            if (roe <= costOfEquity) return Math.Round(data.BookValuePerShare, 2);

            decimal payoutRatio = Math.Clamp(data.DividendPayoutPercent / 100m, 0m, 0.80m);
            decimal retentionRatio = 1m - payoutRatio;

            decimal currentBookValue = data.BookValuePerShare;
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

            decimal totalIntrinsicValue = data.BookValuePerShare + pvExcessReturns + pvTerminalExcess;
            return Math.Round(totalIntrinsicValue, 2);
        }

        public static decimal CalculateDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal dividendGrowth = 0.05m;

            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal denominator = Math.Max(0.005m, costOfEquity - dividendGrowth);
            decimal d1 = d0 * (1m + dividendGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        public static decimal CalculateDdmPassThroughYield(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0) return 0m;

            // Compute realized dividend per share (d0) received by parent shareholders
            decimal d0 = 0m;
            if (data.CurrentPrice > 0 && data.DividendYieldPercent > 0)
            {
                d0 = data.CurrentPrice * (data.DividendYieldPercent / 100m);
            }
            else if (data.BookValuePerShare > 0 && data.ReportedRoePercent > 0 && data.DividendPayoutPercent > 0)
            {
                decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
                decimal rawDividend = eps * (data.DividendPayoutPercent / 100m);

                // Apply a 50% pass-through friction haircut for investment/holding entities
                d0 = rawDividend * 0.50m;
            }

            if (d0 <= 0m) return 0m;

            decimal costOfEquity = CalculateWacc(data);
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

        public static decimal CalculateGordonGrowthDdm(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal payoutRatio = data.DividendPayoutPercent > 0 ? data.DividendPayoutPercent / 100m : 0.40m;
            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal d0 = eps * payoutRatio;

            if (d0 <= 0) return 0m;

            decimal payoutGrowth = Math.Min((data.ReportedRoePercent / 100m) * (1m - payoutRatio), 0.06m);
            if (payoutGrowth >= costOfEquity) payoutGrowth = costOfEquity - 0.01m;

            decimal denominator = Math.Max(0.005m, costOfEquity - payoutGrowth);
            decimal d1 = d0 * (1m + payoutGrowth);
            return Math.Round(d1 / denominator, 2);
        }

        public static decimal CalculateOwnerEarnings(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.TotalSharesCr <= 0) return 0m;

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
                    weightedCapexSum += historyList[i].GrossCapexCr * weight;
                    weightTotal += weight;
                }

                ownerEarningsCr = (weightedOcfSum / weightTotal) - (weightedCapexSum / weightTotal);
            }
            else
            {
                ownerEarningsCr = data.CashFromOperationsCr - data.GrossCapexCr;
            }

            if (ownerEarningsCr <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            decimal terminalGrowth = Math.Min(0.04m, costOfEquity - 0.02m);
            decimal capRateDenominator = Math.Max(0.02m, costOfEquity - terminalGrowth);
            decimal capMultiple = 1m / capRateDenominator;

            return Math.Round((ownerEarningsCr * capMultiple) / data.TotalSharesCr, 2);
        }

        public static decimal CalculateNormalizedPe(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.TotalSharesCr <= 0 || historicals == null || !historicals.Any()) return 0m;

            decimal avgNetProfitCr = historicals.Average(h => h.NetProfitCr);
            if (avgNetProfitCr <= 0) return 0m;

            decimal normalizedEps = avgNetProfitCr / data.TotalSharesCr;
            decimal targetPe = ResolveDynamicTargetPe(data);

            return Math.Round(normalizedEps * targetPe, 2);
        }

        public static decimal CalculatePegRatioValue(DeepFinancial data, IEnumerable<Financial> historicals)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal eps = data.BookValuePerShare * (data.ReportedRoePercent / 100m);
            decimal growthRate = ResolveDynamicGrowthRate(data, historicals, 0.10m) * 100m;

            if (eps <= 0 || growthRate <= 0) return 0m;

            decimal fairPe = Math.Clamp(growthRate, 4.0m, 30.0m);
            return Math.Round(eps * fairPe, 2);
        }

        public static decimal CalculateEvSalesMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.SalesCr <= 0) return 0m;

            decimal targetEvSales = 2.5m;
            decimal revenueCr = data.SalesCr;
            decimal netDebtCr = CalculateNetDebt(data);

            decimal targetEquityValueCr = (revenueCr * targetEvSales) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        public static decimal CalculatePriceToSales(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.SalesCr <= 0) return 0m;

            decimal salesPerShare = data.SalesCr / data.TotalSharesCr;
            const decimal targetPs = 1.5m;
            return Math.Round(salesPerShare * targetPs, 2);
        }

        public static decimal CalculateEvEbitdaMultiple(DeepFinancial data)
        {
            if (data.TotalSharesCr <= 0 || data.EbitCr <= 0) return 0m;

            decimal estimatedEbitdaCr = data.EbitCr * 1.2m;
            decimal targetEvEbitda = data.ReportedRoePercent >= 18.0m ? 12.0m : 8.5m;
            decimal netDebtCr = CalculateNetDebt(data);

            decimal targetEquityValueCr = (estimatedEbitdaCr * targetEvEbitda) - netDebtCr;
            return Math.Max(0m, Math.Round(targetEquityValueCr / data.TotalSharesCr, 2));
        }

        public static decimal CalculatePriceToEarnings(DeepFinancial data)
        {
            if (data.NetProfitCr <= 0 || data.TotalSharesCr <= 0) return 0m;

            decimal eps = data.NetProfitCr / data.TotalSharesCr;
            decimal fairPe = ResolveDynamicTargetPe(data);

            if (!data.IsFinancialSector)
            {
                decimal cashConversion = data.NetProfitCr > 0
                    ? Math.Clamp(data.CashFromOperationsCr / data.NetProfitCr, 0m, 1m)
                    : 0m;

                if (cashConversion < 0.50m)
                {
                    fairPe *= Math.Max(0.20m, cashConversion);
                }
            }

            return Math.Round(eps * fairPe, 2);
        }

        public static decimal CalculateNavPerShare(DeepFinancial data)
        {
            return data.BookValuePerShare <= 0 ? 0m : Math.Round(data.BookValuePerShare, 2);
        }

        public static decimal CalculatePbIntrinsicValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0 || data.ReportedRoePercent <= 0) return 0m;

            decimal costOfEquity = CalculateWacc(data);
            const decimal growth = 0.05m;
            decimal roe = data.ReportedRoePercent / 100m;

            decimal denominator = Math.Max(0.005m, costOfEquity - growth);
            decimal justifiedPb = (roe - growth) / denominator;
            justifiedPb = Math.Clamp(justifiedPb, 0.5m, 12.0m);

            return Math.Round(data.BookValuePerShare * justifiedPb, 2);
        }

        public static decimal CalculateHoldingCompanyValue(DeepFinancial data)
        {
            if (data.BookValuePerShare <= 0) return 0m;

            decimal rawNavPerShare = data.BookValuePerShare;
            decimal holdCoDiscount = 0.50m;

            if (data.DividendYieldPercent < 1.0m)
            {
                holdCoDiscount += 0.10m;
            }

            decimal adjustedNav = rawNavPerShare * (1.0m - holdCoDiscount);
            return Math.Round(adjustedNav, 2);
        }

        public static decimal ResolveDynamicGrowthRate(DeepFinancial data, IEnumerable<Financial> historicals, decimal defaultFallback = 0.08m)
        {
            if (data.ReportedRoePercent > 0)
            {
                decimal roe = data.ReportedRoePercent / 100m;
                decimal payoutRatio = Math.Max(0m, Math.Min(data.DividendPayoutPercent / 100m, 1m));
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

        public static decimal CalculateNetDebt(DeepFinancial data) 
                => data.IsCashEstimateReliable ? data.TotalBorrowingsCr - data.CashAndEquivalentsCr
                : data.TotalBorrowingsCr;
        public static decimal CalculateDebtToEbit(DeepFinancial data) 
                => CalculateNetDebt(data) > 0 && data.EbitCr > 0 ? Math.Max(0m, CalculateNetDebt(data) / data.EbitCr) : 0m;
        public static decimal CalculateCapexToOcf(DeepFinancial data) 
                => data.CashFromOperationsCr > 0 ? Math.Max(0m, data.GrossCapexCr / data.CashFromOperationsCr) : 0m;
        public static decimal CalculateFreeCashFlow(DeepFinancial data) 
                => data.FreeCashFlowCr != 0 ? data.FreeCashFlowCr : data.CashFromOperationsCr - data.GrossCapexCr;
        public static decimal CalculateOcfToNetProfit(DeepFinancial data) 
                => data.NetProfitCr > 0 ? Math.Max(0m, data.CashFromOperationsCr / data.NetProfitCr) : 0m;
        public static decimal CalculateFcfToNetProfit(DeepFinancial data) 
                => data.NetProfitCr > 0 ? Math.Max(0m, CalculateFreeCashFlow(data) / data.NetProfitCr) : 0m;

        public static decimal CalculateWacc(DeepFinancial data, decimal riskFreeRate = 0.07m, decimal equityRiskPremium = 0.055m)
        {
            decimal equityValueCr = data.MarketCapCr;
            decimal debtValueCr = Math.Max(0m, data.TotalBorrowingsCr);
            decimal totalCapitalCr = equityValueCr + debtValueCr;

            if (totalCapitalCr <= 0m) return 0.11m;

            decimal beta = data.Beta > 0 ? Math.Clamp(data.Beta, 0.5m, 2.5m) : 1.0m;
            decimal costOfEquity = riskFreeRate + (beta * equityRiskPremium);

            decimal costOfDebt = data.CostOfDebt;
            decimal taxRate = data.EffectiveTaxRate;

            decimal weightEquity = equityValueCr / totalCapitalCr;
            decimal weightDebt = debtValueCr / totalCapitalCr;

            decimal wacc = (weightEquity * costOfEquity) + (weightDebt * costOfDebt * (1m - taxRate));
            return Math.Clamp(wacc, 0.085m, 0.18m);
        }

        public static decimal ResolveDynamicTargetPe(DeepFinancial data)
        {
            decimal basePe = 15.0m;
            if (data.ReportedRoePercent >= 25.0m) return 25.0m;
            if (data.ReportedRoePercent >= 18.0m) return 20.0m;
            if (data.ReportedRoePercent <= 8.0m) return 10.0m;
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
