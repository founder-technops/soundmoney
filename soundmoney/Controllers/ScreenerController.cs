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

    [HttpGet]
    public IActionResult Error() => View();
}