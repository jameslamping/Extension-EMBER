// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Random number helpers, alias-method site sampler and the daily ignition model.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.SpatialModeling;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    /// <summary>All stochastic draws go through the core RNG so RandomNumberSeed reproduces runs.</summary>
    public static class Rng
    {
        static ICore core;
        public static void Initialize(ICore c) { core = c; }
        public static double Uniform() { return core.GenerateUniform(); }

        public static double Normal()
        {
            double u1 = 1.0 - Uniform(), u2 = Uniform();
            return Math.Sqrt(-2.0 * Math.Log(Math.Max(u1, 1e-300))) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>Lognormal multiplier with mean 1: exp(sigma z - sigma^2/2).</summary>
        public static double LogNormalFactor(double sigma)
        {
            if (sigma <= 0) return 1.0;
            return Math.Exp(sigma * Normal() - 0.5 * sigma * sigma);
        }

        public static int Poisson(double lambda)
        {
            if (lambda <= 0) return 0;
            if (lambda < 30)
            {
                double L = Math.Exp(-lambda), pr = 1.0; int k = 0;
                do { k++; pr *= Uniform(); } while (pr > L);
                return k - 1;
            }
            double g = lambda + Math.Sqrt(lambda) * Normal() + 0.5;
            return g < 0 ? 0 : (int)g;
        }
    }

    /// <summary>Vose alias method for O(1) weighted sampling of cells.</summary>
    public class AliasSampler
    {
        int[] items;
        int[] alias;
        double[] prob;
        public int Count { get { return items == null ? 0 : items.Length; } }
        public double TotalWeight;

        public AliasSampler(List<int> cells, List<double> weights)
        {
            int n = cells.Count;
            if (n == 0) return;
            items = cells.ToArray();
            alias = new int[n]; prob = new double[n];
            double sum = 0; foreach (double w in weights) sum += w;
            TotalWeight = sum;
            double[] scaled = new double[n];
            for (int i = 0; i < n; i++) scaled[i] = weights[i] * n / sum;
            Stack<int> small = new Stack<int>(), large = new Stack<int>();
            for (int i = 0; i < n; i++) { if (scaled[i] < 1.0) small.Push(i); else large.Push(i); }
            while (small.Count > 0 && large.Count > 0)
            {
                int s = small.Pop(), l = large.Pop();
                prob[s] = scaled[s]; alias[s] = l;
                scaled[l] = scaled[l] + scaled[s] - 1.0;
                if (scaled[l] < 1.0) small.Push(l); else large.Push(l);
            }
            while (large.Count > 0) prob[large.Pop()] = 1.0;
            while (small.Count > 0) prob[small.Pop()] = 1.0;
        }

        public int Sample()
        {
            int n = items.Length;
            int i = (int)(Rng.Uniform() * n); if (i >= n) i = n - 1;
            return Rng.Uniform() < prob[i] ? items[i] : items[alias[i]];
        }
    }

    public struct IgnitionAttempt
    {
        public int Cell;
        public IgnitionCause Cause;
    }

    public class IgnitionModel
    {
        readonly ICore core;
        readonly InputParameters p;
        readonly int ecoCount;
        AliasSampler[][] samplers = new AliasSampler[2][];   // [cause][ecoregion]
        double[] ecoAreaFraction;                              // active area fraction per ecoregion
        double[] ecoAreaHa;
        public float[] LightningWeight, AccidentalWeight;      // per cell, may be null (uniform)

        public IgnitionModel(ICore core, InputParameters p)
        {
            this.core = core;
            this.p = p;
            ecoCount = core.Ecoregions.Count;
            int[] counts = new int[ecoCount];
            for (int i = 1; i < CellData.Count; i++) counts[CellData.EcoregionIndex[i]]++;
            ecoAreaFraction = new double[ecoCount];
            ecoAreaHa = new double[ecoCount];
            int total = CellData.Count - 1;
            for (int e = 0; e < ecoCount; e++)
            {
                ecoAreaFraction[e] = total > 0 ? (double)counts[e] / total : 0;
                ecoAreaHa[e] = counts[e] * core.CellArea;
            }
        }

        public void BuildSamplers()
        {
            samplers[0] = Build(LightningWeight);
            samplers[1] = Build(AccidentalWeight);
        }

        AliasSampler[] Build(float[] weight)
        {
            List<int>[] cells = new List<int>[ecoCount];
            List<double>[] w = new List<double>[ecoCount];
            for (int e = 0; e < ecoCount; e++) { cells[e] = new List<int>(); w[e] = new List<double>(); }
            for (int i = 1; i < CellData.Count; i++)
            {
                double wt = weight == null ? 1.0 : weight[i];
                if (wt <= 0) continue;
                int e = CellData.EcoregionIndex[i];
                cells[e].Add(i); w[e].Add(wt);
            }
            AliasSampler[] s = new AliasSampler[ecoCount];
            for (int e = 0; e < ecoCount; e++) s[e] = new AliasSampler(cells[e], w[e]);
            return s;
        }

        double FireDanger(DailyWeather w)
        {
            return p.IgnitionFireDanger == FireDangerIndex.ISI ? w.ISI : w.FWI;
        }

        /// <summary>Draw today's ignition attempts for both causes.</summary>
        public List<IgnitionAttempt> Draw(Weather weather, int day0, out double landscapeFireDanger)
        {
            List<IgnitionAttempt> list = new List<IgnitionAttempt>();
            double[] fd = new double[ecoCount];
            double meanFd = 0, wsum = 0;
            for (int e = 0; e < ecoCount; e++)
            {
                if (!weather.HasClimate(e) || ecoAreaFraction[e] <= 0) { fd[e] = double.NaN; continue; }
                DailyWeather w = weather.Get(e, day0);
                fd[e] = w.Valid ? FireDanger(w) : 0.0;
                meanFd += fd[e] * ecoAreaFraction[e]; wsum += ecoAreaFraction[e];
            }
            landscapeFireDanger = wsum > 0 ? meanFd / wsum : 0.0;
            if (day0 < p.FirstIgnitionDay || day0 > p.LastIgnitionDay) return list;

            for (int c = 0; c < 2; c++)
            {
                IgnitionCause cause = (IgnitionCause)c;
                double b0 = c == 0 ? p.LightningB0 : p.AccidentalB0;
                double b1 = c == 0 ? p.LightningB1 : p.AccidentalB1;
                if (p.IgnitionDist == IgnitionDistribution.ZeroInflatedPoisson)
                {
                    double z0 = c == 0 ? p.LightningBinomialB0 : p.AccidentalBinomialB0;
                    double z1 = c == 0 ? p.LightningBinomialB1 : p.AccidentalBinomialB1;
                    double alpha = Math.Exp(z0 + z1 * landscapeFireDanger);
                    double pZero = alpha / (alpha + 1.0);
                    if (Rng.Uniform() < pZero) continue;
                }
                for (int e = 0; e < ecoCount; e++)
                {
                    if (double.IsNaN(fd[e]) || samplers[c][e] == null || samplers[c][e].Count == 0) continue;
                    double scale = p.IgnitionAreaScaling == AreaScaling.Landscape ? ecoAreaFraction[e] : ecoAreaHa[e];
                    double lambda = scale * Math.Exp(b0 + b1 * fd[e]);
                    int n = Rng.Poisson(lambda);
                    for (int k = 0; k < n; k++)
                    {
                        IgnitionAttempt a = new IgnitionAttempt();
                        a.Cell = samplers[c][e].Sample();
                        a.Cause = cause;
                        list.Add(a);
                    }
                }
            }
            return list;
        }

        /// <summary>Probability that an ignition sustains flaming, logistic in FFMC (Beverly &amp; Wotton 2007 form).</summary>
        public double SustainProbability(double ffmc)
        {
            return Units.Logistic(p.IgnitionSustainB0 + p.IgnitionSustainB1 * ffmc);
        }
    }
}
