---
title: "LANDIS-II EMBER (v1.0) User Guide"
subtitle: "Ecosystem-coupled Moisture, Behavior, Effects and Regimes"
author:
  - James Lamping
date: "Last revised: September 2026"
---

# Introduction

This document describes the EMBER fire disturbance extension for the LANDIS-II model (v8, Universal Cohort Library v2, Climate Library v6). For information about the LANDIS-II model and its core concepts, see the LANDIS-II Conceptual Model Description and the LANDIS-II Model User Guide.

EMBER (Ecosystem-coupled Moisture, Behavior, Effects and Regimes) simulates wildfire as a physical process driven by the vegetation that the succession extension already tracks and by the daily weather supplied through the Climate Library. Rather than fitting statistical spread and severity models to a particular landscape, EMBER derives fire behavior and fire effects from established fire science equations (Rothermel surface fire spread, Van Wagner crown fire initiation, Byram fireline intensity, Van Wagner crown scorch, Ryan and Reinhardt tree mortality, FOFEM-style fuel consumption). Fire size, seasonality, severity and emissions therefore emerge from the interaction of fuel structure, fuel moisture, weather, topography and suppression. This makes the extension suitable for projecting fire regimes under climates and fuel conditions that have no historical analogue, and for evaluating fuel treatments, prescribed fire windows and suppression policy.

## Model Overview

EMBER runs every year (an annual time step) and simulates the year one day at a time. The order of operations within a year is:

1. **Fuel structure.** For every active cell the extension builds a fuelbed from the cohorts: fine surface fuel (1-h and 10-h classes), coarse dead wood (100-h and 1000-h classes), duff, live herbaceous and live woody (shrub) fuel, and a vertical canopy fuel profile that yields canopy fuel load, effective canopy bulk density, effective canopy base height and a ladder fuel index.
2. **Fuel moisture.** For each day of the year the Climate Library's Fine Fuel Moisture Code, Duff Moisture Code and Drought Code for each climate region are converted into moisture contents of the dead fuel classes, adjusted for canopy shading and topographic exposure. Live fuel moisture and conifer foliar moisture respond to a drought index derived from the Drought Code.
3. **Ignition.** The daily number of lightning and accidental ignitions in each climate region is drawn from a Poisson or zero-inflated Poisson model of that region's daily fire danger. Ignitions are located with user-supplied ignition density maps and become fires only if the fuel in the cell can carry fire that day (optionally also passing a sustain test on the Fine Fuel Moisture Code).
4. **Fire behavior and spread.** Each active fire is grown one day at a time by minimum travel time across the eight neighbours of every perimeter cell. Rate of spread, fireline intensity, flame length and crown fire class are computed for each cell from its fuelbed, its stratum's fuel moisture, the day's wind, and its slope and aspect. The number of hours a fire actively spreads each day is a function of the Fire Weather Index; fires stop spreading below a Fire Weather Index floor and on rain days, and end after a number of days without growth.
5. **Suppression (optional).** At the end of each day, initial attack and containment probabilities are evaluated as logistic functions of fire intensity, fire size, suppression zone and the number of fires burning at the same time, subject to a cap on the number of fires that can receive resources.
6. **Fire effects.** As each cell burns, fuel consumption by class is computed from the class moisture, canopy fuel consumed from the crown fraction burned, emissions from the flaming and smoldering consumption, crown scorch height from the fireline intensity, and mortality for every cohort from its crown volume scorched and bark thickness. A severity class is registered for the succession extension, cohorts are killed or partially killed through the cohort library, and the extension's fuel pools are updated.
7. **Outputs.** Maps of fire behavior, effects and fuel structure are written together with event, daily, ignition and summary logs.

The succession extension then grows, decomposes and regenerates the burned sites in the same simulation year, exactly as it does for the other LANDIS-II fire extensions.

## Fuels

### Surface fuel

Surface dead fuel is tracked in the four standard time-lag classes.

The fine fuel (1-h and 10-h) load is taken from the succession extension when it registers the site variable `Succession.FineFuels` (NECN Succession and PnET Succession do), so that fuel treatments, litterfall and decomposition simulated by succession are reflected directly in fire behavior. The fine fuel is split between the 1-h and 10-h classes by the `LitterFraction1h` value of the cell's fuelbed type. When `SyncFineFuelsToSuccession` is set to no (or the succession extension does not register fine fuels), EMBER maintains its own litter pool from annual foliage turnover and foliage inputs from cohort mortality.

The 100-h and 1000-h coarse wood classes and the duff layer are pools owned by EMBER. They are initialized from the input parameters, receive inputs from cohort mortality events raised by the cohort library (wood partitioned between the two classes by the species parameter `CoarseWoodFraction1000h`) plus a small background wood mortality term (`BackgroundWoodMortality`) that represents growth-related mortality applied inside succession extensions without events, decay at fixed annual rates, and are reduced by fire consumption. These are fire behavior pools; the succession extension remains the accountant of carbon.

Live herbaceous fuel is the foliage of grass life-form cohorts and live woody fuel is the foliage and fine twig mass of shrub life-form cohorts (species table, `LifeForm`).

### Fuelbed types

The physical properties needed by the Rothermel model (surface-area-to-volume ratio of each class, heat content, dead fuel moisture of extinction and the packing ratio of the fuelbed) are specified in a small table of fuelbed types. Each species is assigned to one fuelbed type through its `LitterClass`, and each cell is assigned the fuelbed type of its dominant litter source each year (biomass-weighted foliage plus a share of wood). Six to ten fuelbed types (e.g. short-needle conifer litter, long-needle pine litter, hardwood litter, grass, shrub, slash) are sufficient for most landscapes; the values can be taken from the Scott and Burgan (2005) fuel model tables. Fuelbed depth is not a fixed input: it is derived from the load and the packing ratio, so that fuel accumulation deepens the bed.

### Canopy fuel

