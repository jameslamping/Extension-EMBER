#!/usr/bin/env python3
"""Compare an EMBER test run against the SCF-calibrated MORA run inside the same window.

Usage: compare_scf.py <ember_run_dir> [--scf <scf_run_dir>] [--window r0 r1 c0 c1]

Reports annual area burned, fire sizes, durations, intensity/severity summaries for EMBER,
and the SCF statistics for the same window over its full run (the two runs use different
random weather years, so compare distributions, not year by year).
"""
import argparse, csv, os, sys
import numpy as np
from osgeo import gdal

gdal.UseExceptions()


def read(path):
    ds = gdal.Open(path)
    a = ds.GetRasterBand(1).ReadAsArray()
    ds = None
    return a


def scf_window_stats(scf, r0, r1, c0, c1):
    years = []
    y = 1
    while os.path.exists(os.path.join(scf, f"social-climate-fire/fire-severity-{y}.tif")):
        years.append(y)
        y += 1
    cells, sizes = [], []
    for y in years:
        a = read(os.path.join(scf, f"social-climate-fire/fire-severity-{y}.tif"))[r0:r1, c0:c1]
        cells.append(int((a > 1).sum()))
        e = read(os.path.join(scf, f"social-climate-fire/event-ID-{y}.tif"))[r0:r1, c0:c1]
        _, cnt = np.unique(e[e > 0], return_counts=True)
        sizes.extend(cnt.tolist())
    return np.array(cells), np.array(sizes), len(years)


def ember_stats(run):
    summ = list(csv.DictReader(open(os.path.join(run, "ember-summary-log.csv"))))
    ev = list(csv.DictReader(open(os.path.join(run, "ember-events-log.csv"))))
    cells = np.array([int(r["CellsBurned"]) for r in summ])
    sizes = np.array([int(r["Cells"]) for r in ev]) if ev else np.array([0])
    days = np.array([int(r["DaysActive"]) for r in ev]) if ev else np.array([0])
    inten = np.array([float(r["MeanIntensity"]) for r in ev]) if ev else np.array([0.0])
    crown = np.array([float(r["FractionPassive"]) + float(r["FractionActive"]) for r in ev]) if ev else np.array([0.0])
    mort = np.array([float(r["MeanCanopyMortality"]) for r in ev]) if ev else np.array([0.0])
    fwi = np.array([float(r["MeanFWI"]) for r in summ])
    return cells, sizes, days, inten, crown, mort, fwi, len(summ)


def q(a, p):
    return np.percentile(a, p) if len(a) else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run")
    ap.add_argument("--scf", default="/Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/Rep1")
    ap.add_argument("--window", nargs=4, type=int, default=[320, 440, 320, 440])
    ap.add_argument("--active", type=int, default=9419)
    a = ap.parse_args()
    r0, r1, c0, c1 = a.window

    scells, ssizes, sy = scf_window_stats(a.scf, r0, r1, c0, c1)
    ecells, esizes, edays, einten, ecrown, emort, efwi, ey = ember_stats(a.run)

    print(f"{'':34s}{'SCF (window, %d yr)' % sy:>22s}{'EMBER (%d yr)' % ey:>16s}")
    rows = [
        ("cells burned / yr, mean", scells.mean(), ecells.mean()),
        ("cells burned / yr, median", np.median(scells), np.median(ecells)),
        ("cells burned / yr, p90", q(scells, 90), q(ecells, 90)),
        ("cells burned / yr, max", scells.max(), ecells.max()),
        ("fraction of years with fire", (scells > 0).mean(), (ecells > 0).mean()),
        ("annual fraction burned", scells.mean() / a.active, ecells.mean() / a.active),
        ("fire size median (cells)", np.median(ssizes) if len(ssizes) else 0, np.median(esizes)),
        ("fire size p90 (cells)", q(ssizes, 90), q(esizes, 90)),
        ("fire size max (cells)", ssizes.max() if len(ssizes) else 0, esizes.max()),
        ("fires / yr", len(ssizes) / sy, len(esizes) / ey),
    ]
    for name, s, e in rows:
        print(f"{name:34s}{s:22.3f}{e:16.3f}")
    print()
    print("EMBER only: fire days median %.0f max %.0f; mean intensity median %.0f kW/m p90 %.0f; "
          "crown-fire fraction mean %.2f; canopy mortality mean %.2f; mean fire-season FWI %.1f"
          % (np.median(edays), edays.max(), np.median(einten), q(einten, 90), ecrown.mean(), emort.mean(), efwi.mean()))
    print("EMBER cells burned per year:", ecells.tolist())
    print("SCF window cells burned per year (first 20):", scells[:20].tolist())


if __name__ == "__main__":
    main()
