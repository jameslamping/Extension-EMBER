// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
//
// Crown scorch (Van Wagner 1973) and tree mortality (Ryan & Reinhardt 1988, as used in
// FOFEM), plus the bark-thickness-from-age allometry inherited from SCRPPLE.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public static class Mortality
    {
        /// <summary>
        /// Van Wagner (1973) crown scorch height (m) with the ambient temperature and wind
        /// form: h = 0.74 I^(7/6) / ( sqrt(0.025574 I + 0.021433 U^3) (60 - T) ),
        /// I kW m^-1, U km h^-1, T deg C.
        /// </summary>
        public static double ScorchHeight(double intensity_kWm, double wind_kmh, double airTempC)
        {
            double I = Math.Max(intensity_kWm, 0.0);
            if (I <= 0) return 0.0;
            double u = Math.Max(wind_kmh, 0.0);
            double t = Math.Min(airTempC, 55.0);
            double denom = Math.Sqrt(0.025574 * I + 0.021433 * u * u * u) * (60.0 - t);
            if (denom <= 1e-9) return 100.0;
            double h = 0.74 * Math.Pow(I, 7.0 / 6.0) / denom;
            return h < 0 ? 0 : h;
        }

        /// <summary>Crown volume scorched (percent) for a crown from base height cbh to top height h.</summary>
        public static double CrownVolumeScorchedPct(double scorchHeight, double cbh, double height)
        {
            if (height <= cbh) return scorchHeight >= height ? 100.0 : 0.0;
            if (scorchHeight <= cbh) return 0.0;
            if (scorchHeight >= height) return 100.0;
            double lengthScorched = scorchHeight - cbh;
            double crownLength = height - cbh;
            // volume of a conical crown scorched from below (Peterson 1985): fraction of length gives
            // 1 - (1 - f)^2 of the volume
            double f = lengthScorched / crownLength;
            return 100.0 * (1.0 - (1.0 - f) * (1.0 - f));
        }

        /// <summary>Bark thickness (cm) at a given age: BT = BTmax * age / (age + ageDbh). SCRPPLE allometry.</summary>
        public static double BarkThicknessCm(double maxBarkThicknessCm, double ageDbh, double age)
        {
            if (ageDbh <= 0) return maxBarkThicknessCm;
            return maxBarkThicknessCm * age / (age + ageDbh);
        }

        /// <summary>
        /// Ryan &amp; Reinhardt (1988) probability of mortality from bark thickness (cm) and crown volume scorched (%).
        /// logit P = -1.941 + 6.316 (1 - exp(-BT)) - 0.000535 CVS^2 gives probability of SURVIVAL
        /// in the original; here we return the mortality probability.
        /// </summary>
        public static double MortalityProbability(double barkThicknessCm, double crownVolumeScorchedPct)
        {
            double cvs = Units.Clamp(crownVolumeScorchedPct, 0, 100);
            double logitSurvival = -1.941 + 6.316 * (1.0 - Math.Exp(-Math.Max(barkThicknessCm, 0))) - 0.000535 * cvs * cvs;
            double pSurvive = Units.Logistic(logitSurvival);
            return Units.Clamp01(1.0 - pSurvive);
        }

        /// <summary>Tree height (m) from age with a Chapman-Richards style curve H = Hmax (1 - exp(-k age)).</summary>
        public static double Height(double maxHeight, double k, double age)
        {
            return maxHeight * (1.0 - Math.Exp(-k * Math.Max(age, 0)));
        }
    }
}