For each tree cohort the extension estimates height from age (`MaxHeight`, `HeightK`), crown base height from the crown ratio (`CrownRatio`), and canopy fuel as foliage biomass times one plus the fine branch fraction (`FineBranchFraction`). Foliage biomass is read from the cohort's `LeafBiomass` attribute when the succession extension provides one (NECN), otherwise from the species `FoliageFraction`. The canopy fuel of every cohort is distributed evenly into one-metre height bins between its crown base and its top; shrub cohorts and tree cohorts shorter than `LadderMaxHeight` contribute to the lowest bins. Following Scott and Reinhardt (2001), the effective canopy bulk density is the maximum three-metre running mean of the profile, the effective canopy base height is the lowest height at which the running mean exceeds `CBDThreshold`, and ladder fuel is the canopy fuel below the effective base height. Canopy cover, used for wind adjustment and moisture shading, is estimated from the leaf area index implied by foliage biomass and specific leaf area.

## Fuel Moisture

Fuel moisture is tracked for moisture strata, defined as climate region by canopy cover class (three classes) by topographic radiation class (three classes from slope and aspect), and updated every day. Each cell belongs to one stratum. This keeps the daily cost independent of landscape size while retaining the main sub-regional controls on moisture.

**Dead fuel.** The 1-h moisture is derived from the Fine Fuel Moisture Code (FFMC) of the climate region (Van Wagner 1987, eq. 1):

m1 = 147.2 (101 − FFMC) / (59.5 + FFMC)

The 10-h moisture relaxes toward the 1-h moisture with a 10-hour time constant, and the 100-h moisture relaxes toward 1.3 times the 1-h moisture with a 100-hour time constant (initialized from the Duff Moisture Code). The 1000-h moisture and the duff moisture are linear in the drought index between user-specified wet and drought values. Dead fuel moisture is then multiplied by a stratum modifier: canopy shading raises it by up to ±`ShadeModifier` and topographic exposure lowers it on steep south-facing slopes and raises it on north-facing slopes by up to ±`TopographyModifier`.

**Drought index.** D = (DC − `DroughtCodeLow`) / (`DroughtCodeHigh` − `DroughtCodeLow`), clamped to 0–1, where DC is the Drought Code of the climate region.

**Live fuel.** Herbaceous moisture follows a green-up and curing curve defined by the growing season of the climate region (from the Climate Library) and is reduced by drought; live woody (shrub) moisture is linear in the drought index. Conifer foliar moisture content follows the seasonal curve of the Canadian Forest Fire Behavior Prediction System (Forestry Canada 1992), including the spring dip located from latitude and longitude, multiplied by (1 − `FoliarMoistureDroughtEffect` × D). Broadleaf foliar moisture is a constant (`BroadleafFoliarMoisture`). The foliar moisture used for crown fire initiation in a cell is the foliage-weighted mix of the two.

## Ignition

For each climate region *e*, day *d* and cause *c* (lightning, accidental) the expected number of ignitions is

λ = A_e exp(β0_c + β1_c FD_e,d)

where FD is the region's Fire Weather Index (or Initial Spread Index, `IgnitionFireDanger`) on that day. When `IgnitionAreaScaling` is Landscape, A_e is the region's share of the active landscape and the coefficients describe daily landscape-level counts (the form used by the Social Climate Fire extension, so its fitted coefficients can be reused). When it is PerHectare, A_e is the region's active area in hectares and the coefficients describe counts per hectare-day, which transfer between landscapes. The number of ignitions is drawn from a Poisson distribution with mean λ, or from a zero-inflated Poisson (`IgnitionDistribution`) in which the probability of an excess zero for the day is logistic in the landscape-average fire danger with coefficients `…IgnitionsBinomialB0/B1`, as in Social Climate Fire (Scheller et al. 2019).

Each ignition is placed by sampling cells within the climate region with probability proportional to the ignition density map of its cause (uniform if no map is given). An ignition spreads only if the fire behavior in the cell exceeds the extinction threshold (dead fuel below the moisture of extinction, enough fine fuel, fireline intensity above `ExtinctionIntensity`). By default (`IgnitionAlwaysBurns` yes) an attempt whose cell cannot carry fire is still recorded as a one-cell fire that ends the same day (end reason Unsustained), because ignition coefficients are fitted to fire records that count every reported start; set it to no to have such attempts fail instead. Two optional filters can be added: a sustain test with probability logistic(`IgnitionSustainB0` + `IgnitionSustainB1` × FFMC) following the form of Beverly and Wotton (2007) (`UseIgnitionSustain`, for coefficients fitted to ignition rather than fire counts), and a requirement that the day has a non-zero burn period (`IgnitionRequiresBurnPeriod`). By default an ignition on a no-spread day burns its cell and ends, which reproduces the many one-cell fires of fire records. Attempts that fail are recorded in the ignition log; the ratio of fires to attempts is a useful calibration diagnostic.

## Fire Behavior and Spread

### Surface fire behavior

Surface fire behavior in a cell is computed with the Rothermel (1972) model as formulated by Andrews (2018), including the Albini (1976) corrections, dynamic curing of live herbaceous fuel, and the Andrews et al. (2013) wind limit. The inputs are the cell's loads by class, its fuelbed type properties, its stratum's moisture by class, the mid-flame wind speed and the slope. Mid-flame wind is the climate region's 10-m wind multiplied by a wind adjustment factor that declines linearly from `WindAdjustmentOpen` at zero canopy cover to `WindAdjustmentSheltered` at full cover (Andrews 2012). The wind and slope factors are combined as vectors (the FARSITE convention) to give the head fire rate of spread and heading. Outputs are the rate of spread (m min⁻¹), the reaction intensity, the heat per unit area, Byram's (1959) fireline intensity I = H w R (kW m⁻¹) and flame length L = 0.0775 I^0.46 (m).

### Crown fire

