// Models/ScreenerViewModel.cs
using System.Collections.Generic;

namespace SoundMoney.Models
{
    
    public class ScreenerViewModel
    {
        // Filter Inputs
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public decimal? MinMOS { get; set; }
        public decimal? MaxMOS { get; set; }
        public string SelectedSector { get; set; }

        // Dropdown options
        public List<string> Sectors { get; set; } = new List<string> { "Technology", "Finance", "Healthcare", "Energy" };

        // Results
        public List<Stock> Results { get; set; } = new List<Stock>();
    }

}
