// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// A mechanistic, climate-sensitive fire disturbance extension for LANDIS-II v8.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class PlugIn : ExtensionMain
    {
        public static readonly ExtensionType ExtType = new ExtensionType("disturbance:fire");
        public static readonly string ExtensionName = "EMBER";

        public static ICore ModelCore { get; private set; }
        public static InputParameters Parameters;

        Weather weather;
        MoistureStrata strata;
        FuelbedBuilder builder;
        FuelPools pools;
        IgnitionModel ignition;
        Spread spread;
        Effects effects;
        Suppression suppression;
        Outputs outputs;
        Logs logs;
        int nextEventId = 0;
        readonly List<FireEvent> activeFires = new List<FireEvent>();

        public PlugIn() : base(ExtensionName, ExtType) { }

        public override void AddCohortData() { }

        public override void LoadParameters(string dataFile, ICore mCore)
        {
            ModelCore = mCore;
            InputParameterParser parser = new InputParameterParser();
            IInputParameters ip = Landis.Data.Load<IInputParameters>(dataFile, parser);
            Parameters = ip.Values;
            Tables.ReadFuelbeds(Parameters.FuelbedTableFile, Parameters);
            Tables.ReadSpecies(Parameters.SpeciesTableFile, Parameters, mCore.Species);
        }

        public override void Initialize()
        {
            Timestep = 1;
            ModelCore.UI.WriteLine("Initializing {0} ...", ExtensionName);
            Rng.Initialize(ModelCore);
            SiteVars.Initialize(ModelCore);
            CellData.Allocate(ModelCore);

            float[] slope = MapReader.Read(ModelCore, Parameters.SlopeMap, 0, "slope");
            float[] aspect = MapReader.Read(ModelCore, Parameters.AspectMap, 180, "aspect");
            float[] zone = MapReader.Read(ModelCore, Parameters.SuppressionZoneMap, 1, "suppression zone");
            for (int i = 1; i < CellData.Count; i++)
            {
                CellData.SlopeDeg[i] = Math.Max(0, Math.Min(80, slope[i]));
                CellData.AspectDeg[i] = (float)Units.NormalizeDeg(aspect[i]);
                CellData.Zone[i] = (byte)(zone == null ? 1 : Math.Max(0, Math.Min(3, (int)Math.Round(zone[i]))));
                SiteVars.Slope[CellData.Site[i]] = (ushort)Math.Round(CellData.SlopeDeg[i]);
                SiteVars.Aspect[CellData.Site[i]] = (ushort)Math.Round(Units.NormalizeDeg(CellData.AspectDeg[i] + 180.0)); // uphill azimuth, SCRPPLE convention
            }
            Spread.BuildNeighbors(ModelCore);

            weather = new Weather(ModelCore);
            strata = new MoistureStrata(ModelCore, Parameters);
            builder = new FuelbedBuilder(ModelCore, Parameters);
            pools = new FuelPools(ModelCore, Parameters);
            pools.Subscribe();
            logs = new Logs(ModelCore, Parameters);
            effects = new Effects(ModelCore, Parameters, builder, strata) { Weather = weather, Logs = logs };
            spread = new Spread(ModelCore, Parameters, builder, strata, effects) { Weather = weather };
            suppression = new Suppression(ModelCore, Parameters);
            outputs = new Outputs(ModelCore, Parameters);
            ignition = new IgnitionModel(ModelCore, Parameters);
            ignition.LightningWeight = MapReader.Read(ModelCore, Parameters.LightningIgnitionMap, 0, "lightning ignition");
            ignition.AccidentalWeight = MapReader.Read(ModelCore, Parameters.AccidentalIgnitionMap, 0, "accidental ignition");
            ignition.BuildSamplers();
            MetadataHandler.Initialize(ModelCore, logs, Parameters);

            // initial fuel structure so fuel maps at time 0 and other extensions see something sensible
            builder.BuildAll(0);
            strata.AssignCells();
            ModelCore.UI.WriteLine("   EMBER: {0} active cells, {1} moisture strata, {2} fuelbed types, {3} species.",
                CellData.Count - 1, strata.Count, Parameters.FuelbedList.Count, Parameters.SpeciesParameters.Length);
        }

        public override void InitializePhase2()
        {
            SiteVars.InitializePhase2(ModelCore);
        }

        public override void Run()
        {
            int year = ModelCore.CurrentTime;
            ModelCore.UI.WriteLine("   EMBER year {0}: loading weather, building fuels ...", year);
            weather.LoadYear(year);
            spread.Year = year; effects.Year = year;
            SiteVars.ResetAnnual(ModelCore);
            CellData.ResetAnnualFireState();

            // dynamic ignition maps
            bool rebuild = false;
            foreach (DynamicMap dm in Parameters.DynamicLightningIgnitionMaps)
                if (dm.Year == year) { ignition.LightningWeight = MapReader.Read(ModelCore, dm.MapName, 0, "lightning ignition"); rebuild = true; }
            foreach (DynamicMap dm in Parameters.DynamicAccidentalIgnitionMaps)
                if (dm.Year == year) { ignition.AccidentalWeight = MapReader.Read(ModelCore, dm.MapName, 0, "accidental ignition"); rebuild = true; }
            if (rebuild) ignition.BuildSamplers();

            pools.AnnualUpdate(year);
            builder.BuildAll(year);
            strata.AssignCells();
            activeFires.Clear();

            SummaryLog summary = new SummaryLog { SimulationYear = year, CalendarYear = weather.CalendarYear, FirstFireDay = 0, LastFireDay = 0 };
            List<FireEvent> finished = new List<FireEvent>();
            double fwiSum = 0; int fwiDays = 0; double fwiMax = 0;
            int contained = 0;
            bool wroteJulyMoisture = false;

            for (int day = 0; day < 365; day++)
            {
                strata.Update(weather, day);
                if (Parameters.WriteFuelMoistureMaps && day == 212 && !wroteJulyMoisture) { outputs.WriteMoistureMaps(year, strata, "aug1"); wroteJulyMoisture = true; }

                // grow existing fires
                foreach (FireEvent f in activeFires)
                {
                    if (f.Ended) continue;
                    int burned = spread.RunDay(f, day);
                    DailyWeather w = weather.Get(f.Ecoregion, day);
                    if (burned > 0 || f.DaysActive == 1)
                        logs.Daily.AddObject(new DailyLog { EventID = f.Id, SimulationYear = year, Day = day + 1, FWI = w.FWI, BurnHours = f.BurnHoursToday, CellsBurned = burned, CumulativeAreaHa = f.AreaHa(ModelCore.CellArea), MaxRos = f.MaxDayRos, MaxIntensity = f.MaxDayIntensity, Suppressed = f.SuppressedToday ? "yes" : "no" });
                    if (f.IdleDays >= Parameters.NaturalEndDays || f.Frontier.Count == 0) { f.Ended = true; f.EndReason = f.Frontier.Count == 0 ? "FuelLimited" : "Weather"; f.EndDay = day + 1; }
                    else if (f.DaysActive >= Parameters.MaxFireDays) { f.Ended = true; f.EndReason = "MaxDuration"; f.EndDay = day + 1; }
                }

                // new ignitions
                double fd;
                List<IgnitionAttempt> attempts = ignition.Draw(weather, day, out fd);
                if (day >= Parameters.FirstIgnitionDay && day <= Parameters.LastIgnitionDay) { fwiSum += fd; fwiDays++; if (fd > fwiMax) fwiMax = fd; }
                int lAtt = 0, aAtt = 0, lFire = 0, aFire = 0;
                foreach (IgnitionAttempt a in attempts)
                {
                    if (a.Cause == IgnitionCause.Lightning) lAtt++; else aAtt++;
                    DailyWeather w = weather.Get(CellData.EcoregionIndex[a.Cell], day);
                    if (!w.Valid) continue;
                    if (Parameters.UseIgnitionSustain && Rng.Uniform() >= ignition.SustainProbability(w.FFMC)) continue;
                    FireEvent f = spread.Start(nextEventId + 1, a.Cell, a.Cause, day);
                    if (f == null) continue;
                    nextEventId++;
                    activeFires.Add(f);
                    if (a.Cause == IgnitionCause.Lightning) lFire++; else aFire++;
                    int burned = spread.RunDay(f, day);
                    logs.Daily.AddObject(new DailyLog { EventID = f.Id, SimulationYear = year, Day = day + 1, FWI = w.FWI, BurnHours = f.BurnHoursToday, CellsBurned = burned + 1, CumulativeAreaHa = f.AreaHa(ModelCore.CellArea), MaxRos = f.MaxDayRos, MaxIntensity = f.MaxDayIntensity, Suppressed = "no" });
                    if (summary.FirstFireDay == 0) summary.FirstFireDay = day + 1;
                    summary.LastFireDay = day + 1;
                    if (f.Frontier.Count == 0) { f.Ended = true; if (f.EndReason == "") f.EndReason = "FuelLimited"; f.EndDay = day + 1; }
                }
                summary.IgnitionAttempts += lAtt + aAtt;
                summary.LightningFires += lFire; summary.AccidentalFires += aFire;

                contained += suppression.EndOfDay(activeFires, day);

                int activeCount = 0;
                for (int k = activeFires.Count - 1; k >= 0; k--)
                {
                    FireEvent f = activeFires[k];
                    if (f.Ended) { finished.Add(f); activeFires.RemoveAt(k); }
                    else activeCount++;
                }
                if (lAtt + aAtt > 0 || activeCount > 0)
                    logs.Ignitions.AddObject(new IgnitionLog { SimulationYear = year, Day = day + 1, FireDanger = fd, LightningAttempts = lAtt, LightningFires = lFire, AccidentalAttempts = aAtt, AccidentalFires = aFire, ActiveFires = activeCount });
            }
            foreach (FireEvent f in activeFires) { f.Ended = true; f.EndReason = "SeasonEnd"; f.EndDay = 365; finished.Add(f); }
            activeFires.Clear();

            // logs
            int cellsBurned = 0, highSev = 0, crown = 0; double largest = 0;
            foreach (FireEvent f in finished)
            {
                int n = f.Cells.Count;
                cellsBurned += n; highSev += f.HighSeverityCells; crown += f.CellsPassive + f.CellsActive;
                double area = f.AreaHa(ModelCore.CellArea);
                if (area > largest) largest = area;
                double cellM2 = ModelCore.CellArea * 10000.0;
                ActiveSite ign = CellData.Site[f.IgnitionCell];
                logs.Events.AddObject(new EventLog
                {
                    EventID = f.Id, SimulationYear = year, CalendarYear = weather.CalendarYear, Cause = f.Cause.ToString(),
                    IgnitionDay = f.IgnitionDay + 1, IgnitionRow = ign.Location.Row, IgnitionColumn = ign.Location.Column,
                    Ecoregion = ModelCore.Ecoregions[f.Ecoregion].Name, IgnitionFWI = f.IgnitionFWI, DaysActive = f.DaysActive,
                    EndDay = f.EndDay, EndReason = f.EndReason, Cells = n, AreaHa = area, MeanRos = f.MeanRos, MaxRos = f.MaxRos,
                    MeanIntensity = f.MeanIntensity, MaxIntensity = f.MaxIntensity, MaxFlameLength = f.MaxFlame,
                    FractionSurface = n > 0 ? (double)f.CellsSurface / n : 0, FractionPassive = n > 0 ? (double)f.CellsPassive / n : 0,
                    FractionActive = n > 0 ? (double)f.CellsActive / n : 0, MeanCanopyMortality = n > 0 ? f.SumCanopyMortality / n : 0,
                    FractionHighSeverity = n > 0 ? (double)f.HighSeverityCells / n : 0, CohortsKilled = f.CohortsKilled, CohortsPartial = f.CohortsPartial,
                    BiomassKilledMg = f.SumKilledBiomass * cellM2 / 1000.0, ConsumptionMg = f.SumConsumption * cellM2 / 1000.0,
                    SurfaceConsumptionMg = f.SumSurfaceConsumption * cellM2 / 1000.0, DuffConsumptionMg = f.SumDuffConsumption * cellM2 / 1000.0,
                    CanopyConsumptionMg = f.SumCanopyConsumption * cellM2 / 1000.0, PM25Mg = f.SumPM25 * cellM2 / 1e6, CO2Mg = f.SumCO2 * cellM2 / 1e6,
                    PeakDay = f.PeakDay, PeakDayFWI = f.PeakDayFWI
                });
                summary.BiomassKilledMg += f.SumKilledBiomass * cellM2 / 1000.0;
                summary.ConsumptionMg += f.SumConsumption * cellM2 / 1000.0;
                summary.PM25Mg += f.SumPM25 * cellM2 / 1e6;
                summary.CO2Mg += f.SumCO2 * cellM2 / 1e6;
            }
            summary.Fires = finished.Count;
            summary.CellsBurned = cellsBurned;
            summary.AreaBurnedHa = cellsBurned * ModelCore.CellArea;
            summary.LargestFireHa = largest;
            summary.FractionHighSeverity = cellsBurned > 0 ? (double)highSev / cellsBurned : 0;
            summary.FractionCrownFire = cellsBurned > 0 ? (double)crown / cellsBurned : 0;
            summary.FiresContained = contained;
            summary.MeanFWI = fwiDays > 0 ? fwiSum / fwiDays : 0;
            summary.MaxFWI = fwiMax;
            logs.Summary.AddObject(summary);
            logs.Events.WriteToFile(); logs.Events.Clear();
            logs.Daily.WriteToFile(); logs.Daily.Clear();
            logs.Ignitions.WriteToFile(); logs.Ignitions.Clear();
            logs.Summary.WriteToFile(); logs.Summary.Clear();

            ModelCore.UI.WriteLine("   EMBER year {0}: {1} fires, {2} cells burned ({3:0.0} ha), largest {4:0.0} ha, mean FWI {5:0.0}.",
                year, finished.Count, cellsBurned, summary.AreaBurnedHa, largest, summary.MeanFWI);

            outputs.WriteFireMaps(year);
            if (Parameters.FuelMapFrequency > 0 && year % Parameters.FuelMapFrequency == 0)
                outputs.WriteFuelMaps(year);
        }

        public override void CleanUp()
        {
            if (logs != null) logs.Close();
        }
    }
}
