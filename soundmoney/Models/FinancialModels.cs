using SoundMoney.Algorithms;
using System.Collections.Frozen;

namespace SoundMoney.Models
{
    public enum MacroSector
    {
        FinancialServices,
        InformationTechnology,
        Healthcare,
        ConsumerStaples,
        ConsumerDiscretionary,
        Automobile,
        CapitalGoods,
        MaterialsAndChemicals,
        InfrastructureAndConstruction,
        EnergyAndUtilities,
        Telecommunication,
        Other
    }

    public class Financial
    {
        public string Symbol { get; set; } = string.Empty;
        public bool IsFinancialSector { get; set; }
        public bool IsCoreInvestmentCompanyExplicit { get; set; }

        // --- Header & Market Metrics ---
        public decimal CurrentPrice { get; set; }
        public decimal MarketCapCr { get; set; }
        public decimal ReportedPePercent { get; set; }
        public decimal BookValuePerShare { get; set; }
        public decimal DividendYieldPercent { get; set; }
        public decimal ReportedRocePercent { get; set; }
        public decimal ReportedRoePercent { get; set; }
        public decimal FaceValue { get; set; }

        // --- Shareholding Metrics ---
        public decimal Beta { get; set; } = 1.0m;
        public decimal PromoterPledgePercent { get; set; }
        public int Year { get; set; }

        // --- P&L Metrics (Cr) ---
        public decimal SalesCr { get; set; }
        public decimal ExpenseCr { get; set; }
        public decimal OperatingProfitCr { get; set; }
        public decimal OtherIncomeCr { get; set; }
        public decimal IntrestIncomeCr { get; set; }
        public decimal DepreciationCr { get; set; }
        public decimal ProfitBeforeTaxCr { get; set; }
        public decimal TaxPercent { get; set; }
        public decimal NetProfitCr { get; set; }
        public decimal InterestExpenseCr { get; set; }
        public decimal Eps { get; set; }
        public decimal DividendPayoutPercent { get; set; }

        // --- Balance Sheet Metrics (Cr) ---
        public decimal ShareCapitalCr { get; set; }
        public decimal ReservesCr { get; set; }
        public decimal TotalBorrowingsCr { get; set; }
        public decimal OtherLiabilitiesCr { get; set; }
        public decimal FixedAssetsCr { get; set; }
        public decimal CwipCr { get; set; }
        public decimal InvestmentsCr { get; set; }
        public decimal OtherAssetsCr { get; set; }
        public decimal CashAndEquivalentsCr { get; set; }
        public int CashHistoryYears { get; set; }
        public bool IsCashEstimateReliable { get; set; } = true;

        // --- Cash Flow Metrics (Cr) ---
        public decimal CashFromOperationsCr { get; set; }
        public decimal CashFromInvestmentCr { get; set; }
        public decimal CashFromFinanceCr { get; set; }
        public decimal FreeCashFlowCr { get; set; }

        // --- Ratios & Operational Metrics ---
        public decimal CashConversionCycleDays { get; set; }
    }

    public record ValuationMethodology
    {
        public required string PrimaryMethod { get; init; }
        public required string SecondaryMethod { get; init; }
        public required string Rationale { get; init; }
    }
    public class DividendAnalysisResult
    {
        public int ConsecutiveYearsPaid { get; set; }
        public int ConsecutiveYearsGrown { get; set; }
        public decimal FiveYearCagr { get; set; }
        public decimal AveragePayoutRatio { get; set; }
        public bool IsFcfSupported { get; set; }
        public bool IsConsistent { get; set; }
        public string HealthRating { get; set; } = "Unstable";
    }