Fireline intensity is Byram's (1959) I = H w R / 60, where H w is the heat per unit area of the flaming front: Rothermel's heat per unit area for the fine fuels plus the heat of the share of the 1000-h consumption that burns in the flaming front (`CoarseFuelFlamingFraction` × 1000-h consumption × heat content). Because the 1000-h consumption depends on its moisture, intensity, flame length, scorch and the crown fire decision all respond to drought as well as to wind; set the fraction to 0 to use Rothermel's fine-fuel intensity alone.

Crown fire initiation is decided by one of two criteria (`CrownInitiation`). With `Cruz2004` (default) the probability of crown fire occurrence follows the logistic model of Cruz, Alexander and Wakimoto (2004) from the 10-m wind, the effective canopy base height (fuel strata gap), the 1-h fuel moisture and the surface fuel consumed (litter, woody and duff, in the classes < 1, 1–2 and > 2 kg m⁻²); the canopy is involved when this probability is at least 0.05 or the Van Wagner criterion below is met, and the crown fraction burned is at least the probability. Cruz and Alexander (2010) show that the Rothermel–Van Wagner linkage under-predicts crown fire in dense conifer forest because the sheltered midflame wind keeps the surface intensity low; the 2004 model was fitted to experimental crown fires and does not depend on the surface spread rate. With `VanWagner` the criterion of Van Wagner (1977) alone applies: a surface fire whose intensity reaches the critical value

I0 = (0.010 CBH (460 + 25.9 FMC))^1.5

(CBH the effective canopy base height in metres, FMC the foliar moisture in percent) torches the canopy. The active crown fire rate of spread is computed with Cruz et al. (2005) from the 10-m wind, the canopy bulk density and the estimated fine fuel moisture; the fire is an active crown fire when this rate exceeds the critical rate 3.0 / CBD (Van Wagner 1977), otherwise a passive crown fire. The crown fraction burned is 1 − exp(−0.23 (R − R0)) in the passive case (at least 0.9 for active crown fire), the final rate of spread and intensity are blended following Scott and Reinhardt (2001), and canopy fuel consumed equals the crown fraction burned times the canopy fuel load. Crown fire can be switched off with `AllowCrownFire`.

### Spread across the landscape

The head fire rate of spread is converted into a rate in every direction with an elliptical fire shape whose length-to-breadth ratio depends on the effective wind speed (Anderson 1983; Finney 2002). Fires are grown by minimum travel time on the eight-neighbour grid: every cell on the fire perimeter contributes arrival times to its unburned neighbours, computed from the receiving cell's directional rate of spread (and the source cell's where its same-day behavior is available), and cells are burned in order of arrival time until the day's burn period is exhausted. Cells reached later are recomputed the next day with the next day's weather, so daily perimeters are a native output.

The burn period, the number of hours of active spread on a day, is zero when the Fire Weather Index of the fire's climate region is below `BurnPeriodFWILow` or when the day's precipitation is at or above `RainoutPrecipitation`, and otherwise rises linearly from `BurnPeriodMinHours` to `BurnPeriodMaxHours` between `BurnPeriodFWILow` and `BurnPeriodFWIHigh`. A fire ends when it has no burnable neighbours (fuel limited), when it has not grown for `NaturalEndDays` consecutive days (weather), when it reaches `MaxFireDays`, when it is contained by suppression, or at the end of the year.

A cell cannot burn on a day when its fireline intensity is below `ExtinctionIntensity`, when its fine dead fuel is wetter than the fuelbed's moisture of extinction, or when its fine fuel load is below `MinimumFineFuelLoad`. This produces unburned islands and fuel-limited fire edges. Sub-cell heterogeneity is represented by multiplying each cell-day's rate of spread by a lognormal factor with mean 1 and standard deviation `ROSNoiseSigma`.

## Suppression

Suppression is optional (`UseSuppression`). Cells carry a suppression zone (0–3) from the zone map; zone 0 receives no suppression (managed wildfire). At the end of the ignition day, initial attack succeeds with probability logistic(a0 + a1 ln(area) + a2 Imax + a3 zone + a4 L), where area is the fire's area in hectares, Imax the day's maximum fireline intensity, and L the number of fires active on the landscape. On later days, containment succeeds with probability logistic(b0 + b1 ln(area) + b2 Rmax + b3 zone + b4 L + b5 hours), with Rmax the day's maximum rate of spread and hours the day's burn period. Only the `MaxSuppressedFires` largest fires receive resources on a given day; the others are treated as zone 0 that day. This structure reproduces escapes during periods of many simultaneous fires without a resource model.

## Fire Effects

Effects are applied to each cell at the moment it burns, with that day's weather and moisture.

**Consumption.** The 1-h and 10-h classes are consumed at fixed fractions when the cell burns; the 100-h, 1000-h and duff classes are consumed at fractions that decline linearly with their own moisture (intercept and slope parameters, defaults following Brown et al. 1985, 1991 and Reinhardt et al. 1997); live herbaceous and live woody fuel at fixed fractions; canopy fuel by the crown fraction burned. Flaming (fine, live, canopy and half of the 100-h) and smoldering (1000-h, duff and the other half of the 100-h) consumption are tracked separately.

**Emissions.** PM2.5 and CO2 (and CO and CH4 internally) are computed from the flaming and smoldering consumption with emission factors in g kg⁻¹ (defaults after Urbanski 2014).

**Crown scorch and mortality.** Scorch height is computed from the fireline intensity, wind speed and the day's maximum temperature with Van Wagner's (1973) equation. For every tree cohort the crown volume scorched follows from the scorch height, the cohort's crown base height and crown length (conical crown); it is 100 % in active crown fire and at least the crown fraction burned in passive crown fire. Bark thickness at the cohort's age is BT = `BarkThicknessMax` × age / (age + `AgeDBH`), the allometry used by Social Climate Fire (Cansler et al. 2020). The probability of mortality is the Ryan and Reinhardt (1988) model:

P(survival) = logistic(−1.941 + 6.316 (1 − exp(−BT)) − 0.000535 CVS²)

