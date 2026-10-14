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

            builder.CreateTable(
                name: "ScrapedFinancialCaches",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StockValuationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentFinancialJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HistoricalFinancialsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScrapedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapedFinancialCaches", x => x.Symbol);
                });

            // Indexes

            builder.CreateIndex(
                name: "IX_StockValuations_FetchedAt",
                table: "StockValuations",
                column: "FetchedAt");

            builder.CreateIndex(
                name: "IX_StockValuations_Symbol",
                table: "StockValuations",
                column: "Symbol");

            builder.CreateIndex(
               name: "IX_ScrapedFinancialCaches_ScrapedAt",
               table: "ScrapedFinancialCaches",
               column: "ScrapedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder builder)
        {
            builder.DropTable(name: "StockValuations");
            builder.DropTable(name: "ScrapedFinancialCaches");
        }
    }
}