using System;
using System.Collections.Generic;
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
}