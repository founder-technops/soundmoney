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

            // 1. Margin of Safety (Max 30 Pts)
            if (marginOfSafety >= 30m)
                score += 30m;
            else if (marginOfSafety > 0m)
                score += 15m + (marginOfSafety / 30m * 14m);
            else if (marginOfSafety >= -20m)
                score += Math.Max(0m, 14m + (marginOfSafety / 20m * 14m));

            // 2. Capital Efficiency: ROE (Max 20 Pts)
            decimal roe = data.ReportedRoePercent;
            if (roe >= 20m) score += 20m;
            else if (roe >= 15m) score += 15m;
            else if (roe >= 10m) score += 10m;

            // 3. Solvency & Debt Health (Max 20 Pts)
            if (data.NetCashCr >= 0m)
            {
                score += 20m; // Net Cash positive is ideal
            }
            else if (data.EbitCr > 0m)
            {
                decimal netDebt = Math.Abs(data.NetCashCr);
                decimal debtToEbit = netDebt / data.EbitCr;

                if (debtToEbit <= 2.0m) score += 12m;
                else if (debtToEbit <= 3.5m) score += 6m;
            }

            // 4. Cash Flow Quality: OCF / Net Profit (Max 15 Pts)
            if (data.NetProfitCr > 0m && data.CashFromOperationsCr > 0m)
            {
                decimal cashConversion = data.CashFromOperationsCr / data.NetProfitCr;
                if (cashConversion >= 1.0m) score += 15m;
                else if (cashConversion >= 0.7m) score += 10m;
            }

            // 5. OCF Growth Stability (Max 15 Pts)
            var historyList = historicals?.OrderBy(h => h.Year).ToList();
            if (historyList != null && historyList.Count >= 3)
            {
                var oldest = historyList.First();
                var newest = historyList.Last();
                int periods = historyList.Count - 1;

                if (oldest.HistoricalOcfCr > 0 && newest.HistoricalOcfCr > 0)
                {
                    double ratio = (double)(newest.HistoricalOcfCr / oldest.HistoricalOcfCr);
                    decimal ocfCagr = (decimal)(Math.Pow(ratio, 1.0 / periods) - 1.0);

                    if (ocfCagr >= 0.12m) score += 15m;
                    else if (ocfCagr >= 0.05m) score += 10m;
                }
            }
            else
            {
                score += 7m; // Neutral allocation if historical data is limited
            }

            return (int)Math.Clamp(Math.Round(score), 0, 100);
        }
    }
}
