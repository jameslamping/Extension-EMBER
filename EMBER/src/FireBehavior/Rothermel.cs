// EMBER fire behavior library. Pure functions, no LANDIS-II dependency.
//
// Rothermel (1972) surface fire spread model with the Albini (1976) corrections,
// following the formulation in Andrews (2018) "The Rothermel surface fire spread
// model and associated developments: a comprehensive explanation" (RMRS-GTR-371),
// including the dynamic herbaceous curing transfer and the Andrews et al. (2013)
// wind limit. The computation is done in the model's native English units and the
// result is returned in metric.
using System;

namespace Landis.Extension.EMBER.FireBehavior
{
    public struct SurfaceFireResult
    {
        public bool Burnable;                 // false when dead fuel is wetter than extinction or there is no fine fuel
        public double Ros0_mMin;              // no-wind, no-slope rate of spread, m min^-1
        public double Ros_mMin;               // head fire rate of spread with wind and slope, m min^-1
        public double ReactionIntensity_kWm2; // kW m^-2
        public double HeatPerUnitArea_kJm2;   // kJ m^-2
        public double FirelineIntensity_kWm;  // Byram, kW m^-1
        public double FlameLength_m;          // Byram
        public double ResidenceTime_min;
        public double PhiWind, PhiSlope, PhiEffective;
        public double EffectiveWind_kmh;      // wind that alone would produce PhiEffective (midflame)
        public double HeadDirection_deg;      // direction of maximum spread (compass, direction fire moves toward)
        public double CharacteristicSav_perFt;
        public double PackingRatio;
        public double DeadMoistureRatio;      // fine dead moisture / moisture of extinction
    }

    public static class Rothermel
    {
        const double ParticleDensity = 32.0;   // lb ft^-3
        const double TotalMineral = 0.0555;
        const double EffectiveMineral = 0.010;
        const double EtaS = 0.4173969;         // 0.174 * SE^-0.19

