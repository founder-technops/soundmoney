namespace SoundMoney.Models
{
    public class HistoricalFinancial
    {
        public string Symbol { get; set; } = string.Empty;
        public int Year { get; set; }
        public decimal HistoricalOcfCr { get; set; }
        public decimal HistoricalCapexCr { get; set; }
        public decimal HistoricalRevenueCr { get; set; }
    }
}
