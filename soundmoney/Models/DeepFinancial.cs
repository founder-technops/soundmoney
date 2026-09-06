using System;

namespace SoundMoney.Models
{
    public class DeepFinancial : Financial
    {
        public string Symbol { get; set; } = string.Empty;

        // --- Header & Market Metrics ---
        public decimal CurrentPrice { get; set; }
        public decimal MarketCapCr { get; set; }
        public decimal ReportedPePercent { get; set; }
        public decimal BookValuePerShare { get; set; }
        public decimal DividendYieldPercent { get; set; }
        public decimal ReportedRocePercent { get; set; }
        public decimal ReportedRoePercent { get; set; }
        public decimal FaceValue { get; set; }

        // --- Shareholding Metrics ---
        public decimal TotalSharesCr => CurrentPrice > 0m && MarketCapCr > 0m
            ? Math.Round(MarketCapCr / CurrentPrice, 4)
            : 0m;
        public decimal Beta { get; set; } = 1.0m;
        public decimal PromoterPledgePercent { get; set; }
    }   
}