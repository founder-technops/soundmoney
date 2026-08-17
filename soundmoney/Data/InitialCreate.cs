using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoundMoney.Data
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder builder)
        {
            builder.CreateTable(
                name: "DeepFinancials",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarketCapCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BookValuePerShare = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalSharesCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReportedRoePercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    DividendYieldPercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    RevenueCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    OperatingProfitEbitdaCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DepreciationCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetProfitCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DividendPayoutPercent = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    ShareCapitalCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReservesCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalEquityCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalBorrowingsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    NetFixedAssetsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CwipCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CashAndEquivalentsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    IntangibleAssetsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalLiabilitiesCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAssetsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CashFromOperationsCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GrossCapexCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    FreeCashFlowCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeepFinancials", x => x.Symbol);
                });

            builder.CreateTable(
                name: "HistoricalFinancials",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    HistoricalOcfCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    HistoricalCapexCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    HistoricalRevenueCr = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricalFinancials", x => new { x.Symbol, x.Year });
                });

            builder.CreateTable(
                name: "StockValuations",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrimaryMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SecondaryMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntrinsicValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MarginOfSafety = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Verdict = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockValuations", x => x.Symbol);
                });

            // Indexes
            builder.CreateIndex(
                name: "IX_HistoricalFinancials_Symbol",
                table: "HistoricalFinancials",
                column: "Symbol");

            builder.CreateIndex(
                name: "IX_HistoricalFinancials_Year",
                table: "HistoricalFinancials",
                column: "Year");

            builder.CreateIndex(
                name: "IX_StockValuations_FetchedAt",
                table: "StockValuations",
                column: "FetchedAt");

            builder.CreateIndex(
                name: "IX_StockValuations_Symbol",
                table: "StockValuations",
                column: "Symbol");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder builder)
        {
            builder.DropTable(name: "DeepFinancials");
            builder.DropTable(name: "HistoricalFinancials");
            builder.DropTable(name: "StockValuations");
        }
    }
}