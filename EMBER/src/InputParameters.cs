// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
using System.Collections.Generic;
using Landis.Core;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public enum IgnitionDistribution { Poisson, ZeroInflatedPoisson }
    public enum FireDangerIndex { FWI, ISI }
    public enum AreaScaling { Landscape, PerHectare }
    public enum MortalityMode { Fractional, Binomial }
    public enum LifeForm { Tree, Shrub, Grass }
    public enum FoliarMoistureClass { Conifer, Broadleaf, Shrub, Herb }
    public enum IgnitionCause : byte { Lightning = 0, Accidental = 1, Rx = 2 }

    public class SpeciesFireParameters
    {
        public ISpecies Species;
        public LifeForm LifeForm = LifeForm.Tree;
        public string LitterClass = "";
        public FuelbedType Fuelbed;             // resolved from LitterClass
        public double MaxHeight = 40;           // m
        public double HeightK = 0.03;           // yr^-1
        public double CrownRatio = 0.5;         // live crown length / height
        public double FineBranchFraction = 0.3; // fraction of foliage mass added as fine branch canopy fuel
        public double BarkThicknessMax = 3.0;   // cm
        public double AgeDBH = 100;             // yr
        public FoliarMoistureClass FoliarClass = FoliarMoistureClass.Conifer;
        public double FoliageFraction = 0.05;   // fallback share of biomass that is foliage when the succession extension does not expose it
        public double SpecificLeafArea = 5.0;   // m2 kg^-1 for LAI/cover estimate
        public double CoarseWoodFraction1000h = 0.7; // of dead wood inputs from this species going to 1000-h (rest 100-h)
    }

    public class DynamicMap
    {
        public int Year;
        public string MapName;
    }

    public interface IInputParameters
    {
        InputParameters Values { get; }
    }

    /// <summary>All EMBER parameters. Fields carry the documented defaults.</summary>
    public class InputParameters : IInputParameters
    {
        public InputParameters Values { get { return this; } }

        public int Timestep = 1;
        public string SpeciesTableFile;
        public string FuelbedTableFile;
        public string SlopeMap;
        public string AspectMap;
        public string SuppressionZoneMap;
        public string LightningIgnitionMap;
        public string AccidentalIgnitionMap;
        public List<DynamicMap> DynamicLightningIgnitionMaps = new List<DynamicMap>();
        public List<DynamicMap> DynamicAccidentalIgnitionMaps = new List<DynamicMap>();
        public double Latitude = 47.0;
        public double Longitude = -120.0;

        // fuel pools
        public bool SyncFineFuelsToSuccession = true;
        public double InitialCoarseWood100h = 0.5;    // kg m^-2
        public double InitialCoarseWood1000h = 3.0;
        public double InitialDuff = 2.0;
        public double CoarseWoodDecay100h = 0.10;     // yr^-1
        public double CoarseWoodDecay1000h = 0.03;
        public double DuffDecay = 0.05;
        public double DuffInputFraction = 0.15;       // of fine litter per year
        public double LitterInputFraction = 0.30;     // of foliage per year when not synced
        public double LitterDecay = 0.40;
        public double BackgroundWoodMortality = 0.01;   // yr^-1 share of live wood dying without a mortality event

        // moisture
        public double ShadeModifier = 0.15;
        public double TopographyModifier = 0.15;
        public double DroughtCodeLow = 100;
        public double DroughtCodeHigh = 500;
        public double ThousandHourMoistureMin = 0.12, ThousandHourMoistureMax = 0.35;
        public double DuffMoistureMin = 0.20, DuffMoistureMax = 1.50;
        public double LiveHerbMoistureMin = 0.30, LiveHerbMoistureMax = 1.50;
        public double LiveWoodyMoistureMin = 0.60, LiveWoodyMoistureMax = 1.30;
        public double FoliarMoistureDroughtEffect = 0.25;
        public double BroadleafFoliarMoisture = 1.20;

        // ignition
        public FireDangerIndex IgnitionFireDanger = FireDangerIndex.FWI;
        public AreaScaling IgnitionAreaScaling = AreaScaling.Landscape;
        public IgnitionDistribution IgnitionDist = IgnitionDistribution.Poisson;
        public double LightningB0, LightningB1, AccidentalB0, AccidentalB1;
        public double LightningBinomialB0, LightningBinomialB1, AccidentalBinomialB0, AccidentalBinomialB1;
        public bool UseIgnitionSustain = false;         // apply the FFMC sustain test (only when ignition coefficients come from ignition, not fire, counts)
        public double IgnitionSustainB0 = -12.5, IgnitionSustainB1 = 0.15;
        public bool IgnitionRequiresBurnPeriod = false; // if false an ignition burns its cell even on a day with no spread
        public bool IgnitionAlwaysBurns = true;         // an attempt whose cell cannot carry fire still becomes a one-cell fire (fire-record convention)
        public int FirstIgnitionDay = 0, LastIgnitionDay = 364;

        // burn period and duration
        public double BurnPeriodMinHours = 1, BurnPeriodMaxHours = 10;
        public double BurnPeriodFWILow = 8, BurnPeriodFWIHigh = 35;
        public double RainoutPrecipitation = 0.3;      // cm per day; at or above this no active spread that day
        public int NaturalEndDays = 2;
        public int MaxFireDays = 60;

        // behavior
        public double WAFOpen = 0.4, WAFSheltered = 0.15;
        public double WindGustFactor = 1.0;            // multiplies the daily wind speed used for behavior
        public double ExtinctionIntensity = 30;       // kW m^-1
        public double ROSNoiseSigma = 0.3;
        public double MinimumFineFuelLoad = 0.05;     // kg m^-2
        public bool AllowCrownFire = true;
        public CrownInitiationMode CrownInitiation = CrownInitiationMode.Cruz2004;
        public double CoarseFuelFlamingFraction = 0.3;  // share of the 1000-h consumption burning in the flaming front (added to intensity)
        public double LadderMaxHeight = 4.0;          // m: shrub and short tree crowns below this feed the ladder
        public double CBDThreshold = 0.011;           // kg m^-3 Scott & Reinhardt
        public double MaxFuelbedDepthLoad = 4.0;      // kg m^-2 fine fuel cap for Rothermel bed

        // suppression
        public bool UseSuppression = false;
        public double IA_B0 = 2.0, IA_B1 = -0.5, IA_B2 = -0.001, IA_B3 = 1.0, IA_B4 = -0.1;
        public double EA_B0 = -1.0, EA_B1 = -0.3, EA_B2 = -0.05, EA_B3 = 1.0, EA_B4 = -0.1, EA_B5 = -0.1;
        public int MaxSuppressedFires = 10;

        // effects
        public MortalityMode CohortMortalityMode = MortalityMode.Fractional;
        public int SeverityClasses = 10;
        public ConsumptionParameters Consumption = new ConsumptionParameters();
        public EmissionFactors Emissions = new EmissionFactors();
        public double MinimumSurvivingFraction = 0.05;   // fractional cohorts below this are removed

        // outputs
        public string MapOutputFolder = "ember";
        public int FuelMapFrequency = 10;
        public bool WriteCalibrationDiagnostics = false;
        public bool WriteFuelMoistureMaps = false;

        public Dictionary<string, FuelbedType> Fuelbeds = new Dictionary<string, FuelbedType>();
        public List<FuelbedType> FuelbedList = new List<FuelbedType>();
        public SpeciesFireParameters[] SpeciesParameters;   // indexed by species.Index
    }
}
