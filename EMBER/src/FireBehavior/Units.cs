// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
// Unit conversion constants used throughout the behavior code.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public static class Units
    {
        public const double KgM2_to_LbFt2 = 0.2048161;      // kg m^-2 -> lb ft^-2
        public const double LbFt2_to_KgM2 = 1.0 / KgM2_to_LbFt2;
        public const double Ft_to_M = 0.3048;
        public const double M_to_Ft = 1.0 / 0.3048;
        public const double Kmh_to_FtMin = 54.68066;         // km h^-1 -> ft min^-1
        public const double Kmh_to_Mph = 0.6213712;
        public const double FtMin_to_MMin = 0.3048;
        public const double BtuLb_to_KJKg = 2.326;
        public const double KJKg_to_BtuLb = 1.0 / 2.326;
        public const double BtuFt2_to_KJM2 = 11.356;         // BTU ft^-2 -> kJ m^-2
        public const double BtuFtS_to_KWM = 3.4613;          // BTU ft^-1 s^-1 -> kW m^-1
        public const double PerFt_to_PerM = 3.2808399;       // ft^-1 -> m^-1  (SAV)
        public const double Deg_to_Rad = Math.PI / 180.0;
        public const double Rad_to_Deg = 180.0 / Math.PI;

        public static double Clamp(double x, double lo, double hi)
        {
            return x < lo ? lo : (x > hi ? hi : x);
        }

        public static double Clamp01(double x)
        {
            return x < 0.0 ? 0.0 : (x > 1.0 ? 1.0 : x);
        }

        public static double Logistic(double x)
        {
            if (x > 40) return 1.0;
            if (x < -40) return 0.0;
            return 1.0 / (1.0 + Math.Exp(-x));
        }

        /// <summary>Smallest signed angular difference a-b in degrees, in (-180, 180].</summary>
        public static double AngleDiff(double aDeg, double bDeg)
        {
            double d = (aDeg - bDeg) % 360.0;
            if (d <= -180.0) d += 360.0;
            if (d > 180.0) d -= 360.0;
            return d;
        }

        public static double NormalizeDeg(double a)
        {
            a %= 360.0;
            if (a < 0) a += 360.0;
            return a;
        }
    }
}
