# EMBER calibration workspace (Mount Rainier)

A record of how EMBER is being calibrated against the Social Climate Fire (SCF) run and
observations for MORA, written so that the same workflow can be reused on other
landscapes. Newest entries at the bottom of the log.

## Layout

```
R/scf_reference.R        reference statistics from an SCF run (whole map or a window)
R/fpa_fod_targets.R      observed targets from the FPA-FOD record (fires/yr, sizes, causes, season)
R/mtbs_targets.R         observed targets from MTBS dNBR mosaics (area, severity classes)
R/geomac_targets.R       observed daily growth from GeoMAC perimeters
R/fire_day_weather.R     GRIDMET weather on the days of the recorded large fires vs fire-season climate
R/score_run.R            score an EMBER run against the SCF and observed references; plots
R/ignition_diagnostics.R attempts and fires by fire danger and season; daily behavior by FWI bin
R/fuel_diagnostics.R     canopy and surface fuel distributions and crown-fire thresholds from the fuel maps
R/pool_replicates.R      pool the logs of several seeds of one variant into one long run
R/compare_variants.R     one table and one fire-size figure across all scored variants
run_variant.sh           run one parameter variant on the windowed MORA scenario and score it
run_replicates.sh        run one variant with several seeds concurrently, pool and score
variants/*.txt           the override files, one per variant (the record of what changed)
reference/window/        SCF reference for the 120 x 120 test window (rows 320-440, cols 320-440)
reference/observed/      observed targets for the window (FPA-FOD, MTBS, GeoMAC, GRIDMET)
runs/<variant>/          each variant run: inputs, outputs, score_<variant>.csv, plots
summary/                 variants_table.csv and fire_size_cdf.png from compare_variants.R
```

Datasets available for later steps (`~/Documents/Anthropic_Projects/scf_calibration_app/data_raw`):
FPA-FOD 1992–2020 ignitions (fires/yr, sizes, causes, dates), GeoMAC daily perimeters
2000–2018, MTBS dNBR mosaics 1984–2023, Fire and Tree Mortality database, GRIDMET and
ERA-Interim FWI codes, TerraClimate deficit/PET, soils.

## Method

The comparison statistics are distributional (both runs draw random climate years):
cells burned per year (mean, median, p90, max, fraction of years with fire, rotation),
fires per year, fire size quantiles, fractions of fires above 10 and 100 cells. Behavior
diagnostics that have no SCF counterpart (rate of spread, intensity, days active, crown
fire fraction, fires per attempt) are reported alongside.

Runs use the 9 419-cell window (about one minute per simulated year), 40 years, seed 3333
unless noted. Ignition intercepts in the window are the SCF landscape values shifted by
−ln(398 065 ha) with `IgnitionAreaScaling PerHectare`.

## Reusing this on another landscape

1. Build the observed targets: `R/fpa_fod_targets.R` (fires per year by cause, sizes, season),
   `R/mtbs_targets.R` (annual area, severity classes), `R/geomac_targets.R` (daily growth) and
   `R/fire_day_weather.R` (was the large-fire weather windy or dry?). Each takes the LANDIS
   ecoregion map, so the targets are for the modelled area only; for a test window, the counts
   scale by the window's share of the landscape area.
2. Start from the EMBER template of `testing/MORA-window/extensions/EMBER.txt` with the
   landscape's own ignition coefficients (SCRPPLE/SCF coefficients transfer unchanged with
   `IgnitionAreaScaling PerHectare`) and species table.
3. Run variants with `run_replicates.sh <name> 40 variants/<name>.txt <seeds…>`; each variant
   file is a few `Key value` overrides and is the record of what changed. Compare with
   `R/compare_variants.R`. Single 40-year runs of a small window hold 5–10 fires: use several
   seeds.
4. Diagnose before tuning: `R/ignition_diagnostics.R` (where attempts fail, how spread scales
   with FWI) and `R/fuel_diagnostics.R` (canopy base height, bulk density, the wind needed for
   crowning). The order that worked here: (a) fire counts and season — ignition coefficients,
   `FirstIgnitionDay`/`LastIgnitionDay`; (b) everyday spread — fuelbed `PackingRatio`,
   `ExtinctionIntensity`, the burn-period parameters; (c) the severity tail —
   `CrownInitiation`, `CoarseFuelFlamingFraction`, `FoliarMoistureDroughtEffect`; (d) escape
   frequency and size — the initial-attack and containment coefficients, with the intensity
   slopes set for the intensities the behavior model actually produces (check
   `max_intensity_p90_kW_m` in the diagnostics).

