# LANDIS-II v8 (UCLv2 image) — Architecture Notes for Extension Authors

These notes were written after reading every repository pinned by the
`Docker-LANDIS-II-v8-UCL2-release` image plus the fire-related repositories that image
leaves out. Sources are checked out at the exact pinned commits under `../source/`
(see `../README.md`). Everything below was verified against source or, where the
source and the shipped binary disagree, against the shipped DLL via reflection.

---

## 1. What the running image actually contains

| Layer | Repo @ commit | Notes |
|---|---|---|
| Core console | `LANDIS-II-Foundation/Core-Model-v8-LINUX` @ `8725da5` | Only `Tool-Console` and `Tool-Extension-Admin` are source. The real core (`Landis.Core` 3.0.1, `Landis.Core.Implementation`, `Landis.SpatialModeling` 2.0.1, `Landis.Landscapes`, `Landis.RasterIO.Gdal.Linux`, `Landis.Utilities`) arrives as NuGet packages. |
| Prebuilt support DLLs | `Support-Library-Dlls-v8` @ `4257c0f` | Ships `UniversalCohorts-v2` (assembly v1.3), `Climate-v6`, `Succession-v10`, `Metadata-v2`, `Parameters-v2`, `InitialCommunity.Universal-v2`, harvest libs, `MathNet.Numerics`, `Ether.WeightedSelector`. These are what extensions compile against. |
| Rebuilt library | `Library-PnET-Cohort` @ `3c40197` | Rebuilt before extensions because the shipped PnET cohort DLL is UCLv1. |
| Succession | `mslucash/Extension-NECN-Succession` @ `0abcd7f` | NECN v8.2, built against Climate-v6. |
| Fire | `mslucash/Extension-SCRPPLE` @ `9b571c9` | Social Climate Fire v4.1.3. Only works with NECN/PnET (null-deref on fine fuels otherwise). |
| Output | Biomass, Biomass-By-Age, Biomass-Community, Biomass-PnET, Biomass-Reclass, Cohort-Statistics, Max-Species-Age | Standard Foundation repos. |
| Post-extension library rebuilds | Metadata, Universal-Cohort (**builds `-v1`**, not v2), Initial-Community | The UCL rebuild replaces the v1 DLL only; the v2 DLL every extension uses is the prebuilt one. |

Not in the image (but cloned under `source/reference/` for study): Base/Original Fire,
Dynamic Fire System, Dynamic Biomass Fuels, Base Wind, Library-Climate source,
Library-Succession source, the core library sources.

### Build pipeline (what a new extension must satisfy)

`scripts/install_extensions_v8.sh` does, per YAML entry:

1. sparse clone of `src/`, `deploy/current`, `deploy/installer`, `deploy/Defaults`;
2. find the **first** `*.csproj`; rewrite every `<HintPath>` to `..\..\build\extensions\<dll>`,
   force `<OutputPath>` to `build/extensions`, set `AppendTargetFrameworkToOutputPath=false`;
3. `dotnet build -c Release` (fails the image if the build fails);
4. register with `dotnet Landis.Extensions.dll add <the newest deploy/*.txt>`;
5. copy every DLL in `build/extensions` to `build/Release`; delete the clone.

Afterwards `add_console_csproj_dlls.sh` adds a `<Reference>` for every DLL in
`build/extensions` to the console project and rebuilds it, so the console can resolve
extension assemblies at run time. The runtime image contains only the .NET 8 *runtime*
and `build/`.

So a new extension needs exactly: a `netstandard2.0` project with
`PackageReference Landis.Core 3.0.1` and `Reference`+`HintPath` entries for the support
DLLs it uses; a registration text file under `deploy/installer/`; and a YAML entry. No
new NuGet packages beyond what is already in `build/extensions` unless they are also
copied there (`CopyLocalLockFileAssemblies=true` is how Magic Harvest does it).

Registration file format (parsed by `Tool-Extension-Admin/ExtensionParser.cs`):

