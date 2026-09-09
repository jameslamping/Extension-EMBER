// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    /// <summary>
    /// A dynamic surface fuel model in the Rothermel/Scott &amp; Burgan sense:
    /// five particle classes (1-h, 10-h, 100-h dead; live herbaceous; live woody),
    /// each with a load and a surface-area-to-volume ratio, plus fuelbed-level
    /// heat content, dead moisture of extinction and packing ratio.
    /// Loads are kg m^-2, SAV is ft^-1 (the unit fuel-model tables are published in),
    /// heat content kJ kg^-1, moisture of extinction and packing ratio are fractions.
    /// </summary>
    public struct SurfaceFuel
    {
        public double W1h, W10h, W100h, WHerb, WWoody;      // kg m^-2
        public double Sav1h, Sav10h, Sav100h, SavHerb, SavWoody; // ft^-1
        public double HeatContent;                          // kJ kg^-1
        public double MoistureOfExtinction;                 // fraction (dead)
        public double PackingRatio;                         // beta, dimensionless

        public double TotalDeadLoad { get { return W1h + W10h + W100h; } }
        public double TotalLiveLoad { get { return WHerb + WWoody; } }
        public double TotalLoad { get { return TotalDeadLoad + TotalLiveLoad; } }
        public double FineDeadLoad { get { return W1h + W10h; } }

        public static SurfaceFuel Empty
        {
            get
            {
                SurfaceFuel f = new SurfaceFuel();
                f.Sav1h = 2000; f.Sav10h = 109; f.Sav100h = 30; f.SavHerb = 1800; f.SavWoody = 1500;
                f.HeatContent = 18608; f.MoistureOfExtinction = 0.25; f.PackingRatio = 0.01;
                return f;
            }
        }
    }

    /// <summary>Fuel moisture by class, as fractions of dry weight (0.10 = 10 %).</summary>
    public struct FuelMoistureState
    {
        public double M1h, M10h, M100h, M1000h, MHerb, MWoody, MDuff;
        /// <summary>Foliar moisture content of conifer canopy fuel, fraction.</summary>
        public double MFoliar;
    }

    /// <summary>Fuelbed type row: physical properties shared by all cells that carry this litter type.</summary>
    public class FuelbedType
    {
        public string Name;
        public double Sav1h = 2000, Sav10h = 109, Sav100h = 30, SavHerb = 1800, SavWoody = 1500;
        public double HeatContent = 18608;          // kJ kg^-1  (8000 BTU lb^-1)
        public double MoistureOfExtinction = 0.25;  // fraction
        public double PackingRatio = 0.01;
        public double LitterFraction1h = 0.7;       // share of fine litter in the 1-h class, rest is 10-h
        public double DuffBulkDensity = 100;        // kg m^-3 (used for duff depth diagnostics)
        public int Index;
    }
}
