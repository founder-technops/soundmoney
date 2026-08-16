using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SoundMoney.Models;

namespace SoundMoney.Data;

/// <summary>
/// SQLite database context for storing stock screening data.
/// Uses Code-First approach with automatic migrations.
/// </summary>
public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }
    public DbSet<StockValuation> StockValuations { get; set; } = null!;
    public DbSet<DeepFinancial> DeepFinancials { get; set;} = null!;
    public DbSet<HistoricalFinancial> HistoricalFinancials { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new StockValuationConfiguration());
        modelBuilder.ApplyConfiguration(new DeepFinancialConfiguration());
        modelBuilder.ApplyConfiguration(new HistoricalFinancialConfiguration());
    }
    public class StockValuationConfiguration : IEntityTypeConfiguration<StockValuation>
    {
        public void Configure(EntityTypeBuilder<StockValuation> builder)
        {
            // Table Name Mapping
            builder.ToTable("StockValuations");

            // Primary Key
            builder.HasKey(e => e.Symbol);

            // String Properties & Constraints
            builder.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false); // Storage optimization for ticker codes (VARCHAR)

            builder.Property(e => e.CompanyName)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.Sector)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.IntrinsicMethod)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Verdict)
                .IsRequired()
                .HasMaxLength(50);

            // Precision Configuration for Monetary & Percentage Values
            builder.Property(e => e.CurrentPrice)
                .HasPrecision(18, 2);

            builder.Property(e => e.IntrinsicValue)
                .HasPrecision(18, 2);

            builder.Property(e => e.MarginOfSafety)
                .HasPrecision(8, 4); // Standardized to (8,4) for percentage ratios

            // DateTime Configuration
            builder.Property(e => e.FetchedAt)
                .IsRequired();

            // Database Indexes
            // Note: Primary Key already creates a unique index on Symbol automatically.
            // Non-unique index added explicitly per your schema specification.
            builder.HasIndex(e => e.Symbol)
                .IsUnique(false);

            builder.HasIndex(e => e.FetchedAt);
        }
    }
    public class DeepFinancialConfiguration : IEntityTypeConfiguration<DeepFinancial>
    {
        public void Configure(EntityTypeBuilder<DeepFinancial> builder)
        {
            // Table Name
            builder.ToTable("DeepFinancials");

            // Primary Key
            builder.HasKey(e => e.Symbol);

            builder.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false); // Optimization for alphanumeric ticker symbols

            // ==========================================
            // Header Metrics
            // ==========================================
            builder.Property(e => e.CurrentPrice)
                .HasPrecision(18, 2);

            builder.Property(e => e.MarketCapCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.BookValuePerShare)
                .HasPrecision(18, 2);

            builder.Property(e => e.TotalSharesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.ReportedRoePercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.DividendYieldPercent)
                .HasPrecision(8, 4);

            // ==========================================
            // P&L Metrics
            // ==========================================
            builder.Property(e => e.RevenueCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.OperatingProfitEbitdaCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.DepreciationCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.NetProfitCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.DividendPayoutPercent)
                .HasPrecision(8, 4);

            // ==========================================
            // Balance Sheet Metrics
            // ==========================================
            builder.Property(e => e.ShareCapitalCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.ReservesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.TotalEquityCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.TotalBorrowingsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.NetFixedAssetsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CwipCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CashAndEquivalentsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.IntangibleAssetsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.TotalLiabilitiesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.TotalAssetsCr)
                .HasPrecision(18, 4);

            // ==========================================
            // Cash Flow Metrics
            // ==========================================
            builder.Property(e => e.CashFromOperationsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.GrossCapexCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.FreeCashFlowCr)
                .HasPrecision(18, 4);
        }
    }

    public class HistoricalFinancialConfiguration : IEntityTypeConfiguration<HistoricalFinancial>
    {
        public void Configure(EntityTypeBuilder<HistoricalFinancial> builder)
        {
            // Table Name
            builder.ToTable("HistoricalFinancials");

            // Composite Primary Key (Symbol + Year allows tracking multiple historical years per stock)
            builder.HasKey(e => new { e.Symbol, e.Year });

            // Symbol Property Constraints
            builder.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false); // Storage optimization for stock ticker symbols (VARCHAR)

            builder.Property(e => e.Year)
                .IsRequired();

            // Precision Configuration for Monetary Figures (In Crores)
            builder.Property(e => e.HistoricalOcfCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalCapexCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalRevenueCr)
                .HasPrecision(18, 4);

            // Indexes for Query Performance
            builder.HasIndex(e => e.Symbol);
            builder.HasIndex(e => e.Year);
        }
    }
}