with BT in cm and CVS the crown volume scorched in percent; it is 1 in active crown fire. Grass and shrub cohorts are top-killed when their cell burns; resprouting and serotiny are handled by the succession library through the core species parameters, because EMBER kills cohorts with the disturbance type `disturbance:fire`.

With `CohortMortalityMode` Fractional (the default), the mortality probability is applied as the fraction of the cohort's biomass that is removed, so a cohort representing many trees loses the expected share of its stems; cohorts left with less than `MinimumSurvivingFraction` are removed entirely. With Binomial, each cohort is killed entirely with that probability, as in the other LANDIS-II fire extensions. Because the cohort library reduces only the cohort's total biomass while NECN maintains separate leaf and wood attributes, EMBER also scales those attributes after NECN has routed the killed share to its litter pools.

**Severity.** A continuous severity index S = 0.5 × (fraction of tree biomass killed) + 0.3 × (crown fraction burned) + 0.2 × (fraction of surface and duff fuel consumed) is mapped to an integer class 1…`SeverityClasses` and registered as the site variable `Fire.Severity` before cohorts are killed. NECN Succession uses this class to select a row of its `FireReductionParameters` table, so `SeverityClasses` should equal the number of rows in that table (the Social Climate Fire convention is ten classes).

## Requirements and Compatibility

* A **daily** climate time series in the Climate Library with `UsingFireClimate yes`, providing maximum and minimum temperature, precipitation, wind speed and direction, and relative humidity or dew point. Monthly climate is not sufficient because EMBER needs daily fire weather codes, wind and rain.
* A succession extension built on the Universal Cohort Library v2 that registers `Succession.UniversalCohorts` (NECN Succession, PnET Succession, Biomass Succession, ForCS). NECN and PnET also register `Succession.FineFuels`, which EMBER uses for fine fuel by default; with other succession extensions set `SyncFineFuelsToSuccession no`.
* Only one `disturbance:fire` extension can be used in a scenario.
* EMBER runs every year regardless of the succession time step.

## References

Albini, F.A. 1976. Estimating wildfire behavior and effects. USDA Forest Service General Technical Report INT-30.

Anderson, H.E. 1982. Aids to determining fuel models for estimating fire behavior. USDA Forest Service General Technical Report INT-122.

Anderson, H.E. 1983. Predicting wind-driven wild land fire size and shape. USDA Forest Service Research Paper INT-305.

Andrews, P.L. 2012. Modeling wind adjustment factor and midflame wind speed for Rothermel's surface fire spread model. USDA Forest Service General Technical Report RMRS-GTR-266.

Andrews, P.L. 2018. The Rothermel surface fire spread model and associated developments: a comprehensive explanation. USDA Forest Service General Technical Report RMRS-GTR-371.

Andrews, P.L., M.G. Cruz and R.C. Rothermel. 2013. Examination of the wind speed limit function in the Rothermel surface fire spread model. International Journal of Wildland Fire 22: 959–969.

Beverly, J.L. and B.M. Wotton. 2007. Modelling the probability of sustained flaming: predictive value of fire weather index components compared with observations of site weather and fuel moisture conditions. International Journal of Wildland Fire 16: 161–173.

Brown, J.K., M.A. Marsden, K.C. Ryan and E.D. Reinhardt. 1985. Predicting duff and woody fuel consumed by prescribed fire in the Northern Rocky Mountains. USDA Forest Service Research Paper INT-337.

Brown, J.K., E.D. Reinhardt and W.C. Fischer. 1991. Predicting duff and woody fuel consumption in northern Idaho prescribed fires. Forest Science 37: 1550–1566.

Byram, G.M. 1959. Combustion of forest fuels. In: Davis, K.P. (ed.) Forest fire: control and use. McGraw-Hill, New York.

Cansler, C.A., S.M. Hood, J.M. Varner, P.J. van Mantgem, M.C. Agne, R.A. Andrus, et al. 2020. The Fire and Tree Mortality Database, for empirical modeling of individual tree mortality after fire. Scientific Data 7: 194.

Cruz, M.G., M.E. Alexander and R.H. Wakimoto. 2005. Development and testing of models for predicting crown fire rate of spread in conifer forest stands. Canadian Journal of Forest Research 35: 1626–1639.

Finney, M.A. 2002. Fire growth using minimum travel time methods. Canadian Journal of Forest Research 32: 1420–1424.

Forestry Canada Fire Danger Group. 1992. Development and structure of the Canadian Forest Fire Behavior Prediction System. Forestry Canada Information Report ST-X-3.

Reinhardt, E.D., R.E. Keane and J.K. Brown. 1997. First Order Fire Effects Model: FOFEM 4.0, user's guide. USDA Forest Service General Technical Report INT-344.

Rothermel, R.C. 1972. A mathematical model for predicting fire spread in wildland fuels. USDA Forest Service Research Paper INT-115.

Ryan, K.C. and E.D. Reinhardt. 1988. Predicting postfire mortality of seven western conifers. Canadian Journal of Forest Research 18: 1291–1297.

Scheller, R.M., A. Kretchun, T.J. Hawbaker and P.D. Henne. 2019. A landscape model of variable social-ecological fire regimes. Ecological Modelling 401: 85–93.

Scott, J.H. and R.E. Burgan. 2005. Standard fire behavior fuel models: a comprehensive set for use with Rothermel's surface fire spread model. USDA Forest Service General Technical Report RMRS-GTR-153.

Scott, J.H. and E.D. Reinhardt. 2001. Assessing crown fire potential by linking models of surface and crown fire behavior. USDA Forest Service Research Paper RMRS-RP-29.

Urbanski, S.P. 2014. Wildland fire emissions, carbon, and climate: emission factors. Forest Ecology and Management 317: 51–60.

Van Wagner, C.E. 1973. Height of crown scorch in forest fires. Canadian Journal of Forest Research 3: 373–378.

