namespace SoundMoney.Models
{
    // Models/Stock.cs
    public class Stock
    {
        public string Ticker { get; set; }
        public string CompanyName { get; set; }
        public decimal Price { get; set; }
        public decimal PeRatio { get; set; }
        public decimal PriceToBook { get; set; }
        public decimal IntrinsicValue { get; set; }
        public decimal MarginOfSafety { get { return (Price / IntrinsicValue) * 100; } }
        public decimal MarketCap { get; set; }
        public decimal DividendYield { get; set; }
        public string Sector { get; set; }
    }

}
