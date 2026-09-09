// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
// Elliptical fire shape (Anderson 1983; Finney 2002) used to turn a head-fire rate of
// spread into a rate of spread in any direction for minimum-travel-time spread.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public static class Ellipse
    {
        /// <summary>
        /// Length-to-breadth ratio from effective wind speed (Anderson 1983, as used in
        /// BehavePlus/FARSITE). Wind in km h^-1 at midflame; capped at 8.
        /// </summary>
        public static double LengthToBreadth(double effectiveWind_kmh)
        {
            double u = effectiveWind_kmh * Units.Kmh_to_Mph;
            double lb = 0.936 * Math.Exp(0.2566 * u) + 0.461 * Math.Exp(-0.1548 * u) - 0.397;
            if (lb < 1.0) lb = 1.0;
            if (lb > 8.0) lb = 8.0;
            return lb;
        }

        public static double Eccentricity(double lengthToBreadth)
        {
            double lb = Math.Max(lengthToBreadth, 1.0);
            return Math.Sqrt(lb * lb - 1.0) / lb;
        }

        /// <summary>
        /// Rate of spread in a direction that makes angle theta (degrees) with the heading.
        /// R(theta) = R_head (1 - e) / (1 - e cos theta)   (Finney 2002 eq. 4)
        /// </summary>
        public static double DirectionalRos(double headRos, double eccentricity, double thetaDeg)
        {
            double c = Math.Cos(thetaDeg * Units.Deg_to_Rad);
            double denom = 1.0 - eccentricity * c;
            if (denom < 1e-6) denom = 1e-6;
            return headRos * (1.0 - eccentricity) / denom;
        }

        public static double BackingRos(double headRos, double eccentricity)
        {
            return headRos * (1.0 - eccentricity) / (1.0 + eccentricity);
        }
    }
}
