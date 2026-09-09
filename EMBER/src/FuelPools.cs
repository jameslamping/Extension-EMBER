// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
//
// Extension-owned "behavior" fuel pools: 100-h and 1000-h coarse wood, duff, and (when
// not synced to succession) fine litter. Inputs come from cohort mortality events raised
// by the cohort library, plus a background wood mortality term for the growth-related
// mortality that succession extensions apply without raising events.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;

namespace Landis.Extension.EMBER
{
    public class FuelPools
    {
        readonly ICore core;
        readonly InputParameters p;
        bool initialized;

        public FuelPools(ICore core, InputParameters p)
        {
            this.core = core;
            this.p = p;
        }

        public void Subscribe()
        {
            Cohort.MortalityEvent += OnCohortMortality;
        }

        /// <summary>
        /// Cohort library event. For fire kills of a fraction of a cohort, scale the
        /// succession extension's leaf/wood attributes (the library only reduces Biomass and
        /// NECN rebuilds Biomass from those attributes). For non-fire mortality, accumulate
        /// dead wood and foliage inputs to the behavior pools.
        /// </summary>
        void OnCohortMortality(object sender, MortalityEventArgs e)
        {
            ICohort cohort = e.Cohort;
            if (cohort == null) return;
            double fraction = e.FractionBiomassReduction;
            bool fire = e.DisturbanceType != null && e.DisturbanceType.IsMemberOf("disturbance:fire");
            if (fire)
            {
                if (fraction > 0.0 && fraction < 1.0)
                    ScaleLeafWood(cohort, 1.0 - fraction);
                return;   // fire consumption and residue are handled by Effects
            }
            if (fraction <= 0) return;
            int i = (int)e.Site.DataIndex;
            if (i <= 0 || i >= CellData.Count) return;
            SpeciesFireParameters sp = p.SpeciesParameters[cohort.Species.Index];
            double fol, wood;
            FuelbedBuilder.FoliageAndWood(cohort, sp, out fol, out wood);
            AddInputs(i, sp, fol * fraction, wood * fraction);
        }

        public static void AddInputs(int i, SpeciesFireParameters sp, double foliage_kgm2, double wood_kgm2)
        {
            if (sp.LifeForm == LifeForm.Tree)
            {
                CellData.WoodInput1000h[i] += (float)(wood_kgm2 * sp.CoarseWoodFraction1000h);
                CellData.WoodInput100h[i] += (float)(wood_kgm2 * (1.0 - sp.CoarseWoodFraction1000h));
            }
            else
            {
                CellData.WoodInput100h[i] += (float)(wood_kgm2 * 0.5);
                CellData.FoliageInput[i] += (float)(wood_kgm2 * 0.5);
            }
            CellData.FoliageInput[i] += (float)foliage_kgm2;
        }

        /// <summary>Multiply NECN-style LeafBiomass / WoodBiomass attributes by a factor, preserving their stored type.</summary>
        public static void ScaleLeafWood(ICohort cohort, double factor)
        {
            object extra = cohort.Data.AdditionalParameters;
            IDictionary<string, object> dict = extra as IDictionary<string, object>;
            if (dict == null) return;
            ScaleKey(dict, "LeafBiomass", factor);
            ScaleKey(dict, "WoodBiomass", factor);
        }

        static void ScaleKey(IDictionary<string, object> dict, string key, double factor)
        {
            object v;
            if (!dict.TryGetValue(key, out v) || v == null) return;
            double d = Convert.ToDouble(v) * factor;
            try { dict[key] = Convert.ChangeType(d, v.GetType()); }
            catch { dict[key] = d; }
        }

        /// <summary>Annual pool update, run before the fuelbed is rebuilt.</summary>
        public void AnnualUpdate(int year)
        {
            int n = CellData.Count;
            if (!initialized)
            {
                for (int i = 1; i < n; i++)
                {
                    CellData.W100h[i] = (float)p.InitialCoarseWood100h;
                    CellData.W1000h[i] = (float)p.InitialCoarseWood1000h;
                    CellData.Duff[i] = (float)p.InitialDuff;
                    CellData.Litter[i] = (float)(CellData.Foliage[i] * p.LitterInputFraction / Math.Max(p.LitterDecay, 0.05));
                }
                initialized = true;
            }
            for (int i = 1; i < n; i++)
            {
                // background wood mortality not seen through events
                double bg = CellData.Foliage[i] > 0 ? p.BackgroundWoodMortality * WoodProxy(i) : 0.0;
                CellData.W100h[i] = (float)(CellData.W100h[i] * (1.0 - p.CoarseWoodDecay100h) + CellData.WoodInput100h[i] + 0.3 * bg);
                CellData.W1000h[i] = (float)(CellData.W1000h[i] * (1.0 - p.CoarseWoodDecay1000h) + CellData.WoodInput1000h[i] + 0.7 * bg);
                double litter = p.SyncFineFuelsToSuccession && SiteVars.SuccessionFineFuels != null
                    ? Math.Max(0.0, SiteVars.SuccessionFineFuels[CellData.Site[i]]) / 1000.0
                    : CellData.Litter[i];
                CellData.Duff[i] = (float)(CellData.Duff[i] * (1.0 - p.DuffDecay) + p.DuffInputFraction * litter);
                if (!(p.SyncFineFuelsToSuccession && SiteVars.SuccessionFineFuels != null))
                    CellData.Litter[i] = (float)(CellData.Litter[i] * (1.0 - p.LitterDecay) + p.LitterInputFraction * CellData.Foliage[i] + CellData.FoliageInput[i]);
                CellData.WoodInput100h[i] = 0; CellData.WoodInput1000h[i] = 0; CellData.FoliageInput[i] = 0;
                if (CellData.W100h[i] < 0) CellData.W100h[i] = 0;
                if (CellData.W1000h[i] < 0) CellData.W1000h[i] = 0;
                if (CellData.Duff[i] < 0) CellData.Duff[i] = 0;
            }
        }

        /// <summary>Rough live wood biomass proxy from foliage (kg m^-2) when only foliage is tracked per cell.</summary>
        static double WoodProxy(int i)
        {
            return CellData.Foliage[i] * 12.0;   // foliage is ~5-8 % of tree biomass
        }
    }
}
