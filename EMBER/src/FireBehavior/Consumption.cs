// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
//
// Fuel consumption by class as a function of the class's own moisture, in the spirit of
// FOFEM / Consume (Brown et al. 1985, 1991; Reinhardt et al. 1997). Each fraction is a
// clamped linear function C = c0 - c1 * moisture(%). Defaults are starting values and are
// exposed as parameters.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public class ConsumptionParameters
    {
        public double Fine1h = 1.0;                 // fraction consumed when burnable
        public double Fine10h = 0.9;
        public double C100_Intercept = 1.2, C100_Slope = 0.020;   // C = 1.2 - 0.02 * m100 (%)  -> 1.0 at 10 %, 0.6 at 30 %
        public double C100_Max = 1.0, C100_Min = 0.05;
        public double C1000_Intercept = 1.1, C1000_Slope = 0.020;  // 0.8 at 15 %, 0.4 at 35 %
        public double C1000_Max = 0.8, C1000_Min = 0.02;
        public double Duff_Intercept = 0.9, Duff_Slope = 0.0075;  // 0.68 at 30 %, 0.15 at 100 %
        public double Duff_Max = 0.9, Duff_Min = 0.0;
        public double LiveHerb = 0.9;               // of cured + green herb when burnable
        public double LiveWoody = 0.5;              // shrub foliage/twigs
        public double FineBranchFraction = 0.30;    // share of 100-h consumption counted as flaming
        public double KilledFoliageConsumed = 0.9;  // scorched foliage of killed trees that is consumed (passive crowning)
    }

    public struct ConsumptionResult
    {
        public double Surface1h, Surface10h, Surface100h, Surface1000h, Duff, LiveHerb, LiveWoody, Canopy; // kg m^-2
        public double Flaming { get { return Surface1h + Surface10h + Surface100h * 0.5 + LiveHerb + LiveWoody + Canopy; } }
        public double Smoldering { get { return Surface100h * 0.5 + Surface1000h + Duff; } }
        public double Total { get { return Flaming + Smoldering; } }
        public double SurfaceTotal { get { return Surface1h + Surface10h + Surface100h + Surface1000h + LiveHerb + LiveWoody; } }
    }

    public class EmissionFactors
    {
        // g per kg fuel consumed (Urbanski 2014, temperate forest, flaming / smoldering)
        public double PM25_Flaming = 12.0, PM25_Smoldering = 26.0;
        public double CO2_Flaming = 1660.0, CO2_Smoldering = 1420.0;
        public double CO_Flaming = 60.0, CO_Smoldering = 170.0;
        public double CH4_Flaming = 2.0, CH4_Smoldering = 8.0;
    }

    public static class Consumption
    {
        public static ConsumptionResult Compute(ConsumptionParameters p, SurfaceFuel fuel, double w1000h, double duff,
                                                FuelMoistureState m, double canopyFuelConsumed, bool burnable)
        {
            ConsumptionResult r = new ConsumptionResult();
            if (!burnable) return r;
            r.Surface1h = fuel.W1h * p.Fine1h;
            r.Surface10h = fuel.W10h * p.Fine10h;
            double c100 = Units.Clamp(p.C100_Intercept - p.C100_Slope * m.M100h * 100.0, p.C100_Min, p.C100_Max);
            r.Surface100h = fuel.W100h * c100;
            double c1000 = Units.Clamp(p.C1000_Intercept - p.C1000_Slope * m.M1000h * 100.0, p.C1000_Min, p.C1000_Max);
            r.Surface1000h = w1000h * c1000;
            double cduff = Units.Clamp(p.Duff_Intercept - p.Duff_Slope * m.MDuff * 100.0, p.Duff_Min, p.Duff_Max);
            r.Duff = duff * cduff;
            r.LiveHerb = fuel.WHerb * p.LiveHerb;
            r.LiveWoody = fuel.WWoody * p.LiveWoody;
            r.Canopy = canopyFuelConsumed;
            return r;
        }

        /// <summary>Emissions in g m^-2 of a species given consumption in kg m^-2.</summary>
        public static double Emission(double flaming_kgm2, double smoldering_kgm2, double efFlaming, double efSmoldering)
        {
            return flaming_kgm2 * efFlaming + smoldering_kgm2 * efSmoldering;
        }
    }
}
