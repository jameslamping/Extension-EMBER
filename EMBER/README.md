# EMBER — Ecosystem-coupled Moisture, Behavior, Effects and Regimes

A mechanistic, climate-sensitive fire disturbance extension for LANDIS-II v8 (Universal
Cohort Library v2, Climate Library v6). Design rationale: `../docs/02_EMBER_Design.md`.

## What it does each year

1. Rebuilds a fuelbed for every active cell from the cohorts: fine surface fuel (synced to
   the succession extension's litter), extension-owned 100-h / 1000-h wood and duff pools,
   live herb and shrub fuel, and a canopy profile giving canopy fuel load, effective canopy
   bulk density, canopy base height and ladder fuel (Scott & Reinhardt 2001).
2. Each day, converts the Climate Library's FFMC / DMC / DC per ecoregion into 1-h, 10-h,
   100-h, 1000-h and duff moisture for ~9 strata per ecoregion (canopy cover × topographic
   exposure) and live moisture from a Drought Code index (conifer foliar moisture follows
   the FBP seasonal curve).
3. Draws ignitions per ecoregion per cause from Poisson / zero-inflated Poisson models of
   daily FWI (SCRPPLE-compatible coefficients), places them with the ignition rasters and
   tests whether they sustain (logistic in FFMC).
4. Grows each fire one day at a time by minimum travel time on an 8-neighbour grid using
   Rothermel surface fire behavior, Van Wagner / Cruz crown fire behavior and an
   elliptical spread shape; the daily burn period is a function of FWI; cells whose
   intensity falls below an extinction threshold do not burn.
5. Applies effects to each burned cell as it burns: consumption by fuel class from class
   moisture, emissions, Van Wagner scorch height, Ryan & Reinhardt mortality per cohort
   (fractional cohort removal by default), a severity class for the succession
   extension's fire-reduction table, and pool bookkeeping.
6. Optional suppression: initial attack and daily containment as logistic functions of
   intensity, size, zone and the number of concurrent fires, with a resource cap.
7. Writes maps and CSV logs.

## User guide

`docs/LANDIS-II EMBER v1.0 User Guide.docx` (and .pdf) follows the LANDIS-II extension
user-guide format: model description, parameter reference, outputs, sample input. The
Markdown master is beside it; rebuild the .docx with
`node docs/md2docx.js docs/<guide>.md docs/<guide>.docx` (needs the `docx` npm package).

## Layout

```
docs/                user guide (.md master, .docx, .pdf)
src/                 Landis.Extension.EMBER-v1 (netstandard2.0)
src/FireBehavior/    pure fire behavior / effects equations, no LANDIS dependency
tests/               xUnit tests for FireBehavior (dotnet test)
deploy/installer/    LANDIS-II registration file
docker/              dev image: build.sh layers the local DLL onto the base image
testing/MORA-window/ windowed Mount Rainier test scenario (run.sh)
```

## Build and run (developer loop)

```bash
dotnet test tests/FireBehavior.Tests          # equations vs published values
./docker/build.sh                              # builds src, creates image landis-ember:dev
./testing/MORA-window/run.sh                   # runs the test scenario in the image
```

`docker/build.sh` starts from `climatelibrary:v6` (override with `BASE_IMAGE=`), copies the
DLL into `build/extensions` and `build/Release`, adds it to `Landis.Console.deps.json`
(the console only loads assemblies listed there) and registers it with the extension
admin tool. For a production image add this repository to a copy of
`extensions-v8-UCL2-release.yaml` in a new Docker build folder; the project layout
(`src/*.csproj` with `lib\` HintPaths, `deploy/installer/*.txt`) matches what the build
scripts expect.

## Scenario file

```
"EMBER"   extensions/EMBER.txt      << under Disturbance Extensions
```

EMBER needs a **daily** climate series with `UsingFireClimate yes` (wind speed and
direction, RH or dew point, precipitation, Tmax/Tmin) and a succession extension that
registers `Succession.UniversalCohorts` (NECN, PnET, Biomass Succession).

## Input file reference (parameters must appear in this order; optional ones may be omitted)

| Parameter | Default | Meaning |
|---|---|---|
| `Timestep` | required, must be 1 | annual step with an internal daily loop |
| `SpeciesTable` | required | CSV, see below |
| `FuelbedTable` | required | CSV, see below |
| `SlopeMap`, `AspectMap` | required | degrees; aspect is the downslope direction |
| `SuppressionZoneMap` | none (all zone 1) | 0 = no suppression, 1–3 increasing effort |
| `LightningIgnitionMap`, `DynamicLightningIgnitionMaps`, `AccidentalIgnitionMap`, `DynamicAccidentalIgnitionMaps` | none (uniform) | relative ignition density; dynamic tables list `year mapname` per line |
| `Latitude`, `Longitude` | 47, −120 | for the foliar moisture seasonal curve |
| `SyncFineFuelsToSuccession` | yes | use `Succession.FineFuels` as 1-h + 10-h fuel |
| `InitialCoarseWood100h`, `InitialCoarseWood1000h`, `InitialDuff` | 0.5, 3.0, 2.0 kg m⁻² | starting pools |
| `CoarseWoodDecay100h`, `CoarseWoodDecay1000h`, `DuffDecay` | 0.10, 0.03, 0.05 yr⁻¹ | pool decay |
| `DuffInputFraction`, `LitterInputFraction`, `LitterDecay`, `BackgroundWoodMortality` | 0.15, 0.30, 0.40, 0.01 | pool inputs |
| `ShadeModifier`, `TopographyModifier` | 0.15, 0.15 | ± multiplier on dead fuel moisture by cover and exposure |
| `DroughtCodeLow`, `DroughtCodeHigh` | 100, 500 | DC values mapping to drought index 0 and 1 |
| `ThousandHourMoisture`, `DuffMoisture`, `LiveHerbMoisture`, `LiveWoodyMoisture` | `12 35`, `20 150`, `30 150`, `60 130` (%) | min (drought) and max (wet) |
| `FoliarMoistureDroughtEffect` | 0.25 | fractional reduction of conifer FMC at drought index 1 |
| `BroadleafFoliarMoisture` | 120 % | |
| `IgnitionFireDanger` | FWI | FWI or ISI |
| `IgnitionAreaScaling` | Landscape | Landscape: coefficients give landscape-day counts (SCRPPLE-compatible); PerHectare: per-hectare-day |
| `LightningIgnitionsB0/B1`, `AccidentalIgnitionsB0/B1` | required | log-linear rate vs fire danger |
| `IgnitionDistribution` + `…BinomialB0/B1` | Poisson | ZeroInflatedPoisson as in SCRPPLE |
| `UseIgnitionSustain`, `IgnitionSustainB0/B1` | no, −12.5, 0.15 | optional FFMC sustain test (only for ignition-count coefficients) |
| `IgnitionRequiresBurnPeriod` | no | if no, ignitions burn their cell even on no-spread days |
| `IgnitionAlwaysBurns` | yes | an attempt whose cell cannot carry fire is still a one-cell fire (fire-record convention); no = attempt fails |
| `FirstIgnitionDay`, `LastIgnitionDay` | 0, 364 | |
| `BurnPeriodMinHours`, `BurnPeriodMaxHours`, `BurnPeriodFWILow`, `BurnPeriodFWIHigh` | 1, 10, 8, 35 | active burning hours per day vs FWI; none below the low FWI |
| `RainoutPrecipitation` | 0.3 cm | no spread on days with this much rain |
| `NaturalEndDays`, `MaxFireDays` | 2, 60 | |
| `WindAdjustmentOpen`, `WindAdjustmentSheltered`, `WindGustFactor` | 0.4, 0.15, 1.0 | midflame wind factor at cover 0 and 1; daily-wind multiplier |
| `ExtinctionIntensity` | 30 kW m⁻¹ | cells below this do not burn |
| `ROSNoiseSigma` | 0.3 | lognormal ROS noise per cell-day |
| `MinimumFineFuelLoad` | 0.05 kg m⁻² | |
| `AllowCrownFire`, `LadderMaxHeight`, `CBDThreshold`, `MaxFuelbedLoad` | yes, 4 m, 0.011, 4.0 | |
| `CrownInitiation` | Cruz2004 | crown fire initiation criterion: `VanWagner` (surface intensity ≥ critical) or `Cruz2004` (logistic probability from 10-m wind, canopy base height, fine fuel moisture and surface fuel consumed) |
| `CoarseFuelFlamingFraction` | 0.3 | share of the 1000-h consumption burning in the flaming front; its heat is added to the fireline intensity |
| `UseSuppression` + `InitialAttackB0..B4`, `ContainmentB0..B5`, `MaxSuppressedFires` | no | see design §4.5 |
| `CohortMortalityMode` | Fractional | or Binomial (whole cohort) |
| `SeverityClasses` | 10 | number of `Fire.Severity` classes (match the NECN FireReductionParameters rows) |
| `MinimumSurvivingFraction` | 0.05 | |
| `Consumption100h`, `Consumption1000h`, `ConsumptionDuff` | intercept slope pairs | consumed fraction = intercept − slope·moisture % |
| `ConsumptionLiveWoody`, `ConsumptionLiveHerb` | 0.5, 0.9 | |
| `EmissionFactorPM25`, `EmissionFactorCO2` | flaming smoldering g kg⁻¹ | |
| `MapOutputFolder`, `FuelMapFrequency`, `WriteFuelMoistureMaps`, `WriteCalibrationDiagnostics` | ember, 10, no, no | |

**Species table** columns: `SpeciesCode, LifeForm (Tree|Shrub|Grass), LitterClass (a fuelbed
name), MaxHeight (m), HeightK (yr⁻¹), CrownRatio, FineBranchFraction, BarkThicknessMax (cm),
AgeDBH (yr), FoliarMoistureClass (Conifer|Broadleaf|Shrub|Herb), FoliageFraction,
SpecificLeafArea (m² kg⁻¹), CoarseWoodFraction1000h`.

**Fuelbed table** columns: `Name, SAV1h, SAV10h, SAV100h, SAVHerb, SAVWoody (ft⁻¹),
HeatContent (kJ kg⁻¹), MoistureOfExtinction (%), PackingRatio, LitterFraction1h,
DuffBulkDensity`.

## Outputs

Maps in `MapOutputFolder` per year: `severity`, `event-id`, `day-of-burn`,
`fireline-intensity` (kW/m), `flame-length-dm`, `crown-fire-class` (1 surface, 2 passive,
3 active), `rate-of-spread-dm-min`, `canopy-mortality-pct`, `consumption-kg-ha`,
`pm25-kg-ha`, `cause` (1 lightning, 2 accidental). Fuel maps every `FuelMapFrequency`
years: fine, coarse wood, duff and live surface fuel (g/m²), canopy bulk density (×1000),
canopy base height (dm), canopy fuel load, ladder fuel, canopy cover, fuelbed type, time
since fire.

Logs: `ember-events-log.csv` (one row per fire), `ember-daily-log.csv` (per fire-day),
`ember-ignitions-log.csv`, `ember-summary-log.csv`, optional `ember-cell-diagnostics.csv`.

Registered site variables: `Fire.Severity`, `Fire.TimeOfLastEvent`, `Fire.Slope`,
`Fire.Aspect`, `Fire.EventID`, `Fire.Cause`, `Fire.FirelineIntensity`, `Fire.FlameLength`,
`Fire.CrownFractionBurned`, `Fire.CanopyMortalityFraction`, `Fire.SurfaceConsumption`,
`Fire.DuffConsumption`, `Fire.CanopyConsumption`.