## Log

### Baseline (before the parameter pass)

Full-landscape run, first 13 years (`testing/MORA-full`), scored with `compare_scf.py`:

| statistic | SCF (80 yr) | EMBER |
|---|---|---|
| fires / yr | 31 | 2.6 |
| fire size median / p90 / max (cells) | 10 / 124 / 7 541 | 4 / 19 / 62 |
| cells burned / yr, mean | 4 675 | 25 |
| fires per ignition attempt | 1.0 | 0.08 |
| mean ROS median (m/min) | | 0.8 |
| active crown fire | | none |

Diagnosis: (1) the FFMC sustain test, the burn-period floor and the extinction threshold
together reject 92 % of ignitions, but the SCF coefficients were fitted to counts of
recorded fires so every attempt should become a fire; (2) rate of spread is limited by the
sheltered wind factor (0.15 at ~93 % canopy cover), the absence of any understory fuel in
the MORA species list, shallow litter beds, and no crown fire (initiation threshold
~2 900 kW/m against observed maxima ~2 500).

### Observed targets (FPA-FOD 1992-2020, MTBS 1992-2024) — `R/fpa_fod_targets.R`, `R/mtbs_targets.R`

Outputs in `reference/observed/`. Fires whose FPA-FOD point falls on an active MORA cell:

| statistic | FPA-FOD record | SCF run (80 yr) |
|---|---|---|
| fires / yr | 35.0 (12.9 lightning, 22.1 human) | 31.0 (5.8 lightning, 25.2 human) |
| fire size median / p90 / max (cells) | 1 / 1 / 26 008 | 10 / 124 / 7 541 |
| fraction of fires > 10 / > 100 cells | 0.03 / 0.017 | 0.48 / 0.11 |
| fraction of burned area in fires > 100 cells | 0.96 | 0.91 |
| cells burned / yr, mean (median) | 1 255 (65) | 4 675 (2 661) |
| rotation (yr) | 392 | 105 |
| ignition day of year p10 / median / p90 | 176 / 221 / 265 | 61 / 214 / 295 |

MTBS (large fires only, ≥ ~400 ha): 1 789 cells/yr mean, median 0, fire in 6 of 33 years,
rotation 275 yr; burned area 33 % low / 25 % moderate / 42 % high severity by dNBR class;
2017 (18 900 ha) and 2021 (23 900 ha) dominate.

Implications: the record is "many tiny fires, rare huge ones". The SCF run burns roughly
four times the recorded area, has half the recorded lightning fires, too few one-cell fires
and too many 10–100-cell fires, and ignites fires in winter and early spring that the record
does not contain. EMBER's calibration targets are therefore the observed record (fire counts
by cause, size distribution, rotation, severity mix, seasonality), with SCF as a secondary
reference for the spread of the large-fire tail. The 120 × 120 window is 1.9 % of the
landscape, so an observed rotation of ~300–400 yr means ~25–30 cells/yr in the window on
average, almost all of it in a few extreme years.

### Observed daily growth (GeoMAC 2000–2018) — `R/geomac_targets.R`

