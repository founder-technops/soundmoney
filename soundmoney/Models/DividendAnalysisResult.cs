namespace SoundMoney.Models
{
    public class DividendAnalysisResult
    {
        public int ConsecutiveYearsPaid { get; set; }
        public int ConsecutiveYearsGrown { get; set; }
        public decimal FiveYearCagr { get; set; }
        public decimal AveragePayoutRatio { get; set; }
        public bool IsFcfSupported { get; set; }
        public bool IsConsistent { get; set; }
        public string HealthRating { get; set; } = "Unstable";
    }
}