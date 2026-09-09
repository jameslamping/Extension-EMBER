// EMBER: Ecosystem-coupled Moisture, Behavior, Effects and Regimes.
// Initial attack and containment as logistic functions of intensity, size, zone and
// concurrent fire load, with a daily cap on the number of fires that receive resources.
using System;
using System.Collections.Generic;
using Landis.Core;
using Landis.Extension.EMBER.FireBehavior;

namespace Landis.Extension.EMBER
{
    public class Suppression
    {
        readonly ICore core;
        readonly InputParameters p;

        public Suppression(ICore core, InputParameters p) { this.core = core; this.p = p; }

        /// <summary>Apply end-of-day suppression decisions to the active fires. Returns number contained today.</summary>
        public int EndOfDay(List<FireEvent> active, int day0)
        {
            if (!p.UseSuppression) return 0;
            int load = 0;
            foreach (FireEvent f in active) if (!f.Ended) load++;
            // resources go to the largest fires first
            List<FireEvent> ordered = new List<FireEvent>(active);
            ordered.Sort((a, b) => b.Cells.Count.CompareTo(a.Cells.Count));
            int contained = 0, served = 0;
            foreach (FireEvent f in ordered)
            {
                if (f.Ended) continue;
                int zone = CellData.Zone[f.IgnitionCell];
                f.SuppressedToday = false;
                if (zone == 0) continue;
                if (served >= p.MaxSuppressedFires) continue;
                served++;
                f.SuppressedToday = true;
                double area = f.AreaHa(core.CellArea);
                double logit;
                if (f.DaysActive <= 1)
                    logit = p.IA_B0 + p.IA_B1 * Math.Log(area + 0.01) + p.IA_B2 * f.MaxDayIntensity + p.IA_B3 * zone + p.IA_B4 * load;
                else
                    logit = p.EA_B0 + p.EA_B1 * Math.Log(area + 0.01) + p.EA_B2 * f.MaxDayRos + p.EA_B3 * zone + p.EA_B4 * load + p.EA_B5 * f.BurnHoursToday;
                if (Rng.Uniform() < Units.Logistic(logit))
                {
                    f.Ended = true; f.Contained = true; f.EndDay = day0 + 1;
                    f.EndReason = f.DaysActive <= 1 ? "InitialAttack" : "Contained";
                    contained++;
                }
            }
            return contained;
        }
    }
}
