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

public static class SectorMapper
{
    public static SectorCategory Map(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return SectorCategory.Other;

        ReadOnlySpan<char> s = label.Trim();

        // 1. BANKING & FINANCIAL SERVICES
        if (Contains(s, "BANK") && !Contains(s, "INVESTMENT BANK"))
            return SectorCategory.Banking;

        if (Contains(s, "FINANCE") || Contains(s, "FINANCIAL") || Contains(s, "NBFC") ||
            Contains(s, "INSURANCE") || Contains(s, "CAPITAL MARKET") || Contains(s, "BROKING") ||
            Contains(s, "STOCKBROKING") || Contains(s, "RATING") || Contains(s, "ASSET MANAGEMENT") ||
            Contains(s, "DEPOSITOR") || Contains(s, "CLEARING") || Contains(s, "EXCHANGE") ||
            Contains(s, "FINTECH") || Contains(s, "HOLDING COMPANY") || Contains(s, "INVESTMENT COMPANY") ||
            Contains(s, "INVESTMENT BANK") || Contains(s, "MICROFINANCE") || Contains(s, "FINANCIAL PRODUCTS") ||
            Contains(s, "MUTUAL FUND"))
        {
            return SectorCategory.FinancialServices;
        }

        // 2. HEALTHCARE, PHARMA, BIOTECH & LIFE SCIENCES (Evaluated early to prevent 'TECH' collision)
        if (Contains(s, "BIOTECH") || Contains(s, "BIOTECHNOLOGY"))
            return SectorCategory.Biotech;

        if (Contains(s, "LIFE SCIENCE") || Contains(s, "DIAGNOSTIC") || Contains(s, "MEDICAL EQUIPMENT") ||
            Contains(s, "MEDICAL DEVICE") || Contains(s, "HEALTHCARE RESEARCH") || Contains(s, "ANALYTICS"))
        {
            return SectorCategory.LifeSciences;
        }

        if (Contains(s, "PHARMA") || Contains(s, "HEALTHCARE") || Contains(s, "HOSPITAL") ||
            Contains(s, "DRUG") || Contains(s, "WELLNESS"))
        {
            return SectorCategory.Pharma;
        }

        // 3. TECHNOLOGY & TELECOM
        if (Contains(s, "TELECOM") || Contains(s, "COMMUNICATION") || Contains(s, "TOWER") ||
            Contains(s, "SPECTRUM") || Contains(s, "CELLULAR") || Contains(s, "FIXED LINE"))
        {
            return SectorCategory.Telecom;
        }

        if (Contains(s, "IT ") || Contains(s, "SOFTWARE") || Contains(s, "INFORMATION TECHNOLOGY") ||
            Contains(s, "TECH") || Contains(s, "COMPUTERS") || Contains(s, "DATA PROCESSING") ||
            Contains(s, "BPO") || Contains(s, "KPO") || Contains(s, "IT ENABLED") ||
            label.Equals("IT", StringComparison.OrdinalIgnoreCase) || Contains(s, "E-LEARNING") ||
            Contains(s, "E-RETAIL") || Contains(s, "INTERNET"))
        {
            return SectorCategory.InformationTechnology;
        }

        // 4. UTILITIES & ENERGY (Evaluated before Infrastructure/Durables)
        if (Contains(s, "UTILITY") || Contains(s, "GAS DISTRIBUTION") || Contains(s, "WATER MANAGEMENT") ||
            Contains(s, "WASTE MANAGEMENT") || Contains(s, "POWER DISTRIBUTION") || Contains(s, "POWER TRADING"))
        {
            return SectorCategory.Utilities;
        }

        if (Contains(s, "OIL") || Contains(s, "GAS") || Contains(s, "POWER") ||
            Contains(s, "ENERGY") || Contains(s, "PETROCHEMICAL") || Contains(s, "REFINERIES") ||
            Contains(s, "LPG") || Contains(s, "CNG") || Contains(s, "PNG") || Contains(s, "LNG") ||
            Contains(s, "LUBRICANTS"))
        {
            return SectorCategory.Energy;
        }

        // 5. INFRASTRUCTURE & CAPITAL GOODS
        if (Contains(s, "INFRA") || Contains(s, "CONSTRUCTION") || Contains(s, "CEMENT") ||
            Contains(s, " ROAD ") || Contains(s, "ROADS") || Contains(s, "PORT") || Contains(s, "EPC") ||
            Contains(s, "AIRPORT") || Contains(s, "LOGISTICS") || Contains(s, "SHIPPING") ||
            Contains(s, "DREDGING") || Contains(s, "PLASTIC") || Contains(s, "CHEMICAL") ||
            Contains(s, "PESTICIDE") || Contains(s, "AGROCHEMICAL") || Contains(s, "FERTILIZER") ||
            Contains(s, "CARBON BLACK") || Contains(s, "DYES") || Contains(s, "PIGMENTS") ||
            Contains(s, "EXPLOSIVES") || Contains(s, "RUBBER") || Contains(s, "PACKAGING") ||
            Contains(s, "CERAMICS") || Contains(s, "GRANITES") || Contains(s, "MARBLES") ||
            Contains(s, "SANITARY WARE") || Contains(s, "TRANSPORT") || Contains(s, "AIRLINE"))
        {
            return SectorCategory.Infrastructure;
        }

        // 6. CONSUMER DURABLES & RETAIL
        if (Contains(s, "DURABLE") || Contains(s, "ELECTRONICS") || Contains(s, "APPLIANCE") ||
            Contains(s, "FOOTWEAR") || Contains(s, "HOUSEWARE") || Contains(s, "GLASS - CONSUMER") ||
            Contains(s, "FURNITURE") || Contains(s, "LEISURE") || Contains(s, "GEMS") ||
            Contains(s, "JEWELLERY") || Contains(s, "WATCHES"))
        {
            return SectorCategory.ConsumerDurables;
        }

        if (Contains(s, "FMCG") || Contains(s, "CONSUMER") || Contains(s, "FOOD") ||
            Contains(s, "BEVERAGE") || Contains(s, "RETAIL") || Contains(s, "PERSONAL CARE") ||
            Contains(s, "CIGARETTE") || Contains(s, "TOBACCO") || Contains(s, "DAIRY") ||
            Contains(s, "EDIBLE OIL") || Contains(s, " TEA ") || Contains(s, "COFFEE") ||
            Contains(s, "BREWERIES") || Contains(s, "DISTILLERIES") || Contains(s, "MEAT") ||
            Contains(s, "POULTRY") || Contains(s, "SEAFOOD") || Contains(s, "GARMENT") ||
            Contains(s, "APPAREL") || Contains(s, "TEXTILE") || Contains(s, "JUTE") ||
            Contains(s, "LEATHER") || Contains(s, "SUGAR") || Contains(s, "ANIMAL FEED") ||
            Contains(s, "STATIONARY") || Contains(s, "HOTEL") || Contains(s, "RESORT") ||
            Contains(s, "RESTAURANT") || Contains(s, "AMUSEMENT") || Contains(s, "ADVERTISING") ||
            Contains(s, "MEDIA") || Contains(s, "PRINTING") || Contains(s, "PUBLICATION") ||
            Contains(s, "BROADCASTING") || Contains(s, "FILM") || Contains(s, "CINEMA") ||
            Contains(s, "DEALER") || Contains(s, "TRADING") || Contains(s, "DISTRIBUTOR") ||
            Contains(s, "TOUR") || Contains(s, "TRAVEL") || Contains(s, "EDUCATION"))
        {
            return SectorCategory.FMCG;
        }

        // 7. AUTOMOBILE, METALS & CAPITAL GOODS
        if (Contains(s, "AUTO") || Contains(s, "VEHICLE") || Contains(s, "TYRE") ||
            Contains(s, "AUTOMOTIVE") || Contains(s, "2/3 WHEELER") || Contains(s, "TRACTOR") ||
            Contains(s, "CYCLES"))
        {
            return SectorCategory.Automobile;
        }

        if (Contains(s, "METAL") || Contains(s, "STEEL") || Contains(s, "MINING") ||
            Contains(s, "ALUMINIUM") || Contains(s, "COPPER") || Contains(s, "ZINC") ||
            Contains(s, "IRON") || Contains(s, "FERRO") || Contains(s, "MANGANESE") ||
            Contains(s, "PRECIOUS METALS") || Contains(s, "SPONGE IRON") || Contains(s, "PIG IRON") ||
            Contains(s, "MINERALS") || Contains(s, "COAL"))
        {
            return SectorCategory.Metals;
        }

        if (Contains(s, "CAPITAL GOODS") || Contains(s, "MACHINERY") || Contains(s, "ENGINEERING") ||
            Contains(s, "EQUIPMENT") || Contains(s, "CASTINGS") || Contains(s, "FORGINGS") ||
            Contains(s, "BEARINGS") || Contains(s, "ABRASIVES") || Contains(s, "HEAVY ELECTRICAL") ||
            Contains(s, "COMPRESSOR") || Contains(s, "PUMP") || Contains(s, "DIESEL") ||
            Contains(s, "RAILWAY WAGONS") || Contains(s, "SHIP BUILDING") || Contains(s, "AEROSPACE") ||
            Contains(s, "DEFENSE") || Contains(s, "DEFENCE") || Contains(s, "ELECTRODES") ||
            Contains(s, "REFRACTORIES") || Contains(s, "INDUSTRIAL PRODUCTS") || Contains(s, "CABLES"))
        {
            return SectorCategory.CapitalGoods;
        }

        // 8. REAL ESTATE
        if (Contains(s, "REAL ESTATE") || Contains(s, "REALTY") || Contains(s, "HOUSING DEVELOPER") ||
            Contains(s, "REIT") || Contains(s, "RESIDENTIAL") || Contains(s, "COMMERCIAL PROJECTS") ||
            Contains(s, "PLYWOOD") || Contains(s, "LAMINATES") || Contains(s, "FOREST"))
        {
            return SectorCategory.RealEstate;
        }

        return SectorCategory.Other;
    }

    private static bool Contains(ReadOnlySpan<char> source, string value) =>
        source.Contains(value.AsSpan(), StringComparison.OrdinalIgnoreCase);
}

public record ValuationMethodology
{
    public required string PrimaryMethod { get; init; }
    public required string SecondaryMethod { get; init; }
    public required string Rationale { get; init; }
}