Fires with perimeters on the landscape: Norse Peak 2017 (20 872 ha inside MORA, 26 perimeters
over 38 days), Miriam 2018 (2 136 ha, 38 days), American 2017 (1 557 ha, 115 days), plus
~10 fires of 100–300 ha. Growth between perimeters ≤ 3 days apart: median 26 ha/day, p90
432 ha/day, max ~10 000 ha/day (Norse Peak's wind-driven run). Three fires > 1 000 ha in
19 years landscape-wide, i.e. one such fire per ~300 years inside the 1.9 % window. This is
the target for the daily log: established fires should add tens of ha/day for weeks with
rare hundreds-to-thousands ha/day runs, and the escape to that state must be rare.

### Pass 1 — `variants/pass1.txt`

Code additions: `UseIgnitionSustain` (default no), `IgnitionRequiresBurnPeriod`
(default no), `WindGustFactor` (default 1). Parameter changes: sheltered wind factor
0.15 → 0.30, burn period max 10 → 16 h reached at FWI 30, extinction 30 → 15 kW/m,
foliar moisture drought effect 0.25 → 0.35, fuelbed packing ratios lowered
(0.030/0.035/0.020 → 0.020/0.025/0.015, i.e. deeper beds for the same load).
Result (`runs/pass1/`, 40 years): 11 fires. Ignition side improved: 0.46 fires per attempt
(was 0.08), ignition-day distribution now matches the record (p10/median/p90 = 162/242/251
vs 176/221/265 observed) because winter attempts fail. Spread side overshot: fire size
median 256 cells (observed 1, SCF 12), 82 % of fires > 10 cells (observed 3 %), annual
fraction burned 0.032 (rotation 32 yr vs observed 275–390), maximum intensities up to
43 MW/m, yet high-severity area only 0.2 % (MTBS 42 %) because most cells still burn as
low-intensity surface fire under tall canopies and active crown fire stayed at 0.15 % of
cells (median canopy bulk density 0.05 kg/m³ needs > 45 km/h wind for active crowning by
Cruz et al. 2005). Conclusion: the baseline's creeping everyday spread (~0.8 m/min) was
right for Rainier; the large-fire tail must come from east-wind days, crown fire and
spotting, not from faster everyday spread.

### Pass 2 — `variants/pass2a.txt`, `variants/pass2b.txt`

2a: everyday spread pulled back to the middle (sheltered wind factor 0.20, packing ratios
0.025/0.030/0.018, extinction 25 kW/m, burn period 1–16 h between FWI 12 and 40, rain-out
0.2 cm), ignition fixes kept, no suppression (comparable with SCF).
2b: 2a plus initial attack and containment (intercept 3.0 gives ~93 % initial-attack success
for a one-cell 300 kW/m fire, ~50 % at 4 000 kW/m), comparable with the observed record.

Results (40 window-years each):

| statistic | observed (window share) | SCF window | pass 2a | pass 2b |
|---|---|---|---|---|
| annual fraction burned | 0.0026 FPA / 0.0036 MTBS | 0.0084 | 0.0046 | 0.0003 |
| rotation (yr) | 275–390 | 120 | 220 | 3 100 |
| fires / yr | 0.67 | 0.49 | 0.23 | 0.13 |
| fires > 100 cells per 40 yr | ~0.4 | 3.5 | 3 | 1 |
| fire size max (cells) | | 1 283 | 952 | 112 |
| median ROS (m/min), max intensity p90 (kW/m) | | | 0.9, 7 300 | 0.9, 10 800 |
| ignition day p10 / median / p90 | 176 / 221 / 265 | | 202 / 215 / 247 | 167 / 242 / 273 |

Reading: 2a's everyday behavior is right (creeping surface fire, extreme years produce the
large fires) and its area burned sits between the FPA-FOD and MTBS rates; its excess is the
escape frequency (fires > 100 cells about 7× the record). 2b's initial attack at ~93 %
over-corrects. EMBER's fire count is ~0.3 of the record because ignitions on days when the
litter is wetter than its moisture of extinction fail, whereas the record includes every
reported 0.1-acre start; the count of fires > 10 cells is the better comparison. With
only 5–9 fires per 40 window-years, single runs are too noisy: from pass 3 on, variants
are run as 4 seeds × 40 years and scored pooled (`run_replicates.sh`, `R/pool_replicates.R`).

### Pass 3 — `variants/pass3.txt`

Pass 2a behavior with moderate initial attack (intercept 1.5: ~80 % success for a one-cell
300 kW/m fire, ~35 % at 4 000 kW/m; containment intercept 0). Seeds 3333, 4444, 5555, 6666,
160 pooled window-years (`runs/pass3_pooled`).

| statistic | observed (window share) | pass 2a | pass 3 |
|---|---|---|---|
| annual fraction burned | 0.0026 FPA / 0.0036 MTBS | 0.0046 | 0.00055 |
| fires / yr | 0.67 | 0.23 | 0.18 |
| fires > 100 cells per 40 yr | ~0.45 | 3 | 1.0 |
| fire size p90 / max (cells) | 1 / – | 662 / 952 | 116 / 293 |
| days active median / p90 | | 4 / 25 | 1 / 6.8 |
| crown fire share of cells (passive / active) | | 0 / 0 | 0.047 / 0.001 |
| high-severity share of burned area | 0.42 | 0 | 0.001 |
| ignition day p10 / median / p90 | 176 / 221 / 265 | 202 / 215 / 247 | 178 / 238 / 269 |

