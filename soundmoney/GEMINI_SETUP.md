# Gemini-Based Stock Screener - Setup Guide

## Changes Made

### ? Removed Services
- `TwelveDataApiService.cs` - No longer needed (Gemini replaces market data)
- `AlphaVantageService.cs` - No longer needed (Gemini replaces fundamentals)
- `FundamentalDataService.cs` - No longer needed (Gemini replaces CSV-based data)
- `SoundMoneyService.cs` - Replaced with unified `GeminiScreenerService`
- `App_Data/fundamentals.csv` - No longer needed (Gemini provides real-time data)

### ? Created
- `GeminiScreenerService.cs` - Unified service that fetches all screener data from Gemini API

### ? Updated
- `Program.cs` - Simplified DI to use only `GeminiScreenerService`
- `appsettings.json` - Replaced TwelveData/AlphaVantage keys with Gemini configuration

## How It Works

```
ScreenerController ? GeminiScreenerService ? Gemini API
                                          ?
                    Returns: ScreenerResultRow[]
                    (Symbol, Company, Price, Sector, IV, MOS%)
```

For each symbol in the watchlist:
1. GeminiScreenerService calls Gemini API with a structured prompt
2. Gemini returns JSON with: company name, current price, fundamentals (EPS, ROE, etc.), sector
3. Service calculates intrinsic value using sector-specific strategies
4. Service calculates margin of safety
5. Results are filtered and returned

## Setup Steps

### 1. Get a Gemini API Key
- Visit: https://ai.google.dev/
- Create a free account
- Generate an API key
- Replace `YOUR_GEMINI_API_KEY_HERE` in `appsettings.json`

### 2. Update appsettings.json
```json
"Gemini": {
  "ApiKey": "YOUR_ACTUAL_KEY_HERE",
  "Model": "gemini-2.0-flash",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models"
}
```

### 3. Run the Application
```bash
dotnet run
```

Navigate to: `http://localhost:5000/Screener`

## Data Flow

The GeminiScreenerService sends a prompt like:
```
For NSE symbol 'TCS', provide:
- Company name
- Current price (INR)
- Sector
- EPS, Book Value, ROE, Dividend, Growth Rate, Required Return
```

Gemini responds with JSON, which is parsed into a `ScreenerResultRow` containing:
- Symbol & Company Name
- Current Price
- Sector Category (mapped from text)
- Intrinsic Value (calculated via sector strategy)
- Margin of Safety %
- Verdict (Undervalued/Fair/Overvalued)

## Advantages

? **Real-time data** - No manual CSV maintenance  
? **Complete fundamental** - Gemini extracts all needed metrics  
? **Reduced dependencies** - Single API replaces 3 services  
? **Easy to extend** - Modify prompts to get additional metrics  
? **Free tier available** - Gemini has a generous free tier  

## Cleanup (Optional)

You can safely delete these files if you want a clean repo:
- `soundmoney/Services/TwelveDataApiService.cs`
- `soundmoney/Services/AlphaVantageService.cs`
- `soundmoney/Services/FundamentalDataService.cs`
- `soundmoney/Services/SoundMoneyService.cs`
- `soundmoney/App_Data/fundamentals.csv`

The application will work without them since everything now routes through `GeminiScreenerService`.

## Troubleshooting

### "No candidates in Gemini response"
- Check your API key is valid
- Ensure the model name matches your account's available models
- Check network connectivity

### "Error parsing Gemini response"
- Gemini might be returning non-JSON text
- Try increasing timeout or using a more specific prompt

### Rate Limiting
- Free tier: ~60 requests/minute
- The service adds 1-second delays between symbols
- For large watchlists, consider upgrading to a paid tier

---

That's it! Your stock screener now runs entirely on Gemini API. ??
