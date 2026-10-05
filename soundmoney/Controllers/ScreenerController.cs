using Microsoft.AspNetCore.Mvc;
using SoundMoney.Models;
using SoundMoney.Services;

namespace SoundMoney.Controllers;

public class ScreenerController : Controller
{
    private readonly IScreenerService _screener;

    public ScreenerController(IScreenerService screener)
    {
        _screener = screener;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchQuery = null,
        decimal minMarginOfSafety = 0m,
        List<string>? scores = null)
    {
        bool hasScoreFilter = scores != null && scores.Any();

        if (string.IsNullOrWhiteSpace(searchQuery) && minMarginOfSafety == 0m && !hasScoreFilter)
        {
            return View(new ScreenerViewModel());
        }

        // Run screen for the first score or pass null if no scores selected
        var results = await _screener.RunScreenAsync(minMarginOfSafety, searchQuery, scores);

        // Local filtering for multi-selected scores
        if (hasScoreFilter && scores!.Count > 1)
        {
            results = results.Where(r => scores.Contains(r.SoundScoreRating)).ToList();
        }

        // Local text search filter
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.Trim().ToLowerInvariant();
            results = results.Where(r =>
                r.Symbol.ToLowerInvariant().Contains(query) ||
                r.CompanyName.ToLowerInvariant().Contains(query)
            ).ToList();
        }

        var vm = new ScreenerViewModel
        {
            Results = results,
            SearchQuery = searchQuery,
            MinMarginOfSafety = minMarginOfSafety,
            SelectedScores = scores ?? new List<string>()
        };

        return View(vm);
    }

    [HttpGet("Screener/Details/{symbol}")]
    public async Task<IActionResult> Details(string symbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest("Stock symbol is required.");
        }

        var model = await _screener.RunScreenDetailsAsync(symbol);

        return View(model);
    }

    // Plain-English companion to Details, for common-man/retail investors rather than
    // readers comfortable with ratios and jargon. Reuses the exact same service call (and
    // therefore the exact same underlying numbers) as Details - only the view differs -
    // so the two pages can never show inconsistent figures for the same stock.
    [HttpGet("Screener/Summary/{symbol}")]
    public async Task<IActionResult> Summary(string symbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest("Stock symbol is required.");
        }

        var model = await _screener.RunScreenDetailsAsync(symbol);

        return View(model);
    }

    [HttpGet]
    public IActionResult Error() => View();
}