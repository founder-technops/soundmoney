// Controllers/ScreenerController.cs
using Microsoft.AspNetCore.Mvc;
using SoundMoney.Models;
using System.Collections.Generic;
using System.Linq;
namespace SoundMoney.Controllers
{
    
    public class ScreenerController : Controller
    {
        // Mock database lookup. Replace this with an actual API service or Entity Framework context.
        private readonly List<Stock> _allStocks = new List<Stock>
    {
        new Stock { Ticker = "TCS", CompanyName = "Tata Consultancy Services.", IntrinsicValue = 250, Price = 175.00m, PeRatio = 28.5m, MarketCap = 2700000000000m, Sector = "Technology" },
        new Stock { Ticker = "INFOSYS", CompanyName = "Infosys.",  IntrinsicValue = 250,Price = 400.00m, PeRatio = 35.2m, MarketCap = 3000000000000m, Sector = "Technology" },
        new Stock { Ticker = "SBI BANK", CompanyName = "State Bank of India", IntrinsicValue = 250, Price = 180.00m, PeRatio = 11.8m, MarketCap = 520000000000m, Sector = "Finance" }
    };

        [HttpGet]
        public IActionResult Index()
        {
            var model = new ScreenerViewModel();
            model.Results = _allStocks; // Show all by default
            return View(model);
        }

        [HttpPost]
        public IActionResult Index(ScreenerViewModel model)
        {
            var query = _allStocks.AsQueryable();

            // Apply filters conditionally
            if (model.MinPrice.HasValue)
                query = query.Where(s => s.Price >= model.MinPrice.Value);

            if (model.MaxPrice.HasValue)
                query = query.Where(s => s.Price <= model.MaxPrice.Value);

            if (model.MinMOS.HasValue)
                query = query.Where(s => s.MarginOfSafety >= model.MinMOS.Value);

            if (model.MaxMOS.HasValue)
                query = query.Where(s => s.MarginOfSafety >= model.MaxMOS.Value);

            if (!string.IsNullOrEmpty(model.SelectedSector))
                query = query.Where(s => s.Sector == model.SelectedSector);

            model.Results = query.ToList();
            return View(model);
        }
    }

}
