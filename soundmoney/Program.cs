using Microsoft.EntityFrameworkCore;
using SoundMoney.Data;
using SoundMoney.Services;
using SoundMoney.Services.IntrinsicValue;
using Google.GenAI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Register SQLite DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(connectionString));

// Register repository
builder.Services.AddScoped<IStockRepository, StockRepository>();

builder.Services.AddTransient<IScreenerService, ScreenerService>();

// Use Gemini API for all stock screener data
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.AddHostedService<DailyBackgroundService>();

var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    db.Database.Migrate();
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