Van Wagner, C.E. 1977. Conditions for the start and spread of crown fire. Canadian Journal of Forest Research 7: 23–34.

Van Wagner, C.E. 1987. Development and structure of the Canadian Forest Fire Weather Index System. Canadian Forestry Service Forestry Technical Report 35.

## Acknowledgments

EMBER reuses the ignition model, ignition and suppression map conventions and the bark thickness allometry of the Social Climate Fire extension (Scheller, Kretchun, Robbins and colleagues), and the Climate Library's fire weather calculations. The first application and calibration landscape is Mount Rainier National Park.

## Major Versions

### Version 1.0 (September 2026)

First release for LANDIS-II v8 with the Universal Cohort Library v2 and Climate Library v6.

## Minor Versions (this major release)

None yet.

# Parameter Input File

Most of the input parameters for this extension are specified in one input file. This text file must comply with the general format requirements described in section 3.1 Text Input Files in the LANDIS-II Model User Guide. Parameters must appear in the order listed below. Parameters marked *optional* may be omitted, in which case the default value shown is used. Two accompanying CSV tables (species and fuelbed types) are described at the end of this section.

## LandisData

This parameter's value must be "EMBER".

## Timestep

Must be 1. EMBER requires daily weather and therefore cannot produce a fire regime for time steps longer than one year.

## SpeciesTable

Path to the species fire parameter CSV file (see Species Table below).

## FuelbedTable

Path to the fuelbed type CSV file (see Fuelbed Table below).

## SlopeMap

A raster map of ground slope in degrees.

## AspectMap

A raster map of aspect in degrees (0–360), the compass direction that the slope faces (downslope direction). Note that this is the conventional aspect, not the uphill azimuth used by Social Climate Fire; EMBER registers the uphill azimuth as `Fire.Aspect` for compatibility.

## SuppressionZoneMap (optional)

A raster map of integer suppression zones with values 0, 1, 2 or 3. Zone 0 indicates no suppression (managed wildfire). Higher values increase the zone term in the initial attack and containment equations. Default: all cells in zone 1.

## LightningIgnitionMap (optional)

A raster map of the relative density of lightning ignitions. Values are non-negative and need no particular units; within each climate region ignitions are placed with probability proportional to the map value. Cells with a value of zero never ignite. Default: uniform.

## DynamicLightningIgnitionMaps (optional)

A table with one line per map, each giving a simulation year and a map name, that replaces the lightning ignition map from that year onward. Years must increase. For example:

```
DynamicLightningIgnitionMaps
10  ignitions/lightning_2040.tif
40  ignitions/lightning_2070.tif
```

## AccidentalIgnitionMap (optional)

A raster map of the relative density of accidental (human-caused) ignitions, as for lightning. Default: uniform.

## DynamicAccidentalIgnitionMaps (optional)

As for `DynamicLightningIgnitionMaps`, for accidental ignitions.

## Latitude (optional double)

Latitude of the landscape in decimal degrees, used for the seasonal foliar moisture curve. Default: 47.0.

## Longitude (optional double)

Longitude of the landscape in decimal degrees (negative in the western hemisphere). Default: −120.0.

## SyncFineFuelsToSuccession (optional yes/no)

Whether the 1-h and 10-h fuel is taken from the succession extension's registered `Succession.FineFuels` site variable (g m⁻²). Set to no when using a succession extension that does not register fine fuels. Default: yes.

## InitialCoarseWood100h, InitialCoarseWood1000h, InitialDuff (optional double)

Initial loads of the 100-h and 1000-h coarse wood classes and of duff in kg m⁻². Defaults: 0.5, 3.0 and 2.0.

## CoarseWoodDecay100h, CoarseWoodDecay1000h, DuffDecay (optional double)

Annual fractional decay of the three pools. Defaults: 0.10, 0.03 and 0.05.

## DuffInputFraction (optional double)

Fraction of the fine litter load added to the duff pool each year. Default: 0.15.

## LitterInputFraction (optional double)

Fraction of tree foliage biomass added to the extension's litter pool each year when fine fuels are not synced to succession. Default: 0.30.

## LitterDecay (optional double)

Annual fractional decay of the extension's litter pool when fine fuels are not synced. Default: 0.40.

## BackgroundWoodMortality (optional double)

Annual fraction of live wood assumed to die without a cohort mortality event, added to the coarse wood pools. Default: 0.01.

## ShadeModifier (optional double)

Magnitude of the canopy shading effect on dead fuel moisture: the moisture multiplier ranges from 1 − ShadeModifier (open) to 1 + ShadeModifier (closed canopy). Default: 0.15.

## TopographyModifier (optional double)

Magnitude of the topographic exposure effect on dead fuel moisture: the multiplier ranges from 1 − TopographyModifier (steep south-facing) to 1 + TopographyModifier (steep north-facing). Default: 0.15.

## DroughtCodeLow, DroughtCodeHigh (optional double)

Drought Code values at which the drought index equals 0 and 1. Defaults: 100 and 500.

## ThousandHourMoisture (optional, two values)

Minimum (drought index 1) and maximum (drought index 0) 1000-h fuel moisture in percent, on one line. Default: 12 35.

## DuffMoisture (optional, two values)

Minimum and maximum duff moisture in percent. Default: 20 150.

## LiveHerbMoisture (optional, two values)

Minimum and maximum live herbaceous moisture in percent. Default: 30 150.

## LiveWoodyMoisture (optional, two values)

Minimum and maximum live woody (shrub) moisture in percent. Default: 60 130.

## FoliarMoistureDroughtEffect (optional double)

Fractional reduction of conifer foliar moisture content at drought index 1. Default: 0.25.

## BroadleafFoliarMoisture (optional double)

Foliar moisture content of broadleaf species in percent. Default: 120.

## IgnitionFireDanger (optional)

The fire danger index used in the ignition model: FWI or ISI. Default: FWI.

## IgnitionAreaScaling (optional)

