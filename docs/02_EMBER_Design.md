# EMBER — a re-envisioned fire extension for LANDIS-II v8

**E**cosystem-coupled **M**oisture, **B**ehavior, **E**ffects and **R**egimes.
*(Working name; easy to change.)*

Design document, version 0.1 — September 2026. Companion to
`01_LANDIS-II_Architecture_Notes.md`, which records how the model core, cohort library,
NECN, Climate Library and the existing fire extensions actually work.

---

## 0. The idea in one paragraph

Today's fire extensions treat fire as a *statistical contagion*: fire weather sets a
probability of spreading to the next cell, a fitted cap decides how big a day's growth may
be, and a regression converts site covariates into a severity number. EMBER instead treats
fire as a *physical process with a small parameter set*: the cohorts and litter that
succession already tracks are turned into a vertically structured fuelbed once a year;
daily weather from the Climate Library is turned into site-level dead and live fuel
moisture; ignition, rate of spread, fireline intensity, crown fire, consumption, scorch and
tree mortality follow from those two states through published fire behavior and fire
effects equations (Rothermel, Van Wagner, Byram, Ryan & Reinhardt, FOFEM/Consume-style
consumption). Fire size, seasonality, severity and emissions then *emerge* from climate ×
fuel × topography × suppression instead of being fitted per landscape, which is what makes
the extension a predictive tool under climates and fuel conditions that have not been
observed yet. It stays lightweight because fuel structure is rebuilt once a year, moisture
is tracked for a few hundred strata rather than every cell, and spread costs work only
where fire is actually burning.

---

## 1. Why a new extension rather than another SCRPPLE version

| Capability | Original Fire | Dynamic Fire (FBP) | SCRPPLE v4.1 | EMBER |
|---|---|---|---|---|
| Driven by daily climate-library weather | no | no (sampled table) | yes (FWI, landscape mean at ignition) | yes, per ecoregion, downscaled to site strata |
| Site-level dead fuel moisture | no | fuel-type only | no (FWI proxy) | yes (1-h/10-h/100-h/1000-h/duff) |
| Live fuel moisture / drought → crown fire | no | seasonal FMC only | no | yes (drought-modulated foliar & shrub/herb moisture) |
| Fuel structure from cohorts | no | categorical fuel type | one number + ladder biomass | surface load by class, duff, canopy bulk density, canopy base height, ladder index |
| Rate of spread, intensity, flame length | no | ROS, CFB | no | yes, per burned cell |
| Fire size | drawn | drawn | emergent, capped by fitted daily area | emergent from ROS × burn hours × containment |
| Crown fire | no | yes | implicit | yes (initiation + active criteria) |
| Consumption by fuel class, emissions | no | no | via NECN severity table | yes, mechanistic, passed to succession |
| Tree mortality | severity–tolerance table | severity–tolerance table | bark thickness × dNBR logistic, whole cohort | scorch + bark thickness, fractional cohort, optional delayed mortality |
| Suppression | none | none | zone × FWI lookup on P(spread) | initial attack + containment as functions of intensity, size, concurrent fire load |
| Prescribed fire | none | none | count/day windows, fixed size | prescription windows on *modelled* fuel moisture; effects emerge |
| Calibration data need | fire history | FBP tables | daily perimeters to fit every β | staged: fuels, moisture, ignitions, sizes, severity each against their own data; historical replay mode |
| Works with NECN / PnET / Biomass Succession | yes | needs Dynamic Fuels | NECN, PnET only | any UCL succession (owns its fuel pools; richer coupling with NECN) |

SCRPPLE's good ideas are kept: daily zero-inflated-Poisson ignitions vs. fire danger by
cause, ignition-density rasters, suppression zones, Rx windows, dynamic maps by year,
bark-thickness-from-age allometry, event / ignition / summary logs.

---

## 2. Design principles and hard constraints

1. **Mechanism over regression, but low-dimensional.** Every process uses an established
   equation with a handful of transferable parameters. Landscape-specific fitting is
   reduced to a few multipliers with clear physical meaning.
2. **Two state variables carry climate sensitivity:** fuel *structure* (annual, from
   vegetation) and fuel *moisture* (daily, from weather and drought). Everything downstream
   is deterministic given those plus wind and slope, with stochasticity only where nature
   is stochastic (ignition, spotting, sub-cell heterogeneity, suppression outcome).
