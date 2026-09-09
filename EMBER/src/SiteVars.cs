// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
using System;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;

namespace Landis.Extension.EMBER
{
    /// <summary>
    /// Per-site state. Most per-cell numeric state is kept in flat arrays indexed by the
    /// site's DataIndex (see <see cref="CellData"/>) for speed; ISiteVar objects are used
    /// only for what other extensions need through the registry.
    /// </summary>
    public static class SiteVars
    {
        public static ISiteVar<SiteCohorts> Cohorts;
        public static ISiteVar<double> SuccessionFineFuels;   // g m^-2, may be null
        public static ISiteVar<double> SuccessionCWD;         // may be null
        public static ISiteVar<int> HarvestTime;              // may be null

        // registered for other extensions
        public static ISiteVar<byte> Severity;
        public static ISiteVar<int> TimeOfLastFire;
        public static ISiteVar<ushort> Slope;
        public static ISiteVar<ushort> Aspect;
        public static ISiteVar<int> EventID;
        public static ISiteVar<byte> Cause;
        public static ISiteVar<double> FirelineIntensity;
        public static ISiteVar<double> FlameLength;
        public static ISiteVar<double> CrownFractionBurned;
        public static ISiteVar<double> CanopyMortalityFraction;
        public static ISiteVar<double> SurfaceConsumption;
        public static ISiteVar<double> DuffConsumption;
        public static ISiteVar<double> CanopyConsumption;

        public static void Initialize(ICore core)
        {
            Cohorts = core.GetSiteVar<SiteCohorts>("Succession.UniversalCohorts");
            if (Cohorts == null)
                throw new ApplicationException("EMBER requires a succession extension that registers 'Succession.UniversalCohorts'.");
            SuccessionFineFuels = core.GetSiteVar<double>("Succession.FineFuels");
            SuccessionCWD = core.GetSiteVar<double>("Succession.CWD");

            Severity = core.Landscape.NewSiteVar<byte>();
            TimeOfLastFire = core.Landscape.NewSiteVar<int>();
            Slope = core.Landscape.NewSiteVar<ushort>();
            Aspect = core.Landscape.NewSiteVar<ushort>();
            EventID = core.Landscape.NewSiteVar<int>();
            Cause = core.Landscape.NewSiteVar<byte>();
            FirelineIntensity = core.Landscape.NewSiteVar<double>();
            FlameLength = core.Landscape.NewSiteVar<double>();
            CrownFractionBurned = core.Landscape.NewSiteVar<double>();
            CanopyMortalityFraction = core.Landscape.NewSiteVar<double>();
            SurfaceConsumption = core.Landscape.NewSiteVar<double>();
            DuffConsumption = core.Landscape.NewSiteVar<double>();
            CanopyConsumption = core.Landscape.NewSiteVar<double>();
            TimeOfLastFire.ActiveSiteValues = -999;

            core.RegisterSiteVar(Severity, "Fire.Severity");
            core.RegisterSiteVar(TimeOfLastFire, "Fire.TimeOfLastEvent");
            core.RegisterSiteVar(Slope, "Fire.Slope");
            core.RegisterSiteVar(Aspect, "Fire.Aspect");
            core.RegisterSiteVar(EventID, "Fire.EventID");
            core.RegisterSiteVar(Cause, "Fire.Cause");
            core.RegisterSiteVar(FirelineIntensity, "Fire.FirelineIntensity");
            core.RegisterSiteVar(FlameLength, "Fire.FlameLength");
            core.RegisterSiteVar(CrownFractionBurned, "Fire.CrownFractionBurned");
            core.RegisterSiteVar(CanopyMortalityFraction, "Fire.CanopyMortalityFraction");
            core.RegisterSiteVar(SurfaceConsumption, "Fire.SurfaceConsumption");
            core.RegisterSiteVar(DuffConsumption, "Fire.DuffConsumption");
            core.RegisterSiteVar(CanopyConsumption, "Fire.CanopyConsumption");
        }

        /// <summary>Late binding of site variables registered by extensions loaded after EMBER.</summary>
        public static void InitializePhase2(ICore core)
        {
            HarvestTime = core.GetSiteVar<int>("Harvest.TimeOfLastEvent");
        }

        public static void ResetAnnual(ICore core)
        {
            Severity.ActiveSiteValues = 0;
            EventID.ActiveSiteValues = 0;
            Cause.ActiveSiteValues = 0;
            FirelineIntensity.ActiveSiteValues = 0;
            FlameLength.ActiveSiteValues = 0;
            CrownFractionBurned.ActiveSiteValues = 0;
            CanopyMortalityFraction.ActiveSiteValues = 0;
            SurfaceConsumption.ActiveSiteValues = 0;
            DuffConsumption.ActiveSiteValues = 0;
            CanopyConsumption.ActiveSiteValues = 0;
        }
    }