    public static class SectorClassifier
    {
        private static readonly FrozenDictionary<string, MacroSector> SectorMapping =
            new Dictionary<string, MacroSector>(StringComparer.OrdinalIgnoreCase)
            {
                // Financial Services
                ["Public Sector Bank"] = MacroSector.FinancialServices,
                ["Private Sector Bank"] = MacroSector.FinancialServices,
                ["Other Bank"] = MacroSector.FinancialServices,
                ["Non Banking Financial Company (NBFC)"] = MacroSector.FinancialServices,
                ["Housing Finance Company"] = MacroSector.FinancialServices,
                ["Microfinance Institutions"] = MacroSector.FinancialServices,
                ["Asset Management Company"] = MacroSector.FinancialServices,
                ["Stockbroking & Allied"] = MacroSector.FinancialServices,
                ["Exchange and Data Platform"] = MacroSector.FinancialServices,
                ["Depositories, Clearing Houses and Other Intermediaries"] = MacroSector.FinancialServices,
                ["Ratings"] = MacroSector.FinancialServices,
                ["Financial Technology (Fintech)"] = MacroSector.FinancialServices,
                ["Investment Company"] = MacroSector.FinancialServices,
                ["Holding Company"] = MacroSector.FinancialServices,
                ["Insurance Distributors"] = MacroSector.FinancialServices,
                ["Life Insurance"] = MacroSector.FinancialServices,
                ["General Insurance"] = MacroSector.FinancialServices,
                ["Other Capital Market related Services"] = MacroSector.FinancialServices,
                ["Financial Products Distributor"] = MacroSector.FinancialServices,
                ["Financial Institution"] = MacroSector.FinancialServices,
                ["Other Financial Services"] = MacroSector.FinancialServices,

                // Information Technology
                ["Computers - Software & Consulting"] = MacroSector.InformationTechnology,
                ["Software Products"] = MacroSector.InformationTechnology,
                ["IT Enabled Services"] = MacroSector.InformationTechnology,
                ["Business Process Outsourcing (BPO)/ Knowledge Process Outsourcing (KPO)"] = MacroSector.InformationTechnology,
                ["Data Processing Services"] = MacroSector.InformationTechnology,
                ["Computers Hardware & Equipments"] = MacroSector.InformationTechnology,
                ["Internet & Catalogue Retail"] = MacroSector.InformationTechnology,
                ["E-Retail/ E-Commerce"] = MacroSector.InformationTechnology,
                ["E-Learning"] = MacroSector.InformationTechnology,
                ["Digital Entertainment"] = MacroSector.InformationTechnology,

                // Healthcare
                ["Pharmaceuticals"] = MacroSector.Healthcare,
                ["Biotechnology"] = MacroSector.Healthcare,
                ["Hospital"] = MacroSector.Healthcare,
                ["Healthcare Service Provider"] = MacroSector.Healthcare,
                ["Healthcare Research, Analytics & Technology"] = MacroSector.Healthcare,
                ["Medical Equipment & Supplies"] = MacroSector.Healthcare,
                ["Pharmacy Retail"] = MacroSector.Healthcare,
                ["Wellness"] = MacroSector.Healthcare,

                // Consumer Staples
                ["Diversified FMCG"] = MacroSector.ConsumerStaples,
                ["Packaged Foods"] = MacroSector.ConsumerStaples,
                ["Other Food Products"] = MacroSector.ConsumerStaples,
                ["Edible Oil"] = MacroSector.ConsumerStaples,
                ["Dairy Products"] = MacroSector.ConsumerStaples,
                ["Tea & Coffee"] = MacroSector.ConsumerStaples,
                ["Sugar"] = MacroSector.ConsumerStaples,
                ["Meat Products including Poultry"] = MacroSector.ConsumerStaples,
                ["Seafood"] = MacroSector.ConsumerStaples,
                ["Animal Feed"] = MacroSector.ConsumerStaples,
                ["Cigarettes & Tobacco Products"] = MacroSector.ConsumerStaples,
                ["Breweries & Distilleries"] = MacroSector.ConsumerStaples,
                ["Other Beverages"] = MacroSector.ConsumerStaples,
                ["Personal Care"] = MacroSector.ConsumerStaples,
                ["Household Products"] = MacroSector.ConsumerStaples,

                // Consumer Discretionary
                ["Speciality Retail"] = MacroSector.ConsumerDiscretionary,
                ["Diversified Retail"] = MacroSector.ConsumerDiscretionary,
                ["Auto Dealer"] = MacroSector.ConsumerDiscretionary,
                ["Dealers-Commercial Vehicles, Tractors, Construction Vehicles"] = MacroSector.ConsumerDiscretionary,
                ["Hotels & Resorts"] = MacroSector.ConsumerDiscretionary,
                ["Restaurants"] = MacroSector.ConsumerDiscretionary,
                ["Tour, Travel Related Services"] = MacroSector.ConsumerDiscretionary,
                ["Amusement Parks/ Other Recreation"] = MacroSector.ConsumerDiscretionary,
                ["Leisure Products"] = MacroSector.ConsumerDiscretionary,
                ["Consumer Electronics"] = MacroSector.ConsumerDiscretionary,
                ["Household Appliances"] = MacroSector.ConsumerDiscretionary,
                ["Houseware"] = MacroSector.ConsumerDiscretionary,
                ["Glass - Consumer"] = MacroSector.ConsumerDiscretionary,
                ["Furniture, Home Furnishing"] = MacroSector.ConsumerDiscretionary,
                ["Footwear"] = MacroSector.ConsumerDiscretionary,
                ["Garments & Apparels"] = MacroSector.ConsumerDiscretionary,
                ["Other Textile Products"] = MacroSector.ConsumerDiscretionary,
                ["Leather And Leather Products"] = MacroSector.ConsumerDiscretionary,
                ["Gems, Jewellery And Watches"] = MacroSector.ConsumerDiscretionary,
                ["Cycles"] = MacroSector.ConsumerDiscretionary,
                ["Media & Entertainment"] = MacroSector.ConsumerDiscretionary,
                ["Advertising & Media Agencies"] = MacroSector.ConsumerDiscretionary,
                ["TV Broadcasting & Software Production"] = MacroSector.ConsumerDiscretionary,
                ["Film Production, Distribution & Exhibition"] = MacroSector.ConsumerDiscretionary,
                ["Print Media"] = MacroSector.ConsumerDiscretionary,
                ["Electronic Media"] = MacroSector.ConsumerDiscretionary,
                ["Printing & Publication"] = MacroSector.ConsumerDiscretionary,
                ["Education"] = MacroSector.ConsumerDiscretionary,

                // Automobile
                ["Passenger Cars & Utility Vehicles"] = MacroSector.Automobile,
                ["Commercial Vehicles"] = MacroSector.Automobile,
                ["2/3 Wheelers"] = MacroSector.Automobile,
                ["Tractors"] = MacroSector.Automobile,
                ["Construction Vehicles"] = MacroSector.Automobile,
                ["Auto Components & Equipments"] = MacroSector.Automobile,
                ["Tyres & Rubber Products"] = MacroSector.Automobile,
                ["Trading - Auto components"] = MacroSector.Automobile,

                // Capital Goods
                ["Heavy Electrical Equipment"] = MacroSector.CapitalGoods,
                ["Compressors, Pumps & Diesel Engines"] = MacroSector.CapitalGoods,
                ["Other Electrical Equipment"] = MacroSector.CapitalGoods,
                ["Cables - Electricals"] = MacroSector.CapitalGoods,
                ["Castings & Forgings"] = MacroSector.CapitalGoods,
                ["Abrasives & Bearings"] = MacroSector.CapitalGoods,
                ["Electrodes & Refractories"] = MacroSector.CapitalGoods,
                ["Industrial Products"] = MacroSector.CapitalGoods,
                ["Other Industrial Products"] = MacroSector.CapitalGoods,
                ["Plastic Products - Industrial"] = MacroSector.CapitalGoods,
                ["Glass - Industrial"] = MacroSector.CapitalGoods,
                ["Railway Wagons"] = MacroSector.CapitalGoods,
                ["Aerospace & Defense"] = MacroSector.CapitalGoods,
                ["Ship Building & Allied Services"] = MacroSector.CapitalGoods,
                ["Diversified Commercial Services"] = MacroSector.CapitalGoods,
                ["Consulting Services"] = MacroSector.CapitalGoods,

                // Materials & Chemicals
                ["Iron & Steel"] = MacroSector.MaterialsAndChemicals,
                ["Sponge Iron"] = MacroSector.MaterialsAndChemicals,
                ["Pig Iron"] = MacroSector.MaterialsAndChemicals,
                ["Ferro & Silica Manganese"] = MacroSector.MaterialsAndChemicals,
                ["Iron & Steel Products"] = MacroSector.MaterialsAndChemicals,
                ["Aluminium"] = MacroSector.MaterialsAndChemicals,
                ["Copper"] = MacroSector.MaterialsAndChemicals,
                ["Zinc"] = MacroSector.MaterialsAndChemicals,
                ["Precious Metals"] = MacroSector.MaterialsAndChemicals,
                ["Diversified Metals"] = MacroSector.MaterialsAndChemicals,
                ["Aluminium, Copper & Zinc Products"] = MacroSector.MaterialsAndChemicals,
                ["Coal"] = MacroSector.MaterialsAndChemicals,
                ["Industrial Minerals"] = MacroSector.MaterialsAndChemicals,
                ["Granites & Marbles"] = MacroSector.MaterialsAndChemicals,
                ["Specialty Chemicals"] = MacroSector.MaterialsAndChemicals,
                ["Commodity Chemicals"] = MacroSector.MaterialsAndChemicals,
                ["Trading - Chemicals"] = MacroSector.MaterialsAndChemicals,
                ["Petrochemicals"] = MacroSector.MaterialsAndChemicals,
                ["Fertilizers"] = MacroSector.MaterialsAndChemicals,
                ["Pesticides & Agrochemicals"] = MacroSector.MaterialsAndChemicals,
                ["Dyes And Pigments"] = MacroSector.MaterialsAndChemicals,
                ["Carbon Black"] = MacroSector.MaterialsAndChemicals,
                ["Printing Inks"] = MacroSector.MaterialsAndChemicals,
                ["Explosives"] = MacroSector.MaterialsAndChemicals,
                ["Industrial Gases"] = MacroSector.MaterialsAndChemicals,

                // Infrastructure & Construction
                ["Civil Construction"] = MacroSector.InfrastructureAndConstruction,
                ["Residential, Commercial Projects"] = MacroSector.InfrastructureAndConstruction,
                ["Real Estate related services"] = MacroSector.InfrastructureAndConstruction,
                ["Cement & Cement Products"] = MacroSector.InfrastructureAndConstruction,
                ["Ceramics"] = MacroSector.InfrastructureAndConstruction,
                ["Sanitary Ware"] = MacroSector.InfrastructureAndConstruction,
                ["Other Construction Materials"] = MacroSector.InfrastructureAndConstruction,
                ["Plywood Boards/ Laminates"] = MacroSector.InfrastructureAndConstruction,
                ["Forest Products"] = MacroSector.InfrastructureAndConstruction,
                ["Paints"] = MacroSector.InfrastructureAndConstruction,
                ["Airport & Airport services"] = MacroSector.InfrastructureAndConstruction,
                ["Port & Port services"] = MacroSector.InfrastructureAndConstruction,
                ["Road AssetsToll, Annuity, Hybrid-Annuity"] = MacroSector.InfrastructureAndConstruction,
                ["Dredging"] = MacroSector.InfrastructureAndConstruction,
                ["Road Transport"] = MacroSector.InfrastructureAndConstruction,
                ["Shipping"] = MacroSector.InfrastructureAndConstruction,
                ["Logistics Solution Provider"] = MacroSector.InfrastructureAndConstruction,
                ["Transport Related Services"] = MacroSector.InfrastructureAndConstruction,
                ["Waste Management"] = MacroSector.InfrastructureAndConstruction,
                ["Water Supply & Management"] = MacroSector.InfrastructureAndConstruction,
                ["Jute & Jute Products"] = MacroSector.InfrastructureAndConstruction,
                ["Packaging"] = MacroSector.InfrastructureAndConstruction,

                // Energy & Utilities
                ["Oil Exploration & Production"] = MacroSector.EnergyAndUtilities,
                ["Refineries & Marketing"] = MacroSector.EnergyAndUtilities,
                ["Oil Storage & Transportation"] = MacroSector.EnergyAndUtilities,
                ["Oil Equipment & Services"] = MacroSector.EnergyAndUtilities,
                ["Offshore Support Solution Drilling"] = MacroSector.EnergyAndUtilities,
                ["Lubricants"] = MacroSector.EnergyAndUtilities,
                ["Power Generation"] = MacroSector.EnergyAndUtilities,
                ["Integrated Power Utilities"] = MacroSector.EnergyAndUtilities,
                ["Power Distribution"] = MacroSector.EnergyAndUtilities,
                ["Power - Transmission"] = MacroSector.EnergyAndUtilities,
                ["Power Trading"] = MacroSector.EnergyAndUtilities,
                ["LPG/CNG/PNG/LNG Supplier"] = MacroSector.EnergyAndUtilities,
                ["Gas Transmission/Marketing"] = MacroSector.EnergyAndUtilities,
                ["Trading - Gas"] = MacroSector.EnergyAndUtilities,
                ["Trading - Coal"] = MacroSector.EnergyAndUtilities,
                ["Trading - Metals"] = MacroSector.EnergyAndUtilities,
                ["Trading - Minerals"] = MacroSector.EnergyAndUtilities,
                ["Trading - Textile Products"] = MacroSector.EnergyAndUtilities,
                ["Trading & Distributors"] = MacroSector.EnergyAndUtilities,
                ["Distributors"] = MacroSector.EnergyAndUtilities,
                ["Diversified"] = MacroSector.EnergyAndUtilities,

                // Telecommunication
                ["Telecom - Cellular & Fixed line services"] = MacroSector.Telecommunication,
                ["Telecom - Infrastructure"] = MacroSector.Telecommunication,
                ["Telecom - Equipment & Accessories"] = MacroSector.Telecommunication,
                ["Other Telecom Services"] = MacroSector.Telecommunication
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public static MacroSector GetMacroSector(string? subSector)
        {
            if (string.IsNullOrWhiteSpace(subSector))
                return MacroSector.Other;

            return SectorMapping.TryGetValue(subSector.Trim(), out var macro)
                ? macro
                : MacroSector.Other;
        }
    }


