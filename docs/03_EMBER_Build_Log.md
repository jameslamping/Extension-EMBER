# EMBER build log

Running notes on implementation and testing decisions, newest last.

## 2026-09-08 — Phase 1 and 2 in one day

**Decisions confirmed by James:** name EMBER; Rothermel / Scott & Burgan behavior core;
extension-owned behavior fuel pools; deterministic minimum-travel-time spread with lognormal
ROS noise; fractional cohort mortality; Mount Rainier (MORA) as the first landscape, using
the SCF-calibrated historical run at `~/Desktop/LANDIS/EMBER/MORA/Hist/Rep1` as reference.

**Facts verified against the shipped binaries that changed the implementation**

* UCL v2 `IDisturbance.ReduceOrKillMarkedCohort` returns a fraction; the library only reduces
  `Biomass`, and NECN rebuilds `Biomass` from its `LeafBiomass` / `WoodBiomass` attributes.
  EMBER therefore scales those attributes itself in a `Cohort.MortalityEvent` handler that
  runs after NECN's (subscription order), so NECN's litter routing sees the pre-scaling values.
* NECN's `ComputeNonWoodyPercentage` throws `NotImplementedException`, so foliage must be
  read from the `LeafBiomass` attribute (fallback: species foliage fraction).
* `Landis.Core` 3.0.1 restores from the LANDIS MyGet feed on this Mac, so the extension
  builds locally with the .NET 10 SDK (netstandard2.0).
* The console only loads assemblies listed in `Landis.Console.deps.json`; the dev image
  patches that file instead of rebuilding the console.

**Code** — `EMBER/src` (≈2 300 lines): `FireBehavior/` pure library (Rothermel with
Albini corrections and the Andrews 2013 wind limit, dynamic herb curing, Van Wagner crown
initiation, Cruz 2005 crown ROS, Anderson ellipse, FWI-code moisture conversions, FBP
foliar moisture curve, Consume-style consumption, Van Wagner scorch, Ryan & Reinhardt
mortality); extension classes for parameters, tables, site variables, weather access,
moisture strata, fuelbed builder, fuel pools, ignition (alias sampler, Poisson/ZIP), MTT
spread (binary heap, day-by-day), effects, suppression, outputs, logs.
11 xUnit tests pass, including Rothermel vs Anderson (1982) fuel models 1 and 10.

**Test scenario** — `EMBER/testing/MORA-window`: MORA inputs bind-mounted read-only at
`/mora`, ecoregion map zeroed outside rows 320–440 × cols 320–440 (9 419 active cells, 31
climate regions). NECN ≈ 45 s per year under amd64 emulation; EMBER ≈ 0.5 s per year
including 25 map writes.

**Bugs found by the first runs**

1. Multi-day spread stalled: burned frontier cells had their fine fuel consumed, so
   recomputing their behavior the next day made them "unburnable" and they seeded nothing.
   Fix: travel time uses the receiving cell's ROS (FlamMap convention), with the source's
   same-day cache only when still valid.
2. 100-h moisture came from the Duff Moisture Code conversion (100–250 %). Now lags toward
   1.3 × fine-fuel moisture with a 100-h time constant; DMC still seeds the first value.
3. Fires never ended in dry spells: 1-hour minimum burn period at any FWI. Added
   `BurnPeriodFWILow` as a hard floor (no active spread below it), `RainoutPrecipitation`
   (no spread on days with ≥ 0.3 cm), raised the default `ExtinctionIntensity` to 30 kW/m,
   and ignitions on zero-burn-period days now fail.
4. `IgnitionAreaScaling Landscape` puts the whole landscape's ignition count into whatever
   is active. For windowed tests use `PerHectare` with SCF intercepts shifted by
   −ln(398 065 ha).

**First results (run 2, before fixes 3–4)** — year 1 (climate 2020) burned 1 998 cells;
its largest fire ignited Aug 27, exploded during the Sept 7–8 Labor Day east-wind event
(FWI 46, 1.5 MW/m) and crept for 24 days. NECN logged fire C efflux in every burned
ecoregion, confirming the `Fire.Severity` coupling. SCF reference inside the same window:
84 cells/yr mean, median 0, max 1 629 over 80 years; events median 10 cells, max 6 days.

**Run 3 (all fixes, per-hectare ignitions), 5 windowed years:** 1 fire in 5 years, 80 cells
(65 ha), ignited Aug 16 of climate-year 2020 at FWI 25, grew six days at 170 kW/m mean
intensity (max 1 150), ended when FWI fell below 8. Window comparison: EMBER 16 cells/yr
mean vs SCF 84 (median 0 for both, SCF max 1 629 over 80 years); five years is too short
to calibrate against, but the order of magnitude and the seasonality are right. Year-1
fuel maps: fine fuel median 1.1 kg/m², canopy bulk density median 0.05 (p90 0.15) kg/m³,
canopy base height median 6 m, Aug-1 moisture 13 % / 29 % / 112 % (1-h / 1000-h / foliar).
No crown fire yet: with CBH ≈ 6 m the initiation threshold is ≈ 2 900 kW/m.

## Calibration pass (2026-09-08/09) — structural changes made from the diagnosis

Record and R scripts in `EMBER/calibration/README.md`. Three changes to the source after
pass 2, all optional parameters with the new behavior as default:

1. `IgnitionAlwaysBurns` (default yes): an ignition attempt whose cell cannot carry fire is
   a one-cell fire with no spread (end reason Unsustained) instead of a failed attempt. All
   failed attempts had been on FWI ≤ 5 days; fire-record coefficients count those starts.
2. `CrownInitiation Cruz2004` (default): crown fire probability from Cruz, Alexander and
   Wakimoto (2004) — 10-m wind, canopy base height, 1-h moisture, surface fuel consumed —
   in addition to the Van Wagner intensity criterion. The Rothermel/Van Wagner linkage under
   a 100 % canopy (WAF 0.2) never reached the 1 900 kW/m median critical intensity; the
   record's stand-replacing fires grew on 14–17 km/h days (drought-, not wind-driven).
3. `CoarseFuelFlamingFraction` (default 0.3): the flaming share of the 1000-h consumption is
   added to Byram's intensity, so intensity, scorch and crown initiation respond to drought.

Effect (pass 4 vs pass 3, 160 window-years each): fires per attempt 0.29 → 0.98, high
severity share of burned area 0.001 → 0.36 (MTBS 0.42), canopy mortality 0.07 → 0.41.
Suppression coefficients then had to be rescaled to the larger intensities (passes 5–6).

## Open items

* Calibration: see `EMBER/calibration/README.md` (passes 1–6 on the MORA window); the full-
  landscape comparison with SCF is still to run with the calibrated set.
* Rx fire, spotting, delayed mortality, hazard mode, replay mode (design phase 3).
* NECN patch to read consumption fractions (design phase 5).
* Windowed ecoregion map: strata count scales with ecoregions × 9 (1 566 here), fine.