    /// <summary>
    /// Flat per-cell arrays indexed by ActiveSite.DataIndex (1..N; index 0 is the shared
    /// inactive slot). Kept here rather than in ISiteVar objects because the spread
    /// algorithm touches them millions of times a year.
    /// </summary>
    public static class CellData
    {
        public static int Count;                   // = ActiveSiteCount + 1
        public static ActiveSite[] Site;           // DataIndex -> site
        public static int[] EcoregionIndex;
        public static float[] SlopeDeg, AspectDeg;
        public static byte[] Zone;                 // suppression zone 0..3
        public static byte[] Stratum;              // moisture stratum id
        public static byte[] FuelbedIndex;

        // fuelbed (kg m^-2, m)
        public static float[] W1h, W10h, W100h, W1000h, Duff, WHerb, WWoody;
        public static float[] CanopyFuelLoad, CBD, CBH, CanopyHeight, CanopyCover, LadderFuel;
        public static float[] Foliage;             // total foliage biomass of tree cohorts, kg m^-2
        public static float[] BroadleafFraction;   // share of tree foliage from broadleaf species
        public static float[] CanopyMortality, ConsumptionTotal, PM25;
        public static byte[] SeverityClass;

        // extension-owned litter when not synced
        public static float[] Litter;

        // annual fire state
        public static int[] BurnedYear;            // year in which the cell last burned (0 = never)
        public static int[] EventId;
        public static short[] DayOfBurn;
        public static float[] Ros, Intensity, FlameLength, Cfb;
        public static byte[] CrownClass;

        // daily behavior cache
        public static int[] BehaviorDay;           // day stamp for cache validity
        public static float[] CacheRos, CacheDir, CacheEcc, CacheIntensity, CacheFlame, CacheCfb, CacheHpa;
        public static byte[] CacheCrown;
        public static bool[] CacheBurnable;

        // mortality inputs accumulated from succession events (kg m^-2 per year)
        public static float[] WoodInput100h, WoodInput1000h, FoliageInput;

        public static void Allocate(ICore core)
        {
            Count = core.Landscape.ActiveSiteCount + 1;
            Site = new ActiveSite[Count];
            foreach (ActiveSite s in core.Landscape.ActiveSites) Site[s.DataIndex] = s;
            EcoregionIndex = new int[Count];
            foreach (ActiveSite s in core.Landscape.ActiveSites) EcoregionIndex[s.DataIndex] = core.Ecoregion[s].Index;
            SlopeDeg = new float[Count]; AspectDeg = new float[Count];
            Zone = new byte[Count]; Stratum = new byte[Count]; FuelbedIndex = new byte[Count];
            W1h = new float[Count]; W10h = new float[Count]; W100h = new float[Count]; W1000h = new float[Count];
            Duff = new float[Count]; WHerb = new float[Count]; WWoody = new float[Count];
            CanopyFuelLoad = new float[Count]; CBD = new float[Count]; CBH = new float[Count];
            CanopyHeight = new float[Count]; CanopyCover = new float[Count]; LadderFuel = new float[Count];
            Foliage = new float[Count]; Litter = new float[Count]; BroadleafFraction = new float[Count];
            CanopyMortality = new float[Count]; ConsumptionTotal = new float[Count]; PM25 = new float[Count]; SeverityClass = new byte[Count];
            BurnedYear = new int[Count]; EventId = new int[Count]; DayOfBurn = new short[Count];
            Ros = new float[Count]; Intensity = new float[Count]; FlameLength = new float[Count]; Cfb = new float[Count];
            CrownClass = new byte[Count];
            BehaviorDay = new int[Count];
            CacheRos = new float[Count]; CacheDir = new float[Count]; CacheEcc = new float[Count];
            CacheIntensity = new float[Count]; CacheFlame = new float[Count]; CacheCfb = new float[Count]; CacheHpa = new float[Count];
            CacheCrown = new byte[Count]; CacheBurnable = new bool[Count];
            WoodInput100h = new float[Count]; WoodInput1000h = new float[Count]; FoliageInput = new float[Count];
            for (int i = 0; i < Count; i++) BehaviorDay[i] = -1;
        }

        public static void ResetAnnualFireState()
        {
            Array.Clear(EventId, 0, Count);
            Array.Clear(DayOfBurn, 0, Count);
            Array.Clear(Ros, 0, Count);
            Array.Clear(Intensity, 0, Count);
            Array.Clear(FlameLength, 0, Count);
            Array.Clear(Cfb, 0, Count);
            Array.Clear(CrownClass, 0, Count);
            Array.Clear(CanopyMortality, 0, Count); Array.Clear(ConsumptionTotal, 0, Count); Array.Clear(PM25, 0, Count); Array.Clear(SeverityClass, 0, Count);
            for (int i = 0; i < Count; i++) BehaviorDay[i] = -1;
        }
    }
}
