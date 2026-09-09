// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
//
// Crown fire initiation and propagation:
//   initiation   Van Wagner (1977) critical surface intensity from canopy base height and
//                foliar moisture content;
//   active ROS   Cruz, Alexander & Wakimoto (2005) empirical crown fire rate of spread;
//   active/passive  Van Wagner (1977) critical mass flow rate 3.0 / CBD, expressed through
//                the criterion for active crowning CAC (Cruz et al. 2005);
//   CFB          FBP / Van Wagner form 1 - exp(-0.23 (R - R0));
//   final ROS and intensity   Scott & Reinhardt (2001) blending.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public enum CrownFireClass : byte { None = 0, Surface = 1, Passive = 2, Active = 3 }

    /// <summary>How crown fire initiation is decided.</summary>
    public enum CrownInitiationMode
    {
        VanWagner,   // surface fireline intensity >= Van Wagner (1977) critical intensity
        Cruz2004     // Cruz, Alexander & Wakimoto (2004) logistic probability of crown fire occurrence
    }

    public struct CanopyFuel
    {
        public double CanopyBaseHeight_m;     // effective CBH
        public double CanopyBulkDensity_kgm3; // effective CBD
        public double CanopyFuelLoad_kgm2;    // available canopy fuel (foliage + fine branches)
        public double CanopyHeight_m;
        public double CanopyCover;            // fraction
        public double LadderFuel_kgm2;        // diagnostic
    }

    public struct FireBehaviorResult
    {
        public bool Burnable;
        public CrownFireClass CrownClass;
        public double CrownFractionBurned;
        public double SurfaceRos_mMin;
        public double FinalRos_mMin;
        public double SurfaceIntensity_kWm;
        public double FinalIntensity_kWm;
        public double FlameLength_m;
        public double HeadDirection_deg;
        public double EffectiveWind_kmh;
        public double LengthToBreadth;
        public double Eccentricity;
        public double HeatPerUnitArea_kJm2;
        public double CanopyFuelConsumed_kgm2;
        public double CriticalSurfaceIntensity_kWm;
        public double CrownFireProbability;      // Cruz et al. (2004) probability (1/0 under the Van Wagner criterion)
    }

    public static class CrownFire
    {
        /// <summary>Van Wagner (1977) critical surface fireline intensity, kW m^-1. CBH in m, FMC in percent.</summary>
        public static double CriticalSurfaceIntensity(double cbh_m, double foliarMoisturePct)
        {
            double cbh = Math.Max(cbh_m, 0.1);
            double fmc = Math.Max(foliarMoisturePct, 30.0);
            return Math.Pow(0.010 * cbh * (460.0 + 25.9 * fmc), 1.5);
        }

        /// <summary>
        /// Cruz, Alexander and Wakimoto (2004, Forest Science 50:640-658, Table 4) probability of crown
        /// fire occurrence from the 10-m open wind (km/h), the fuel strata gap (m, here the effective
        /// canopy base height), the estimated fine dead fuel moisture (%) and the surface fuel consumed
        /// (kg m^-2, entered as the classes &lt; 1, 1-2 and &gt; 2).
        /// </summary>
        public static double CruzCrownFireProbability(double wind10_kmh, double fsg_m, double effm_pct, double sfc_kgm2)
        {
            double d1 = sfc_kgm2 < 1.0 ? 1.0 : 0.0;
            double d2 = (sfc_kgm2 >= 1.0 && sfc_kgm2 < 2.0) ? 1.0 : 0.0;
            double g = 4.236 + 0.357 * wind10_kmh - 0.710 * fsg_m - 0.331 * effm_pct - 4.613 * d1 - 1.856 * d2;
            return 1.0 / (1.0 + Math.Exp(-g));
        }

        /// <summary>
        /// Cruz et al. (2005) active crown fire rate of spread, m min^-1.
        /// U10 = 10-m open wind, km h^-1; CBD kg m^-3; EFFM = estimated fine fuel moisture, percent.
        /// </summary>
        public static double ActiveCrownRos(double wind10_kmh, double cbd, double effm_pct)
        {
            double u = Math.Max(wind10_kmh, 0.1);
            double c = Math.Max(cbd, 0.005);
            return 11.02 * Math.Pow(u, 0.90) * Math.Pow(c, 0.19) * Math.Exp(-0.17 * effm_pct);
        }

        /// <summary>Critical rate of spread for active crowning, m min^-1 (Van Wagner 1977): 3.0 / CBD.</summary>
        public static double CriticalActiveRos(double cbd)
        {
            return 3.0 / Math.Max(cbd, 0.005);
        }

        /// <summary>
        /// Evaluate crown fire potential given a surface fire result and canopy fuel.
        /// </summary>
        public static FireBehaviorResult Evaluate(SurfaceFireResult surf, CanopyFuel canopy,
                                                  double foliarMoisturePct, double wind10_kmh,
                                                  double fineFuelMoisturePct, bool allowCrownFire)
        {
            return Evaluate(surf, canopy, foliarMoisturePct, wind10_kmh, fineFuelMoisturePct, allowCrownFire,
                            CrownInitiationMode.VanWagner, 0.0, 0.0);
        }

        /// <summary>
        /// Evaluate crown fire potential. surfaceConsumption_kgm2 is the surface fuel (including forest
        /// floor) consumed, used by the Cruz (2004) initiation model; extraSurfaceHpa_kJm2 is heat released
        /// in the flaming front by fuel that Rothermel's heat per unit area does not include (the flaming
        /// share of the 1000-h consumption), added to the fireline intensity (Byram 1959).
        /// </summary>
        public static FireBehaviorResult Evaluate(SurfaceFireResult surf, CanopyFuel canopy,
                                                  double foliarMoisturePct, double wind10_kmh,
                                                  double fineFuelMoisturePct, bool allowCrownFire,
                                                  CrownInitiationMode initiation, double surfaceConsumption_kgm2,
                                                  double extraSurfaceHpa_kJm2)
        {
            FireBehaviorResult r = new FireBehaviorResult();
            double surfHpa = surf.HeatPerUnitArea_kJm2 + Math.Max(extraSurfaceHpa_kJm2, 0.0);
            double surfI = surf.Burnable ? surfHpa * surf.Ros_mMin / 60.0 : 0.0;
            r.Burnable = surf.Burnable;
            r.SurfaceRos_mMin = surf.Ros_mMin;
            r.SurfaceIntensity_kWm = surfI;
            r.FinalRos_mMin = surf.Ros_mMin;
            r.FinalIntensity_kWm = surfI;
            r.FlameLength_m = Rothermel.ByramFlameLength(surfI);
            r.HeadDirection_deg = surf.HeadDirection_deg;
            r.EffectiveWind_kmh = surf.EffectiveWind_kmh;
            r.HeatPerUnitArea_kJm2 = surfHpa;
            r.LengthToBreadth = Ellipse.LengthToBreadth(surf.EffectiveWind_kmh);
            r.Eccentricity = Ellipse.Eccentricity(r.LengthToBreadth);
            r.CrownClass = surf.Burnable ? CrownFireClass.Surface : CrownFireClass.None;
            if (!surf.Burnable) return r;

            bool hasCanopy = allowCrownFire && canopy.CanopyFuelLoad_kgm2 > 0.02 && canopy.CanopyBulkDensity_kgm3 > 0.005;
            if (!hasCanopy) return r;

            double I0 = CriticalSurfaceIntensity(canopy.CanopyBaseHeight_m, foliarMoisturePct);
            r.CriticalSurfaceIntensity_kWm = I0;
            bool byIntensity = surfI >= I0;
            double pCrown = byIntensity ? 1.0 : 0.0;
            if (initiation == CrownInitiationMode.Cruz2004)
                pCrown = Math.Max(pCrown, CruzCrownFireProbability(wind10_kmh, canopy.CanopyBaseHeight_m, fineFuelMoisturePct, surfaceConsumption_kgm2));
            r.CrownFireProbability = pCrown;
            if (!byIntensity && pCrown < 0.05) return r;   // surface fire only

            // torching: crown fraction burned from surface ROS relative to the critical surface ROS;
            // under the Cruz (2004) criterion at least the probability of crowning (the expected share
            // of the cell in which crowns are involved)
            double R0 = I0 / Math.Max(surfHpa, 1.0) * 60.0;  // m min^-1 that yields I0 at this HPA
            double cfb = 1.0 - Math.Exp(-0.23 * Math.Max(surf.Ros_mMin - R0, 0.0));
            if (initiation == CrownInitiationMode.Cruz2004 && !byIntensity) cfb = Math.Max(cfb, pCrown);
            cfb = Units.Clamp01(cfb);

            double rActive = ActiveCrownRos(wind10_kmh, canopy.CanopyBulkDensity_kgm3, fineFuelMoisturePct);
            double rCrit = CriticalActiveRos(canopy.CanopyBulkDensity_kgm3);
            double cac = rActive / rCrit;

            if (cac >= 1.0)
            {
                r.CrownClass = CrownFireClass.Active;
                cfb = Math.Max(cfb, 0.9);
                r.FinalRos_mMin = Math.Max(surf.Ros_mMin, rActive);
            }
            else
            {
                r.CrownClass = CrownFireClass.Passive;
                double rPassive = rActive * Math.Exp(-cac);     // Cruz et al. 2005 passive crown ROS
                cfb = Math.Min(cfb, 0.89);
                r.FinalRos_mMin = surf.Ros_mMin + cfb * Math.Max(rPassive - surf.Ros_mMin, 0.0);
            }
            r.CrownFractionBurned = cfb;
            r.CanopyFuelConsumed_kgm2 = cfb * canopy.CanopyFuelLoad_kgm2;

            // total intensity: surface heat per unit area plus canopy fuel consumed at 18 000 kJ/kg
            double hpaTotal = surfHpa + r.CanopyFuelConsumed_kgm2 * 18000.0;
            r.FinalIntensity_kWm = hpaTotal * r.FinalRos_mMin / 60.0;
            r.HeatPerUnitArea_kJm2 = hpaTotal;
            r.FlameLength_m = r.CrownClass == CrownFireClass.Active
                ? Rothermel.ThomasFlameLength(r.FinalIntensity_kWm)
                : Rothermel.ByramFlameLength(r.FinalIntensity_kWm);
            // crown fires stretch the ellipse a little more than surface fires; keep the surface LB
            return r;
        }
    }
}
