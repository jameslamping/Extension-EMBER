// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// CSV logs through the Metadata library plus an optional per-cell diagnostics file.
using System;
using System.IO;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.Metadata;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class EventLog
    {
        [DataFieldAttribute(Desc = "Event ID")] public int EventID { get; set; }
        [DataFieldAttribute(Unit = FieldUnits.Year, Desc = "Simulation year")] public int SimulationYear { get; set; }
        [DataFieldAttribute(Desc = "Calendar year of climate")] public int CalendarYear { get; set; }
        [DataFieldAttribute(Desc = "Cause")] public string Cause { get; set; }
        [DataFieldAttribute(Desc = "Ignition day of year")] public int IgnitionDay { get; set; }
        [DataFieldAttribute(Desc = "Ignition row")] public int IgnitionRow { get; set; }
        [DataFieldAttribute(Desc = "Ignition column")] public int IgnitionColumn { get; set; }
        [DataFieldAttribute(Desc = "Ignition ecoregion")] public string Ecoregion { get; set; }
        [DataFieldAttribute(Desc = "FWI at ignition", Format = "0.0")] public double IgnitionFWI { get; set; }
        [DataFieldAttribute(Desc = "Days active")] public int DaysActive { get; set; }
        [DataFieldAttribute(Desc = "Last day of year burning")] public int EndDay { get; set; }
        [DataFieldAttribute(Desc = "End reason")] public string EndReason { get; set; }
        [DataFieldAttribute(Unit = FieldUnits.Count, Desc = "Cells burned")] public int Cells { get; set; }
        [DataFieldAttribute(Desc = "Area (ha)", Format = "0.0")] public double AreaHa { get; set; }
        [DataFieldAttribute(Desc = "Mean ROS (m/min)", Format = "0.00")] public double MeanRos { get; set; }
        [DataFieldAttribute(Desc = "Max ROS (m/min)", Format = "0.00")] public double MaxRos { get; set; }
        [DataFieldAttribute(Desc = "Mean fireline intensity (kW/m)", Format = "0")] public double MeanIntensity { get; set; }
        [DataFieldAttribute(Desc = "Max fireline intensity (kW/m)", Format = "0")] public double MaxIntensity { get; set; }
        [DataFieldAttribute(Desc = "Max flame length (m)", Format = "0.0")] public double MaxFlameLength { get; set; }
        [DataFieldAttribute(Desc = "Fraction surface fire", Format = "0.00")] public double FractionSurface { get; set; }
        [DataFieldAttribute(Desc = "Fraction passive crown", Format = "0.00")] public double FractionPassive { get; set; }
        [DataFieldAttribute(Desc = "Fraction active crown", Format = "0.00")] public double FractionActive { get; set; }
        [DataFieldAttribute(Desc = "Mean canopy mortality fraction", Format = "0.00")] public double MeanCanopyMortality { get; set; }
        [DataFieldAttribute(Desc = "Fraction high severity", Format = "0.00")] public double FractionHighSeverity { get; set; }
        [DataFieldAttribute(Desc = "Cohorts killed")] public int CohortsKilled { get; set; }
        [DataFieldAttribute(Desc = "Cohorts partially killed")] public int CohortsPartial { get; set; }
        [DataFieldAttribute(Desc = "Biomass killed (Mg)", Format = "0.0")] public double BiomassKilledMg { get; set; }
        [DataFieldAttribute(Desc = "Total consumption (Mg)", Format = "0.0")] public double ConsumptionMg { get; set; }
        [DataFieldAttribute(Desc = "Surface consumption (Mg)", Format = "0.0")] public double SurfaceConsumptionMg { get; set; }
        [DataFieldAttribute(Desc = "Duff consumption (Mg)", Format = "0.0")] public double DuffConsumptionMg { get; set; }
        [DataFieldAttribute(Desc = "Canopy consumption (Mg)", Format = "0.0")] public double CanopyConsumptionMg { get; set; }
        [DataFieldAttribute(Desc = "PM2.5 emitted (Mg)", Format = "0.00")] public double PM25Mg { get; set; }
        [DataFieldAttribute(Desc = "CO2 emitted (Mg)", Format = "0.0")] public double CO2Mg { get; set; }
        [DataFieldAttribute(Desc = "Peak day of year")] public int PeakDay { get; set; }
        [DataFieldAttribute(Desc = "FWI on peak day", Format = "0.0")] public double PeakDayFWI { get; set; }
    }

    public class DailyLog
    {
        [DataFieldAttribute(Desc = "Event ID")] public int EventID { get; set; }
        [DataFieldAttribute(Unit = FieldUnits.Year, Desc = "Simulation year")] public int SimulationYear { get; set; }
        [DataFieldAttribute(Desc = "Day of year")] public int Day { get; set; }
        [DataFieldAttribute(Desc = "FWI", Format = "0.0")] public double FWI { get; set; }
        [DataFieldAttribute(Desc = "Burn period hours", Format = "0.0")] public double BurnHours { get; set; }
        [DataFieldAttribute(Desc = "Cells burned this day")] public int CellsBurned { get; set; }
        [DataFieldAttribute(Desc = "Cumulative area (ha)", Format = "0.0")] public double CumulativeAreaHa { get; set; }
        [DataFieldAttribute(Desc = "Max ROS this day (m/min)", Format = "0.00")] public double MaxRos { get; set; }
        [DataFieldAttribute(Desc = "Max intensity this day (kW/m)", Format = "0")] public double MaxIntensity { get; set; }
        [DataFieldAttribute(Desc = "Suppression applied")] public string Suppressed { get; set; }
    }

    public class IgnitionLog
    {
        [DataFieldAttribute(Unit = FieldUnits.Year, Desc = "Simulation year")] public int SimulationYear { get; set; }
        [DataFieldAttribute(Desc = "Day of year")] public int Day { get; set; }
        [DataFieldAttribute(Desc = "Landscape fire danger", Format = "0.0")] public double FireDanger { get; set; }
        [DataFieldAttribute(Desc = "Lightning attempts")] public int LightningAttempts { get; set; }
        [DataFieldAttribute(Desc = "Lightning fires started")] public int LightningFires { get; set; }
        [DataFieldAttribute(Desc = "Accidental attempts")] public int AccidentalAttempts { get; set; }
        [DataFieldAttribute(Desc = "Accidental fires started")] public int AccidentalFires { get; set; }
        [DataFieldAttribute(Desc = "Active fires at end of day")] public int ActiveFires { get; set; }
    }

    public class SummaryLog
    {
        [DataFieldAttribute(Unit = FieldUnits.Year, Desc = "Simulation year")] public int SimulationYear { get; set; }
        [DataFieldAttribute(Desc = "Calendar year")] public int CalendarYear { get; set; }
        [DataFieldAttribute(Desc = "Ignition attempts")] public int IgnitionAttempts { get; set; }
        [DataFieldAttribute(Desc = "Fires")] public int Fires { get; set; }
        [DataFieldAttribute(Desc = "Lightning fires")] public int LightningFires { get; set; }
        [DataFieldAttribute(Desc = "Accidental fires")] public int AccidentalFires { get; set; }
        [DataFieldAttribute(Desc = "Cells burned")] public int CellsBurned { get; set; }
        [DataFieldAttribute(Desc = "Area burned (ha)", Format = "0.0")] public double AreaBurnedHa { get; set; }
        [DataFieldAttribute(Desc = "Largest fire (ha)", Format = "0.0")] public double LargestFireHa { get; set; }
        [DataFieldAttribute(Desc = "Fraction area high severity", Format = "0.00")] public double FractionHighSeverity { get; set; }
        [DataFieldAttribute(Desc = "Fraction area crown fire", Format = "0.00")] public double FractionCrownFire { get; set; }
        [DataFieldAttribute(Desc = "First fire day")] public int FirstFireDay { get; set; }
        [DataFieldAttribute(Desc = "Last fire day")] public int LastFireDay { get; set; }
        [DataFieldAttribute(Desc = "Biomass killed (Mg)", Format = "0")] public double BiomassKilledMg { get; set; }
        [DataFieldAttribute(Desc = "Consumption (Mg)", Format = "0")] public double ConsumptionMg { get; set; }
        [DataFieldAttribute(Desc = "PM2.5 (Mg)", Format = "0.0")] public double PM25Mg { get; set; }
        [DataFieldAttribute(Desc = "CO2 (Mg)", Format = "0")] public double CO2Mg { get; set; }
        [DataFieldAttribute(Desc = "Fires contained by suppression")] public int FiresContained { get; set; }
        [DataFieldAttribute(Desc = "Mean landscape FWI over fire season", Format = "0.0")] public double MeanFWI { get; set; }
        [DataFieldAttribute(Desc = "Max landscape FWI", Format = "0.0")] public double MaxFWI { get; set; }
    }

    public class Logs
    {
        public MetadataTable<EventLog> Events;
        public MetadataTable<DailyLog> Daily;
        public MetadataTable<IgnitionLog> Ignitions;
        public MetadataTable<SummaryLog> Summary;
        StreamWriter diagnostics;
        readonly ICore core;

        public Logs(ICore core, InputParameters p)
        {
            this.core = core;
            Events = new MetadataTable<EventLog>("ember-events-log.csv");
            Daily = new MetadataTable<DailyLog>("ember-daily-log.csv");
            Ignitions = new MetadataTable<IgnitionLog>("ember-ignitions-log.csv");
            Summary = new MetadataTable<SummaryLog>("ember-summary-log.csv");
            if (p.WriteCalibrationDiagnostics)
            {
                diagnostics = Landis.Data.CreateTextFile("ember-cell-diagnostics.csv");
                diagnostics.WriteLine("Year,EventID,Row,Column,Day,W1h,W10h,W100h,WHerb,WWoody,M1h,M100h,M1000h,MDuff,MFoliar,ROS,Intensity,CFB,CrownClass,ScorchHeight,ConsSurface,ConsDuff,ConsCanopy,CanopyMortality,Severity");
                diagnostics.AutoFlush = true;
            }
        }

        public void WriteCellDiagnostic(int year, int eventId, ActiveSite site, int day0, SurfaceFuel fuel, FuelMoistureState m,
                                        double ros, double intensity, double cfb, byte crown, double scorch, ConsumptionResult cons,
                                        double canopyMortality, int severity)
        {
            if (diagnostics == null) return;
            diagnostics.WriteLine(string.Join(",", new string[] {
                year.ToString(), eventId.ToString(), site.Location.Row.ToString(), site.Location.Column.ToString(), (day0 + 1).ToString(),
                fuel.W1h.ToString("0.000"), fuel.W10h.ToString("0.000"), fuel.W100h.ToString("0.000"), fuel.WHerb.ToString("0.000"), fuel.WWoody.ToString("0.000"),
                m.M1h.ToString("0.000"), m.M100h.ToString("0.000"), m.M1000h.ToString("0.000"), m.MDuff.ToString("0.000"), m.MFoliar.ToString("0.000"),
                ros.ToString("0.00"), intensity.ToString("0"), cfb.ToString("0.00"), crown.ToString(), scorch.ToString("0.0"),
                cons.SurfaceTotal.ToString("0.000"), cons.Duff.ToString("0.000"), cons.Canopy.ToString("0.000"), canopyMortality.ToString("0.00"), severity.ToString() }));
        }

        public void Close()
        {
            if (diagnostics != null) { diagnostics.Flush(); diagnostics.Close(); diagnostics = null; }
        }
    }

    public static class MetadataHandler
    {
        public static ExtensionMetadata Extension;

        public static void Initialize(ICore core, Logs logs, InputParameters p)
        {
            ScenarioReplicationMetadata scenRep = new ScenarioReplicationMetadata()
            {
                RasterOutCellArea = core.CellArea,
                TimeMin = core.StartTime,
                TimeMax = core.EndTime
            };
            Extension = new ExtensionMetadata(core) { Name = PlugIn.ExtensionName, TimeInterval = 1, ScenarioReplicationMetadata = scenRep };
            AddTable("EMBER_Events", logs.Events.FilePath, typeof(EventLog));
            AddTable("EMBER_Daily", logs.Daily.FilePath, typeof(DailyLog));
            AddTable("EMBER_Ignitions", logs.Ignitions.FilePath, typeof(IgnitionLog));
            AddTable("EMBER_Summary", logs.Summary.FilePath, typeof(SummaryLog));
            AddMap("Severity", Path.Combine(p.MapOutputFolder, "severity-{timestep}.tif"), MapDataType.Ordinal, FieldUnits.Severity_Rank);
            AddMap("FirelineIntensity", Path.Combine(p.MapOutputFolder, "fireline-intensity-{timestep}.tif"), MapDataType.Continuous, "kW/m");
            AddMap("FlameLength", Path.Combine(p.MapOutputFolder, "flame-length-dm-{timestep}.tif"), MapDataType.Continuous, "dm");
            AddMap("CrownFireClass", Path.Combine(p.MapOutputFolder, "crown-fire-class-{timestep}.tif"), MapDataType.Nominal, FieldUnits.None);
            AddMap("EventID", Path.Combine(p.MapOutputFolder, "event-id-{timestep}.tif"), MapDataType.Nominal, "Index");
            AddMap("DayOfBurn", Path.Combine(p.MapOutputFolder, "day-of-burn-{timestep}.tif"), MapDataType.Ordinal, "Day of year");
            AddMap("CanopyMortality", Path.Combine(p.MapOutputFolder, "canopy-mortality-pct-{timestep}.tif"), MapDataType.Continuous, FieldUnits.Percentage);
            MetadataProvider mp = new MetadataProvider(Extension);
            mp.WriteMetadataToXMLFile("Metadata", Extension.Name, Extension.Name);
        }

        static void AddTable(string name, string path, Type type)
        {
            OutputMetadata om = new OutputMetadata() { Type = OutputType.Table, Name = name, FilePath = path, Visualize = false };
            om.RetriveFields(type);
            Extension.OutputMetadatas.Add(om);
        }

        static void AddMap(string name, string path, MapDataType type, string unit)
        {
            Extension.OutputMetadatas.Add(new OutputMetadata() { Type = OutputType.Map, Name = name, FilePath = path, Map_DataType = type, Map_Unit = unit, Visualize = true });
        }
    }
}
