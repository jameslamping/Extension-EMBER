# EMBER — a mechanistic, climate-sensitive fire extension for LANDIS-II v8

**EMBER** (Ecosystem-coupled Moisture, Behavior, Effects and Regimes) simulates wildfire as a
physical process driven by the vegetation the succession extension already tracks and by the
daily weather supplied through the LANDIS-II Climate Library. Instead of fitting statistical
spread and severity models to one landscape, EMBER derives fire behavior and effects from
established fire science: Rothermel surface spread, Van Wagner and Cruz crown fire, Byram
fireline intensity, Van Wagner crown scorch, Ryan and Reinhardt mortality, FOFEM-style
consumption. Fire size, seasonality, severity and emissions emerge from the interaction of
fuel structure, fuel moisture, weather, topography and suppression.

That makes it usable for climates and fuel conditions with no historical analogue, and for
evaluating fuel treatments, prescribed fire windows and suppression policy. It is designed to
run on a personal computer: a 9 400-cell test landscape simulates in about a minute per year,
of which EMBER is seconds.

## Layout

```
EMBER/
  src/                  the extension
    FireBehavior/       standalone fire behavior library (no LANDIS dependencies)
  tests/                xUnit tests for the behavior library
  docker/               dev image: the EMBER DLL layered onto a working LANDIS-II v8 image
  deploy/installer/     extension registration file
  docs/                 user guide (Markdown, docx, PDF) and container instructions
  calibration/          the calibration record: R scripts, parameter variants, results
  testing/              example scenarios (windowed and full Mount Rainier landscape)
docs/
  01_LANDIS-II_Architecture_Notes.md   how the core, cohort library, NECN, Climate Library
                                       and the existing fire extensions work
  02_EMBER_Design.md                   design: mechanism, inputs, outputs, calibration, roadmap
  03_EMBER_Build_Log.md                implementation and testing notes
```

## Documentation

* **[EMBER user guide](EMBER/docs/LANDIS-II%20EMBER%20v1.0%20User%20Guide.md)** — the LANDIS-II
  format guide: every input parameter, the equations behind them, the output files.
* **[EMBER/README.md](EMBER/README.md)** — quick input reference and developer notes.
* **[Calibration record](EMBER/calibration/README.md)** — how the Mount Rainier parameter set
  was arrived at, pass by pass, with the R scripts and a section on reusing the workflow on
  another landscape.
* **[Container instructions](EMBER/docs/Getting_EMBER_running_container.md)** — build the image
  and run a full landscape from a terminal.

## Building

EMBER targets `netstandard2.0` and builds against the LANDIS-II v8 libraries (Landis.Core from
the LANDIS-II MyGet feed; the universal cohort, climate, metadata and parameter libraries from
`EMBER/src/lib/`).

```bash
cd EMBER/src && dotnet build EMBER.csproj -c Release
cd ../tests/FireBehavior.Tests && dotnet test
```

`EMBER/docker/build.sh` builds the DLL and layers it onto an existing LANDIS-II v8 runtime
image, registering the extension without needing an SDK in the image.

## What is not in this repository

* **Run outputs.** The calibration runs and example scenarios produce tens of gigabytes of
  LANDIS-II output. `EMBER/calibration/results/` keeps the scored logs, diagnostics and plots
  of every calibration variant; the raw run folders are ignored.
* **Pinned upstream clones.** Development used exact-commit clones of the LANDIS-II Foundation
  repositories (core model, NECN succession, SCRPPLE, the cohort and climate libraries) for
  reference. They are third-party code and are not redistributed here; clone them from
  [github.com/LANDIS-II-Foundation](https://github.com/LANDIS-II-Foundation).
* **Landscape inputs.** The example scenarios reference large Mount Rainier inputs (initial
  communities, soils, climate, ignition density maps) by path; the parameter files that
  configure EMBER are included, the landscape rasters are not.

## Status

The extension is implemented and tested, with a calibrated parameter set for Mount Rainier
National Park derived against the FPA-FOD, MTBS and GeoMAC records. The full-landscape
comparison against a Social Climate Fire run of the same landscape is in progress. Prescribed
fire, spotting, delayed mortality and hazard/replay modes are on the roadmap in
`docs/02_EMBER_Design.md`.
