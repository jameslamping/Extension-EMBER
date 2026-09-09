// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
//
// Fuel moisture is tracked for strata = ecoregion x canopy-cover class x radiation class,
// updated once per day from the Climate Library's FWI codes. Each cell stores a stratum id.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class MoistureStrata
    {
        public const int CoverClasses = 3, RadiationClasses = 3;
        readonly ICore core;
        readonly InputParameters p;
        readonly int ecoCount;

        // stratum id = eco * 9 + cover * 3 + rad ; stored per cell as byte when < 256, else as ushort
        public ushort[] CellStratum;
        int strataCount;
        FuelMoistureState[] state;        // current moisture by stratum
        double[] drought;                 // D by stratum
        double[] coverMid = { 0.15, 0.5, 0.85 };
        double[] radMid = { 0.3, 0.5, 0.7 };
        bool initialized;

        public MoistureStrata(ICore core, InputParameters p)
        {
            this.core = core;
            this.p = p;
            ecoCount = core.Ecoregions.Count;
            strataCount = ecoCount * CoverClasses * RadiationClasses;
            state = new FuelMoistureState[strataCount];
            drought = new double[strataCount];
            CellStratum = new ushort[CellData.Count];
        }

        public static int CoverClass(double cover) { return cover < 0.3 ? 0 : (cover < 0.7 ? 1 : 2); }
        public static int RadiationClass(double ri) { return ri < 0.4 ? 0 : (ri < 0.6 ? 1 : 2); }

        /// <summary>Assign every cell to a stratum from its ecoregion, canopy cover and topography. Called after the annual fuelbed build.</summary>
        public void AssignCells()
        {
            for (int i = 1; i < CellData.Count; i++)
            {
                int e = CellData.EcoregionIndex[i];
                int c = CoverClass(CellData.CanopyCover[i]);
                int r = RadiationClass(FuelMoisture.RadiationIndex(CellData.SlopeDeg[i], CellData.AspectDeg[i]));
                CellStratum[i] = (ushort)(e * 9 + c * 3 + r);
            }
        }

        /// <summary>Update all strata for one day.</summary>
        public void Update(Weather weather, int day0)
        {
            for (int e = 0; e < ecoCount; e++)
            {
                if (!weather.HasClimate(e)) continue;
                DailyWeather w = weather.Get(e, day0);
                double m1eco = w.Valid ? FuelMoisture.FromFFMC(w.FFMC) : 0.35;
                double m100eco = w.Valid ? Math.Min(FuelMoisture.FromDMC(w.DMC), 0.60) : 0.60;
                double D = w.Valid ? FuelMoisture.DroughtIndex(w.DC, p.DroughtCodeLow, p.DroughtCodeHigh) : 0.0;
                double m1000 = FuelMoisture.FromDrought(D, p.ThousandHourMoistureMin, p.ThousandHourMoistureMax);
                double mduff = FuelMoisture.FromDrought(D, p.DuffMoistureMin, p.DuffMoistureMax);
                double green = FuelMoisture.HerbGreenness(day0, weather.BeginGrowing[e], weather.EndGrowing[e], D);
                double mherb = p.LiveHerbMoistureMin + (p.LiveHerbMoistureMax - p.LiveHerbMoistureMin) * green * (1.0 - D);
                double mwoody = FuelMoisture.FromDrought(D, p.LiveWoodyMoistureMin, p.LiveWoodyMoistureMax);
                double mfol = FuelMoisture.ConiferFoliarMoisturePct(day0 + 1, p.Latitude, p.Longitude, D, p.FoliarMoistureDroughtEffect) / 100.0;

                for (int c = 0; c < CoverClasses; c++)
                    for (int r = 0; r < RadiationClasses; r++)
                    {
                        int s = e * 9 + c * 3 + r;
                        double mod = FuelMoisture.SiteModifier(coverMid[c], radMid[r], p.ShadeModifier, p.TopographyModifier);
                        FuelMoistureState st = state[s];
                        double m1 = Math.Min(m1eco * mod, 2.5);
                        st.M1h = m1;
                        st.M10h = initialized ? FuelMoisture.LagTowards(st.M10h, m1, 10) : m1;
                        st.M100h = initialized ? FuelMoisture.LagTowards(st.M100h, Math.Max(m1 * 1.3, 0.06), 100) : m100eco * mod;
                        st.M1000h = m1000;
                        st.MDuff = mduff * Math.Max(mod, 1.0);
                        st.MHerb = mherb;
                        st.MWoody = mwoody;
                        st.MFoliar = mfol;
                        state[s] = st;
                        drought[s] = D;
                    }
            }
            initialized = true;
        }

        public FuelMoistureState Get(int cellIndex) { return state[CellStratum[cellIndex]]; }
        public double Drought(int cellIndex) { return drought[CellStratum[cellIndex]]; }
        public FuelMoistureState GetStratum(int s) { return state[s]; }
        public int Count { get { return strataCount; } }
    }
}