        /// <summary>
        /// Compute surface fire behavior.
        /// </summary>
        /// <param name="fuel">fuel model (metric loads, ft^-1 SAV)</param>
        /// <param name="m">fuel moisture fractions</param>
        /// <param name="midflameWind_kmh">wind speed at midflame height, km h^-1</param>
        /// <param name="windToDeg">compass direction the wind blows toward</param>
        /// <param name="slopeDeg">ground slope in degrees</param>
        /// <param name="aspectDeg">downslope (aspect) direction in degrees</param>
        public static SurfaceFireResult Compute(SurfaceFuel fuel, FuelMoistureState m,
                                                double midflameWind_kmh, double windToDeg,
                                                double slopeDeg, double aspectDeg)
        {
            SurfaceFireResult r = new SurfaceFireResult();

            // ---- loads to lb/ft^2, with dynamic curing of herbaceous fuel -------------
            double w1 = fuel.W1h * Units.KgM2_to_LbFt2;
            double w10 = fuel.W10h * Units.KgM2_to_LbFt2;
            double w100 = fuel.W100h * Units.KgM2_to_LbFt2;
            double wHerbLive = fuel.WHerb * Units.KgM2_to_LbFt2;
            double wWoody = fuel.WWoody * Units.KgM2_to_LbFt2;

            double wHerbDead = 0.0;
            if (wHerbLive > 0)
            {
                // Andrews 2018 eq. 22-24: curing transfers live herb to a dead class with the same SAV
                double cure = Units.Clamp((1.20 - m.MHerb) / 0.9, 0.0, 1.0);
                wHerbDead = wHerbLive * cure;
                wHerbLive -= wHerbDead;
            }

            // six classes: dead 1h, 10h, 100h, deadHerb ; live herb, woody
            double[] w0 = { w1, w10, w100, wHerbDead, wHerbLive, wWoody };
            double[] sav = { fuel.Sav1h, fuel.Sav10h, fuel.Sav100h, fuel.SavHerb, fuel.SavHerb, fuel.SavWoody };
            double[] mf = { m.M1h, m.M10h, m.M100h, m.M1h, m.MHerb, m.MWoody };
            bool[] isDead = { true, true, true, true, false, false };
            double h = fuel.HeatContent * Units.KJKg_to_BtuLb;

            double wTotal = 0; for (int i = 0; i < 6; i++) wTotal += w0[i];
            if (wTotal <= 1e-9 || (w0[0] + w0[1] + w0[3]) <= 1e-9)
            {
                r.Burnable = false;
                return r;
            }

            // ---- weighting factors (Andrews 2018 eq. 2-8) ----------------------------
            double[] a = new double[6];
            double aDead = 0, aLive = 0;
            for (int i = 0; i < 6; i++)
            {
                a[i] = sav[i] * w0[i] / ParticleDensity;
                if (isDead[i]) aDead += a[i]; else aLive += a[i];
            }
            double aTot = aDead + aLive;
            double[] f = new double[6];
            for (int i = 0; i < 6; i++)
            {
                double denom = isDead[i] ? aDead : aLive;
                f[i] = denom > 0 ? a[i] / denom : 0.0;
            }
            double fDead = aDead / aTot, fLive = aLive / aTot;

            // characteristic SAV
            double savDead = 0, savLive = 0;
            for (int i = 0; i < 6; i++) { if (isDead[i]) savDead += f[i] * sav[i]; else savLive += f[i] * sav[i]; }
            double sigma = fDead * savDead + fLive * savLive;

            // net loads by category using size-class groups g_ij (Andrews 2018 eq. 9-10)
            double[] g = new double[6];
            for (int i = 0; i < 6; i++)
            {
                if (w0[i] <= 0) { g[i] = 0; continue; }
                int cls = SizeClass(sav[i]);
                double gsum = 0;
                for (int j = 0; j < 6; j++)
                    if (isDead[j] == isDead[i] && SizeClass(sav[j]) == cls) gsum += f[j];
                g[i] = gsum;
            }
            double wnDead = 0, wnLive = 0;
            for (int i = 0; i < 6; i++)
            {
                double wn = g[i] * w0[i] * (1.0 - TotalMineral);
                if (isDead[i]) wnDead += wn; else wnLive += wn;
            }

            // category moisture
            double mDead = 0, mLive = 0;
            for (int i = 0; i < 6; i++) { if (isDead[i]) mDead += f[i] * mf[i]; else mLive += f[i] * mf[i]; }

            // ---- live moisture of extinction (Andrews 2018 eq. 26-28) ----------------
            double mxDead = fuel.MoistureOfExtinction;
            double sumDeadExp = 0, sumDeadExpM = 0, sumLiveExp = 0;
            for (int i = 0; i < 6; i++)
            {
                if (isDead[i]) { double e = w0[i] * Math.Exp(-138.0 / Math.Max(sav[i], 1)); sumDeadExp += e; sumDeadExpM += e * mf[i]; }
                else { sumLiveExp += w0[i] * Math.Exp(-500.0 / Math.Max(sav[i], 1)); }
            }
            double mxLive = mxDead;
            if (aLive > 0 && sumLiveExp > 0 && sumDeadExp > 0)
            {
                double W = sumDeadExp / sumLiveExp;
                double mfDeadFine = sumDeadExpM / sumDeadExp;
                mxLive = 2.9 * W * (1.0 - mfDeadFine / mxDead) - 0.226;
                if (mxLive < mxDead) mxLive = mxDead;
            }
            r.DeadMoistureRatio = mDead / mxDead;
            if (mDead >= mxDead)
            {
                r.Burnable = false;
                r.CharacteristicSav_perFt = sigma;
                return r;
            }

            // ---- reaction intensity (eq. 11-21) --------------------------------------
            double rhoB = wTotal / Math.Max(1e-9, DepthFt(fuel, wTotal));
            double beta = rhoB / ParticleDensity;
            double betaOp = 3.348 * Math.Pow(sigma, -0.8189);
            double A = 133.0 * Math.Pow(sigma, -0.7913);
            double gammaMax = Math.Pow(sigma, 1.5) / (495.0 + 0.0594 * Math.Pow(sigma, 1.5));
            double gamma = gammaMax * Math.Pow(beta / betaOp, A) * Math.Exp(A * (1.0 - beta / betaOp));

            double etaMDead = MoistureDamping(mDead / mxDead);
            double etaMLive = MoistureDamping(mLive / mxLive);
            double IR = gamma * (wnDead * h * etaMDead + wnLive * h * etaMLive) * EtaS; // BTU ft^-2 min^-1

            // ---- propagating flux, heat sink (eq. 29-33) -------------------------------
            double xi = Math.Exp((0.792 + 0.681 * Math.Sqrt(sigma)) * (beta + 0.1)) / (192.0 + 0.2595 * sigma);
            double heatSink = 0;
            for (int i = 0; i < 6; i++)
            {
                if (w0[i] <= 0) continue;
                double eps = Math.Exp(-138.0 / Math.Max(sav[i], 1));
                double qig = 250.0 + 1116.0 * mf[i];
                heatSink += (isDead[i] ? fDead : fLive) * f[i] * eps * qig;
            }
            heatSink *= rhoB;
            if (heatSink <= 0 || IR <= 0) { r.Burnable = false; return r; }

            double R0 = IR * xi / heatSink;    // ft min^-1, no wind, no slope

            // ---- wind and slope factors (eq. 34-40, 42) -----------------------------------
            double B = 0.02526 * Math.Pow(sigma, 0.54);
            double C = 7.47 * Math.Exp(-0.133 * Math.Pow(sigma, 0.55));
            double E = 0.715 * Math.Exp(-3.59e-4 * sigma);
            double U = midflameWind_kmh * Units.Kmh_to_FtMin;
            // Andrews et al. 2013 wind limit: U <= 96.8 * IR^(1/3)
            double uLimit = 96.8 * Math.Pow(IR, 1.0 / 3.0);
            if (U > uLimit) U = uLimit;
            double phiW = C * Math.Pow(U, B) * Math.Pow(beta / betaOp, -E);
            double tanSlope = Math.Tan(Units.Clamp(slopeDeg, 0, 80) * Units.Deg_to_Rad);
            double phiS = 5.275 * Math.Pow(beta, -0.3) * tanSlope * tanSlope;

            // vector combination of wind and slope effects (FARSITE convention):
            // wind vector points where the wind blows to, slope vector points upslope.
            double upslopeDeg = Units.NormalizeDeg(aspectDeg + 180.0);
            double wx = phiW * Math.Sin(windToDeg * Units.Deg_to_Rad), wy = phiW * Math.Cos(windToDeg * Units.Deg_to_Rad);
            double sx = phiS * Math.Sin(upslopeDeg * Units.Deg_to_Rad), sy = phiS * Math.Cos(upslopeDeg * Units.Deg_to_Rad);
            double vx = wx + sx, vy = wy + sy;
            double phiE = Math.Sqrt(vx * vx + vy * vy);
            double headDir = phiE > 1e-9 ? Units.NormalizeDeg(Math.Atan2(vx, vy) * Units.Rad_to_Deg) : windToDeg;

            double R = R0 * (1.0 + phiE);

            // effective wind: the midflame wind that alone would give phiE
            double uEff = 0.0;
            if (phiE > 0)
                uEff = Math.Pow(phiE / (C * Math.Pow(beta / betaOp, -E)), 1.0 / B); // ft min^-1
            if (uEff > uLimit) uEff = uLimit;

            // ---- intensity, flame length (Byram) --------------------------------------
            double tr = 384.0 / sigma;                       // residence time, min
            double HA = IR * tr;                              // BTU ft^-2
            double IB = HA * R / 60.0;                        // BTU ft^-1 s^-1
            double IB_kWm = IB * Units.BtuFtS_to_KWM;

            r.Burnable = true;
            r.Ros0_mMin = R0 * Units.FtMin_to_MMin;
            r.Ros_mMin = R * Units.FtMin_to_MMin;
            r.ReactionIntensity_kWm2 = IR * Units.BtuFt2_to_KJM2 / 60.0;
            r.HeatPerUnitArea_kJm2 = HA * Units.BtuFt2_to_KJM2;
            r.FirelineIntensity_kWm = IB_kWm;
            r.FlameLength_m = 0.0775 * Math.Pow(Math.Max(IB_kWm, 0.0), 0.46);
            r.ResidenceTime_min = tr;
            r.PhiWind = phiW; r.PhiSlope = phiS; r.PhiEffective = phiE;
            r.EffectiveWind_kmh = uEff / Units.Kmh_to_FtMin;
            r.HeadDirection_deg = headDir;
            r.CharacteristicSav_perFt = sigma;
            r.PackingRatio = beta;
            return r;
        }