Reading: the moderate initial attack brings the escape rate (fires > 100 cells) within about
2× of the record, and the ignition season now matches. Area burned is 5× too low because the
escaped fires stay small (max 293 cells; 25 % end on weather); the record's area comes from
a few stand-replacing fires that this configuration cannot produce (no crown fire, high
severity 0.1 %). Fire counts remain 0.26 of the record for the attempt-failure reason
diagnosed below. Both are addressed structurally in pass 4.

### Diagnosis after pass 2 (R scripts: `R/ignition_diagnostics.R`, `R/fuel_diagnostics.R`, `R/fire_day_weather.R`)

1. **Why only a quarter of attempts become fires.** Tabulating the ignition log by fire danger:
   all 18 failed attempts of pass 2a fell on days with FWI ≤ 5 (spring and autumn); every attempt
   at FWI > 5 became a fire. The SCRPPLE-style coefficients are fitted to the FPA-FOD record,
   which counts every reported start, so the attempts are already "fires" by the record's
   convention. Structural change: `IgnitionAlwaysBurns` (default yes) makes an attempt whose cell
   cannot carry fire a one-cell fire with no spread (end reason Unsustained). Fire counts then
   match the record by construction and the comparison moves to fires > 10 cells.

2. **Why there is no crown fire or high severity.** `fuel_diagnostics.R` on the year-10 fuel maps:
   canopy base height median 5 m (p90 11 m), canopy bulk density median 0.11 kg m⁻³, canopy
   cover ≥ 90 % almost everywhere, fine fuel median 0.7 kg m⁻², 1000-h fuel median 4.5 kg m⁻²,
   duff 2.5 kg m⁻². The Van Wagner critical intensity is 1 900 kW m⁻¹ at the median cell, but
   under a 100 % canopy the midflame wind is a fifth of the 10-m wind and the Rothermel surface
   intensity on fire days is 100–700 kW m⁻¹ (daily log by FWI bin below). Active crowning by the
   Cruz (2005) criterion needs ≈ 20 km h⁻¹ of 10-m wind at 8 % fine fuel moisture; the GRIDMET
   daily mean over the window exceeds 20 km h⁻¹ on 5 % of July–September days and 30 km h⁻¹ on
   0.5 %. The observed stand-replacing fires (American 2017, 2021) grew on days with 14–17 km h⁻¹
   mean wind and 18–40 % minimum RH, i.e. they were drought- not wind-driven
   (`R/fire_day_weather.R`, output `reference/observed/fire_day_weather.csv`: Norse Peak 2017,
   20 900 ha, grew from 13 Aug to 19 Sep with a p90 daily wind of 15.6 km h⁻¹ and minimum RH
   16 %; July–September winds over the window: median 11, p90 17, p99 28 km h⁻¹). This is the
   under-prediction of the Rothermel–Van Wagner linkage in dense conifer forest described by
   Cruz and Alexander (2010). Two structural changes: (a) `CrownInitiation Cruz2004` — the
   Cruz, Alexander and Wakimoto (2004) logistic probability of crown fire from 10-m wind,
   canopy base height, fine fuel moisture and surface fuel consumed, which is independent of the
   sheltered surface spread rate and rises steeply with drought through the consumption term;
   (b) `CoarseFuelFlamingFraction` — the flaming share of the 1000-h consumption is added to
   Byram's intensity, so intensity, scorch height and the Van Wagner criterion respond to
   drought (1000-h moisture) and not only to wind.

   Pass 2a daily log by FWI bin (all fire-days):

   | FWI | days | burn h | cells/day median | max ROS median (m/min) | max intensity median (kW/m) |
   |---|---|---|---|---|---|
   | 15–20 | 17 | 4.2 | 2 | 1.0 | 110 |
   | 20–25 | 19 | 6.7 | 10 | 2.3 | 380 |
   | 25–30 | 16 | 9.1 | 20 | 2.9 | 600 |
   | 30–40 | 11 | 12.4 | 31 | 3.2 | 720 |
   | > 40 | 2 | 16 | 170 | 4.3 | 1 340 |

3. **Wind units.** The MORA climate file holds GRIDMET wind in m s⁻¹; the Climate Library
   multiplies by 3.6, so EMBER's 10-m wind is in km h⁻¹ as intended (checked in
   `ClimateDataParser.cs`). No correction needed.