Landscape (coefficients give daily counts for the whole active landscape, as fitted for Social Climate Fire) or PerHectare (coefficients give counts per hectare and day). Default: Landscape. Note: with Landscape scaling, deactivating part of a landscape (for example to test on a window) concentrates the whole landscape's ignitions on the active part; use PerHectare with the intercept reduced by the natural logarithm of the full active area in hectares.

## LightningIgnitionsB0, LightningIgnitionsB1

Intercept and slope of the log-linear model of daily lightning ignitions against fire danger.

## AccidentalIgnitionsB0, AccidentalIgnitionsB1

Intercept and slope of the log-linear model of daily accidental ignitions against fire danger.

## IgnitionDistribution (optional)

Poisson or ZeroInflatedPoisson. Default: Poisson. If ZeroInflatedPoisson, the following four parameters are required.

## LightningIgnitionsBinomialB0, LightningIgnitionsBinomialB1, AccidentalIgnitionsBinomialB0, AccidentalIgnitionsBinomialB1

Intercept and slope of the logistic model of the probability of a zero-ignition day against the landscape-average fire danger, by cause. Required only with ZeroInflatedPoisson. These are the same parameters as in Social Climate Fire.

## UseIgnitionSustain (optional yes/no)

Whether the FFMC sustain test is applied to ignition attempts. Use it only when the ignition coefficients were fitted to counts of ignitions rather than of recorded fires; with fire-count coefficients (as in Social Climate Fire) it double-counts failures. Default: no.

## IgnitionSustainB0, IgnitionSustainB1 (optional double)

Intercept and slope of the logistic probability that an ignition sustains flaming as a function of FFMC, used when `UseIgnitionSustain` is yes. Defaults: −12.5 and 0.15 (probability 0.5 at FFMC 83, 0.9 at FFMC 98).

## IgnitionRequiresBurnPeriod (optional yes/no)

If yes, an ignition on a day with a zero burn period fails; if no (default), the ignition burns its cell when the fuel can carry fire and the fire then ends unless later days allow spread. The default reproduces the many one-cell fires in fire records.

## IgnitionAlwaysBurns (optional yes/no)

If yes (default), an ignition attempt on a cell that cannot carry fire that day (dead fuel above its moisture of extinction, too little fine fuel, or intensity below `ExtinctionIntensity`) still becomes a one-cell fire with no spread, negligible intensity and end reason Unsustained. This matches the counting convention of fire records, to which ignition coefficients are fitted. If no, the attempt fails and is counted only in the ignition log.

## FirstIgnitionDay, LastIgnitionDay (optional integer)

First and last day of year (0-based) on which ignitions are attempted. Defaults: 0 and 364. Fires already burning continue past the last ignition day.

## BurnPeriodMinHours, BurnPeriodMaxHours (optional double)

Hours of active spread per day at `BurnPeriodFWILow` and at or above `BurnPeriodFWIHigh`. Defaults: 1 and 10.

## BurnPeriodFWILow, BurnPeriodFWIHigh (optional double)

Fire Weather Index below which a fire does not spread on a day, and at which the burn period reaches its maximum. Defaults: 8 and 35.

## RainoutPrecipitation (optional double)

Daily precipitation (cm) at or above which a fire does not spread that day. Default: 0.3.

## NaturalEndDays (optional integer)

Number of consecutive days without growth after which a fire ends. Default: 2.

## MaxFireDays (optional integer)

Maximum number of days a fire can remain active. Default: 60.

## WindAdjustmentOpen, WindAdjustmentSheltered (optional double)

Ratio of mid-flame to 10-m wind speed at zero and at full canopy cover. Defaults: 0.4 and 0.15.

## WindGustFactor (optional double)

Multiplier applied to the daily wind speed from the Climate Library before the wind adjustment factor, to represent afternoon or gust winds when the climate data are daily means. Default: 1.0.

## ExtinctionIntensity (optional double)

Fireline intensity (kW m⁻¹) below which a cell does not burn. Default: 30.

## ROSNoiseSigma (optional double)

Standard deviation of the lognormal multiplier applied to each cell-day's rate of spread (0 disables). Default: 0.3.

## MinimumFineFuelLoad (optional double)

Fine dead plus herbaceous fuel load (kg m⁻²) below which a cell cannot burn. Default: 0.05.

## AllowCrownFire (optional yes/no)

Whether crown fire is simulated. Default: yes.

## CrownInitiation (optional VanWagner | Cruz2004)

Crown fire initiation criterion: `VanWagner` (surface fireline intensity at or above the Van Wagner (1977) critical intensity) or `Cruz2004` (crown fire probability from the Cruz, Alexander and Wakimoto (2004) logistic model, in addition to the Van Wagner criterion). Default: Cruz2004.

## CoarseFuelFlamingFraction (optional double)

Share of the 1000-h fuel consumption that burns in the flaming front and whose heat is added to the fireline intensity. Default: 0.3; 0 gives Rothermel's fine-fuel intensity alone.

## LadderMaxHeight (optional double)

Height (m) below which tree cohort crowns are counted as ladder fuel. Default: 4.

## CBDThreshold (optional double)

Canopy bulk density (kg m⁻³) defining the effective canopy base height. Default: 0.011 (Scott and Reinhardt 2001).

## MaxFuelbedLoad (optional double)

Cap on the fine fuel load (kg m⁻²) used in the Rothermel fuelbed. Default: 4.0.

## UseSuppression (optional yes/no)

Whether suppression is simulated. Default: no. If yes, the following eleven coefficients are required.

## InitialAttackB0 … InitialAttackB4

Coefficients a0–a4 of the initial attack success model: intercept, ln(area in ha), maximum fireline intensity of the day, suppression zone, number of concurrent fires.

## ContainmentB0 … ContainmentB5

Coefficients b0–b5 of the daily containment model: intercept, ln(area), maximum rate of spread of the day, suppression zone, number of concurrent fires, burn period hours.

## MaxSuppressedFires (optional integer)

Maximum number of fires that receive suppression on one day. Default: 10.