    public class EvaluationContext
    {
        public Financial Current { get; set; }
        public List<Financial> Historicals { get; set; }
        public decimal ActualNetDebtCr { get; set; }
        public decimal DebtToEbit { get; set; }
        public decimal CapexToOcf { get; set; }
        public decimal OcfToNetProfit { get; set; }
        public decimal FcfCr { get; set; }

        // Advanced Capital & Earnings Quality Metrics
        public decimal RoicPercent { get; set; }
        public decimal CroicPercent { get; set; }
        public decimal FcfToNetProfit { get; set; }
        public decimal SloanRatio { get; set; }
        public decimal InterestCoverage { get; set; }
        public decimal CashConversionCycleDays { get; set; }
        public decimal MarginTrend { get; set; }

        public bool IsCashPredictable { get; set; }
        public bool IsCyclical { get; set; }
        public bool IsInfrastructureUtility { get; set; }

        public bool CanComputeCashFlowDcf => FcfCr > 0m && FinancialAlgorithms.CalculateEbit(Current) > 0m && SloanRatio <= 12m;
    }

    internal static class RulePriority
    {
        public const int CoreInvestmentCompany = 8;
        public const int FinancialSector = 10;
        public const int ReinvestingGrowth = 20;
        public const int DistressTurnaround = 30;
        public const int HighLeverageCapitalIntensive = 40;
        public const int CyclicalEarnings = 50;
        public const int MatureHighPayout = 60;
        public const int AssetLightMoat = 70;
        public const int PoorCashConversionOrAccrual = 85;
        public const int DefaultFallback = 999;
    }