        /// <summary>Fuelbed depth in feet from the target packing ratio: delta = w / (beta * rho_p).</summary>
        public static double DepthFt(SurfaceFuel fuel, double totalLoadLbFt2)
        {
            double beta = Math.Max(fuel.PackingRatio, 1e-4);
            return totalLoadLbFt2 / (beta * ParticleDensity);
        }

        public static double MoistureDamping(double ratio)
        {
            double rm = Math.Min(ratio, 1.0);
            double eta = 1.0 - 2.59 * rm + 5.11 * rm * rm - 3.52 * rm * rm * rm;
            return eta < 0 ? 0 : eta;
        }

        static int SizeClass(double sav)
        {
            if (sav >= 1200) return 0;
            if (sav >= 192) return 1;
            if (sav >= 96) return 2;
            if (sav >= 48) return 3;
            if (sav >= 16) return 4;
            return 5;
        }

        /// <summary>Byram flame length (m) from fireline intensity (kW m^-1).</summary>
        public static double ByramFlameLength(double intensity_kWm)
        {
            return 0.0775 * Math.Pow(Math.Max(intensity_kWm, 0.0), 0.46);
        }

        /// <summary>Thomas (1963) flame length (m) for crown fires.</summary>
        public static double ThomasFlameLength(double intensity_kWm)
        {
            return 0.0266 * Math.Pow(Math.Max(intensity_kWm, 0.0), 2.0 / 3.0);
        }

        /// <summary>
        /// Wind adjustment factor from canopy cover (fraction). Linear between an open value
        /// (cover 0) and a fully sheltered value (cover 1); Andrews (2012) gives ~0.4 open
        /// and ~0.1-0.2 under dense canopy.
        /// </summary>
        public static double WindAdjustmentFactor(double canopyCover, double wafOpen, double wafSheltered)
        {
            return wafOpen + (wafSheltered - wafOpen) * Units.Clamp01(canopyCover);
        }
    }
}
