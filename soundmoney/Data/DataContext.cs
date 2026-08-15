using Microsoft.EntityFrameworkCore;
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

        // Configure StockValuation entity
        modelBuilder.Entity<StockValuation>(entity =>
        {
            entity.HasKey(e => e.Symbol);

            entity.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.CompanyName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Sector)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.IntrinsicMethod)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Verdict)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CurrentPrice)
                .HasPrecision(18, 2);

            entity.Property(e => e.IntrinsicValue)
                .HasPrecision(18, 2);

            entity.Property(e => e.MarginOfSafety)
                .HasPrecision(18, 2);

            // Create index on Symbol for faster queries
            entity.HasIndex(e => e.Symbol)
                .IsUnique(false);

            // Create index on FetchedAt for sorting
            entity.HasIndex(e => e.FetchedAt);
        });
    }
}