    public interface IValuationRule
    {
        int Priority { get; }
        bool IsMatch(EvaluationContext ctx);
        ValuationMethodology Result(EvaluationContext ctx);
    }

   

    public class FinancialSectorRule : IValuationRule
    {
        public int Priority => RulePriority.FinancialSector;
        public bool IsMatch(EvaluationContext ctx) => ctx.Current.IsFinancialSector;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Excess Returns Model",
            SecondaryMethod = "Price-to-TBV (Tangible Book Value)",
            Rationale = "Financial institution: Operational inventory is capital; requiring equity residual income models."
        };
    }

    public class ReinvestingGrowthRule : IValuationRule
    {
        public int Priority => RulePriority.ReinvestingGrowth;
        public bool IsMatch(EvaluationContext ctx) => ctx.Current.NetProfitCr <= 0 && ctx.Current.CashFromOperationsCr > 0;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "EV/Sales Relative Multiple",
            SecondaryMethod = "Price-to-Sales (P/S)",
            Rationale = "Unprofitable on net income but cash-flow positive; valued on revenue scale and operating cash efficiency."
        };
    }

    public class DistressTurnaroundRule : IValuationRule
    {
        public int Priority => RulePriority.DistressTurnaround;
        public bool IsMatch(EvaluationContext ctx) =>
            ctx.Current.NetProfitCr <= 0 || (ctx.InterestCoverage < 1.8m && !ctx.Current.IsFinancialSector);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Severe earnings distress or interest coverage strain (< 1.8x); falling back to asset liquidation floor."
        };
    }

    public class CyclicalEarningsRule : IValuationRule
    {
        public int Priority => RulePriority.CyclicalEarnings;
        public bool IsMatch(EvaluationContext ctx) => ctx.IsCyclical;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Normalized Mid-Cycle P/E",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "High earnings volatility detected; using mid-cycle normalized metrics to avoid peak/trough valuation errors."
        };
    }

    public class HighLeverageCapitalIntensiveRule : IValuationRule
    {
        public int Priority => RulePriority.HighLeverageCapitalIntensive;
        public bool IsMatch(EvaluationContext ctx) => ctx.DebtToEbit >= 2.5m || ctx.CapexToOcf >= 0.60m || ctx.IsInfrastructureUtility;
        public ValuationMethodology Result(EvaluationContext ctx)
        {
            if (ctx.CanComputeCashFlowDcf && ctx.CroicPercent >= 8.0m)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "Exit Multiple DCF (FCFF)",
                    SecondaryMethod = "EV/EBITDA Relative Multiple",
                    Rationale = "Capital-intensive profile with adequate Cash Return on Invested Capital (CROIC >= 8%); using Enterprise FCFF DCF."
                };
            }

            if (FinancialAlgorithms.CalculateEbitda(ctx.Current)> 0m)
            {
                return new ValuationMethodology
                {
                    PrimaryMethod = "EV/EBITDA Relative Multiple",
                    SecondaryMethod = "Price-to-Book (P/B)",
                    Rationale = "Capital-intensive/high-leverage profile with low cash return efficiency; using EV/EBITDA multiple."
                };
            }

            return new ValuationMethodology
            {
                PrimaryMethod = "Price-to-Book (P/B)",
                SecondaryMethod = "Net Asset Value (NAV)",
                Rationale = "High leverage asset-heavy profile lacking positive cash flow; falling back to asset-based floor."
            };
        }
    }

    public class MatureHighPayoutRule : IValuationRule
    {
        public int Priority => RulePriority.MatureHighPayout;
        public bool IsMatch(EvaluationContext ctx) => ctx.Current.DividendPayoutPercent >= 40m && ctx.IsCashPredictable;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Dividend Discount Model (DDM)",
            SecondaryMethod = "Gordon Growth Model",
            Rationale = "Mature entity distributing over 40% of earnings with predictable free cash flow support."
        };
    }

    public class AssetLightMoatRule : IValuationRule
    {
        public int Priority => RulePriority.AssetLightMoat;
        public bool IsMatch(EvaluationContext ctx) =>
            ctx.IsCashPredictable && (ctx.RoicPercent >= 15.0m || ctx.CroicPercent >= 12.0m);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = (ctx.Current.ReportedRoePercent >= 20m && ctx.MarginTrend >= 0m)
                ? "Buffett Owner Earnings Model"
                : "2-Stage FCFE DCF",
            SecondaryMethod = "Price-to-Earnings-to-Growth (PEG)",
            Rationale = "High ROIC/CROIC moat confirmed with predictable FCF; suitable for equity-level discounted cash flow modeling."
        };
    }

    public class PoorCashConversionOrAccrualRule : IValuationRule
    {
        public int Priority => RulePriority.PoorCashConversionOrAccrual;
        public bool IsMatch(EvaluationContext ctx) =>
            !ctx.Current.IsFinancialSector
            && ctx.Current.NetProfitCr > 0m
            && (ctx.OcfToNetProfit < 0.30m || ctx.FcfToNetProfit < 0.20m || ctx.SloanRatio > 12.0m);

        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "High accrual risk (Sloan Ratio > 12%) or severe paper profits (FCF conversion < 20%). Overriding cash/earnings multiples with asset floor."
        };
    }

    public class CoreInvestmentCompanyRule : IValuationRule
    {
        public int Priority => RulePriority.CoreInvestmentCompany;
        public bool IsMatch(EvaluationContext ctx) => ctx.Current.IsFinancialSector && FinancialAlgorithms.CheckCoreInvestmentCompany(ctx.Current);
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Adjusted Net Asset Value (SOTP with HoldCo Discount)",
            SecondaryMethod = "Dividend Discount Model (Pass-Through Yield)",
            Rationale = "Core Investment / Holding Company detected: Applying standard 50% Holding Company discount to NAV."
        };
    }

    public class DefaultFallbackRule : IValuationRule
    {
        public int Priority => RulePriority.DefaultFallback;
        public bool IsMatch(EvaluationContext ctx) => true;
        public ValuationMethodology Result(EvaluationContext ctx) => new()
        {
            PrimaryMethod = "Price-to-Earnings (P/E) Multiple",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Standard financial operating profile; applying relative earnings multiples."
        };
    }

}