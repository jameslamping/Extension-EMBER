// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
//
// Fuel moisture from the Canadian Forest Fire Weather Index System codes (Van Wagner
// 1987) plus simple live fuel moisture curves. Returned moistures are FRACTIONS.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public static class FuelMoisture
    {
        /// <summary>1-h (litter) moisture fraction implied by a Fine Fuel Moisture Code (Van Wagner 1987 eq. 1).</summary>
        public static double FromFFMC(double ffmc)
        {
            double f = Units.Clamp(ffmc, 0.0, 101.0);
            return 147.2 * (101.0 - f) / (59.5 + f) / 100.0;
        }

        /// <summary>Duff/100-h moisture fraction implied by a Duff Moisture Code (Van Wagner 1987 eq. 12).</summary>
        public static double FromDMC(double dmc)
        {
            double d = Math.Max(dmc, 0.0);
            return (20.0 + 280.0 / Math.Exp(0.023 * d)) / 100.0;
        }

        /// <summary>Drought index 0..1 from the Drought Code, linear between low and high thresholds.</summary>
        public static double DroughtIndex(double dc, double dcLow, double dcHigh)
        {
            if (dcHigh <= dcLow) return dc >= dcHigh ? 1.0 : 0.0;
            return Units.Clamp01((dc - dcLow) / (dcHigh - dcLow));
        }

        /// <summary>Moisture (fraction) that declines linearly from max (D=0) to min (D=1).</summary>
        public static double FromDrought(double droughtIndex, double minFraction, double maxFraction)
        {
            return maxFraction - (maxFraction - minFraction) * Units.Clamp01(droughtIndex);
        }

        /// <summary>
        /// Initial Spread Index from FFMC and 10-m wind (km h^-1), Van Wagner 1987 eq. 24-27.
        /// </summary>
        public static double InitialSpreadIndex(double ffmc, double wind_kmh)
        {
            double m = FromFFMC(ffmc) * 100.0;
            double fW = Math.Exp(0.05039 * Math.Max(wind_kmh, 0.0));
            double fF = 91.9 * Math.Exp(-0.1386 * m) * (1.0 + Math.Pow(m, 5.31) / 4.93e7);
            return 0.208 * fW * fF;
        }

        /// <summary>
        /// 10-h moisture as a lagged response toward the 1-h (boundary) moisture:
        /// m10(t) = m10(t-1) + (m1 - m10(t-1)) (1 - exp(-24/10)).
        /// </summary>
        public static double LagTowards(double previous, double boundary, double timelagHours)
        {
            double k = 1.0 - Math.Exp(-24.0 / Math.Max(timelagHours, 1.0));
            return previous + (boundary - previous) * k;
        }

        /// <summary>
        /// Canopy shading and topographic exposure adjustments to dead fuel moisture.
        /// cover 0..1; radiationIndex 0..1 (0.5 = flat). Returns a multiplier.
        /// </summary>
        public static double SiteModifier(double canopyCover, double radiationIndex, double shadeModifier, double topographyModifier)
        {
            double shade = 1.0 + shadeModifier * (Units.Clamp01(canopyCover) - 0.5) * 2.0;      // -shade..+shade
            double topo = 1.0 - topographyModifier * (Units.Clamp01(radiationIndex) - 0.5) * 2.0; // south steep drier
            double m = shade * topo;
            return m < 0.5 ? 0.5 : m;
        }

        /// <summary>
        /// Conifer foliar moisture content (percent) by day of year following the Canadian
        /// FBP System (Forestry Canada 1992, eq. 1-4) with the spring dip, then reduced by
        /// drought: FMC_eff = FMC * (1 - droughtEffect * D).
        /// </summary>
        public static double ConiferFoliarMoisturePct(int dayOfYear1Based, double latitude, double longitude,
                                                       double droughtIndex, double droughtEffect)
        {
            double latn = 46.0 + 23.4 * Math.Exp(-0.0360 * (150.0 - Math.Abs(longitude)));
            double d0 = 151.0 * (latitude / latn);
            double nd = Math.Abs(dayOfYear1Based - d0);
            double fmc;
            if (nd < 30) fmc = 85.0 + 0.0189 * nd * nd;
            else if (nd < 50) fmc = 32.9 + 3.17 * nd - 0.0288 * nd * nd;
            else fmc = 120.0;
            return fmc * (1.0 - droughtEffect * Units.Clamp01(droughtIndex));
        }

        /// <summary>
        /// Greenness of herbaceous fuel 0..1 by day of year given the growing season bounds.
        /// Ramps up over the first 30 days, holds, then cures over the last 30 days; drought
        /// advances curing.
        /// </summary>
        public static double HerbGreenness(int dayOfYear0, int beginGrowingDay, int endGrowingDay, double droughtIndex)
        {
            if (endGrowingDay <= beginGrowingDay) return 0.0;
            double g;
            if (dayOfYear0 < beginGrowingDay) g = 0.0;
            else if (dayOfYear0 < beginGrowingDay + 30) g = (dayOfYear0 - beginGrowingDay) / 30.0;
            else if (dayOfYear0 < endGrowingDay - 30) g = 1.0;
            else if (dayOfYear0 < endGrowingDay) g = (endGrowingDay - dayOfYear0) / 30.0;
            else g = 0.0;
            return Units.Clamp01(g * (1.0 - 0.6 * Units.Clamp01(droughtIndex)));
        }

        /// <summary>
        /// Topographic radiation index 0..1: 0.5 on flat ground, higher on steep
        /// south-facing slopes (aspect 180) and lower on north-facing ones.
        /// </summary>
        public static double RadiationIndex(double slopeDeg, double aspectDeg)
        {
            double s = Math.Sin(Units.Clamp(slopeDeg, 0, 60) * Units.Deg_to_Rad);
            double c = Math.Cos((aspectDeg - 180.0) * Units.Deg_to_Rad);
            return Units.Clamp01(0.5 + 0.5 * s * c);
        }
    }
}
