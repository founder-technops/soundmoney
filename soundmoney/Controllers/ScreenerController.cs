using Microsoft.AspNetCore.Mvc;
using SoundMoney.Models;
using SoundMoney.Services;

namespace SoundMoney.Controllers;

public class ScreenerController : Controller
{
    private readonly ISoundMoneyService _screener;

    public ScreenerController(ISoundMoneyService screener)
    {
        _screener = screener;
    }

    [HttpGet]
    public async Task<IActionResult> Index(decimal minMarginOfSafety = 0m, SectorCategory? sector = null)
    {
        if(minMarginOfSafety == 0)
        {
            return View(new ScreenerViewModel());
        }
        var results = await _screener.RunScreenAsync(minMarginOfSafety, sector);

        var vm = new ScreenerViewModel
        {
            Results = results,
            MinMarginOfSafety = minMarginOfSafety,
            SelectedSector = sector
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Error() => View();
}