```
LandisData   Extension
Name         "Social Climate Fire"
Version      4.1.3
Type         disturbance:fire
Assembly     Landis.Extension.SocialClimateFire-v4
Class        Landis.Extension.SocialClimateFire.PlugIn
Description  "..."
CoreVersion  8.0
```

---

## 2. Core run loop (`Landis.Core.Implementation/Model.cs`)

```
Run(scenario):
  load species, ecoregions, ecoregion map -> Landscape (active sites = ecoregion.Active)
  succession.LoadParameters(); succession.Initialize()
  for each disturbance ext: LoadParameters()   then Initialize()     (in scenario order)
  for each other ext (output etc.): LoadParameters(); Initialize()
  InitializePhase2() on all non-succession extensions
  run output extensions at time 0
  for currentTime = start+1 .. end:
      distToRun = disturbance exts whose (timeSinceStart % Timestep == 0)
      if scenario says random order: shuffle distToRun
      run each disturbance ext            <- fire runs here, BEFORE succession
      if succession timestep OR any disturbance ran: run succession
      run other/output exts due this year
  CleanUp() on all
```

Consequences for a fire extension:

* At the fire step in year *t* the cohorts and soil pools reflect the end of year *t−1*.
* Succession runs on *disturbed sites only* in non-succession years (via
  `Library.Succession.SiteVars.Disturbed`, set when a cohort mortality event carries a
  non-null disturbance type), so post-fire regeneration and litter inputs happen the same
  year the fire burns.
* `Timestep` is set by the extension in `Initialize()`; an extension with `Timestep = 1`
  runs every year. SCRPPLE forces 1 because it needs daily weather.

### `ExtensionMain` contract (shipped `Landis.Core` 3.0.1)

```csharp
abstract void LoadParameters(string dataFile, ICore modelCore);   // parse inputs, grab ICore
abstract void Initialize();                                        // set Timestep, site vars, metadata
abstract void Run();                                               // one timestep
virtual  void InitializePhase2();                                  // after all exts loaded
virtual  void CleanUp();
abstract void AddCohortData();                                     // may add keys to additionalCohortParameters
protected ExpandoObject additionalCohortParameters;
```

`ICore` gives: `Landscape` (active site enumeration, `NewSiteVar<T>()`), `Species`,
`Ecoregions`, `Ecoregion[site]`, `CellLength` (m) / `CellArea` (ha), `StartTime`,
`EndTime`, `CurrentTime`, `RegisterSiteVar` / `GetSiteVar<T>(name)`, the
Mersenne-Twister RNG (`GenerateUniform()`, `NextDouble()`, `shuffle`, Troschuetz
distributions), `OpenRaster<TPixel>` / `CreateRaster<TPixel>` (GDAL; `.tif`, `.img`, `.gis`,
`.bin`), and `UI.WriteLine`.

### Spatial model

* `Site` / `ActiveSite` are structs holding `(Location(row,col), DataIndex)`. Inactive sites
  all share data index 0. `site.GetNeighbor(RelativeLocation)` returns a `Site` whose
  `IsActive` you must check. There is no built-in neighbour list, cost surface, or
  priority queue; every fire extension writes its own.
* `ISiteVar<T>` is a dense array indexed by `DataIndex`; `ActiveSiteValues = x` bulk-sets.
  Reference types (e.g. `SiteCohorts`) are stored per site as references.
* Rasters are read/written pixel by pixel in row-major order with typed pixels
  (`BytePixel`, `ShortPixel`, `IntPixel`, `FloatPixel`, `DoublePixel`, …). Output rasters are
  **ungeoreferenced** (the core never carries a geotransform). The core creates missing
  output directories.

### Site-variable registry (the only cross-extension channel)

Extensions never reference each other's assemblies. They share state by name:

