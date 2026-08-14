# SQLite Database Integration - Setup Guide

## ✅ What Was Added

### Database Infrastructure
- **SoundMoneyDbContext** - Entity Framework Core DbContext for SQLite
- **StockValuation** - Model class for storing stock data
- **IStockRepository** / **StockRepository** - Repository pattern for data access

### Features
✅ Code-First approach with automatic migrations  
✅ Stock data persisted in SQLite database  
✅ Fetch from Gemini API → Calculate valuation → Store in DB  
✅ Filter and retrieve from database on subsequent requests  
✅ Automatic database creation and schema migration on startup  

## 📊 Database Schema

### StockValuation Table
```
Id (int) - Primary key
Symbol (string) - Stock ticker symbol
CompanyName (string) - Official company name
CurrentPrice (decimal) - Current market price in INR
Sector (string) - Sector category
EPS (decimal) - Earnings per share
BookValuePerShare (decimal) - Book value per share
ROE (decimal) - Return on equity %
DividendPerShare (decimal) - Dividend per share
EstimatedGrowthRate (decimal) - Annual growth rate %
RequiredRateOfReturn (decimal) - Cost of equity %
IntrinsicValue (decimal) - Calculated intrinsic value
MarginOfSafetyPercent (decimal) - Margin of safety %
Verdict (string) - Undervalued/Fair/Overvalued
FetchedAt (DateTime) - When data was fetched from Gemini
UpdatedAt (DateTime) - When record was last updated
```

### Indexes
- **Symbol** - For quick lookups
- **FetchedAt** - For sorting by recency

## 🔄 Data Flow

```
Run Button Click
    ↓
GeminiScreenerService.RunScreenAsync()
    ↓
For each symbol:
  1. Call Gemini API
  2. Parse response
  3. Calculate intrinsic value
  4. Store in SQLite via StockRepository
  5. Return ScreenerResultRow
    ↓
Apply filters (min MOS, sector)
    ↓
Display results in UI
```

## 🛠️ Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=soundmoney.db;Cache=Shared"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
    "Model": "gemini-2.0-flash",
    "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/models"
  },
  "Watchlist": ["RELIANCE", "TCS", "INFY", ...]
}
```

## 📦 NuGet Packages Added
- `Microsoft.EntityFrameworkCore.Sqlite` (8.0.0)
- `Microsoft.EntityFrameworkCore.Design` (8.0.0)

## 🚀 Running the Application

### First Run
```bash
dotnet restore
dotnet run
```

On first startup:
1. DbContext detects missing database
2. Creates `soundmoney.db` in project root
3. Applies all migrations
4. Application is ready to use

### Subsequent Runs
```bash
dotnet run
```

Database persists between runs. Filtering and retrieval use cached data from the database.

## 💾 Database Operations

### Repository Methods
```csharp
// Get by symbol
var stock = await _stockRepository.GetBySymbolAsync("TCS");

// Get all stocks
var allStocks = await _stockRepository.GetAllAsync();

// Filter by MOS and sector
var filtered = await _stockRepository.GetByFilterAsync(20m, SectorCategory.Banking);

// Add or update
await _stockRepository.AddOrUpdateAsync(StockValuation);

// Delete
await _stockRepository.DeleteAsync("TCS");

// Clear all
await _stockRepository.DeleteAllAsync();
```

## 🔍 View Database

### Using SQLite Browser
1. Download [DB Browser for SQLite](https://sqlitebrowser.org/)
2. Open `soundmoney.db`
3. Browse `Stocks` table
4. View all fetched and calculated data

### Using CLI
```bash
sqlite3 soundmoney.db
SELECT * FROM Stocks;
SELECT * FROM Stocks WHERE MarginOfSafetyPercent > 20;
SELECT * FROM Stocks WHERE Sector = 'Banking' ORDER BY MarginOfSafetyPercent DESC;
```

## 📈 Workflow Example

### First Run
```
Click "Run Screener"
  → Fetch TCS from Gemini
  → Calculate intrinsic value
  → Store in DB with timestamp
  → Fetch RELIANCE from Gemini
  → Calculate intrinsic value
  → Store in DB with timestamp
  ... (repeat for all symbols)
  → Filter results
  → Display table
```

### Second Run (Later Same Day)
```
Click "Run Screener"
  → Same process - fetches fresh data from Gemini
  → Updates existing records in DB
  → Displays updated results
```

## 🎯 Benefits

✅ **Persistent storage** - Stock data survives app restarts  
✅ **Fast filtering** - Queries on indexed columns are fast  
✅ **Real-time updates** - Refresh by clicking Run button  
✅ **Historical tracking** - `FetchedAt` and `UpdatedAt` timestamps  
✅ **Easy to extend** - Add more columns to StockValuation as needed  
✅ **No manual CSV** - Automated data fetch and storage  

## 🔄 Database File Location

- **Development:** `soundmoney/soundmoney.db` (relative to project root)
- **Production:** Configure in connection string

To change location, modify in `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=C:/path/to/soundmoney.db;Cache=Shared"
}
```

---

**Database integration complete! Your stock screener now persists data and allows efficient filtering.** 🎉