## CohortMortalityMode (optional)

Fractional or Binomial (see Fire Effects). Default: Fractional.

## SeverityClasses (optional integer)

Number of severity classes registered as `Fire.Severity`; should match the number of rows in NECN's `FireReductionParameters` table. Default: 10.

## MinimumSurvivingFraction (optional double)

Cohorts whose surviving biomass fraction would fall below this value are removed entirely. Default: 0.05.

## Consumption100h, Consumption1000h, ConsumptionDuff (optional, two values each)

Intercept and slope of the consumed fraction of each class as a linear function of its moisture in percent, C = intercept − slope × moisture, clamped to the class's valid range. Defaults: 1.2 0.020; 1.1 0.020; 0.9 0.0075.

## ConsumptionLiveWoody, ConsumptionLiveHerb (optional double)

Fractions of live woody and live herbaceous fuel consumed in a burned cell. Defaults: 0.5 and 0.9.

## EmissionFactorPM25, EmissionFactorCO2 (optional, two values each)

Flaming and smoldering emission factors in g kg⁻¹. Defaults: 12 26 and 1660 1420.

## MapOutputFolder (optional)

Folder for the output maps. Default: ember.

## FuelMapFrequency (optional integer)

Years between fuel structure maps (0 = never). Default: 10.

## WriteFuelMoistureMaps (optional yes/no)

Whether to write maps of 1-h, 1000-h and foliar moisture on August 1 each year. Default: no.

## WriteCalibrationDiagnostics (optional yes/no)

Whether to write the per-cell diagnostics file (see Output Files). Default: no.

## Species Table

A CSV file with one row per species in the scenario and the following columns:

* **SpeciesCode** – species name as in the core species file.
* **LifeForm** – Tree, Shrub or Grass.
* **LitterClass** – the name of a fuelbed type from the fuelbed table.
* **MaxHeight** – asymptotic height (m).
* **HeightK** – height growth rate (yr⁻¹) in H = MaxHeight (1 − exp(−HeightK × age)).
* **CrownRatio** – live crown length as a fraction of height.
* **FineBranchFraction** – fine branch mass as a fraction of foliage mass (canopy fuel = foliage × (1 + FineBranchFraction)).
* **BarkThicknessMax** – asymptotic bark thickness (cm).
* **AgeDBH** – age (yr) at which bark thickness reaches half of BarkThicknessMax.
* **FoliarMoistureClass** – Conifer, Broadleaf, Shrub or Herb (optional; defaults from LifeForm).
* **FoliageFraction** – foliage as a fraction of cohort biomass, used only when the succession extension does not provide leaf biomass (optional).
* **SpecificLeafArea** – m² kg⁻¹, used for the leaf area and canopy cover estimate (optional; default 5).
* **CoarseWoodFraction1000h** – fraction of this species' dead wood entering the 1000-h class (optional; default 0.7).

## Fuelbed Table

A CSV file with one row per fuelbed type and the following columns: **Name**; **SAV1h**, **SAV10h**, **SAV100h**, **SAVHerb**, **SAVWoody** (surface-area-to-volume ratios, ft⁻¹, as in the standard fuel model tables); **HeatContent** (kJ kg⁻¹; 18608 = 8000 BTU lb⁻¹); **MoistureOfExtinction** (percent); **PackingRatio** (dimensionless; the fuelbed depth is derived from load and packing ratio); **LitterFraction1h** (share of fine litter in the 1-h class); **DuffBulkDensity** (kg m⁻³, diagnostic).

# Output Files

Full metadata for all outputs is written to the Metadata/EMBER folder. Maps are written to `MapOutputFolder` with the simulation year appended to the name. In all maps, inactive cells have the value 0 and, for the fire maps, unburned active cells also have the value 0.

## Fire Maps (every year)

* **severity** – severity class 1 … SeverityClasses of burned cells.
* **event-id** – the event ID of the fire that burned the cell, to be paired with the event log.
* **day-of-burn** – day of year (1–365) on which the cell burned.
* **fireline-intensity** – kW m⁻¹.
* **flame-length-dm** – flame length in decimetres.
* **crown-fire-class** – 1 surface fire, 2 passive crown fire, 3 active crown fire.
* **rate-of-spread-dm-min** – rate of spread in decimetres per minute.
* **canopy-mortality-pct** – percent of tree biomass killed.
* **consumption-kg-ha** – total fuel consumed.
* **pm25-kg-ha** – PM2.5 emitted.
* **cause** – 1 lightning, 2 accidental.

## Fuel Maps (every FuelMapFrequency years)

* **fuel-fine-g-m2**, **fuel-coarse-wood-g-m2**, **fuel-duff-g-m2**, **fuel-live-surface-g-m2** – loads of the fuel pools.
* **canopy-bulk-density-x1000** – effective canopy bulk density × 1000 (kg m⁻³).
* **canopy-base-height-dm** – effective canopy base height in decimetres.
* **canopy-fuel-load-g-m2**, **ladder-fuel-g-m2**.
* **canopy-cover-pct**.
* **fuelbed-type** – index (1-based) of the fuelbed type in the fuelbed table.
* **time-since-fire** – years since the cell last burned (−1 = never burned in the simulation).

## Fuel Moisture Maps (optional)

**moisture-1h-pct-aug1**, **moisture-1000h-pct-aug1**, **moisture-foliar-pct-aug1** – stratum moisture on August 1, in percent.

## Event Log

`ember-events-log.csv` records one row per fire: event ID, simulation year, calendar year of the climate, cause, ignition day, row and column, ignition climate region, FWI at ignition, days active, last day burning, end reason (FuelLimited, Weather, MaxDuration, InitialAttack, Contained, SeasonEnd), cells and area burned (ha), mean and maximum rate of spread (m min⁻¹), mean and maximum fireline intensity (kW m⁻¹), maximum flame length (m), fractions of the fire in surface, passive crown and active crown fire, mean canopy mortality fraction, fraction of high-severity cells, cohorts killed and partially killed, biomass killed and consumption by pool (Mg), PM2.5 and CO2 emitted (Mg), the day of peak intensity and its FWI.

