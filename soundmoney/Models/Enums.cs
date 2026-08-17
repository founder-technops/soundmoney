namespace SoundMoney.Models;

public enum SectorCategory
{
    Banking,            // 1. Retail, Commercial & Investment Banks
    FinancialServices,  // 2. NBFCs, Insurance, Broking, AMC, Exchanges, Fintech
    InformationTechnology, // 3. Software, ITES, Cloud, Hardware & Digital Services
    Telecom,            // 4. Telecom Carriers, Towers & Spectrum Infra
    Biotech,            // 5. Biotechnology & Gene/Cell Therapy
    LifeSciences,       // 6. Medical Devices, Diagnostics, CRO & Genomics
    Pharma,             // 7. Pharmaceuticals, API, Generic Drugs & Hospitals
    ConsumerDurables,   // 8. Electronics, Appliances, Footwear, Furnishings
    FMCG,               // 9. Packaged Goods, Foods, Retail, Textiles, Hotels & Services
    Automobile,         // 10. Auto OEMs, Auto Ancillaries, EVs, Tyres
    Metals,             // 11. Steel, Mining, Aluminum, Copper & Precious Metals
    CapitalGoods,       // 12. Heavy Engineering, Defense, Machinery & Industrial Products
    RealEstate,         // 13. Developers, Commercial Projects, REITs, Wood Products
    Utilities,          // 14. Gas Distribution, Power Transmission, Water & Waste Ops
    Energy,             // 15. Oil & Gas Refining, Exploration, Thermal/Solar Power
    Infrastructure,     // 16. EPC, Road/Port Infra, Cement, Chemicals & Logistics
    Other               // Unmapped or fallback catch-all
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

        var s = label.ToUpperInvariant().Trim();

        // -----------------------------------------------------------------
        // 1. BANKING & FINANCIAL SERVICES
        // -----------------------------------------------------------------
        if (s.Contains("BANK") && !s.Contains("INVESTMENT BANK"))
            return SectorCategory.Banking;

        if (s.Contains("FINANCE") || s.Contains("FINANCIAL") || s.Contains("NBFC") ||
            s.Contains("INSURANCE") || s.Contains("CAPITAL MARKET") || s.Contains("BROKING") ||
            s.Contains("STOCKBROKING") || s.Contains("RATING") || s.Contains("ASSET MANAGEMENT") ||
            s.Contains("DEPOSITOR") || s.Contains("CLEARING") || s.Contains("EXCHANGE") ||
            s.Contains("FINTECH") || s.Contains("HOLDING COMPANY") || s.Contains("INVESTMENT COMPANY") ||
            s.Contains("MICROFINANCE") || s.Contains("FINANCIAL PRODUCTS") || s.Contains("MUTUAL FUND"))
        {
            return SectorCategory.FinancialServices;
        }

        // -----------------------------------------------------------------
        // 2. TECHNOLOGY & TELECOM
        // -----------------------------------------------------------------
        if (s.Contains("TELECOM") || s.Contains("COMMUNICATION") || s.Contains("TOWER") ||
            s.Contains("SPECTRUM") || s.Contains("CELLULAR") || s.Contains("FIXED LINE"))
        {
            return SectorCategory.Telecom;
        }

        if (s.Contains("IT ") || s.Contains("SOFTWARE") || s.Contains("INFORMATION TECHNOLOGY") ||
            s.Contains("TECH") || s.Contains("COMPUTERS") || s.Contains("DATA PROCESSING") ||
            s.Contains("BPO") || s.Contains("KPO") || s.Contains("IT ENABLED") ||
            s == "IT" || s.Contains("E-LEARNING") || s.Contains("E-RETAIL") || s.Contains("INTERNET"))
        {
            return SectorCategory.InformationTechnology;
        }

        // -----------------------------------------------------------------
        // 3. HEALTHCARE, PHARMA, BIOTECH & LIFE SCIENCES
        // -----------------------------------------------------------------
        if (s.Contains("BIOTECH") || s.Contains("BIOTECHNOLOGY"))
            return SectorCategory.Biotech;

        if (s.Contains("LIFE SCIENCE") || s.Contains("DIAGNOSTIC") || s.Contains("MEDICAL EQUIPMENT") ||
            s.Contains("MEDICAL DEVICE") || s.Contains("HEALTHCARE RESEARCH") || s.Contains("ANALYTICS"))
        {
            return SectorCategory.LifeSciences;
        }

        if (s.Contains("PHARMA") || s.Contains("HEALTHCARE") || s.Contains("HOSPITAL") ||
            s.Contains("DRUG") || s.Contains("WELLNESS"))
        {
            return SectorCategory.Pharma;
        }

        // -----------------------------------------------------------------
        // 4. CONSUMER DURABLES & RETAIL
        // -----------------------------------------------------------------
        if (s.Contains("DURABLE") || s.Contains("ELECTRONICS") || s.Contains("APPLIANCE") ||
            s.Contains("FOOTWEAR") || s.Contains("HOUSEWARE") || s.Contains("GLASS - CONSUMER") ||
            s.Contains("SANITARY") || s.Contains("FURNITURE") || s.Contains("LEISURE") ||
            s.Contains("GEMS") || s.Contains("JEWELLERY") || s.Contains("WATCHES"))
        {
            return SectorCategory.ConsumerDurables;
        }

