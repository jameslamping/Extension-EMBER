// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
//
// Fire events and the minimum-travel-time spread engine. A fire is grown one day at a
// time: every burning-perimeter cell seeds a binary heap with arrival times of its
// unburned neighbours computed from today's elliptical rate of spread; cells reached
// within today's burn period burn (and receive effects immediately); the rest are
// recomputed tomorrow with tomorrow's weather.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class FireEvent
    {
        public int Id;
        public IgnitionCause Cause;
        public int IgnitionDay;           // 0-based day of year
        public int IgnitionCell;
        public int Ecoregion;
        public double IgnitionFWI;
        public List<int> Cells = new List<int>();
        public List<int> Frontier = new List<int>();
        public int DaysActive;
        public int IdleDays;
        public bool Ended;
        public bool Contained;
        public string EndReason = "";
        public int EndDay;
        public bool SuppressedToday;

        // statistics
        public double MaxRos, SumRos, MaxIntensity, SumIntensity, MaxFlame;
        public int CellsSurface, CellsPassive, CellsActive;
        public double SumConsumption, SumSurfaceConsumption, SumDuffConsumption, SumCanopyConsumption;
        public double SumFlaming, SumSmoldering, SumPM25, SumCO2;
        public double SumKilledBiomass;          // kg m^-2 summed over cells
        public double SumCanopyMortality;         // fraction summed
        public int HighSeverityCells;
        public int CohortsKilled, CohortsPartial;
        public double MaxDayIntensity, MaxDayRos;
        public int CellsToday;
        public double BurnHoursToday;
        public int PeakDay; public double PeakDayFWI;

        public double AreaHa(double cellArea) { return Cells.Count * cellArea; }
        public double MeanRos { get { return Cells.Count > 0 ? SumRos / Cells.Count : 0; } }
        public double MeanIntensity { get { return Cells.Count > 0 ? SumIntensity / Cells.Count : 0; } }
    }

    /// <summary>Binary min-heap of (time, cell).</summary>
    public class TimeHeap
    {
        double[] t = new double[1024];
        int[] c = new int[1024];
        int n;
        public int Count { get { return n; } }
        public void Clear() { n = 0; }
        public void Push(double time, int cell)
        {
            if (n == t.Length) { Array.Resize(ref t, n * 2); Array.Resize(ref c, n * 2); }
            int i = n++;
            t[i] = time; c[i] = cell;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (t[parent] <= t[i]) break;
                Swap(i, parent); i = parent;
            }
        }
        public void Pop(out double time, out int cell)
        {
            time = t[0]; cell = c[0];
            n--;
            if (n > 0)
            {
                t[0] = t[n]; c[0] = c[n];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < n && t[l] < t[m]) m = l;
                    if (r < n && t[r] < t[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
            }
        }
        void Swap(int a, int b) { double tt = t[a]; t[a] = t[b]; t[b] = tt; int cc = c[a]; c[a] = c[b]; c[b] = cc; }
    }

    public class Spread
    {
        readonly ICore core;
        readonly InputParameters p;
        readonly FuelbedBuilder builder;
        readonly MoistureStrata strata;
        readonly Effects effects;
        public Weather Weather;
        public int Year;
        readonly TimeHeap heap = new TimeHeap();

        // 8 neighbours per cell: N, NE, E, SE, S, SW, W, NW
        public static int[] Neighbors;                      // Count*8, 0 = none
        static readonly double[] NeighborDir = { 0, 45, 90, 135, 180, 225, 270, 315 };
        static readonly int[] DRow = { -1, -1, 0, 1, 1, 1, 0, -1 };
        static readonly int[] DCol = { 0, 1, 1, 1, 0, -1, -1, -1 };
        double[] neighborDist;                              // m, cardinal or diagonal

        public Spread(ICore core, InputParameters p, FuelbedBuilder builder, MoistureStrata strata, Effects effects)
        {
            this.core = core; this.p = p; this.builder = builder; this.strata = strata; this.effects = effects;
            double L = core.CellLength;
            neighborDist = new double[8];
            for (int k = 0; k < 8; k++) neighborDist[k] = (k % 2 == 0) ? L : L * Math.Sqrt(2.0);
        }

        public static void BuildNeighbors(ICore core)
        {
            Neighbors = new int[CellData.Count * 8];
            for (int i = 1; i < CellData.Count; i++)
            {
                ActiveSite s = CellData.Site[i];
                for (int k = 0; k < 8; k++)
                {
                    Site nb = s.GetNeighbor(new RelativeLocation(DRow[k], DCol[k]));
                    Neighbors[i * 8 + k] = (nb && nb.IsActive) ? (int)nb.DataIndex : 0;
                }
            }
        }

        // ---- daily behavior cache -------------------------------------------------------

        /// <summary>Compute (or fetch) today's fire behavior for a cell; returns false if the cell cannot burn today.</summary>
        public bool Behavior(int i, int day0)
        {
            if (CellData.BehaviorDay[i] == day0) return CellData.CacheBurnable[i];
            CellData.BehaviorDay[i] = day0;
            CellData.CacheBurnable[i] = false;
            if (CellData.BurnedYear[i] == Year) return false;
            int e = CellData.EcoregionIndex[i];
            if (!Weather.HasClimate(e)) return false;
            DailyWeather w = Weather.Get(e, day0);
            if (!w.Valid) return false;
            SurfaceFuel fuel = builder.SurfaceFuelOf(i);
            if (fuel.FineDeadLoad + fuel.WHerb < p.MinimumFineFuelLoad) return false;
            FuelMoistureState m = strata.Get(i);
            double waf = Rothermel.WindAdjustmentFactor(CellData.CanopyCover[i], p.WAFOpen, p.WAFSheltered);
            double wind = w.WindSpeed_kmh * p.WindGustFactor;
            SurfaceFireResult surf = Rothermel.Compute(fuel, m, wind * waf, w.WindTo_deg, CellData.SlopeDeg[i], CellData.AspectDeg[i]);
            if (!surf.Burnable) return false;
            CanopyFuel canopy = builder.CanopyFuelOf(i);
            double bl = CellData.BroadleafFraction[i];
            double fmcPct = (m.MFoliar * (1.0 - bl) + p.BroadleafFoliarMoisture * bl) * 100.0;
            // surface fuel consumed today (for the Cruz 2004 crown initiation model) and the flaming share
            // of the 1000-h consumption, whose heat is added to the fireline intensity
            ConsumptionResult cons = Consumption.Compute(p.Consumption, fuel, CellData.W1000h[i], CellData.Duff[i], m, 0.0, true);
            double sfc = cons.SurfaceTotal + cons.Duff;
            double extraHpa = p.CoarseFuelFlamingFraction * cons.Surface1000h * fuel.HeatContent;
            FireBehaviorResult r = CrownFire.Evaluate(surf, canopy, fmcPct, wind, m.M1h * 100.0, p.AllowCrownFire,
                                                      p.CrownInitiation, sfc, extraHpa);
            double noise = Rng.LogNormalFactor(p.ROSNoiseSigma);
            double ros = r.FinalRos_mMin * noise;
            double intensity = r.FinalIntensity_kWm * noise;
            if (intensity < p.ExtinctionIntensity) return false;
            CellData.CacheRos[i] = (float)ros;
            CellData.CacheDir[i] = (float)r.HeadDirection_deg;
            CellData.CacheEcc[i] = (float)r.Eccentricity;
            CellData.CacheIntensity[i] = (float)intensity;
            CellData.CacheFlame[i] = (float)(r.CrownClass == CrownFireClass.Active ? Rothermel.ThomasFlameLength(intensity) : Rothermel.ByramFlameLength(intensity));
            CellData.CacheCfb[i] = (float)r.CrownFractionBurned;
            CellData.CacheCrown[i] = (byte)r.CrownClass;
            CellData.CacheHpa[i] = (float)r.HeatPerUnitArea_kJm2;
            CellData.CacheBurnable[i] = true;
            return true;
        }

        double DirectionalRos(int i, int k)
        {
            double theta = Units.AngleDiff(NeighborDir[k], CellData.CacheDir[i]);
            return Ellipse.DirectionalRos(CellData.CacheRos[i], CellData.CacheEcc[i], theta);
        }

        /// <summary>
        /// Travel time in minutes from burning cell i to neighbour j in direction k; +inf if j
        /// cannot burn today. Uses the receiving cell's directional rate of spread (FlamMap
        /// convention) and, when the source burned today and its pre-fire behavior is still
        /// cached, the harmonic mean of both.
        /// </summary>
        double TravelTime(int i, int j, int k, int day0)
        {
            if (!Behavior(j, day0)) return double.PositiveInfinity;
            double rj = DirectionalRos(j, k);
            if (rj <= 1e-6) return double.PositiveInfinity;
            bool srcValid = CellData.BehaviorDay[i] == day0 && CellData.CacheBurnable[i];
            if (!srcValid) return neighborDist[k] / rj;
            double ri = DirectionalRos(i, k);
            if (ri <= 1e-6) return neighborDist[k] / rj;
            return 0.5 * neighborDist[k] * (1.0 / ri + 1.0 / rj);
        }

        /// <summary>
        /// Active burning hours for the day: zero below the low FWI threshold or on a rain day,
        /// otherwise linear between the minimum and maximum hours up to the high FWI threshold.
        /// </summary>
        public double BurnPeriodHours(DailyWeather w)
        {
            if (!w.Valid) return 0.0;
            if (w.FWI < p.BurnPeriodFWILow) return 0.0;
            if (w.Precip_cm >= p.RainoutPrecipitation) return 0.0;
            double f = Units.Clamp01((w.FWI - p.BurnPeriodFWILow) / Math.Max(p.BurnPeriodFWIHigh - p.BurnPeriodFWILow, 1e-6));
            return p.BurnPeriodMinHours + (p.BurnPeriodMaxHours - p.BurnPeriodMinHours) * f;
        }

        // ---- fire lifecycle ---------------------------------------------------------------

        /// <summary>Attempt to start a fire at a cell. Returns null if the cell cannot burn today.</summary>
        public FireEvent Start(int id, int cell, IgnitionCause cause, int day0)
        {
            if (CellData.BurnedYear[cell] == Year) return null;
            if (p.IgnitionRequiresBurnPeriod && BurnPeriodHours(Weather.Get(CellData.EcoregionIndex[cell], day0)) <= 0.0) return null;
            if (!Behavior(cell, day0))
            {
                // The cell cannot carry fire today. With IgnitionAlwaysBurns the attempt is still a
                // recorded fire (the convention of fire records, to which ignition coefficients are
                // fitted): it burns its cell with negligible behavior and ends.
                if (!p.IgnitionAlwaysBurns) return null;
                int eco = CellData.EcoregionIndex[cell];
                if (!Weather.HasClimate(eco) || !Weather.Get(eco, day0).Valid) return null;
                CellData.CacheRos[cell] = 0; CellData.CacheIntensity[cell] = 0; CellData.CacheFlame[cell] = 0;
                CellData.CacheCfb[cell] = 0; CellData.CacheCrown[cell] = (byte)CrownFireClass.Surface; CellData.CacheHpa[cell] = 0;
                CellData.CacheDir[cell] = 0; CellData.CacheEcc[cell] = 0;
                FireEvent u = new FireEvent();
                u.Id = id; u.Cause = cause; u.IgnitionDay = day0; u.IgnitionCell = cell;
                u.Ecoregion = eco; u.IgnitionFWI = Weather.Get(eco, day0).FWI;
                u.EndReason = "Unsustained";
                BurnCell(u, cell, day0, 0.0);
                return u;
            }
            FireEvent f = new FireEvent();
            f.Id = id; f.Cause = cause; f.IgnitionDay = day0; f.IgnitionCell = cell;
            f.Ecoregion = CellData.EcoregionIndex[cell];
            f.IgnitionFWI = Weather.Get(f.Ecoregion, day0).FWI;
            BurnCell(f, cell, day0, 0.0);
            f.Frontier.Add(cell);
            return f;
        }

        void BurnCell(FireEvent f, int i, int day0, double minutes)
        {
            CellData.BurnedYear[i] = Year;
            CellData.EventId[i] = f.Id;
            CellData.DayOfBurn[i] = (short)(day0 + 1);
            CellData.Ros[i] = CellData.CacheRos[i];
            CellData.Intensity[i] = CellData.CacheIntensity[i];
            CellData.FlameLength[i] = CellData.CacheFlame[i];
            CellData.Cfb[i] = CellData.CacheCfb[i];
            CellData.CrownClass[i] = CellData.CacheCrown[i];
            f.Cells.Add(i);
            f.CellsToday++;
            double ros = CellData.CacheRos[i], I = CellData.CacheIntensity[i];
            f.SumRos += ros; f.SumIntensity += I;
            if (ros > f.MaxRos) f.MaxRos = ros;
            if (I > f.MaxIntensity) f.MaxIntensity = I;
            if (I > f.MaxDayIntensity) f.MaxDayIntensity = I;
            if (ros > f.MaxDayRos) f.MaxDayRos = ros;
            if (CellData.CacheFlame[i] > f.MaxFlame) f.MaxFlame = CellData.CacheFlame[i];
            switch ((CrownFireClass)CellData.CacheCrown[i])
            {
                case CrownFireClass.Active: f.CellsActive++; break;
                case CrownFireClass.Passive: f.CellsPassive++; break;
                default: f.CellsSurface++; break;
            }
            effects.Apply(f, i, day0);
        }

        /// <summary>Grow the fire for one day. Returns cells burned today.</summary>
        public int RunDay(FireEvent f, int day0)
        {
            f.CellsToday = 0; f.MaxDayIntensity = 0; f.MaxDayRos = 0;
            DailyWeather w = Weather.Get(f.Ecoregion, day0);
            double hours = BurnPeriodHours(w);
            f.BurnHoursToday = hours;
            double limit = hours * 60.0;
            f.DaysActive++;
            if (limit <= 0) { f.IdleDays++; return 0; }

            heap.Clear();
            foreach (int i in f.Frontier)
                SeedNeighbors(f, i, day0, 0.0);
            while (heap.Count > 0)
            {
                double t; int j;
                heap.Pop(out t, out j);
                if (t > limit) break;
                if (CellData.BurnedYear[j] == Year) continue;
                BurnCell(f, j, day0, t);
                SeedNeighbors(f, j, day0, t);
            }
            if (f.CellsToday == 0) f.IdleDays++; else f.IdleDays = 0;
            if (f.CellsToday > 0 && (f.PeakDay == 0 || f.MaxDayIntensity > f.PeakDayFWI)) { f.PeakDay = day0 + 1; f.PeakDayFWI = w.FWI; }
            RebuildFrontier(f);
            return f.CellsToday;
        }

        void SeedNeighbors(FireEvent f, int i, int day0, double t0)
        {
            int baseIdx = i * 8;
            for (int k = 0; k < 8; k++)
            {
                int j = Neighbors[baseIdx + k];
                if (j == 0 || CellData.BurnedYear[j] == Year) continue;
                double dt = TravelTime(i, j, k, day0);
                if (double.IsPositiveInfinity(dt)) continue;
                heap.Push(t0 + dt, j);
            }
        }

        void RebuildFrontier(FireEvent f)
        {
            f.Frontier.Clear();
            foreach (int i in f.Cells)
            {
                int baseIdx = i * 8;
                for (int k = 0; k < 8; k++)
                {
                    int j = Neighbors[baseIdx + k];
                    if (j != 0 && CellData.BurnedYear[j] != Year) { f.Frontier.Add(i); break; }
                }
            }
        }
    }
}
