// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Thin accessor over the Climate Library's per-ecoregion daily weather for the current year.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.Library.Climate;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public struct DailyWeather
    {
        public double FFMC, DMC, DC, BUI, FWI, ISI;
        public double WindSpeed_kmh;     // 10-m wind
        public double WindTo_deg;        // direction the wind blows toward
        public double Tmax, RH, Precip_cm;
        public bool Valid;
    }

    public class Weather
    {
        readonly ICore core;
        AnnualClimate[] annual;          // by ecoregion index, null for inactive
        public int Year;                 // simulation year (1-based)
        public int CalendarYear;
        public int[] BeginGrowing, EndGrowing;
        public int EcoregionCount;

        public Weather(ICore core)
        {
            this.core = core;
            EcoregionCount = core.Ecoregions.Count;
        }

        public void LoadYear(int simulationYear)
        {
            Year = simulationYear;
            annual = new AnnualClimate[EcoregionCount];
            BeginGrowing = new int[EcoregionCount];
            EndGrowing = new int[EcoregionCount];
            if (Climate.FutureEcoregionYearClimate == null)
                throw new ApplicationException("EMBER: the Climate Library has not been initialized (the succession extension must load it).");
            for (int e = 0; e < EcoregionCount; e++)
            {
                if (!core.Ecoregions[e].Active) continue;
                List<AnnualClimate> list = Climate.FutureEcoregionYearClimate[e];
                if (list == null) continue;
                if (simulationYear >= list.Count)
                    throw new ApplicationException(string.Format("EMBER: no climate data for simulation year {0} in ecoregion {1}.", simulationYear, core.Ecoregions[e].Name));
                AnnualClimate ac = list[simulationYear];
                if (ac == null) continue;
                if (ac.DailyFireWeatherIndex == null || ac.DailyMaxTemp == null)
                    throw new ApplicationException("EMBER requires DAILY climate data with UsingFireClimate yes in the climate configuration file.");
                annual[e] = ac;
                BeginGrowing[e] = ac.BeginGrowingDay;
                EndGrowing[e] = ac.EndGrowingDay;
                if (CalendarYear == 0) CalendarYear = ac.CalendarYear;
            }
            if (Climate.FutureTimeStep != TimeSeriesTimeStep.Daily)
                throw new ApplicationException("EMBER requires a Daily climate time series.");
        }

        public bool HasClimate(int ecoregionIndex)
        {
            return annual != null && ecoregionIndex >= 0 && ecoregionIndex < annual.Length && annual[ecoregionIndex] != null;
        }

        public DailyWeather Get(int ecoregionIndex, int day0)
        {
            DailyWeather w = new DailyWeather();
            AnnualClimate ac = annual[ecoregionIndex];
            if (ac == null || day0 < 0 || day0 > 364) return w;
            w.FFMC = ac.DailyFineFuelMoistureCode[day0];
            w.DMC = ac.DailyDuffMoistureCode[day0];
            w.DC = ac.DailyDroughtCode[day0];
            w.BUI = ac.DailyBuildUpIndex[day0];
            w.FWI = ac.DailyFireWeatherIndex[day0];
            w.WindSpeed_kmh = Sane(ac.DailyWindSpeed[day0], 0);
            w.WindTo_deg = Sane(ac.DailyWindDirection[day0], 0);
            w.Tmax = Sane(ac.DailyMaxTemp[day0], 15);
            double rh = ac.DailyRH != null ? ac.DailyRH[day0] : double.NaN;
            if (double.IsNaN(rh) && ac.DailyMinRH != null) rh = ac.DailyMinRH[day0];
            w.RH = Sane(rh, 50);
            w.Precip_cm = Sane(ac.DailyPrecip[day0], 0);
            w.ISI = FuelMoisture.InitialSpreadIndex(w.FFMC, w.WindSpeed_kmh);
            w.Valid = w.FFMC > 0;   // codes are zero outside the fire season window
            return w;
        }

        static double Sane(double v, double fallback)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? fallback : v;
        }
    }
}