        if (s.Contains("FMCG") || s.Contains("CONSUMER") || s.Contains("FOOD") ||
            s.Contains("BEVERAGE") || s.Contains("RETAIL") || s.Contains("PERSONAL CARE") ||
            s.Contains("CIGARETTE") || s.Contains("TOBACCO") || s.Contains("DAIRY") ||
            s.Contains("EDIBLE OIL") || s.Contains("TEA") || s.Contains("COFFEE") ||
            s.Contains("BREWERIES") || s.Contains("DISTILLERIES") || s.Contains("MEAT") ||
            s.Contains("POULTRY") || s.Contains("SEAFOOD") || s.Contains("GARMENT") ||
            s.Contains("APPAREL") || s.Contains("TEXTILE") || s.Contains("JUTE") ||
            s.Contains("LEATHER") || s.Contains("SUGAR") || s.Contains("ANIMAL FEED") ||
            s.Contains("STATIONARY") || s.Contains("HOTEL") || s.Contains("RESORT") ||
            s.Contains("RESTAURANT") || s.Contains("AMUSEMENT") || s.Contains("ADVERTISING") ||
            s.Contains("MEDIA") || s.Contains("PRINTING") || s.Contains("PUBLICATION") ||
            s.Contains("BROADCASTING") || s.Contains("FILM") || s.Contains("CINEMA") ||
            s.Contains("DEALER") || s.Contains("TRADING") || s.Contains("DISTRIBUTOR") ||
            s.Contains("TOUR") || s.Contains("TRAVEL") || s.Contains("EDUCATION"))
        {
            return SectorCategory.FMCG;
        }

        // -----------------------------------------------------------------
        // 5. AUTOMOBILE, METALS & CAPITAL GOODS
        // -----------------------------------------------------------------
        if (s.Contains("AUTO") || s.Contains("VEHICLE") || s.Contains("TYRE") ||
            s.Contains("AUTOMOTIVE") || s.Contains("2/3 WHEELER") || s.Contains("TRACTOR") ||
            s.Contains("CYCLES"))
        {
            return SectorCategory.Automobile;
        }

        if (s.Contains("METAL") || s.Contains("STEEL") || s.Contains("MINING") ||
            s.Contains("ALUMINIUM") || s.Contains("COPPER") || s.Contains("ZINC") ||
            s.Contains("IRON") || s.Contains("FERRO") || s.Contains("MANGANESE") ||
            s.Contains("PRECIOUS METALS") || s.Contains("SPONGE IRON") || s.Contains("PIG IRON") ||
            s.Contains("MINERALS") || s.Contains("COAL"))
        {
            return SectorCategory.Metals;
        }

        if (s.Contains("CAPITAL GOODS") || s.Contains("MACHINERY") || s.Contains("ENGINEERING") ||
            s.Contains("EQUIPMENT") || s.Contains("CASTINGS") || s.Contains("FORGINGS") ||
            s.Contains("BEARINGS") || s.Contains("ABRASIVES") || s.Contains("HEAVY ELECTRICAL") ||
            s.Contains("COMPRESSOR") || s.Contains("PUMP") || s.Contains("DIESEL") ||
            s.Contains("RAILWAY WAGONS") || s.Contains("SHIP BUILDING") || s.Contains("AEROSPACE") ||
            s.Contains("DEFENSE") || s.Contains("DEFENCE") || s.Contains("ELECTRODES") ||
            s.Contains("REFRACTORIES") || s.Contains("INDUSTRIAL PRODUCTS") || s.Contains("CABLES"))
        {
            return SectorCategory.CapitalGoods;
        }

        // -----------------------------------------------------------------
        // 6. ENERGY, REAL ESTATE, INFRASTRUCTURE & UTILITIES
        // -----------------------------------------------------------------
        if (s.Contains("REAL ESTATE") || s.Contains("REALTY") || s.Contains("HOUSING DEVELOPER") ||
            s.Contains("REIT") || s.Contains("RESIDENTIAL") || s.Contains("COMMERCIAL PROJECTS") ||
            s.Contains("PLYWOOD") || s.Contains("LAMINATES") || s.Contains("FOREST"))
        {
            return SectorCategory.RealEstate;
        }

        if (s.Contains("UTILITY") || s.Contains("GAS DISTRIBUTION") || s.Contains("WATER MANAGEMENT") ||
            s.Contains("WASTE MANAGEMENT") || s.Contains("POWER DISTRIBUTION") || s.Contains("POWER TRADING"))
        {
            return SectorCategory.Utilities;
        }

        if (s.Contains("OIL") || s.Contains("GAS") || s.Contains("POWER") ||
            s.Contains("ENERGY") || s.Contains("PETROCHEMICAL") || s.Contains("REFINERIES") ||
            s.Contains("LPG") || s.Contains("CNG") || s.Contains("PNG") || s.Contains("LNG") ||
            s.Contains("LUBRICANTS"))
        {
            return SectorCategory.Energy;
        }

        if (s.Contains("INFRA") || s.Contains("CONSTRUCTION") || s.Contains("CEMENT") ||
            s.Contains("ROAD") || s.Contains("PORT") || s.Contains("EPC") ||
            s.Contains("AIRPORT") || s.Contains("LOGISTICS") || s.Contains("SHIPPING") ||
            s.Contains("DREDGING") || s.Contains("PLASTIC") || s.Contains("CHEMICAL") ||
            s.Contains("PESTICIDE") || s.Contains("AGROCHEMICAL") || s.Contains("FERTILIZER") ||
            s.Contains("CARBON BLACK") || s.Contains("DYES") || s.Contains("PIGMENTS") ||
            s.Contains("EXPLOSIVES") || s.Contains("RUBBER") || s.Contains("PACKAGING") ||
            s.Contains("CERAMICS") || s.Contains("GRANITES") || s.Contains("MARBLES") ||
            s.Contains("SANITARY WARE") || s.Contains("TRANSPORT") || s.Contains("AIRLINE"))
        {
            return SectorCategory.Infrastructure;
        }

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

    public static ValuationMethodology ResolveBiotechMethod(decimal annualRevenue, decimal netProfit)
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

