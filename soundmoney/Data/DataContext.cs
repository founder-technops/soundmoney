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
    public DbSet<DeepFinancial> DeepFinancials { get; set; } = null!;
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

            builder.Property(e => e.PrimaryMethod)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.SecondaryMethod)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Verdict)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.SoundScoreRating)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.ErrorMessage)
                .HasMaxLength(1000);

            // Precision Configuration for Monetary & Percentage Values
            builder.Property(e => e.CurrentPrice)
                .HasPrecision(18, 2);

            builder.Property(e => e.IntrinsicValue)
                .HasPrecision(18, 2);

            builder.Property(e => e.MarginOfSafety)
                .HasPrecision(8, 4); // Standardized to (8,4) for percentage ratios

            builder.Property(e => e.SoundScore)
                .HasPrecision(5, 2);

            builder.Property(e => e.DividendYieldPercent)
                .HasPrecision(8, 4);

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

            builder.Property(e => e.ReportedPePercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.BookValuePerShare)
                .HasPrecision(18, 2);

            builder.Property(e => e.DividendYieldPercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.ReportedRocePercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.ReportedRoePercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.FaceValue)
                .HasPrecision(10, 4);

            // ==========================================
            // Shareholding Metrics
            // ==========================================
            builder.Property(e => e.Beta)
                .HasPrecision(8, 4);

            builder.Property(e => e.PromoterPledgePercent)
                .HasPrecision(8, 4);

            // ==========================================
            // P&L Metrics
            // ==========================================
            builder.Property(e => e.SalesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.ExpenseCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.OperatingProfitCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.OtherIncomeCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.IntrestIncomeCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.DepreciationCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.ProfitBeforeTaxCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.TaxPercent)
                .HasPrecision(8, 4);

            builder.Property(e => e.NetProfitCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.InterestExpenseCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.Eps)
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

            builder.Property(e => e.TotalBorrowingsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.OtherLiabilitiesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.NetFixedAssetsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CwipCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.InvestmentsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.OtherAssetsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CashAndEquivalentsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CashHistoryYears);

            builder.Property(e => e.IsCashEstimateReliable)
                .HasDefaultValue(true);

            // ==========================================
            // Cash Flow Metrics
            // ==========================================
            builder.Property(e => e.CashFromOperationsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CashFromInvestmentCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.CashFromFinanceCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.FreeCashFlowCr)
                .HasPrecision(18, 4);

            // ==========================================
            // Computed / derived properties (get-only, no backing field) — these are
            // calculated on the fly from the stored fields above and must never be
            // mapped to columns. Explicitly configuring precision on a property with no
            // setter (as TotalSharesCr, TotalEquityCapitalCr, TotalLiabilitiesCr,
            // TotalAssetsCr and GrossCapexCr previously were) forces EF to add it to the
            // model anyway, which throws at startup because EF has no way to persist a
            // value back into it. Ignore() keeps them purely in-memory.
            // ==========================================
            builder.Ignore(e => e.TotalSharesCr);
            builder.Ignore(e => e.OperatingProfitMargin);
            builder.Ignore(e => e.EbitCr);
            builder.Ignore(e => e.TotalLiabilitiesCr);
            builder.Ignore(e => e.TotalAssetsCr);
            builder.Ignore(e => e.TotalEquityCapitalCr);
            builder.Ignore(e => e.NetCashCr);
            builder.Ignore(e => e.NonCurrentAssetsCr);
            builder.Ignore(e => e.CurrentAssetsCr);
            builder.Ignore(e => e.WorkingCapitalCr);
            builder.Ignore(e => e.NetCashFlowCr);
            builder.Ignore(e => e.GrossCapexCr);
            builder.Ignore(e => e.CfoToOpRatio);
            builder.Ignore(e => e.CapitalAdequacyPercent);
            builder.Ignore(e => e.ReportedRoaPercent);
            builder.Ignore(e => e.EffectiveTaxRate);
            builder.Ignore(e => e.CostOfDebt);
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
            builder.Property(e => e.EquityCapitalCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalRevenueCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalOperatingProfitCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalNetProfitCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalOcfCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalCapexCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalCashAndEquivalentsCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalFcfCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalPatCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.HistoricalSharesCr)
                .HasPrecision(18, 4);

            builder.Property(e => e.DividendPayoutPercent)
                .HasPrecision(8, 4);

            // Computed / derived properties (get-only, no backing field) — never map these.
            builder.Ignore(e => e.CashConversionRatio);
            builder.Ignore(e => e.CfoToOpRatio);

            // Indexes for Query Performance
            builder.HasIndex(e => e.Symbol);
            builder.HasIndex(e => e.Year);
        }
    }
}