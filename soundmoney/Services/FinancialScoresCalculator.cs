using System;
using System.Collections.Generic;
using System.Linq;
using SoundMoney.Models;

namespace SoundMoney.Services
{
    public static class FinancialScoresCalculator
    {
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
    }
}