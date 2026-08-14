namespace SoundMoney.Models;

public enum SectorCategory
{
    Banking,
    FinancialServices,
    InformationTechnology,
    FMCG,
    Pharma,
    Automobile,
    Metals,
    Energy,
    Infrastructure,
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
        if (string.IsNullOrWhiteSpace(label)) return SectorCategory.Other;
        var s = label.ToUpperInvariant();

        if (s.Contains("BANK")) return SectorCategory.Banking;
        if (s.Contains("FINANCE") || s.Contains("FINANCIAL") || s.Contains("NBFC") || s.Contains("INSURANCE"))
            return SectorCategory.FinancialServices;
        if (s.Contains("IT ") || s.Contains("SOFTWARE") || s.Contains("INFORMATION TECHNOLOGY") || s == "IT")
            return SectorCategory.InformationTechnology;
        if (s.Contains("FMCG") || s.Contains("CONSUMER") || s.Contains("FOOD"))
            return SectorCategory.FMCG;
        if (s.Contains("PHARMA") || s.Contains("HEALTHCARE") || s.Contains("HOSPITAL"))
            return SectorCategory.Pharma;
        if (s.Contains("AUTO")) return SectorCategory.Automobile;
        if (s.Contains("METAL") || s.Contains("STEEL") || s.Contains("MINING"))
            return SectorCategory.Metals;
        if (s.Contains("OIL") || s.Contains("GAS") || s.Contains("POWER") || s.Contains("ENERGY"))
            return SectorCategory.Energy;
        if (s.Contains("CONSTRUCTION") || s.Contains("INFRA") || s.Contains("CEMENT") || s.Contains("CAPITAL GOODS"))
            return SectorCategory.Infrastructure;

        return SectorCategory.Other;
    }
}