### Pass 4 — `variants/pass4.txt`

Pass 3 parameters with the three structural changes (`IgnitionAlwaysBurns yes`,
`CrownInitiation Cruz2004`, `CoarseFuelFlamingFraction 0.3`), same four seeds, image rebuilt
from the patched source. 160 pooled window-years (`runs/pass4_pooled`).

| statistic | observed (window share) | pass 3 | pass 4 |
|---|---|---|---|
| annual fraction burned | 0.0026 FPA / 0.0036 MTBS | 0.00055 | 0.0165 |
| fires / yr (lightning) | 0.67 (0.25) | 0.18 (0.05) | 0.54 (0.06) |
| fires per attempt | ~1 | 0.29 | 0.98 |
| fires > 100 cells per 40 yr | ~0.45 | 1.0 | 3.3 |
| fire size p90 / max (cells) | 1 / – | 116 / 293 | 247 / 6 102 |
| days active p90 / max | | 6.8 / 11 | 9 / 26 |
| cells per fire-day p90 | | 33 | 330 |
| crown fire share of cells (passive / active) | | 0.047 / 0.001 | 0.73 / 0.03 |
| high-severity share of burned area | 0.42 | 0.001 | 0.36 |
| canopy mortality mean | | 0.07 | 0.41 |
| ignition day p10 / median / p90 | 176 / 221 / 265 | 178 / 238 / 269 | 57 / 224 / 287 |
| end reasons (of 86 fires) | | | 41 unsustained, 22 initial attack, 2 contained, 21 weather |

Reading: the three structural changes did what they were meant to. Fire counts are 0.8 of the
record and the burned area now carries the record's severity signature (36 % high severity vs
42 %; the 13 escaped fires crowned over 70–90 % of their cells and grew 200–800 cells on
FWI > 30 days, like Norse Peak 2017). Two things overshoot: escapes (fires > 100 cells) are
7× the record and the escaped fires are too large (max 6 100 cells, 65 % of the window, over
19–26 days; only 2 of 23 escapes were contained), so area burned is 4.5–6.5× the record. The
fireline intensities of the new model are 5–10× the old ones, so the initial-attack intensity
slope fitted in pass 3 now switches suppression off on every drought day. Lightning fires are
a quarter of the record's (the SCF coefficients also under-predict them) and 19 % of
accidental attempts fall before day 120 (record: 0.8 %), all of them unsustained.

### Pass 5 — `variants/pass5.txt`

Pass 4 with suppression rescaled to the new intensities (`InitialAttackB0` 2.5,
`InitialAttackB2` −0.0001; `ContainmentB0` 1.5, `ContainmentB1` −0.3), the ignition season
limited to days 120–305, and `LightningIgnitionsB0` raised by ln(3.9) to −14.66. Same seeds.
160 pooled window-years (`runs/pass5_pooled`).

| statistic | observed (window share) | pass 4 | pass 5 |
|---|---|---|---|
| annual fraction burned | 0.0026 FPA / 0.0036 MTBS | 0.0165 | 0.0103 |
| fires / yr (lightning) | 0.67 (0.25) | 0.54 (0.06) | 0.72 (0.36) |
| fires > 10 cells per 40 yr | ~0.8 | 3.5 | 7.5 |
| fires > 100 cells per 40 yr | ~0.45 | 3.3 | 4.3 |
| fire size p90 / max (cells) | 1 / – | 247 / 6 102 | 210 / 3 729 |
| days active p90 / max | | 9 / 26 | 3 / 17 |
| high-severity share of burned area | 0.42 | 0.36 | 0.45 |
| ignition day p10 / median / p90 | 176 / 221 / 265 | 57 / 224 / 287 | 181 / 230 / 281 |
| end reasons (of 115 fires) | | | 28 unsustained, 61 initial attack, 19 contained, 7 weather |

Reading: fire counts (1.08×), season and severity (0.45 vs 0.42) now match the record.
Lightning overshoots by 1.45×. Escapes are still 9× the record and area burned 2.8–4×: the
initial attack caught 70 % of sustained fires, not the 90 % intended, because ignition-day
intensities under the new intensity model are commonly 5 000–30 000 kW/m (85 % of burned
cells crown), and the largest escapes were contained only after 7–17 days at 1 600–3 700
cells. Both suppression stages need to be stronger for this landscape (a national park with
full suppression of most starts).

