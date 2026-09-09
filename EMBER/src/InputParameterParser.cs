// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Parser for the main EMBER text input file. Parameters must appear in the order
// listed here; most are optional and fall back to the defaults in InputParameters.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.Utilities;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class InputParameterParser : TextParser<IInputParameters>
    {
        public override string LandisDataValue { get { return PlugIn.ExtensionName; } }

        public InputParameterParser() { }

        protected override IInputParameters Parse()
        {
            ReadLandisDataVar();
            InputParameters p = new InputParameters();

            InputVar<int> timestep = new InputVar<int>("Timestep");
            ReadVar(timestep);
            p.Timestep = timestep.Value;
            if (p.Timestep != 1)
                throw new InputValueException(timestep.Value.String, "EMBER requires an annual timestep (Timestep 1) because it uses daily weather.");

            p.SpeciesTableFile = ReadString("SpeciesTable");
            p.FuelbedTableFile = ReadString("FuelbedTable");
            p.SlopeMap = ReadString("SlopeMap");
            p.AspectMap = ReadString("AspectMap");
            p.SuppressionZoneMap = ReadOptionalString("SuppressionZoneMap", null);
            p.LightningIgnitionMap = ReadOptionalString("LightningIgnitionMap", null);
            ReadDynamicMaps("DynamicLightningIgnitionMaps", p.DynamicLightningIgnitionMaps, "AccidentalIgnitionMap");
            p.AccidentalIgnitionMap = ReadOptionalString("AccidentalIgnitionMap", null);
            ReadDynamicMaps("DynamicAccidentalIgnitionMaps", p.DynamicAccidentalIgnitionMaps, "Latitude");
            p.Latitude = ReadOptionalDouble("Latitude", p.Latitude);
            p.Longitude = ReadOptionalDouble("Longitude", p.Longitude);

            // fuel pools
            p.SyncFineFuelsToSuccession = ReadOptionalBool("SyncFineFuelsToSuccession", p.SyncFineFuelsToSuccession);
            p.InitialCoarseWood100h = ReadOptionalDouble("InitialCoarseWood100h", p.InitialCoarseWood100h);
            p.InitialCoarseWood1000h = ReadOptionalDouble("InitialCoarseWood1000h", p.InitialCoarseWood1000h);
            p.InitialDuff = ReadOptionalDouble("InitialDuff", p.InitialDuff);
            p.CoarseWoodDecay100h = ReadOptionalDouble("CoarseWoodDecay100h", p.CoarseWoodDecay100h);
            p.CoarseWoodDecay1000h = ReadOptionalDouble("CoarseWoodDecay1000h", p.CoarseWoodDecay1000h);
            p.DuffDecay = ReadOptionalDouble("DuffDecay", p.DuffDecay);
            p.DuffInputFraction = ReadOptionalDouble("DuffInputFraction", p.DuffInputFraction);
            p.LitterInputFraction = ReadOptionalDouble("LitterInputFraction", p.LitterInputFraction);
            p.LitterDecay = ReadOptionalDouble("LitterDecay", p.LitterDecay);
            p.BackgroundWoodMortality = ReadOptionalDouble("BackgroundWoodMortality", p.BackgroundWoodMortality);

            // moisture
            p.ShadeModifier = ReadOptionalDouble("ShadeModifier", p.ShadeModifier);
            p.TopographyModifier = ReadOptionalDouble("TopographyModifier", p.TopographyModifier);
            p.DroughtCodeLow = ReadOptionalDouble("DroughtCodeLow", p.DroughtCodeLow);
            p.DroughtCodeHigh = ReadOptionalDouble("DroughtCodeHigh", p.DroughtCodeHigh);
            ReadOptionalPair("ThousandHourMoisture", ref p.ThousandHourMoistureMin, ref p.ThousandHourMoistureMax, 0.01);
            ReadOptionalPair("DuffMoisture", ref p.DuffMoistureMin, ref p.DuffMoistureMax, 0.01);
            ReadOptionalPair("LiveHerbMoisture", ref p.LiveHerbMoistureMin, ref p.LiveHerbMoistureMax, 0.01);
            ReadOptionalPair("LiveWoodyMoisture", ref p.LiveWoodyMoistureMin, ref p.LiveWoodyMoistureMax, 0.01);
            p.FoliarMoistureDroughtEffect = ReadOptionalDouble("FoliarMoistureDroughtEffect", p.FoliarMoistureDroughtEffect);
            p.BroadleafFoliarMoisture = ReadOptionalDouble("BroadleafFoliarMoisture", p.BroadleafFoliarMoisture * 100.0) / 100.0;

            // ignition
            p.IgnitionFireDanger = ReadOptionalEnum<FireDangerIndex>("IgnitionFireDanger", p.IgnitionFireDanger);
            p.IgnitionAreaScaling = ReadOptionalEnum<AreaScaling>("IgnitionAreaScaling", p.IgnitionAreaScaling);
            p.LightningB0 = ReadDouble("LightningIgnitionsB0");
            p.LightningB1 = ReadDouble("LightningIgnitionsB1");
            p.AccidentalB0 = ReadDouble("AccidentalIgnitionsB0");
            p.AccidentalB1 = ReadDouble("AccidentalIgnitionsB1");
            p.IgnitionDist = ReadOptionalEnum<IgnitionDistribution>("IgnitionDistribution", p.IgnitionDist);
            if (p.IgnitionDist == IgnitionDistribution.ZeroInflatedPoisson)
            {
                p.LightningBinomialB0 = ReadDouble("LightningIgnitionsBinomialB0");
                p.LightningBinomialB1 = ReadDouble("LightningIgnitionsBinomialB1");
                p.AccidentalBinomialB0 = ReadDouble("AccidentalIgnitionsBinomialB0");
                p.AccidentalBinomialB1 = ReadDouble("AccidentalIgnitionsBinomialB1");
            }
            p.UseIgnitionSustain = ReadOptionalBool("UseIgnitionSustain", p.UseIgnitionSustain);
            p.IgnitionSustainB0 = ReadOptionalDouble("IgnitionSustainB0", p.IgnitionSustainB0);
            p.IgnitionSustainB1 = ReadOptionalDouble("IgnitionSustainB1", p.IgnitionSustainB1);
            p.IgnitionRequiresBurnPeriod = ReadOptionalBool("IgnitionRequiresBurnPeriod", p.IgnitionRequiresBurnPeriod);
            p.IgnitionAlwaysBurns = ReadOptionalBool("IgnitionAlwaysBurns", p.IgnitionAlwaysBurns);
            p.FirstIgnitionDay = ReadOptionalInt("FirstIgnitionDay", p.FirstIgnitionDay);
            p.LastIgnitionDay = ReadOptionalInt("LastIgnitionDay", p.LastIgnitionDay);

            // burn period
            p.BurnPeriodMinHours = ReadOptionalDouble("BurnPeriodMinHours", p.BurnPeriodMinHours);
            p.BurnPeriodMaxHours = ReadOptionalDouble("BurnPeriodMaxHours", p.BurnPeriodMaxHours);
            p.BurnPeriodFWILow = ReadOptionalDouble("BurnPeriodFWILow", p.BurnPeriodFWILow);
            p.BurnPeriodFWIHigh = ReadOptionalDouble("BurnPeriodFWIHigh", p.BurnPeriodFWIHigh);
            p.RainoutPrecipitation = ReadOptionalDouble("RainoutPrecipitation", p.RainoutPrecipitation);
            p.NaturalEndDays = ReadOptionalInt("NaturalEndDays", p.NaturalEndDays);
            p.MaxFireDays = ReadOptionalInt("MaxFireDays", p.MaxFireDays);

            // behavior
            p.WAFOpen = ReadOptionalDouble("WindAdjustmentOpen", p.WAFOpen);
            p.WAFSheltered = ReadOptionalDouble("WindAdjustmentSheltered", p.WAFSheltered);
            p.WindGustFactor = ReadOptionalDouble("WindGustFactor", p.WindGustFactor);
            p.ExtinctionIntensity = ReadOptionalDouble("ExtinctionIntensity", p.ExtinctionIntensity);
            p.ROSNoiseSigma = ReadOptionalDouble("ROSNoiseSigma", p.ROSNoiseSigma);
            p.MinimumFineFuelLoad = ReadOptionalDouble("MinimumFineFuelLoad", p.MinimumFineFuelLoad);
            p.AllowCrownFire = ReadOptionalBool("AllowCrownFire", p.AllowCrownFire);
            p.CrownInitiation = ReadOptionalEnum<CrownInitiationMode>("CrownInitiation", p.CrownInitiation);
            p.CoarseFuelFlamingFraction = ReadOptionalDouble("CoarseFuelFlamingFraction", p.CoarseFuelFlamingFraction);
            p.LadderMaxHeight = ReadOptionalDouble("LadderMaxHeight", p.LadderMaxHeight);
            p.CBDThreshold = ReadOptionalDouble("CBDThreshold", p.CBDThreshold);
            p.MaxFuelbedDepthLoad = ReadOptionalDouble("MaxFuelbedLoad", p.MaxFuelbedDepthLoad);

            // suppression
            p.UseSuppression = ReadOptionalBool("UseSuppression", p.UseSuppression);
            if (p.UseSuppression)
            {
                p.IA_B0 = ReadDouble("InitialAttackB0"); p.IA_B1 = ReadDouble("InitialAttackB1"); p.IA_B2 = ReadDouble("InitialAttackB2");
                p.IA_B3 = ReadDouble("InitialAttackB3"); p.IA_B4 = ReadDouble("InitialAttackB4");
                p.EA_B0 = ReadDouble("ContainmentB0"); p.EA_B1 = ReadDouble("ContainmentB1"); p.EA_B2 = ReadDouble("ContainmentB2");
                p.EA_B3 = ReadDouble("ContainmentB3"); p.EA_B4 = ReadDouble("ContainmentB4"); p.EA_B5 = ReadDouble("ContainmentB5");
                p.MaxSuppressedFires = ReadOptionalInt("MaxSuppressedFires", p.MaxSuppressedFires);
            }

            // effects
            p.CohortMortalityMode = ReadOptionalEnum<MortalityMode>("CohortMortalityMode", p.CohortMortalityMode);
            p.SeverityClasses = ReadOptionalInt("SeverityClasses", p.SeverityClasses);
            p.MinimumSurvivingFraction = ReadOptionalDouble("MinimumSurvivingFraction", p.MinimumSurvivingFraction);
            ReadOptionalPair("Consumption100h", ref p.Consumption.C100_Intercept, ref p.Consumption.C100_Slope, 1.0);
            ReadOptionalPair("Consumption1000h", ref p.Consumption.C1000_Intercept, ref p.Consumption.C1000_Slope, 1.0);
            ReadOptionalPair("ConsumptionDuff", ref p.Consumption.Duff_Intercept, ref p.Consumption.Duff_Slope, 1.0);
            p.Consumption.LiveWoody = ReadOptionalDouble("ConsumptionLiveWoody", p.Consumption.LiveWoody);
            p.Consumption.LiveHerb = ReadOptionalDouble("ConsumptionLiveHerb", p.Consumption.LiveHerb);
            ReadOptionalPair("EmissionFactorPM25", ref p.Emissions.PM25_Flaming, ref p.Emissions.PM25_Smoldering, 1.0);
            ReadOptionalPair("EmissionFactorCO2", ref p.Emissions.CO2_Flaming, ref p.Emissions.CO2_Smoldering, 1.0);

            // outputs
            p.MapOutputFolder = ReadOptionalString("MapOutputFolder", p.MapOutputFolder);
            p.FuelMapFrequency = ReadOptionalInt("FuelMapFrequency", p.FuelMapFrequency);
            p.WriteFuelMoistureMaps = ReadOptionalBool("WriteFuelMoistureMaps", p.WriteFuelMoistureMaps);
            p.WriteCalibrationDiagnostics = ReadOptionalBool("WriteCalibrationDiagnostics", p.WriteCalibrationDiagnostics);

            if (!AtEndOfInput)
                throw NewParseException("Unexpected input '{0}' (check parameter order in the EMBER user guide).", CurrentName);
            return p;
        }

        // ---- helpers ------------------------------------------------------------------

        string ReadString(string name)
        {
            InputVar<string> v = new InputVar<string>(name);
            ReadVar(v);
            return v.Value.Actual;
        }

        string ReadOptionalString(string name, string def)
        {
            InputVar<string> v = new InputVar<string>(name);
            if (ReadOptionalVar(v)) return v.Value.Actual;
            return def;
        }

        double ReadDouble(string name)
        {
            InputVar<double> v = new InputVar<double>(name);
            ReadVar(v);
            return v.Value.Actual;
        }

        double ReadOptionalDouble(string name, double def)
        {
            InputVar<double> v = new InputVar<double>(name);
            if (ReadOptionalVar(v)) return v.Value.Actual;
            return def;
        }

        int ReadOptionalInt(string name, int def)
        {
            InputVar<int> v = new InputVar<int>(name);
            if (ReadOptionalVar(v)) return v.Value.Actual;
            return def;
        }

        bool ReadOptionalBool(string name, bool def)
        {
            InputVar<string> v = new InputVar<string>(name);
            if (!ReadOptionalVar(v)) return def;
            string s = v.Value.Actual.Trim().ToLowerInvariant();
            if (s == "yes" || s == "true" || s == "y" || s == "on") return true;
            if (s == "no" || s == "false" || s == "n" || s == "off") return false;
            throw new InputValueException(v.Value.String, "{0} must be yes or no.", name);
        }

        T ReadOptionalEnum<T>(string name, T def) where T : struct
        {
            InputVar<string> v = new InputVar<string>(name);
            if (!ReadOptionalVar(v)) return def;
            return Tables.ParseEnum<T>(v.Value.Actual, name);
        }

        /// <summary>Reads "Name a b" on one line; values multiplied by scale.</summary>
        void ReadOptionalPair(string name, ref double a, ref double b, double scale)
        {
            if (AtEndOfInput || CurrentName != name) return;
            StringReader line = new StringReader(CurrentLine);
            InputVar<string> nm = new InputVar<string>(name);
            ReadValue(nm, line);
            InputVar<double> va = new InputVar<double>(name + " first value");
            InputVar<double> vb = new InputVar<double>(name + " second value");
            ReadValue(va, line);
            ReadValue(vb, line);
            a = va.Value.Actual * scale;
            b = vb.Value.Actual * scale;
            CheckNoDataAfter("the " + name + " values", line);
            GetNextLine();
        }

        void ReadDynamicMaps(string tableName, List<DynamicMap> list, string nextName)
        {
            if (!ReadOptionalName(tableName)) return;
            InputVar<int> year = new InputVar<int>("Year");
            InputVar<string> map = new InputVar<string>("MapName");
            int prev = 0;
            while (!AtEndOfInput && CurrentName != nextName)
            {
                StringReader line = new StringReader(CurrentLine);
                ReadValue(year, line);
                ReadValue(map, line);
                if (year.Value.Actual <= prev)
                    throw new InputValueException(year.Value.String, "Years in {0} must increase.", tableName);
                prev = year.Value.Actual;
                DynamicMap dm = new DynamicMap();
                dm.Year = year.Value.Actual;
                dm.MapName = map.Value.Actual;
                list.Add(dm);
                CheckNoDataAfter("the map name", line);
                GetNextLine();
            }
        }
    }
}
