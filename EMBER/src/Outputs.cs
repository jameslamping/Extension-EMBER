// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Raster input and output.
using System;
using System.IO;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Utilities;

namespace Landis.Extension.EMBER
{
    public static class MapReader
    {
        /// <summary>Read a raster into a per-cell float array (by DataIndex). NoData/NaN become the fallback.</summary>
        public static float[] Read(ICore core, string path, float fallback, string what)
        {
            if (string.IsNullOrEmpty(path)) return null;
            IInputRaster<DoublePixel> map;
            try { map = core.OpenRaster<DoublePixel>(path); }
            catch (FileNotFoundException) { throw new ApplicationException(string.Format("EMBER: the {0} map '{1}' does not exist.", what, path)); }
            if (map.Dimensions != core.Landscape.Dimensions)
                throw new ApplicationException(string.Format("EMBER: the {0} map '{1}' does not have the same dimensions as the ecoregion map.", what, path));
            float[] values = new float[CellData.Count];
            using (map)
            {
                DoublePixel pixel = map.BufferPixel;
                foreach (Site site in core.Landscape.AllSites)
                {
                    map.ReadBufferPixel();
                    if (!site.IsActive) continue;
                    double v = pixel.MapCode.Value;
                    if (double.IsNaN(v) || double.IsInfinity(v) || v <= -9999 || v >= 1e30) v = fallback;
                    values[site.DataIndex] = (float)v;
                }
            }
            core.UI.WriteLine("   EMBER read {0} map {1}", what, path);
            return values;
        }
    }

    public class Outputs
    {
        readonly ICore core;
        readonly InputParameters p;

        public Outputs(ICore core, InputParameters p) { this.core = core; this.p = p; }

        string Path(string name, int year)
        {
            return System.IO.Path.Combine(p.MapOutputFolder, name + "-" + year + ".tif");
        }

        delegate double CellValue(int i);

        void WriteShort(string name, int year, CellValue f, short inactive = 0)
        {
            using (IOutputRaster<ShortPixel> r = core.CreateRaster<ShortPixel>(Path(name, year), core.Landscape.Dimensions))
            {
                ShortPixel px = r.BufferPixel;
                foreach (Site s in core.Landscape.AllSites)
                {
                    if (s.IsActive)
                    {
                        double v = f((int)s.DataIndex);
                        px.MapCode.Value = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(v)));
                    }
                    else px.MapCode.Value = inactive;
                    r.WriteBufferPixel();
                }
            }
        }

        void WriteInt(string name, int year, CellValue f)
        {
            using (IOutputRaster<IntPixel> r = core.CreateRaster<IntPixel>(Path(name, year), core.Landscape.Dimensions))
            {
                IntPixel px = r.BufferPixel;
                foreach (Site s in core.Landscape.AllSites)
                {
                    if (s.IsActive) px.MapCode.Value = (int)Math.Round(f((int)s.DataIndex));
                    else px.MapCode.Value = 0;
                    r.WriteBufferPixel();
                }
            }
        }

        public void WriteFireMaps(int year)
        {
            WriteShort("severity", year, i => CellData.BurnedYear[i] == year ? CellData.SeverityClass[i] : 0);
            WriteInt("event-id", year, i => CellData.BurnedYear[i] == year ? CellData.EventId[i] : 0);
            WriteShort("day-of-burn", year, i => CellData.BurnedYear[i] == year ? CellData.DayOfBurn[i] : 0);
            WriteInt("fireline-intensity", year, i => CellData.BurnedYear[i] == year ? CellData.Intensity[i] : 0);
            WriteShort("flame-length-dm", year, i => CellData.BurnedYear[i] == year ? CellData.FlameLength[i] * 10.0 : 0);
            WriteShort("crown-fire-class", year, i => CellData.BurnedYear[i] == year ? CellData.CrownClass[i] : 0);
            WriteShort("rate-of-spread-dm-min", year, i => CellData.BurnedYear[i] == year ? CellData.Ros[i] * 10.0 : 0);
            WriteShort("canopy-mortality-pct", year, i => CellData.BurnedYear[i] == year ? CellData.CanopyMortality[i] * 100.0 : 0);
            WriteInt("consumption-kg-ha", year, i => CellData.BurnedYear[i] == year ? CellData.ConsumptionTotal[i] * 10000.0 : 0);
            WriteInt("pm25-kg-ha", year, i => CellData.BurnedYear[i] == year ? CellData.PM25[i] * 10.0 : 0);
            WriteShort("cause", year, i => CellData.BurnedYear[i] == year ? SiteVars.Cause[CellData.Site[i]] : 0);
        }

        public void WriteFuelMaps(int year)
        {
            WriteInt("fuel-fine-g-m2", year, i => (CellData.W1h[i] + CellData.W10h[i]) * 1000.0);
            WriteInt("fuel-coarse-wood-g-m2", year, i => (CellData.W100h[i] + CellData.W1000h[i]) * 1000.0);
            WriteInt("fuel-duff-g-m2", year, i => CellData.Duff[i] * 1000.0);
            WriteInt("fuel-live-surface-g-m2", year, i => (CellData.WHerb[i] + CellData.WWoody[i]) * 1000.0);
            WriteShort("canopy-bulk-density-x1000", year, i => CellData.CBD[i] * 1000.0);
            WriteShort("canopy-base-height-dm", year, i => CellData.CBH[i] * 10.0);
            WriteInt("canopy-fuel-load-g-m2", year, i => CellData.CanopyFuelLoad[i] * 1000.0);
            WriteInt("ladder-fuel-g-m2", year, i => CellData.LadderFuel[i] * 1000.0);
            WriteShort("canopy-cover-pct", year, i => CellData.CanopyCover[i] * 100.0);
            WriteShort("fuelbed-type", year, i => CellData.FuelbedIndex[i] + 1);
            WriteShort("time-since-fire", year, i => CellData.BurnedYear[i] > 0 ? year - CellData.BurnedYear[i] : -1, -1);
        }

        public void WriteMoistureMaps(int year, MoistureStrata strata, string suffix)
        {
            WriteShort("moisture-1h-pct-" + suffix, year, i => strata.Get(i).M1h * 100.0);
            WriteShort("moisture-1000h-pct-" + suffix, year, i => strata.Get(i).M1000h * 100.0);
            WriteShort("moisture-foliar-pct-" + suffix, year, i => strata.Get(i).MFoliar * 100.0);
        }
    }
}