## Daily Log

`ember-daily-log.csv` records one row per fire per day of activity: event ID, year, day of year, FWI, burn period hours, cells burned that day, cumulative area, maximum rate of spread and intensity of the day, and whether suppression was applied. This log is designed for comparison with observed daily fire perimeters.

## Ignition Log

`ember-ignitions-log.csv` records, for each day with ignition attempts or active fires: year, day, landscape-average fire danger, lightning and accidental attempts, fires started by each cause, and the number of fires active at the end of the day.

## Summary Log

`ember-summary-log.csv` records one row per year: calendar year, ignition attempts, fires by cause, cells and area burned, largest fire, fraction of burned area in high severity and in crown fire, first and last fire day, biomass killed, consumption, PM2.5 and CO2, fires contained by suppression, and the mean and maximum landscape fire danger over the ignition season.

## Cell Diagnostics (optional)

`ember-cell-diagnostics.csv` records, for every burned cell, the fuel loads, moisture by class, rate of spread, intensity, crown fraction burned, crown fire class, scorch height, consumption by pool, canopy mortality and severity class. It is intended for calibration against field and remote-sensing observations and can be large.

## Registered Site Variables

EMBER registers the following site variables for use by other extensions: `Fire.Severity` (byte), `Fire.TimeOfLastEvent` (int), `Fire.Slope` and `Fire.Aspect` (ushort, degrees; aspect as uphill azimuth), `Fire.EventID` (int), `Fire.Cause` (byte), `Fire.FirelineIntensity`, `Fire.FlameLength`, `Fire.CrownFractionBurned`, `Fire.CanopyMortalityFraction`, `Fire.SurfaceConsumption`, `Fire.DuffConsumption` and `Fire.CanopyConsumption` (double).

# Sample Input File

The following input file is the Mount Rainier test configuration distributed with the extension (testing folder). Ignition coefficients are those calibrated for Social Climate Fire on the same landscape.

```
LandisData  "EMBER"

Timestep            1

SpeciesTable        extensions/ember_species.csv
FuelbedTable        extensions/ember_fuelbeds.csv
SlopeMap            input_maps/MORA_slope.tif        << degrees
AspectMap           input_maps/MORA_aspect.tif       << degrees, downslope direction
LightningIgnitionMap    input_maps/Lightning_Ignition_Map.tif
AccidentalIgnitionMap   input_maps/Accidental_Ignition_Map.tif
Latitude            46.85
Longitude           -121.75

>> fuel pools
SyncFineFuelsToSuccession   yes
InitialCoarseWood100h       0.6
InitialCoarseWood1000h      4.0
InitialDuff                 3.0

>> moisture
ShadeModifier          0.15
TopographyModifier     0.15
DroughtCodeLow         150
DroughtCodeHigh        600

>> ignitions
IgnitionFireDanger      FWI
IgnitionAreaScaling     Landscape
LightningIgnitionsB0   -3.126639
LightningIgnitionsB1    0.050481
AccidentalIgnitionsB0  -2.55915
AccidentalIgnitionsB1   0.064927
IgnitionDistribution    ZeroInflatedPoisson
LightningIgnitionsBinomialB0    1.96777
LightningIgnitionsBinomialB1   -0.139142
AccidentalIgnitionsBinomialB0  -0.068511
AccidentalIgnitionsBinomialB1  -0.114866
IgnitionSustainB0     -12.5
IgnitionSustainB1       0.15

>> burn period
BurnPeriodMinHours     1
BurnPeriodMaxHours    10
BurnPeriodFWILow       8
BurnPeriodFWIHigh     35
RainoutPrecipitation   0.3
NaturalEndDays         2
MaxFireDays           60

>> behavior
ExtinctionIntensity   30
ROSNoiseSigma          0.3
AllowCrownFire        yes

>> suppression
UseSuppression        no

>> effects
CohortMortalityMode   Fractional
SeverityClasses       10

>> outputs
MapOutputFolder       ember
FuelMapFrequency      10
WriteFuelMoistureMaps no
WriteCalibrationDiagnostics no
```

Fuelbed table (`ember_fuelbeds.csv`):

```
Name,SAV1h,SAV10h,SAV100h,SAVHerb,SAVWoody,HeatContent,MoistureOfExtinction,PackingRatio,LitterFraction1h,DuffBulkDensity
ShortNeedle,2000,109,30,1800,1500,18608,25,0.030,0.65,110
LongNeedle,1800,109,30,1800,1500,18608,35,0.035,0.75,90
Broadleaf,2000,109,30,1800,1500,18608,25,0.020,0.80,80
```

Species table (`ember_species.csv`, first rows):

```
SpeciesCode,LifeForm,LitterClass,MaxHeight,HeightK,CrownRatio,FineBranchFraction,BarkThicknessMax,AgeDBH,FoliarMoistureClass,SpecificLeafArea,CoarseWoodFraction1000h
PseudotsugaMenziesii,Tree,ShortNeedle,70,0.012,0.45,0.30,8.27,100,Conifer,5.0,0.80
TsugaHeterophylla,Tree,ShortNeedle,60,0.012,0.60,0.30,3.68,105,Conifer,6.5,0.75
AbiesAmabilis,Tree,ShortNeedle,50,0.011,0.65,0.30,2.84,157,Conifer,6.0,0.70
PinusPonderosa,Tree,LongNeedle,50,0.014,0.40,0.30,12.1,92,Conifer,4.0,0.75
AlnusRubra,Tree,Broadleaf,30,0.040,0.50,0.25,1.12,29,Broadleaf,14.0,0.50
```

The scenario file lists the extension under Disturbance Extensions:

```
>> Disturbance Extensions   Initialization File
   "EMBER"                  extensions/EMBER.txt
```
