// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Small CSV reader and the two CSV tables (species fire parameters, fuelbed types).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Landis.Core;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class CsvTable
    {
        public List<string> Header = new List<string>();
        public List<string[]> Rows = new List<string[]>();
        Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static CsvTable Read(string path)
        {
            if (!File.Exists(path))
                throw new ApplicationException(string.Format("EMBER: CSV file '{0}' does not exist.", path));
            CsvTable t = new CsvTable();
            using (StreamReader r = new StreamReader(path))
            {
                string line;
                bool first = true;
                while ((line = r.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(">>")) continue;
                    string[] fields = line.Split(',');
                    for (int i = 0; i < fields.Length; i++) fields[i] = fields[i].Trim().Trim('"');
                    if (first)
                    {
                        first = false;
                        for (int i = 0; i < fields.Length; i++) { t.Header.Add(fields[i]); t.index[fields[i]] = i; }
                    }
                    else t.Rows.Add(fields);
                }
            }
            return t;
        }

        public bool Has(string column) { return index.ContainsKey(column); }

        public string Get(string[] row, string column)
        {
            int i;
            if (!index.TryGetValue(column, out i))
                throw new ApplicationException(string.Format("EMBER: CSV is missing required column '{0}'.", column));
            return i < row.Length ? row[i] : "";
        }

        public double GetDouble(string[] row, string column, double? defaultValue = null)
        {
            int i;
            if (!index.TryGetValue(column, out i) || i >= row.Length || row[i].Length == 0)
            {
                if (defaultValue.HasValue) return defaultValue.Value;
                throw new ApplicationException(string.Format("EMBER: CSV is missing required column '{0}'.", column));
            }
            double v;
            if (!double.TryParse(row[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new ApplicationException(string.Format("EMBER: cannot parse '{0}' in column '{1}' as a number.", row[i], column));
            return v;
        }

        public string GetString(string[] row, string column, string defaultValue)
        {
            int i;
            if (!index.TryGetValue(column, out i) || i >= row.Length || row[i].Length == 0) return defaultValue;
            return row[i];
        }
    }

    public static class Tables
    {
        public static void ReadFuelbeds(string path, InputParameters p)
        {
            CsvTable t = CsvTable.Read(path);
            int k = 0;
            foreach (string[] row in t.Rows)
            {
                FuelbedType fb = new FuelbedType();
                fb.Name = t.Get(row, "Name");
                fb.Sav1h = t.GetDouble(row, "SAV1h", 2000);
                fb.Sav10h = t.GetDouble(row, "SAV10h", 109);
                fb.Sav100h = t.GetDouble(row, "SAV100h", 30);
                fb.SavHerb = t.GetDouble(row, "SAVHerb", 1800);
                fb.SavWoody = t.GetDouble(row, "SAVWoody", 1500);
                fb.HeatContent = t.GetDouble(row, "HeatContent", 18608);
                fb.MoistureOfExtinction = t.GetDouble(row, "MoistureOfExtinction", 25) / 100.0;
                fb.PackingRatio = t.GetDouble(row, "PackingRatio", 0.01);
                fb.LitterFraction1h = t.GetDouble(row, "LitterFraction1h", 0.7);
                fb.DuffBulkDensity = t.GetDouble(row, "DuffBulkDensity", 100);
                fb.Index = k++;
                if (p.Fuelbeds.ContainsKey(fb.Name))
                    throw new ApplicationException(string.Format("EMBER: fuelbed '{0}' is listed twice.", fb.Name));
                p.Fuelbeds[fb.Name] = fb;
                p.FuelbedList.Add(fb);
            }
            if (p.FuelbedList.Count == 0)
                throw new ApplicationException("EMBER: the fuelbed table has no rows.");
        }

        public static void ReadSpecies(string path, InputParameters p, ISpeciesDataset species)
        {
            CsvTable t = CsvTable.Read(path);
            p.SpeciesParameters = new SpeciesFireParameters[species.Count];
            foreach (string[] row in t.Rows)
            {
                string code = t.Get(row, "SpeciesCode");
                ISpecies sp = species[code];
                if (sp == null)
                    throw new ApplicationException(string.Format("EMBER: species '{0}' in the species table is not in the scenario species list.", code));
                SpeciesFireParameters s = new SpeciesFireParameters();
                s.Species = sp;
                s.LifeForm = ParseEnum<LifeForm>(t.GetString(row, "LifeForm", "Tree"), "LifeForm");
                s.LitterClass = t.Get(row, "LitterClass");
                FuelbedType fb;
                if (!p.Fuelbeds.TryGetValue(s.LitterClass, out fb))
                    throw new ApplicationException(string.Format("EMBER: species '{0}' has litter class '{1}' which is not in the fuelbed table.", code, s.LitterClass));
                s.Fuelbed = fb;
                s.MaxHeight = t.GetDouble(row, "MaxHeight", 40);
                s.HeightK = t.GetDouble(row, "HeightK", 0.03);
                s.CrownRatio = t.GetDouble(row, "CrownRatio", 0.5);
                s.FineBranchFraction = t.GetDouble(row, "FineBranchFraction", 0.3);
                s.BarkThicknessMax = t.GetDouble(row, "BarkThicknessMax", 3.0);
                s.AgeDBH = t.GetDouble(row, "AgeDBH", 100);
                s.FoliarClass = ParseEnum<FoliarMoistureClass>(t.GetString(row, "FoliarMoistureClass", s.LifeForm == LifeForm.Tree ? "Conifer" : (s.LifeForm == LifeForm.Shrub ? "Shrub" : "Herb")), "FoliarMoistureClass");
                s.FoliageFraction = t.GetDouble(row, "FoliageFraction", s.LifeForm == LifeForm.Tree ? 0.05 : (s.LifeForm == LifeForm.Shrub ? 0.3 : 0.9));
                s.SpecificLeafArea = t.GetDouble(row, "SpecificLeafArea", 5.0);
                s.CoarseWoodFraction1000h = t.GetDouble(row, "CoarseWoodFraction1000h", 0.7);
                p.SpeciesParameters[sp.Index] = s;
            }
            foreach (ISpecies sp in species)
                if (p.SpeciesParameters[sp.Index] == null)
                    throw new ApplicationException(string.Format("EMBER: species '{0}' is missing from the species fire table.", sp.Name));
        }

        public static T ParseEnum<T>(string word, string what) where T : struct
        {
            T v;
            if (Enum.TryParse<T>(word, true, out v)) return v;
            throw new ApplicationException(string.Format("EMBER: '{0}' is not a valid {1}. Valid values: {2}", word, what, string.Join(", ", Enum.GetNames(typeof(T)))));
        }
    }
}
