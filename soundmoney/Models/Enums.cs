namespace SoundMoney.Models;

public enum SectorCategory
{
    Banking,
    FinancialServices,
    InformationTechnology,
    FMCG,
    ConsumerDurables,
    Pharma,
    Biotech,
    LifeSciences,
    Automobile,
    Metals,
    CapitalGoods,
    Energy,
    RealEstate,
    Infrastructure,
    Utilities,
    Telecom,
    Other
}

/// <summary>
/// Maps a free-text industry/sector label (from NSE's "industry" field or
/// your fundamentals CSV) onto the coarse SectorCategory buckets that
/// drive which valuation strategy gets used. Keyword-matching is
/// deliberately simple/overridable rather than exhaustive -- extend the
/// checks below as you add more sectors to your watchlist.
/// </summary>
public static class SectorMapper
{
    public static SectorCategory Map(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return SectorCategory.Other;

        var s = label.ToUpperInvariant();

        // 1. Banking & Financial Services
        if (s.Contains("BANK"))
            return SectorCategory.Banking;

        if (s.Contains("FINANCE") || s.Contains("FINANCIAL") || s.Contains("NBFC") ||
            s.Contains("INSURANCE") || s.Contains("CAPITAL MARKET") || s.Contains("BROKING"))
            return SectorCategory.FinancialServices;

        // 2. Technology & Telecom
        if (s.Contains("TELECOM") || s.Contains("COMMUNICATION") || s.Contains("TOWER") || s.Contains("SPECTRUM"))
            return SectorCategory.Telecom;

        if (s.Contains("IT ") || s.Contains("SOFTWARE") || s.Contains("INFORMATION TECHNOLOGY") ||
            s.Contains("TECH") || s == "IT")
            return SectorCategory.InformationTechnology;

        // 3. Healthcare & Life Sciences (Specific checks first)
        if (s.Contains("BIOTECH") || s.Contains("BIOTECHNOLOGY"))
            return SectorCategory.Biotech;

        if (s.Contains("LIFE SCIENCE") || s.Contains("DIAGNOSTIC") || s.Contains("MEDICAL DEVICE"))
            return SectorCategory.LifeSciences;

        if (s.Contains("PHARMA") || s.Contains("HEALTHCARE") || s.Contains("HOSPITAL") || s.Contains("DRUG"))
            return SectorCategory.Pharma;

        // 4. Consumer Goods & Retail
        if (s.Contains("DURABLE") || s.Contains("ELECTRONICS") || s.Contains("APPLIANCE") || s.Contains("FOOTWEAR"))
            return SectorCategory.ConsumerDurables;

        if (s.Contains("FMCG") || s.Contains("CONSUMER") || s.Contains("FOOD") || s.Contains("BEVERAGE") || s.Contains("RETAIL"))
            return SectorCategory.FMCG;

        // 5. Automobiles, Metals & Industrial
        if (s.Contains("AUTO") || s.Contains("VEHICLE") || s.Contains("TYRE") || s.Contains("AUTOMOTIVE"))
            return SectorCategory.Automobile;

        if (s.Contains("METAL") || s.Contains("STEEL") || s.Contains("MINING") || s.Contains("ALUMINIUM") || s.Contains("COPPER"))
            return SectorCategory.Metals;

        if (s.Contains("CAPITAL GOODS") || s.Contains("MACHINERY") || s.Contains("ENGINEERING") || s.Contains("EQUIPMENT"))
            return SectorCategory.CapitalGoods;

        // 6. Energy, Real Estate & Infrastructure
        if (s.Contains("REAL ESTATE") || s.Contains("REALTY") || s.Contains("HOUSING DEVELOPER") || s.Contains("REIT"))
            return SectorCategory.RealEstate;

        if (s.Contains("UTILITY") || s.Contains("GAS DISTRIBUTION") || s.Contains("WATER MANAGEMENT"))
            return SectorCategory.Utilities;

        if (s.Contains("OIL") || s.Contains("GAS") || s.Contains("POWER") || s.Contains("ENERGY") || s.Contains("PETROCHEMICAL"))
            return SectorCategory.Energy;

        if (s.Contains("INFRA") || s.Contains("CONSTRUCTION") || s.Contains("CEMENT") || s.Contains("ROAD") || s.Contains("PORT") || s.Contains("EPC"))
            return SectorCategory.Infrastructure;

        return SectorCategory.Other;
    }
}

public class ValuationMethodology
{
    public string PrimaryMethod { get; set; }
    public string SecondaryMethod { get; set; }
    public string Rationale { get; set; }
}

