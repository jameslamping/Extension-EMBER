// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
//
// Annual construction of the per-cell fuelbed from cohorts and litter pools:
// surface dead fuel by time-lag class, live herb/shrub fuel, and the canopy fuel profile
// (effective canopy bulk density and base height following Scott & Reinhardt 2001).
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class FuelbedBuilder
    {
        readonly ICore core;
        readonly InputParameters p;
        const int Bins = 80;                 // 1-m canopy bins
        readonly double[] profile = new double[Bins];
        readonly double[] litterByFuelbed;

        public FuelbedBuilder(ICore core, InputParameters p)
        {
            this.core = core;
            this.p = p;
            litterByFuelbed = new double[p.FuelbedList.Count];
        }

        /// <summary>Foliage and wood biomass (kg m^-2) of a cohort, from NECN's attributes when present.</summary>
        public static void FoliageAndWood(ICohort cohort, SpeciesFireParameters sp, out double foliage, out double wood)
        {
            double total = cohort.Data.Biomass / 1000.0;   // g m^-2 -> kg m^-2
            object extra = cohort.Data.AdditionalParameters;
            IDictionary<string, object> dict = extra as IDictionary<string, object>;
            if (dict != null)
            {
                object leaf, wd;
                if (dict.TryGetValue("LeafBiomass", out leaf) && dict.TryGetValue("WoodBiomass", out wd))
                {
                    foliage = Convert.ToDouble(leaf) / 1000.0;
                    wood = Convert.ToDouble(wd) / 1000.0;
                    if (foliage >= 0 && wood >= 0) return;
                }
            }
            foliage = total * sp.FoliageFraction;
            wood = total - foliage;
        }

        /// <summary>Build fuels for all active cells for the coming fire season. Pools (100-h, 1000-h, duff, litter) are updated by FuelPools before this call.</summary>
        public void BuildAll(int year)
        {
            foreach (ActiveSite site in core.Landscape.ActiveSites)
                Build(site, year);
        }

        void Build(ActiveSite site, int year)
        {
            int i = (int)site.DataIndex;
            SiteCohorts cohorts = SiteVars.Cohorts[site];
            Array.Clear(profile, 0, Bins);
            Array.Clear(litterByFuelbed, 0, litterByFuelbed.Length);

            double foliageTree = 0, herb = 0, shrub = 0, ladder = 0, lai = 0, broadleaf = 0;
            double topHeight = 0;

            if (cohorts != null)
            {
                foreach (ISpeciesCohorts sc in cohorts)
                {
                    SpeciesFireParameters sp = p.SpeciesParameters[sc.Species.Index];
                    foreach (ICohort c in sc)
                    {
                        double fol, wood;
                        FoliageAndWood(c, sp, out fol, out wood);
                        if (fol <= 0 && wood <= 0) continue;
                        litterByFuelbed[sp.Fuelbed.Index] += fol + 0.2 * wood;   // litter source weighting
                        lai += fol * sp.SpecificLeafArea;

                        if (sp.LifeForm == LifeForm.Grass) { herb += fol + wood; continue; }
                        if (sp.LifeForm == LifeForm.Shrub) { shrub += fol + 0.3 * wood; ladder += fol; continue; }

                        // tree cohort: distribute canopy fuel between crown base and top
                        double h = Mortality.Height(sp.MaxHeight, sp.HeightK, c.Data.Age);
                        if (h < 0.5) h = 0.5;
                        double cbh = h * (1.0 - sp.CrownRatio);
                        double canopyFuel = fol * (1.0 + sp.FineBranchFraction);
                        foliageTree += fol;
                        if (sp.FoliarClass == FoliarMoistureClass.Broadleaf) broadleaf += fol;
                        if (h > topHeight) topHeight = h;
                        if (h <= p.LadderMaxHeight) ladder += canopyFuel;
                        int b0 = (int)Math.Floor(cbh), b1 = (int)Math.Ceiling(h);
                        if (b1 <= b0) b1 = b0 + 1;
                        if (b1 > Bins) b1 = Bins;
                        if (b0 >= Bins) b0 = Bins - 1;
                        double perBin = canopyFuel / (b1 - b0);
                        for (int b = b0; b < b1; b++) profile[b] += perBin;
                    }
                }
            }

            // dominant litter class -> fuelbed type
            int best = 0; double bestW = -1;
            for (int k = 0; k < litterByFuelbed.Length; k++)
                if (litterByFuelbed[k] > bestW) { bestW = litterByFuelbed[k]; best = k; }
            FuelbedType fb = p.FuelbedList[best];
            CellData.FuelbedIndex[i] = (byte)best;

            // surface fine fuel: synced from succession litter, or the extension's own litter pool
            double litter;
            if (p.SyncFineFuelsToSuccession && SiteVars.SuccessionFineFuels != null)
                litter = Math.Max(0.0, SiteVars.SuccessionFineFuels[site]) / 1000.0;
            else
                litter = CellData.Litter[i];
            if (litter > p.MaxFuelbedDepthLoad) litter = p.MaxFuelbedDepthLoad;
            CellData.W1h[i] = (float)(litter * fb.LitterFraction1h);
            CellData.W10h[i] = (float)(litter * (1.0 - fb.LitterFraction1h));
            CellData.WHerb[i] = (float)herb;
            CellData.WWoody[i] = (float)shrub;
            CellData.Foliage[i] = (float)foliageTree;
            CellData.BroadleafFraction[i] = (float)(foliageTree > 0 ? broadleaf / foliageTree : 0.0);

            // canopy profile -> effective CBD (max 3-m running mean) and CBH (lowest bin above threshold)
            double cfl = 0, maxRun = 0; int cbhBin = -1, topBin = 0;
            for (int b = 0; b < Bins; b++)
            {
                cfl += profile[b];
                if (profile[b] > 0) topBin = b + 1;
                double run = (profile[b] + (b + 1 < Bins ? profile[b + 1] : 0) + (b + 2 < Bins ? profile[b + 2] : 0)) / 3.0;
                if (run > maxRun) maxRun = run;
                if (cbhBin < 0 && run >= p.CBDThreshold) cbhBin = b;
            }
            CellData.CanopyFuelLoad[i] = (float)cfl;
            CellData.CBD[i] = (float)maxRun;                       // kg m^-3 (1-m bins, per m^2)
            CellData.CBH[i] = (float)(cbhBin < 0 ? Math.Max(topHeight, 1.0) : cbhBin);
            CellData.CanopyHeight[i] = (float)Math.Max(topBin, topHeight);
            CellData.LadderFuel[i] = (float)ladder;
            CellData.CanopyCover[i] = (float)(1.0 - Math.Exp(-0.5 * lai));
        }

        public SurfaceFuel SurfaceFuelOf(int i)
        {
            FuelbedType fb = p.FuelbedList[CellData.FuelbedIndex[i]];
            SurfaceFuel f = new SurfaceFuel();
            f.W1h = CellData.W1h[i]; f.W10h = CellData.W10h[i]; f.W100h = CellData.W100h[i];
            f.WHerb = CellData.WHerb[i]; f.WWoody = CellData.WWoody[i];
            f.Sav1h = fb.Sav1h; f.Sav10h = fb.Sav10h; f.Sav100h = fb.Sav100h; f.SavHerb = fb.SavHerb; f.SavWoody = fb.SavWoody;
            f.HeatContent = fb.HeatContent; f.MoistureOfExtinction = fb.MoistureOfExtinction; f.PackingRatio = fb.PackingRatio;
            return f;
        }

        public CanopyFuel CanopyFuelOf(int i)
        {
            CanopyFuel c = new CanopyFuel();
            c.CanopyBaseHeight_m = CellData.CBH[i];
            c.CanopyBulkDensity_kgm3 = CellData.CBD[i];
            c.CanopyFuelLoad_kgm2 = CellData.CanopyFuelLoad[i];
            c.CanopyHeight_m = CellData.CanopyHeight[i];
            c.CanopyCover = CellData.CanopyCover[i];
            c.LadderFuel_kgm2 = CellData.LadderFuel[i];
            return c;
        }
    }
}
