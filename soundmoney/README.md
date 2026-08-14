# SoundMoney — NSE Intrinsic Value Screener

ASP.NET Core MVC (.NET 8) app that screens a watchlist of NSE-listed stocks
by intrinsic value and margin of safety, using a sector-specific valuation
model powered by Google Gemini API.

## How it works

All stock data (price, company name, fundamentals: EPS, Book Value, ROE, etc.) is fetched in real-time from **Google Gemini API**, which acts as a unified data aggregator.

| Data | Source |
|---|---|
| Company name, current price, EPS, Book Value, ROE, dividend, growth rate, sector | [Google Gemini API](https://ai.google.dev/) via `GeminiScreenerService` |
| Intrinsic value | Sector-specific strategy in `Services/IntrinsicValue/` |
| Margin of safety | `(Intrinsic Value − Current Price) / Intrinsic Value × 100` |

### Why Gemini API?

Gemini API provides a unified, real-time data interface that:
- ✅ Returns all fundamental metrics in a single call
- ✅ Automatically classifies sectors
- ✅ No need to maintain separate data sources or manual CSV files
- ✅ Generous free tier (60 requests/minute)
- ✅ Easy to extend with additional metrics

### Sector-specific valuation

- **Banking** (`BankingValuationStrategy`) — justified Price/Book multiple
  from the excess-return model: `(ROE − g) / (CostOfEquity − g)`.
- **IT / software services** (`ItServicesValuationStrategy`) — Graham's
  revised earnings formula with a higher growth cap, reflecting the
  sector's asset-light, growth-driven economics.
- **Everything else** (`DefaultGrahamStrategy`) — average of Graham's
  revised earnings formula and the more conservative Graham Number
  (`sqrt(22.5 × EPS × BookValue)`), with a per-sector growth cap (FMCG,
  Pharma, Auto, Metals, Energy, Infrastructure, Other).

Add a new strategy by implementing `IIntrinsicValueStrategy` and
registering it in `IntrinsicValueStrategyFactory`.

## Setup

### 1. Get a Gemini API Key
- Visit: https://ai.google.dev/
- Create a free account
- Generate an API key

### 2. Update appsettings.json
```json
"Gemini": {
  "ApiKey": "YOUR_ACTUAL_API_KEY_HERE",
  "Model": "gemini-2.0-flash",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models"
},
"Watchlist": [
  "RELIANCE",
  "TCS",
  "INFY",
  "HDFCBANK",
  "ICICIBANK",
  "HINDUNILVR",
  "SUNPHARMA",
  "MARUTI",
  "TATASTEEL",
  "NTPC"
]
```

### 3. Run the Application
```bash
dotnet restore
dotnet run
```

Then open the URL shown in the console (e.g. `https://localhost:5001`).

## Project layout

```
Controllers/ScreenerController.cs          Web entry point
Services/GeminiScreenerService.cs          Unified data fetch + valuation via Gemini API
Services/IntrinsicValue/                   Sector-specific valuation strategies
Models/                                    DTOs + sector enum/mapper
Views/Screener/Index.cshtml                Results table + filter form
appsettings.json                           Configuration (Gemini API key, watchlist)
```

## Rate Limiting

- **Free tier:** ~60 requests/minute
- **Default:** 1-second delay between symbols to be respectful
- **Large watchlists:** Consider upgrading to a paid Gemini tier for faster screening

---

Stock screener powered by **Google Gemini API** 🚀