### Pass 6 — `variants/pass6.txt`

Pass 5 with `InitialAttackB0` 3.5, `ContainmentB0` 2.5 and `LightningIgnitionsB0` −15.0.
Same seeds. 160 pooled window-years (`runs/pass6_pooled`).

| statistic | observed (window share) | pass 5 | pass 6 |
|---|---|---|---|
| annual fraction burned | 0.0026 FPA / 0.0036 MTBS | 0.0103 | 0.0044 |
| fires / yr (lightning) | 0.67 (0.25) | 0.72 (0.36) | 0.54 (0.19) |
| fires > 10 cells per 40 yr | ~0.8 | 7.5 | 4.5 |
| fires > 100 cells per 40 yr | ~0.45 | 4.3 | 2.75 |
| fire size p90 / max (cells) | 1 / – | 210 / 3 729 | 144 / 3 305 |
| days active p90 / max | | 3 / 17 | 2 / 8 |
| high-severity share of burned area | 0.42 | 0.45 | 0.47 |
| ignition day p10 / median / p90 | 176 / 221 / 265 | 181 / 230 / 281 | 163 / 233 / 285 |
| end reasons (of 86 fires) | | | 27 unsustained, 41 initial attack, 17 contained, 1 weather |

Reading: fire counts (0.8×), lightning share, season, area burned (1.2–1.7× the record; 1 638
cells per 40 years, half of it one 3 305-cell fire on an FWI-40 ignition day) and severity
(0.47 vs 0.42) are all within the uncertainty of a 29-year record for a 9 400-cell window.
Escapes remain 5–7× the record's point estimate, but that estimate rests on roughly one
escaped fire in the window in 29 years (Poisson 95 % interval 0.03–5.6 per 29 years); EMBER's
3.3 per 29 years is inside it. The window cannot decide this; the full-landscape run (53×
the area, ~35 fires per year) can, and the suppression intercepts are the knobs.

**Pass 6 is the accepted working set.** Its values are applied to
`testing/MORA-window/extensions/EMBER.txt` and, with `LightningIgnitionsB0` −15.33 (the
landscape-wide SCF-to-record lightning ratio is 2, not 2.8) and `MaxSuppressedFires` 40, to
`testing/MORA-full/extensions/EMBER.txt` (`apply_variant.sh`).

### What changed from the original template to pass 6

| parameter | template | pass 6 | reason |
|---|---|---|---|
| fuelbed `PackingRatio` (short / long needle, broadleaf) | 0.01 | 0.025 / 0.030 / 0.018 | everyday spread 2× too fast (pass 1) |
| `BurnPeriodMaxHours`, `BurnPeriodFWILow/High` | 10, 8 / 35 | 16, 12 / 40 | steeper response to fire danger |
| `RainoutPrecipitation`, `ExtinctionIntensity` | 0.3, 30 | 0.2, 25 | |
| `WindAdjustmentSheltered` | 0.15 | 0.20 | |
| `FoliarMoistureDroughtEffect` | 0.25 | 0.35 | crown fire under drought |
| `IgnitionAlwaysBurns`, `CrownInitiation`, `CoarseFuelFlamingFraction` | (new) | yes, Cruz2004, 0.3 | structural, see diagnosis |
| `FirstIgnitionDay`, `LastIgnitionDay` | 0, 364 | 120, 305 | record: < 1 % of fires before day 120 |
| `LightningIgnitionsB0` | −16.02 | −15.0 (window) / −15.33 (full) | SCF coefficients under-predict lightning fires |
| `UseSuppression` + coefficients | no | yes; IA 3.5 −0.5 −0.0001 0 −0.1; containment 2.5 −0.3 −0.1 0 −0.1 −0.05; max 10 | escape rate and escaped-fire size |

### Next

* Full-landscape run with the pass 6 template and comparison with the SCF run and the
  landscape-wide record (fires per year by cause, MTBS annual area, severity, and the daily
  growth of the GeoMAC fires).
* If escapes are still high at landscape scale: `InitialAttackB0` up to ~4, or a stronger
  `ContainmentB1`; if the large fires are too small: `BurnPeriodMaxHours`/`WindGustFactor`.
* `R/fire_day_weather.R` shows the record's large fires were drought-driven; the
  `WindGustFactor` was therefore left at 1.0 and never exercised.
