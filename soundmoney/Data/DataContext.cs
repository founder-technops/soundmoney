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
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new StockValuationConfiguration());
        
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
}