3. **Laptop budget.** Target ≤ 10 s of fire computation per simulated year on a 1-million
   active-cell landscape in a normal fire year, ≤ 60 s in an extreme one; ≤ 150 bytes of
   fire state per cell. No per-day loops over the whole landscape.
4. **Succession-agnostic core, richer when NECN is present.** The extension owns a light
   "behavior fuelbed" fed by cohort foliage (via `ICohort.ComputeNonWoodyBiomass`) and
   mortality events; when NECN registers its pools, it can synchronise to them and hand
   consumption back.
5. **Reproducible.** All draws come from the core RNG. Same seed, same fires.
6. **Testable outside LANDIS.** Behavior and effects equations live in a pure C# library
   (`Landis.Library.FireBehavior`) with unit tests against BehavePlus / FOFEM reference
   values, so the science can be checked without running a landscape.
7. **Outputs designed for questions**, not for debugging: every quantity a fire scientist
   would measure after a fire (intensity, flame length, crown fire class, consumption,
   mortality, emissions, day of burn, containment cause) is written per cell and per event.

---

## 3. The annual cycle

```
Year t, fire extension runs BEFORE succession (core loop):

  [A] Build fuelbed for every active cell              once/year, from cohorts + litter pools
  [B] Build moisture strata; compute D(drought)        once/year (strata), then daily
  for day d in FirstDayFire..LastDayFire:
      [B] update dead & live fuel moisture per stratum
      [C] draw ignitions per ecoregion × cause; allocate to cells; test sustainability
      [D] for each active fire: burn period T_d → MTT spread with today's ROS field
          per newly burned cell: behavior → [F] consumption, scorch, mortality (applied immediately)
      [E] end-of-day suppression / containment decisions; Rx ignitions inside windows
  [G] write maps, logs; register site vars for succession and output extensions
Succession then grows, decomposes, and regenerates the burned sites the same year.
```

---

## 4. Modules

### 4.1 Fuel structure (annual)

Built once per year for every active cell from the cohort list and (if present) NECN's
registered litter. Units: kg m⁻² (biomass), m.

**Surface dead fuels by time-lag class**

| Class | Source | Notes |
|---|---|---|
| 1-h (litter, < 0.6 cm) | fire extension litter pool *or* `Succession.FineFuels` × f₁ | f₁ set by litter class (needles vs. broadleaf vs. grass) |
| 10-h | same, × (1 − f₁) | |
| 100-h / 1000-h | extension coarse-wood pools | inputs from cohort wood mortality (via `Cohort.MortalityEvent`, fraction × wood biomass), partitioned by species diameter class; exponential decay with a temperature/moisture modifier |
| Duff | extension duff pool | inputs = litter decay flux; decay slow; depth = load / bulk density |
| Live herbaceous | cohorts of grass life-form | foliage biomass, cured fraction from season and drought |
| Live woody (shrub) | cohorts of shrub life-form | foliage + fine twig fraction |

Owning these pools keeps the extension usable with PnET or Biomass Succession and gives
sound/rotten and size-class detail NECN does not expose. NECN stays the carbon accountant;
these are *behavior* pools. An optional `SyncFineFuelsToSuccession yes` uses NECN's
registered `Succession.FineFuels` as the 1-h + 10-h source so the two never drift.

**Fuelbed physical properties** (surface-area-to-volume ratio, heat content, moisture of
extinction, packing / depth per unit load) come from a small user table of *fuelbed
types* (e.g. short-needle conifer litter, long-needle pine litter, hardwood leaf litter,
grass, shrub, slash) chosen per cell each year by the biomass-weighted litter class of its
cohorts, with a slash override for `Harvest.TimeOfLastEvent` within *n* years. Six to ten
rows cover most landscapes; the values are standard Scott & Burgan / Anderson fuel-model
numbers.

**Canopy fuels** (tree life-form cohorts only)

For each cohort *c*: foliage *F_c* (from `ComputeNonWoodyBiomass`), height
*H_c = H_max·(1 − e^(−k·age))*, crown base *CBH_c = H_c·(1 − CR)* with crown ratio *CR*
reduced under high site LAI (self-pruning), canopy fuel *F_c·(1 + b_fine)* spread evenly
between CBH_c and H_c into 1-m bins. Species-level cohorts of shrub life-form and tree
cohorts below a ladder height add to the lowest bins. From the bin profile:

* canopy fuel load CFL (kg m⁻²), canopy height (top occupied bin);
* effective canopy bulk density CBD = max 3-m running mean of the profile (kg m⁻³),
  the Scott & Reinhardt convention;
* effective canopy base height CBH = lowest bin where the running mean exceeds
  0.011 kg m⁻³ (so ladder fuels lower CBH mechanistically);
* ladder index = fuel mass in bins between the surface fuelbed and CBH.

Cost: one pass over cohorts and a 60-element bin array per cell; a few seconds per million
cells.

Species parameters needed: life form (tree / shrub / grass), litter class, *H_max*, *k*,
crown ratio, fine-branch fraction, bark-thickness allometry (either SCRPPLE's
`MaxBarkThickness`, `AgeDBH` or a DBH-at-age curve plus bark factor), foliar moisture
class (conifer / broadleaf / evergreen shrub / herb), resprout / serotiny already in the
core species file. Nine or ten numbers per species, most available from FIA / FOFEM /
Cansler et al. (2020).

### 4.2 Fuel moisture (daily, per stratum)

A stratum = ecoregion × canopy-cover class (3) × radiation class (3 from slope/aspect),
so a landscape with 20 ecoregions has ~180 strata. Each cell stores its stratum id (one
byte) and looks moisture up; per-cell moisture is an optional mode for small landscapes.

**Dead fuels.** Default method `FWICodes` uses the codes the Climate Library already
computes per ecoregion with cffdrs-verified constants:

* 1-h moisture *m₁ = 147.2·(101 − FFMC)/(59.5 + FFMC)* (%)
* 10-h: relaxation toward *m₁* with a 10-h time constant
* 100-h: from DMC, *m₁₀₀ = 20 + 280·e^(−0.023·DMC)*
* 1000-h and duff: from DC via *Q = 800·e^(−DC/400)* rescaled to percent by a user pair
  (calibrated once against fuel-stick or NFMD data)

Stratum modifiers: canopy shading raises moisture (*m × (1 + a_shade·Δcover)*),
south-facing steep slopes lower it (*m × (1 − a_topo·(radiation index − 0.5))*). Two
parameters. An alternative method `NFDRSLag` (Fosberg equilibrium moisture from daily
T/RH with 1/10/100/1000-h lag relaxation and precipitation bumps) is a drop-in for US
users who calibrate against NFDRS.

**Live fuels.** Drought index *D ∈ [0,1]* per stratum from the Drought Code (default) or,
when NECN exposes it, from the site's soil-water relative to field capacity. Conifer
foliar moisture follows the FBP seasonal curve (spring dip from date and latitude) with
its floor lowered by *D*; herbaceous and shrub live moisture
*LFM = LFM_min + (LFM_max − LFM_min)·(1 − D)·g(day)* where *g* is a green-up / curing curve.
This is the lever through which multi-year drought raises crown-fire likelihood and
consumption — the mechanism SCRPPLE can only approximate with last year's CWD.

### 4.3 Ignition (daily)

For each ecoregion *e*, day *d*, cause *c* ∈ {lightning, human, (Rx handled in 4.5)}:

*λ = area_e · exp(β₀_c + β₁_c · FD_{e,d})*, with *FD* the Initial Spread Index or FWI of
**that ecoregion**, and *N ~ Poisson(λ)* or zero-inflated Poisson exactly as in SCRPPLE
(so the existing R fitting workflow against Short's FPA-FOD database transfers). Expressing
*λ* per unit area makes coefficients portable between landscapes.

Each ignition is placed by an alias-method sampler built once per year from the cause's
ignition-density raster restricted to the ecoregion (O(1) per draw; SCRPPLE re-sorts every
site every year). The ignition *sustains* with probability from fine-fuel moisture
(Beverly & Wotton 2007 logistic on FFMC) and only if 1-h + 10-h load exceeds a minimum;
failed ignitions are logged, which is itself a calibration diagnostic (observed
ignitions ≫ observed fires).

Optional inputs: dynamic ignition-density rasters by year (as SCRPPLE), an observed
ignition file for replay mode (year, day, row, column, cause; see 7.4).

### 4.4 Fire behavior and spread

**Per-cell behavior** (evaluated when a cell is reached, with that day's weather):

* Surface fire: Rothermel (1972) with Albini's corrections on the cell's dynamic fuel
  model (loads by class, live loads, moisture by class, fuelbed-type SAV/depth/heat/Mx).
  Mid-flame wind = ecoregion 10-m wind × wind-adjustment factor from canopy cover and
  height (Andrews 2012). Slope factor from the slope map. Result: head rate of spread
  *R* (m min⁻¹), reaction intensity, heat per unit area; Byram fireline intensity
  *I = H·w·R* (kW m⁻¹); flame length *L = 0.0775·I^0.46* (m).
* Crown fire (Van Wagner 1977; Scott & Reinhardt 2001): critical surface intensity
  *I₀ = (0.010·CBH·(460 + 25.9·FMC))^1.5*; if *I ≥ I₀* the fire torches (passive); active if
  the crown rate of spread (Rothermel 1991 × 3.34 or Cruz et al. 2005) exceeds the critical
  *R_active = 3.0 / CBD*. Crown fraction burned *CFB = 1 − e^(−0.23·(R − R₀))*. Total
  intensity adds the canopy fuel consumed.
* Direction: effective wind vector = wind vector + slope-equivalent wind (the FARSITE
  convention; SCRPPLE's Nelson form is an acceptable substitute). Length-to-breadth ratio
  *LB* from effective wind (Anderson 1983), eccentricity *ε*, and
  *R(θ) = R·(1 − ε)/(1 − ε·cos θ)* for the eight neighbour directions.
* Sub-cell heterogeneity: *R* multiplied by a lognormal factor (σ_R, calibrated) drawn per
  cell-day; a cell **fails to burn** if *I* < an extinction threshold (default 10 kW m⁻¹) or
  *m₁* exceeds the fuelbed's moisture of extinction. This is what produces unburned
  islands and fuel-limited fire edges.

**Landscape spread**: minimum travel time (Finney 2002) on the 8-neighbour grid with a
binary heap, run **one day at a time**. Each day the fire has a burn period
*T_d = T_min + (T_max − T_min)·clamp((FD − FD_lo)/(FD_hi − FD_lo))* hours (e.g. 2 h on a
marginal day, 10 h on an extreme one). Front cells seed the heap with their residual
arrival times; cells whose arrival time falls inside *T_d* burn today (behavior and
effects applied with today's weather); the rest carry over. The fire ends when the heap is
empty (fuel-limited), when it is contained (4.5), or at `LastDayFire`. Daily perimeters are
therefore a native output, directly comparable to FIRED / GeoMAC daily progression data.

Spotting (optional, default off): cells with *CFB > 0* launch embers with probability
proportional to CFB per burn period; landing distance lognormal with a mean scaled by
flame height and wind; the receptor ignites subject to the sustainability test. Cheap,
and the only way to reproduce the extreme-day size distribution in crown-fire systems.

Complexity per day is O(B log B) in the number of cells burned that day; a 20 000-ha fire on
30-m cells costs a few hundred milliseconds.

### 4.5 Suppression and management

Suppression is a process with resources, not a spread multiplier.

* **Initial attack** at the end of the ignition day:
  *logit P_IA = a₀ + a₁·ln(A₁) + a₂·I_max,1 + a₃·Z + a₄·L*, with *A₁* area after day 1,
  *I_max,1* the day's maximum intensity, *Z* the suppression-zone code (raster 0–3, 0 = no
  suppression / managed wildfire), *L* the number of fires simultaneously active on the
  landscape. Success stops the fire at its day-1 footprint.
* **Extended attack** each later day: *logit P_contain = b₀ + b₁·ln(A) + b₂·R_max + b₃·Z +
  b₄·L + b₅·T_d*. Fires also end naturally when *T_d* stays below a threshold for *k* days.
* **Resource cap**: at most *N_max* fires receive suppression on a given day; the rest are
  treated as *Z = 0* that day. This single parameter reproduces "busy season" escapes.
* **Managed wildfire zones**: *Z = 0*, optional seasonal window.
* **Prescribed fire**: burn units (zone raster) with annual area targets, a prescription
  window on *modelled* 1-h/10-h moisture, wind, RH, temperature and day of year, ignition
  as a block inside the unit, burn period capped, spread confined to the unit. Effects
  are computed mechanistically, so Rx severity and consumption respond to the same
  moisture the wildfires see — which is what lets the model answer whether burn windows
  are shrinking.

Ten coefficients total for suppression; three to five for Rx.

### 4.6 Fire effects

Applied per burned cell at the time it burns.

* **Consumption by class**: 1-h, 10-h ≈ fully consumed when below extinction moisture;
  100-h, 1000-h and duff consumed as linear functions of their own moisture
  (*C_k = clamp(c₀_k − c₁_k·m_k)*, defaults from Brown et al. 1985/1991 and FOFEM, to be
  confirmed during implementation); live herb/shrub consumed as function of cured fraction;
  canopy consumed = *CFB × canopy fuel*. Flaming (fine, canopy, part of 100-h) and smoldering
  (1000-h, duff) totals are kept separately.
* **Emissions**: emission factors (g kg⁻¹, Urbanski 2014 defaults, editable) by phase for
  PM2.5, CO₂, CO, CH₄; written per cell and per event.
* **Crown scorch**: scorch height *h_s* from Van Wagner (1973) with the ambient-temperature
  and wind form; per cohort crown volume scorched *CVS* from *h_s* against *CBH_c* and crown
  length.
* **Mortality** (Ryan & Reinhardt 1988 / FOFEM):
  *logit P_m = −1.941 + 6.316·(1 − e^(−BT)) − 0.000535·CVS²*, bark thickness *BT* (cm) from the
  species allometry at the cohort's age; *P_m = 1* where CFB ≥ 0.9; optional basal-injury term
  from duff consumption for thin-barked species. The extension returns *P_m* as the
  **fraction of the cohort removed** (the UCL v2 kill callback accepts fractions), so a
  cohort of 500 trees losing 30 % of its stems behaves like one; a user switch reverts to
  SCRPPLE-style whole-cohort Bernoulli kills.
* **Delayed mortality** (optional): surviving cohorts store an injury index in their
  additional parameters; the next 1–3 years apply an extra mortality fraction that decays,
  reproducing the lagged mortality documented by Robbins et al. (2022) and enabling
  fire × drought × beetle interactions with other extensions.
* **Coupling to succession**: cohort kills carry `ExtensionType "disturbance:fire"`, so the
  succession library handles resprouting and serotiny. For NECN's litter/duff pools the
  extension registers `Fire.Severity` (byte 1–5 from canopy mortality and surface
  consumption thresholds) so the existing `FireReductionParameters` table keeps working
  unchanged (phase 1). Phase 2 is a ~20-line NECN patch that reads optional registered
  fractions (`Fire.FineLitterReduction`, `Fire.CoarseWoodReduction`, `Fire.DuffReduction`,
  `Fire.CohortWoodReduction`, `Fire.CohortLeafReduction`) when present, so carbon
  accounting uses the mechanistic consumption directly.
* Registered for other extensions: `Fire.TimeOfLastEvent`, `Fire.EventID`, `Fire.Cause`,
  `Fire.FirelineIntensity`, `Fire.FlameLength`, `Fire.CrownFractionBurned`,
  `Fire.SurfaceConsumption`, `Fire.DuffConsumption`, `Fire.CanopyConsumption`,
  `Fire.CanopyMortalityFraction`, `Fire.Slope`, `Fire.Aspect`.

---

## 5. Inputs

**Rasters** (same grid as the ecoregion map): slope (°), aspect (°), suppression zone
(0–3), Rx burn units (optional), ignition density per cause (optional; uniform if absent),
canopy-cover reference (optional; otherwise derived from LAI or foliage), and optional
year-keyed replacements for any of them.

**Tables**

| File | Rows | Content |
|---|---|---|
| Species fire table (CSV) | one per species | life form, litter class, H_max, k, crown ratio, fine-branch fraction, bark allometry, foliar moisture class |
| Fuelbed types (CSV) | 6–10 | SAV by class, depth per unit load, heat content, moisture of extinction, litter fraction f₁ |
| Suppression (CSV) | 1 | a₀…a₄, b₀…b₅, N_max, natural-end rule |
| Rx prescriptions (CSV, optional) | per unit or zone | window bounds, annual area target, unit size |
| Emission factors (CSV, optional) | by phase | PM2.5, CO₂, CO, CH₄ |
| Observed ignitions (CSV, optional) | per ignition | year, day, row, col, cause — replay mode |

**Main parameter file** (`LandisData "EMBER"`): timestep (must be 1), moisture method and
its two stratum modifiers, drought-index bounds, ignition coefficients per cause (2 or 4
each), burn-period parameters (4), extinction intensity, ROS noise σ_R, spotting switch and
2 parameters, cohort-mortality mode, delayed-mortality switch, coupling mode (severity
classes or fractions), output frequency and map selection, hazard-mode weather percentiles.

Rough parameter budget: ≈ 35 landscape-level numbers, most with literature defaults, plus
9–10 per species and the fuelbed table. SCRPPLE needs ≈ 30 fitted coefficients whose values
have no meaning off the landscape they were fitted on; EMBER's are mostly physical
constants with two or three landscape multipliers.

---

## 6. Outputs

**Maps** (GeoTIFF, `{timestep}` templates), every year for burned cells unless noted:

* burn severity class (1–5), canopy mortality fraction (%), fireline intensity (kW m⁻¹),
  flame length (m), crown-fire class (surface / passive / active), rate of spread (m min⁻¹),
  day of burn, event ID, cause, containment outcome (IA / extended / natural / edge),
  surface, duff and canopy consumption (Mg ha⁻¹), PM2.5 emitted (kg ha⁻¹), fuel moisture
  at time of burn;
* landscape-wide at a user frequency, burned or not: time since fire, cumulative fire
  count, fine-fuel load, coarse-wood load, duff load, CBD, CBH, ladder index, minimum live
  fuel moisture of the year — the layers needed for hazard assessment and treatment
  planning;
* **hazard mode**: potential flame length, crown-fire class and ROS under user-specified
  weather percentiles (e.g. 90th/97th) without igniting anything. This makes EMBER a
  FlamMap-style planning tool that follows vegetation forward in time.

**Tables** (CSV with metadata)

* events log: one row per fire — ID, cause, ignition day and location, days burning, area,
  perimeter, mean/max ROS, mean/max intensity, crown-fire area fraction, high-severity
  fraction, consumption by pool, emissions, containment day and cause, weather at ignition
  and on the peak day, concurrent-fire load;
* daily progression log: one row per fire-day (area added, burn hours, max ROS/I,
  contained?) for comparison with observed daily perimeters;
* ignition log by ecoregion, day and cause: attempted, sustained, suppressed at IA;
* annual summary by cause and severity class; fire-season length; size-distribution
  quantiles; rotation estimate; emissions; Rx accomplishment vs. target; burn-window days;
* annual fuel summary by ecoregion; optional per-cell calibration diagnostics for burned
  cells (loads, moisture, ROS, I, CFB, scorch, mortality) for regression against field and
  remote-sensing data;
* optional predicted dNBR per cell from a user-supplied linear mapping of modelled canopy
  mortality and consumption, for direct comparison with MTBS.

---

## 7. Calibration and validation strategy

Because each module produces an observable quantity, calibrate them in order and hold
earlier modules fixed — this is the main practical advantage over fitting a single spread
regression.

1. **Fuels** (no fire needed). Run the landscape with fire off; compare annual fuel-class
   loads, CBD and CBH by forest type against LANDFIRE fuel layers, FIA down-woody-material
   plots and any local fuel inventories. Adjust litter-class fractions, coarse-wood
   partition and decay, and the crown-ratio / height allometries.
2. **Fuel moisture**. Compare stratum 10-h/100-h/1000-h and live moisture against RAWS
   fuel sticks and the National Fuel Moisture Database for the same years (the Climate
   Library can sequence real years). Fit the two stratum modifiers and the DC→percent pair.
3. **Ignitions**. Fit β per cause from FPA-FOD ignition counts vs. daily ISI/FWI per
   ecoregion (existing SCRPPLE R scripts with the area normalisation added). Check the
   sustained-ignition fraction against the ratio of recorded fires to ignitions.
4. **Behavior sanity**. Hazard mode under 90th/97th percentile weather vs. FlamMap /
   BehavePlus run on LANDFIRE fuels for the same landscape; the unit-test library covers
   the equations themselves.
5. **Spread and size**. *Replay mode*: feed observed ignitions with the observed weather
   years and compare each fire's final size, daily growth and shape to MTBS / FIRED.
   Tune burn-period parameters, σ_R, extinction threshold and spotting. Then free-running
   replicates vs. the historical size distribution, annual area burned and season length.
6. **Severity and mortality**. Modelled canopy mortality and consumption vs. MTBS
   dNBR/RdNBR (with the CBI relationship) and vs. field mortality (Fire and Tree Mortality
   Database, FOFEM). Fit the optional dNBR mapping; adjust scorch/bark allometries only if
   systematic bias appears.
7. **Suppression**. Escape fraction (fires > 100 ha) by zone and the relationship between
   concurrent-fire load and escapes, from FPA-FOD final sizes. Fit *a*, *b*, *N_max*.
8. **Regime-level checks** on 10–30 replicate runs: fire rotation, high-severity patch-size
   distribution, inter-annual variance, fraction of area burned on the top 5 % of days.

Method: Latin-hypercube screening on the ≈ 8 parameters that survive steps 1–4 as
uncertain, then rejection ABC or a simple emulator on summary statistics (annual area,
size quantiles, high-severity fraction, season length). The calibration diagnostics output
is designed so every step is a plain regression in R or Python. Because replay uses the
same code path as projection, a model that reproduces 2000–2020 fires is directly usable
for 2030–2100 climate inputs.

---

## 8. Questions EMBER is built to answer

| Question | Which mechanism carries the answer |
|---|---|
| How will fire size, severity and season length change as drought and vapour-pressure deficit rise? | daily moisture from climate, burn period from fire danger, live-fuel drought lever |
| Which fuel treatments (thinning, Rx, mastication) reduce high-severity fire, by how much, and for how long? | cohort-derived CBD/CBH/ladder, fuel accumulation and decay, hazard mode |
| Is drought-driven live fuel moisture a real driver of crown fire, and where? | crown-fire initiation vs. FMC and CBH |
| Does fuel limitation self-regulate fire under warmer climates (fire begets less fire)? | consumption + reaccumulation + extinction threshold |
| How do concurrent fires and suppression capacity change the escape rate? | initial attack with resource cap |
| Where can managed wildfire replace suppression with acceptable severity? | zone map + mechanistic severity |
| Are prescribed-burn windows shrinking, and where are they still viable? | Rx windows on modelled moisture; burn-window days output |
| How much carbon and PM2.5 does each scenario emit, flaming vs. smoldering? | consumption by pool with emission factors |
| What are the reburn / short-interval consequences for regeneration and conversion? | time since fire, serotiny/resprout through succession, delayed mortality |
| Where does mixed-severity fire come from: topography, fuels or weather? | per-cell drivers logged with outcomes |
| What is the burn probability and conditional flame-length distribution for each stand? | replicate runs; hazard mode |

---

## 9. Performance and engineering plan

**Data per cell**: stratum id (1 B), fuelbed type (1 B), loads by class (6 × float32),
CBD, CBH, canopy height, ladder index (4 × float32), coarse-wood and duff pools (3 ×
float32), event state (event id int32, arrival time float32, burned-this-year byte),
time-since-fire (int16) ≈ 70 bytes; outputs are written directly from these.

**Costs on a 1 M-cell landscape** (estimates)

| Step | Work | Time |
|---|---|---|
| annual fuelbed build | one pass over cohorts (~10 per cell) | 2–5 s |
| daily moisture | ~200 strata × 300 days | negligible |
| ignitions | alias sampler, O(1) per draw | negligible |
| spread + effects | ∝ burned cells; 2 % of landscape | < 1 s; extreme year (20 %) ≈ 10 s |
| map writes | 15 rasters via GDAL | 3–5 s |

That is at or below SCRPPLE's current cost and well inside the laptop budget. Memory
≈ 70 MB per million cells. Parallelism is not needed; if wanted, per-fire spread and the
annual fuel build are embarrassingly parallel.

**Code structure**

* `Landis.Library.FireBehavior` (netstandard2.0, no LANDIS dependency): moisture,
  Rothermel, crown fire, ellipse geometry, consumption, scorch, mortality, emissions.
  xUnit tests against published worked examples. Buildable today with the local .NET 10 SDK.
* `Landis.Extension.EMBER` (netstandard2.0, `Landis.Core` 3.0.1, UCL-v2, Climate-v6,
  Metadata-v2, Parameters-v2): parameter parser, fuelbed builder, strata, ignition,
  MTT spread, suppression, effects application, outputs. Same project layout as SCRPPLE
  so the Docker scripts patch it without special cases.
* No new NuGet dependencies.

---

## 10. Integration with LANDIS-II and the Docker image

* Extension type `disturbance:fire`, so NECN's fire hooks and the succession library's
  post-fire regeneration engage automatically. Only one `disturbance:fire` extension can
  run per scenario (both would try to register `Fire.Severity`).
* Requires a daily climate series with `UsingFireClimate yes` (same requirement as SCRPPLE).
  Wind direction and speed, RH and precipitation are required; the extension refuses to run
  on monthly climate.
* Build: a new repository (e.g. `Extension-EMBER`) with `src/EMBER.csproj`,
  `deploy/installer/EMBER 1.0.txt`, tests under `testing/`. Add one YAML entry to a **copy**
  of `extensions-v8-UCL2-release.yaml` in a new Docker build folder (e.g.
  `Docker-LANDIS-II-v8-UCL2-ember/`); the working image and its files are not touched.
* NECN coupling phase 2 is a small patch on the `mslucash` fork (register consumption
  fractions and soil-water; read fractions when present). It is optional and backwards
  compatible.
* Output extensions keep working: severity and time-since-fire site variables use the same
  names SCRPPLE registers.

---

## 11. Roadmap

| Phase | Deliverable | Verification |
|---|---|---|
| 0 (done) | this design; source corpus under `source/` | — |
| 1 | `Landis.Library.FireBehavior` + unit tests | matches BehavePlus/FOFEM worked examples |
| 2 | extension skeleton: parser, fuelbed, strata moisture, ignition, MTT spread, effects, outputs | runs the NECN test scenario in a new Docker build; produces every map and log |
| 3 | suppression, Rx, spotting, delayed mortality, hazard mode, replay mode | scenario tests per feature |
| 4 | calibration toolkit (R/Python notebooks for steps 7.1–7.8) | reproduces a historical decade on one landscape |
| 5 | NECN consumption-fraction coupling patch | carbon budget closes against NECN logs |
| 6 | user guide, example inputs, release | — |

Phase 2 is the largest (roughly 4–6 k lines of C#, comparable to SCRPPLE's 5 k).

---

## 12. Decisions to confirm before coding

1. **Name.** EMBER is a placeholder.
2. **Behavior core**: Rothermel / Scott & Burgan (US) as proposed, or Canadian FBP fuel
   types (matches the FWI codes more naturally, but categorical and worse for fuel
   treatments)? The moisture module works with either.
3. **Fuel pools**: extension-owned behavior pools (proposed) vs. depending on an NECN patch
   from day one.
4. **Spread**: deterministic MTT with stochastic ROS noise (proposed) vs. keeping
   SCRPPLE-style probabilistic contagion as an alternative mode.
5. **Cohort mortality default**: fractional (proposed) vs. whole-cohort.
6. Which landscape and data (MTBS, FPA-FOD, FIRED, RAWS/NFMD, FIA) will be the first
   calibration case, so the test scenario can be built around it.

---

## 13. Key references

Albini 1976 (Rothermel corrections); Anderson 1983 (ellipse L/B); Andrews 2012 (wind
adjustment); Beverly & Wotton 2007 (sustained flaming); Brown et al. 1985, 1991 (woody and
duff consumption); Byram 1959 (fireline intensity, flame length); Cansler et al. 2020 (bark
thickness); Cruz, Alexander & Wakimoto 2005 (crown fire ROS); Finney 2002 (minimum travel
time); Rothermel 1972, 1991; Ryan & Reinhardt 1988 (mortality); Scott & Burgan 2005 (fuel
models); Scott & Reinhardt 2001 (crown fire, effective CBD/CBH); Urbanski 2014 (emission
factors); Van Wagner 1973 (scorch height), 1977 (crown fire), 1987 (FWI System); Robbins
et al. 2022 (delayed mortality); Scheller et al. 2019 (SCRPPLE).
