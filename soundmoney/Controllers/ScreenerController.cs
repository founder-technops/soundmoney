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

        var (model, errorMessage) = await _screener.RunScreenDetailsAsync(symbol);

        // RunScreenDetailsAsync returns a null model when scraping/valuation fails
        // (invalid or delisted symbol, a network error reaching Screener.in, a parsing
        // failure, etc.) - passing that straight to View() crashed the page with a
        // NullReferenceException the moment the view touched Model.Symbol. Render the
        // same Error view used for unhandled exceptions, but with the specific reason.
        if (model is null)
        {
            return RenderScrapeError(symbol, errorMessage);
        }

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

        var (model, errorMessage) = await _screener.RunScreenDetailsAsync(symbol);

        // Same null guard as Details, for the same reason - see comment there.
        if (model is null)
        {
            return RenderScrapeError(symbol, errorMessage);
        }

        return View(model);
    }

    // Shared by Details and Summary: renders the same Error view
    // (Views/Screener/Error.cshtml) that app.UseExceptionHandler("/Screener/Error") shows
    // for a genuinely unhandled exception, but with the specific, user-safe reason this
    // particular symbol's scrape failed (e.g. "Could not reach Screener.in: ...") rather
    // than that view's generic "something went wrong" text. The view falls back to its
    // original generic wording whenever ViewData["ErrorMessage"] isn't set, so the
    // unhandled-exception path (which never sets it) is unaffected by this reuse.
    private IActionResult RenderScrapeError(string symbol, string? errorMessage)
    {
        ViewData["ErrorMessage"] = errorMessage ?? $"Could not retrieve or analyze data for symbol '{symbol}'.";
        ViewData["ErrorSymbol"] = symbol;
        Response.StatusCode = 404;
        return View("Error");
    }

    [HttpGet]
    public IActionResult Error() => View();
}