| Name | Type | Registered by | Used by |
|---|---|---|---|
| `Succession.UniversalCohorts` | `ISiteVar<SiteCohorts>` | NECN (and every UCL succession) | all disturbance/output exts |
| `Succession.FineFuels` | `double` (g biomass m⁻²) | NECN (= (structural + metabolic surface litter C)×2), PnET-Cohort lib | SCRPPLE spread & severity |
| `Succession.SmolderConsumption`, `.FlamingConsumption` | `double` | NECN, PnET | SCRPPLE map output; NECN fills them in `FireEffects.ReduceLayers` |
| `Succession.CWD`, `Succession.PET` | `double` (annual, cm) | NECN, PnET | SCRPPLE severity |
| `Succession.Litter`, `.WoodyDebris`, `.PressureHead`, `.SoilFieldCapacity`, … | various | PnET only | |
| `Fire.Severity` | `byte` | fire extension | **NECN reads it** to pick a row of `FireReductionParameters` |
| `Fire.TimeOfLastEvent`, `Fire.Slope`, `Fire.Aspect` | int / ushort | SCRPPLE | Dynamic Fuels, outputs |
| `Harvest.TimeOfLastEvent`, `Harvest.PrescriptionName` | int / string | harvest exts | NECN, SCRPPLE, Dynamic Fuels |
| `TimeOfLastSuccession`, `Shade` | int / byte | Succession library | |

`GetSiteVar<T>` returns `null` when nothing is registered, and throws if the type
differs. Registering the same name twice throws, so only one fire extension can register
`Fire.Severity` per scenario.

NECN does **not** register its dead-wood, duff (SOM1 surface), soil-water, or LAI pools; a
fire extension cannot read them without a small NECN patch. Its `Layer` type is internal to
NECN, so any patch should expose plain `double` site variables.

---

## 3. Universal Cohort Library v2 (shipped DLL, assembly version 1.3)

* `SiteCohorts` holds `List<SpeciesCohorts>`; each holds `List<CohortData>` **oldest to
  youngest**. `CohortData` is a struct `{ushort Age; int Biomass (g m⁻²); double ANPP;
  ExpandoObject AdditionalParameters}`. NECN adds `WoodBiomass`, `LeafBiomass`,
  `MineralNallocation`, `MineralNfraction`, `Nresorption` to the expando.
* Enumerating cohorts allocates a new `Cohort` wrapper per cohort per iteration. Cheap
  but not free; avoid re-walking all cohorts every simulated day.
* Disturbance entry point: `siteCohorts.ReduceOrKillCohorts(IDisturbance)` walks species
  back-to-front, calls `double IDisturbance.ReduceOrKillMarkedCohort(ICohort)` on each
  cohort, and applies the **returned fraction** of biomass: 1.0 removes the cohort,
  0 < f < 1 reduces it (partial cohort mortality is a first-class operation), 0 leaves it.
  Either path raises the static `Cohort.MortalityEvent(sender, cohort, site,
  disturbanceType, fraction)`; NECN subscribes and (a) routes the killed biomass to litter
  and dead wood pools, (b) if `disturbanceType.IsMemberOf("disturbance:fire")` calls
  `Reproduction.CheckForPostFireRegen` (serotiny / resprout flags) and, on the first
  cohort touched at a site that year, `FireEffects.ReduceLayers(Fire.Severity[site])`.
* `ICohort.ComputeNonWoodyBiomass(site)` is supplied by the succession extension's
  `ICalculator`, so **foliage biomass per cohort is obtainable from any UCL succession**
  without knowing its internals.
* A disturbance extension can persist per-cohort state: `CohortData.AdditionalParameters`
  is a shared `ExpandoObject`; `AddAdditionalCohortParameters()` adds keys that are missing,
  `ChangeParameters()` increments existing keys. `ExtensionMain.AddCohortData()` exists for
  extensions to declare such keys. (Verify at implementation time that the key survives
  `CombineYoungCohorts`, which merges young cohorts by summing expando values.)

---

## 4. Succession library and NECN behaviours that matter to fire

* `ExtensionBase.Run()`: age/grow cohorts → compute shade → reproduce, on all sites in a
  succession year, otherwise on disturbed sites only.
