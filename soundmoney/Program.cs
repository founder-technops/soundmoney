using Microsoft.EntityFrameworkCore;
using SoundMoney.Data;
using SoundMoney.Services;
using Google.GenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Register SQLite DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        );
    }));

// Register repository
builder.Services.AddScoped<IValuationRepository, ValuationRepository>();
// Register repository
builder.Services.AddScoped<IFinancialRepository, FinancialRepository>();

builder.Services.AddTransient<IValuationService, ValuationService>();

builder.Services.AddTransient<IScreenerService, ScreenerService>();

// Use Gemini API for all stock screener data
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.AddHttpClient<IScraperService, ScraperService>();

builder.Services.AddHttpClient<NseStockSeederService>();

builder.Services.AddHostedService<AutomationService>();

var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    db.Database.Migrate();
}

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<NseStockSeederService>();

    // Pass local CSV path if available, or leave empty to fetch automatically via HTTP
    await seeder.SeedFromNseCsvAsync();
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Screener/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Screener}/{action=Index}/{id?}");

app.Run();