public static class IntrinsicMapper
{
    private static readonly Dictionary<SectorCategory, ValuationMethodology> Mappings
        = new Dictionary<SectorCategory, ValuationMethodology>
        {
            [SectorCategory.Banking] = new ValuationMethodology
            {
                PrimaryMethod = "Excess Returns Model",
                SecondaryMethod = "Dividend Discount Model (DDM)",
                Rationale = "Debt is raw material rather than leverage. Traditional DCF fails due to undefined cash flows."
            },
            [SectorCategory.FinancialServices] = new ValuationMethodology
            {
                PrimaryMethod = "Dividend Discount Model (DDM)",
                SecondaryMethod = "Price-to-Book (P/B) Intrinsic Multiples",
                Rationale = "Earnings depend heavily on interest spreads and regulatory capital requirements."
            },
            [SectorCategory.InformationTechnology] = new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "Capitalized Free Cash Flow Yield",
                Rationale = "High margins, light capital expenditure, and predictable recurring free cash flows."
            },
            [SectorCategory.FMCG] = new ValuationMethodology
            {
                PrimaryMethod = "Buffett Owner Earnings Model",
                SecondaryMethod = "Gordon Growth DDM",
                Rationale = "Strong brand moats provide high return on capital and steady cash generation."
            },
            [SectorCategory.ConsumerDurables] = new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Moderate capital expenditure with consumer-driven demand growth cycles."
            },
            [SectorCategory.Pharma] = new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "Risk-Adjusted DCF (rDCF)",
                Rationale = "Established generic/formulation cash flows combined with ongoing R&D reinvestment."
            },
            [SectorCategory.Biotech] = new ValuationMethodology
            {
                PrimaryMethod = "Risk-Adjusted DCF (rDCF)",
                SecondaryMethod = "Real Options Valuation",
                Rationale = "Pipeline cash flows must be probability-weighted based on clinical trial success rates."
            },
            [SectorCategory.LifeSciences] = new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Combines equipment/diagnostic manufacturing revenues with recurring service contracts."
            },
            [SectorCategory.Automobile] = new ValuationMethodology
            {
                PrimaryMethod = "Normalized DCF (Cycle-Adjusted)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Cyclical demand requires earnings to be smoothed across a full economic cycle."
            },
            [SectorCategory.Metals] = new ValuationMethodology
            {
                PrimaryMethod = "Normalized DCF (Cycle-Adjusted)",
                SecondaryMethod = "Asset Replacement Value",
                Rationale = "Capital-intensive commodity business heavily reliant on global market cycles."
            },
            [SectorCategory.CapitalGoods] = new ValuationMethodology
            {
                PrimaryMethod = "Normalized DCF (Cycle-Adjusted)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Long order-execution timelines require cycle-adjusted operating cash flow estimates."
            },
            [SectorCategory.Energy] = new ValuationMethodology
            {
                PrimaryMethod = "Sum of the Parts (SOTP)",
                SecondaryMethod = "Net Asset Value (NAV)",
                Rationale = "Conglomerate structures with distinct upstream, downstream, and retail operations."
            },
            [SectorCategory.RealEstate] = new ValuationMethodology
            {
                PrimaryMethod = "Net Asset Value (NAV)",
                SecondaryMethod = "Discounted Cash Flow (DCF)",
                Rationale = "Value directly correlates to land bank valuation and market value of real properties minus debt."
            },
            [SectorCategory.Infrastructure] = new ValuationMethodology
            {
                PrimaryMethod = "Net Asset Value (NAV)",
                SecondaryMethod = "Funds From Operations (FFO) DDM",
                Rationale = "Long-term concession assets with predictable cash yields and finite asset lifespans."
            },
            [SectorCategory.Utilities] = new ValuationMethodology
            {
                PrimaryMethod = "Dividend Discount Model (DDM)",
                SecondaryMethod = "Regulatory Asset Base (RAB)",
                Rationale = "Regulated return-on-equity rates yield highly predictable dividend distributions."
            },
            [SectorCategory.Telecom] = new ValuationMethodology
            {
                PrimaryMethod = "Sum of the Parts (SOTP)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Combines infrastructure tower assets, spectrum licenses, and consumer digital services."
            },
            [SectorCategory.Other] = new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Standard baseline valuation model for diversified or unclassified businesses."
            }
        };

    /// <summary>
    /// Retrieves the recommended valuation methodology details for a given sector.
    /// </summary>
    public static ValuationMethodology GetValuationMethodology(SectorCategory sector)
    {
        return Mappings.TryGetValue(sector, out var valuationInfo)
            ? valuationInfo
            : Mappings[SectorCategory.Other];
    }

    /// <summary>
    /// Gets the actionable valuation method based on whether segment-level data is available.
    /// </summary>
    public static string ResolveMethodology(SectorCategory sector, bool hasSegmentData = false)
    {
        var methodology = GetValuationMethodology(sector);

        // If it's a conglomerate/energy sector needing SOTP but we lack segment data, drop to secondary
        if ((sector == SectorCategory.Energy || sector == SectorCategory.Telecom) && !hasSegmentData)
        {
            return methodology.SecondaryMethod; // Returns NAV / Consolidated Multiples
        }

        return methodology.PrimaryMethod;
    }

    public static ValuationMethodology ResolveBiotechMethod(double annualRevenue, double netProfit)
    {
        // 1. Established Indian Biotech (Commercial production / CDMO / Enzymes)
        if (annualRevenue > 0 && netProfit > 0)
        {
            return new ValuationMethodology
            {
                PrimaryMethod = "2-Stage Discounted Cash Flow (DCF)",
                SecondaryMethod = "EV/EBITDA Multiples",
                Rationale = "Company has active commercial revenues and profits. Screener metrics support standard cash flow discounting."
            };
        }

        // 2. Pre-Revenue / Early Clinical Biotech (Fallback to Net Asset Value)
        return new ValuationMethodology
        {
            PrimaryMethod = "Net Asset Value (NAV)",
            SecondaryMethod = "Price-to-Book (P/B)",
            Rationale = "Early stage biotech with zero/negative operational profits. Valued on cash balance and net book assets."
        };
    }

    /// <summary>
    /// Retrieves just the primary valuation method name for direct use.
    /// </summary>
    public static string GetPrimaryMethod(SectorCategory sector)
    {
        return GetValuationMethodology(sector).PrimaryMethod;
    }
}