* Post-fire regeneration lives in the succession library, keyed on species
  `PostFireRegeneration` (None / Resprout / Serotiny) from the core species file, triggered
  by cohort death events whose type is `disturbance:fire`. A fire extension does not need
  to implement resprouting or serotiny; it only needs to kill/reduce cohorts with the right
  `ExtensionType`.
* NECN's `FireReductionParameters` table is indexed by the `Fire.Severity` byte. The test
  scenario defines rows 1–3 while SCRPPLE emits 1–10; missing rows silently apply zero
  reduction. Rows carry: wood litter, fine litter, cohort wood, cohort foliage, and SOM
  (duff) reduction fractions. Cohort wood/foliage reductions are applied to the biomass of
  killed cohorts (combustion of live material) and everything else becomes litter.
* NECN computes monthly `SoilWater`, `MeanSoilWater`, `PlantAvailableWater`, monthly and
  annual `CWD`, `PET`, `AET`, `LAI`, and, optionally, a drought-mortality module with
  lagged CWD/SWA/temperature anomalies. Only annual CWD and PET are registered.
* NECN's fine fuels are refreshed once per year (end of `Main.Run`) and again inside
  `FireEffects.ReduceLayers`.

---

## 5. Climate Library v6 (what daily weather the model already carries)

* Config file (`LandisData "Climate Config"`): `ClimateTimeSeries`
  (`Daily_SequencedYears | Daily_RandomYears | Daily_AverageAllYears | Monthly_*`),
  `ClimateFile`, spin-up equivalents, `UsingFireClimate yes` plus initial `FineFuelMoistureCode`,
  `DuffMoistureCode`, `DroughtCode`, `FirstDayFire`, `LastDayFire`.
* Input CSV: long format `Year,Month,Day,Variable,<eco1>,<eco2>,…` with variables
  `Tmax, Tmin, Temp, Precip(cm), WindSpeed(m/s → stored km/h), WindDirection(from → stored to),
  RH/MinRH/MaxRH, SpecificHumidity, DewPoint, PET, PAR, CO2, NDep, Ozone, SWR`.
  Leap years are normalised to 365 days.
* Per ecoregion per year (`Climate.FutureEcoregionYearClimate[eco.Index][simYear]`, simYear
  is **1-based**) an `AnnualClimate` exposes daily lists of every variable plus daily
  **FFMC, DMC, DC, BUI, FWI** computed with cffdrs-checked constants between
  `FirstDayFire` and `LastDayFire`. Monthly SPEI, VPD, GDD, Thornthwaite PET are also
  available.
* Climate is initialised by the succession extension (`Climate.Initialize(configFile, …)`
  inside NECN `Initialize()`), so a fire extension can rely on it being loaded as long as it
  is listed after NECN in the scenario (it always is: disturbances load after succession).
* Weather is **per ecoregion**, not per cell. Sub-ecoregion variation (aspect, canopy) has
  to be modelled inside the fire extension.

---

## 6. The three existing fire extensions

### 6.1 Social Climate Fire / SCRPPLE v4.1 (in the image)

* Annual step; internal loop over 364 days.
* Ignitions: **landscape-average** daily FWI → Poisson or zero-inflated-Poisson count per
  cause (lightning, accidental); location drawn from weighted ignition rasters via
  `Ether.WeightedSelector`. Rx fires: fixed annual count, daily attempts within FWI /
  wind / temperature / RH / day-of-year windows, spread to a target size inside optional
  Rx zones.
* Spread: FIFO cell list; `P(spread) = logistic(β0 + β1·FWI + β2·fineFuelFraction +
  β3·effectiveWindSpeed)` to 4 cardinal (×1.0) and 4 diagonal (×0.71) neighbours;
  effective wind speed from Nelson (2002) with slope, uphill azimuth and the *source
  cell's* severity as combustion buoyancy; day advances when area burned that day exceeds
  `β0 + β1·FWI + β2·EWS` (ha). Suppression multiplies P(spread) by a table value chosen by
  zone map code and FWI break-points; none above a wind threshold.
