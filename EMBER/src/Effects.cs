// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
//
// First-order fire effects for a burned cell: fuel consumption, emissions, crown scorch,
// cohort mortality (fractional or whole-cohort), severity class for the succession
// extension, and pool bookkeeping.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class Effects : IDisturbance
    {
        readonly ICore core;
        readonly InputParameters p;
        readonly FuelbedBuilder builder;
        readonly MoistureStrata strata;
        public Weather Weather;
        public Logs Logs;
        public int Year;

        // per-cell context for the IDisturbance callback
        ActiveSite currentSite;
        double scorchHeight;
        double cfb;
        bool activeCrown;
        bool applyPass;
        double killedTreeBiomass, totalTreeBiomass, killedFoliage, killedWood;
        int cohortsKilled, cohortsPartial;

        public Effects(ICore core, InputParameters p, FuelbedBuilder builder, MoistureStrata strata)
        {
            this.core = core; this.p = p; this.builder = builder; this.strata = strata;
        }

        ExtensionType IDisturbance.Type { get { return PlugIn.ExtType; } }
        ActiveSite IDisturbance.CurrentSite { get { return currentSite; } }

        public void Apply(FireEvent f, int i, int day0)
        {
            ActiveSite site = CellData.Site[i];
            currentSite = site;
            DailyWeather w = Weather.Get(CellData.EcoregionIndex[i], day0);
            FuelMoistureState m = strata.Get(i);
            SurfaceFuel fuel = builder.SurfaceFuelOf(i);
            cfb = CellData.CacheCfb[i];
            activeCrown = CellData.CacheCrown[i] == (byte)CrownFireClass.Active;
            double intensity = CellData.CacheIntensity[i];
            scorchHeight = Mortality.ScorchHeight(intensity, w.WindSpeed_kmh, w.Tmax);

            // consumption
            double canopyConsumed = cfb * CellData.CanopyFuelLoad[i];
            ConsumptionResult cons = Consumption.Compute(p.Consumption, fuel, CellData.W1000h[i], CellData.Duff[i], m, canopyConsumed, true);
            double surfaceAvailable = fuel.TotalLoad + CellData.W1000h[i] + CellData.Duff[i];
            double surfaceFraction = surfaceAvailable > 0 ? (cons.SurfaceTotal + cons.Duff) / surfaceAvailable : 0.0;

            // pass 1: expected mortality (no changes) -> severity for the succession extension
            SiteCohorts cohorts = SiteVars.Cohorts[site];
            killedTreeBiomass = totalTreeBiomass = killedFoliage = killedWood = 0;
            cohortsKilled = cohortsPartial = 0;
            applyPass = false;
            if (cohorts != null)
                foreach (ISpeciesCohorts sc in cohorts)
                    foreach (ICohort c in sc)
                        MortalityOf(c);
            double canopyMortality = totalTreeBiomass > 0 ? killedTreeBiomass / totalTreeBiomass : 0.0;
            double severity = 0.5 * canopyMortality + 0.3 * cfb + 0.2 * surfaceFraction;
            int cls = 1 + (int)(Units.Clamp01(severity) * p.SeverityClasses);
            if (cls > p.SeverityClasses) cls = p.SeverityClasses;
            if (cls < 1) cls = 1;
            SiteVars.Severity[site] = (byte)cls;
            SiteVars.TimeOfLastFire[site] = Year;
            SiteVars.EventID[site] = f.Id;
            SiteVars.Cause[site] = (byte)(f.Cause + 1);
            SiteVars.FirelineIntensity[site] = intensity;
            SiteVars.FlameLength[site] = CellData.CacheFlame[i];
            SiteVars.CrownFractionBurned[site] = cfb;
            SiteVars.SurfaceConsumption[site] = cons.SurfaceTotal;
            SiteVars.DuffConsumption[site] = cons.Duff;
            SiteVars.CanopyConsumption[site] = cons.Canopy;

            // pass 2: apply kills through the cohort library (raises NECN's mortality handler)
            killedTreeBiomass = totalTreeBiomass = killedFoliage = killedWood = 0;
            cohortsKilled = cohortsPartial = 0;
            applyPass = true;
            if (cohorts != null) cohorts.ReduceOrKillCohorts(this);
            canopyMortality = totalTreeBiomass > 0 ? killedTreeBiomass / totalTreeBiomass : 0.0;
            SiteVars.CanopyMortalityFraction[site] = canopyMortality;

            // pool bookkeeping (kg m^-2)
            CellData.W100h[i] = (float)Math.Max(0, CellData.W100h[i] - cons.Surface100h);
            CellData.W1000h[i] = (float)Math.Max(0, CellData.W1000h[i] - cons.Surface1000h + 0.5 * killedWood);
            CellData.Duff[i] = (float)Math.Max(0, CellData.Duff[i] - cons.Duff);
            CellData.W1h[i] = (float)Math.Max(0, CellData.W1h[i] - cons.Surface1h);
            CellData.W10h[i] = (float)Math.Max(0, CellData.W10h[i] - cons.Surface10h);
            CellData.WHerb[i] = (float)Math.Max(0, CellData.WHerb[i] - cons.LiveHerb);
            CellData.WWoody[i] = (float)Math.Max(0, CellData.WWoody[i] - cons.LiveWoody);
            CellData.Litter[i] = (float)Math.Max(0, CellData.Litter[i] - cons.Surface1h - cons.Surface10h + killedFoliage * (1.0 - p.Consumption.KilledFoliageConsumed * cfb));
            CellData.CanopyFuelLoad[i] = (float)Math.Max(0, CellData.CanopyFuelLoad[i] - canopyConsumed - killedFoliage);

            // emissions (g m^-2)
            double pm25 = Consumption.Emission(cons.Flaming, cons.Smoldering, p.Emissions.PM25_Flaming, p.Emissions.PM25_Smoldering);
            double co2 = Consumption.Emission(cons.Flaming, cons.Smoldering, p.Emissions.CO2_Flaming, p.Emissions.CO2_Smoldering);

            // fire statistics
            f.SumConsumption += cons.Total;
            f.SumSurfaceConsumption += cons.SurfaceTotal;
            f.SumDuffConsumption += cons.Duff;
            f.SumCanopyConsumption += cons.Canopy;
            f.SumFlaming += cons.Flaming; f.SumSmoldering += cons.Smoldering;
            f.SumPM25 += pm25; f.SumCO2 += co2;
            f.SumKilledBiomass += killedTreeBiomass;
            f.SumCanopyMortality += canopyMortality;
            if (cls > p.SeverityClasses * 0.7) f.HighSeverityCells++;
            f.CohortsKilled += cohortsKilled; f.CohortsPartial += cohortsPartial;

            CellData.CanopyMortality[i] = (float)canopyMortality;
            CellData.ConsumptionTotal[i] = (float)cons.Total;
            CellData.PM25[i] = (float)pm25;
            CellData.SeverityClass[i] = (byte)cls;

            if (Logs != null && p.WriteCalibrationDiagnostics)
                Logs.WriteCellDiagnostic(Year, f.Id, site, day0, fuel, m, CellData.CacheRos[i], intensity, cfb, CellData.CacheCrown[i], scorchHeight, cons, canopyMortality, cls);
        }

        /// <summary>Mortality fraction for a cohort in the current cell context.</summary>
        double MortalityOf(ICohort cohort)
        {
            SpeciesFireParameters sp = p.SpeciesParameters[cohort.Species.Index];
            double fol, wood;
            FuelbedBuilder.FoliageAndWood(cohort, sp, out fol, out wood);
            double biomass = fol + wood;
            double pm;
            if (sp.LifeForm != LifeForm.Tree)
            {
                pm = 1.0;   // top-kill; resprouting is handled by the succession library
            }
            else
            {
                double h = Math.Max(0.5, Mortality.Height(sp.MaxHeight, sp.HeightK, cohort.Data.Age));
                double cbh = h * (1.0 - sp.CrownRatio);
                double cvs = Mortality.CrownVolumeScorchedPct(scorchHeight, cbh, h);
                if (activeCrown || cfb >= 0.9) cvs = 100.0;
                else if (cfb > 0) cvs = Math.Max(cvs, 100.0 * cfb);
                double bt = Mortality.BarkThicknessCm(sp.BarkThicknessMax, sp.AgeDBH, cohort.Data.Age);
                pm = Mortality.MortalityProbability(bt, cvs);
                if (activeCrown) pm = 1.0;
                totalTreeBiomass += biomass;
            }
            double fraction;
            if (p.CohortMortalityMode == MortalityMode.Fractional)
            {
                fraction = pm;
                if (1.0 - fraction < p.MinimumSurvivingFraction) fraction = 1.0;
                if (fraction < 0.02) fraction = 0.0;
            }
            else
            {
                fraction = applyPass ? (Rng.Uniform() < pm ? 1.0 : 0.0) : pm;
            }
            if (sp.LifeForm == LifeForm.Tree) killedTreeBiomass += biomass * fraction;
            killedFoliage += fol * fraction;
            killedWood += wood * fraction;
            if (applyPass)
            {
                if (fraction >= 1.0) cohortsKilled++;
                else if (fraction > 0) cohortsPartial++;
            }
            return fraction;
        }

        double IDisturbance.ReduceOrKillMarkedCohort(ICohort cohort)
        {
            return MortalityOf(cohort);
        }
    }
}