* Severity: site "dNBR" from an inverse-link GLM of clay %, previous-year PET, effective
  wind, CWD, fine-fuel fraction, ladder-fuel biomass; capped 0–2000, mapped to
  intensity class 1–10 (`dNBR/100`). Cohort mortality: Bernoulli with
  `logit P = β0 + β1·barkThickness(age) + β2·dNBR`. Killed cohorts are removed entirely.
* Effects on pools happen in NECN via `Fire.Severity`. Ladder fuels = biomass of listed
  species below a max age. Fine fuels = NECN litter (capped by `MaximumFineFuels`).
* Outputs: severity, dNBR, ignition type, day of fire, event ID, spread probability,
  fine fuels, biomass killed, optional PET/CWD/EWS/ladder maps; events, ignitions, summary
  CSVs.
* Limits relevant to a redesign: no rate of spread, flame length, or fireline intensity;
  fire size is a fitted daily-area cap rather than an emergent property of fuel, weather
  and duration; one number for fuel; no canopy fuel structure or crown fire; no live-fuel
  or site-level fuel moisture (drought enters only through last year's PET/CWD); severity is
  a regression not linked to consumption; suppression is a static lookup; the ignition
  step discards spatial weather variation; the supporting R workflows need daily fire
  perimeters to fit spread and severity coefficients per landscape.

### 6.2 Dynamic Fire System v3/v4 (Canadian FBP, not in the image)

* Requires the Dynamic Biomass Fuels extension to classify each cell into a CFS fuel
  type (C1–C7, D1, M1–M4, O1, S1–S3) from cohort biomass rules plus disturbance overrides.
* Weather is **sampled** from a user table by season and fire-size bin (no link to the
  climate library or to inter-annual climate). Fire size or duration drawn lognormal per
  fire region.
* Spread: FBP initial spread index → rate of spread by fuel type, slope/wind vector,
  elliptical length-to-breadth, minimum-travel-time cost surface with an iterative
  relaxation loop (up to 2000 passes). Severity 1–5 from crown fraction burned via
  Van Wagner's critical surface intensity and crown base height. Cohort death from
  severity–tolerance difference and age fraction.
* Strengths worth borrowing: mechanistic ROS → intensity → crown fire → severity chain,
  elliptical MTT spread. Weaknesses: FBP fuel types are categorical and Canadian, weather
  is not climate-driven, the cost-surface relaxation is expensive, no consumption or
  emissions, no fuel moisture below the fuel-type level.

### 6.3 Original / Base Fire v5

Ignition probability and exponential fire size per fire region, severity from
time-since-fire "fuel curves", wind-biased 8-neighbour contagion. Fast, not climate
sensitive, no fuels.

---

## 7. Practical constraints and gotchas gathered from the code

* `ModelCore.CellArea` is in hectares, `CellLength` in metres. Biomass is g m⁻² (int on
  cohorts). Climate wind speed is km h⁻¹ after import; precipitation cm.
* Simulation-year indexing into the climate library is 1-based; `CurrentTime` in a
  scenario with `Duration N` runs 1..N (StartTime = 0). Real calendar years are recovered
  with `Climate.FutureCalendarYear(year)`.
* Output map paths are templates with `{timestep}`; SCRPPLE hard-codes its folder.
  Metadata for tables/maps is declared through `Landis.Library.Metadata`
  (`MetadataTable<T>` with `[DataFieldAttribute]` properties; `WriteToFile()` appends).
* The core RNG should be used for all stochastic draws so `RandomNumberSeed` reproduces
  runs (SCRPPLE partly uses `System.Random`, which breaks reproducibility).
* The console rebuild references every DLL, so an extension DLL name must be unique
  across the image.
* Local tooling: `.NET 10 SDK` is installed on this Mac (`/opt/homebrew/bin/dotnet`), which
  can compile `netstandard2.0` libraries and run `net10.0` test projects. The image builds
  with the .NET 8 SDK on Ubuntu; code must stay within C# features supported by both.
