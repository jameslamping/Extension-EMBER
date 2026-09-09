#!/usr/bin/env Rscript
# Ignition diagnostics for one EMBER run: attempts and fires by fire-danger bin and by season,
# and the daily fire behavior by FWI bin from the daily log. Use it to see where ignition
# attempts fail and how strongly spread responds to fire danger.
# Usage: Rscript ignition_diagnostics.R <run_dir> [label]
suppressMessages(library(data.table))
args <- commandArgs(trailingOnly = TRUE); run <- args[1]; label <- if (length(args) >= 2) args[2] else basename(run)
rd <- function(f) { d <- fread(f, strip.white = TRUE); d[, which(names(d) == "" | grepl("^V\\d+$", names(d))) := NULL]; d }
ig <- rd(file.path(run, "ember-ignitions-log.csv"))
ig[, att := LightningAttempts + AccidentalAttempts][, fires := LightningFires + AccidentalFires]
a <- ig[att > 0]
cat(sprintf("== %s: %d attempts, %d fires (%.2f per attempt)\n", label, sum(a$att), sum(a$fires), sum(a$fires) / sum(a$att)))
a[, fwi_bin := cut(FireDanger, c(-1, 5, 10, 15, 20, 30, 100))]
by_fwi <- a[, .(attempts = sum(att), fires = sum(fires), success = round(sum(fires) / sum(att), 2)), by = fwi_bin][order(fwi_bin)]
print(by_fwi)
a[, doy_bin := cut(Day, c(0, 150, 180, 210, 240, 270, 366))]
by_doy <- a[, .(attempts = sum(att), fires = sum(fires), success = round(sum(fires) / sum(att), 2)), by = doy_bin][order(doy_bin)]
print(by_doy)
d <- rd(file.path(run, "ember-daily-log.csv"))
d[, fwi_bin := cut(FWI, c(-1, 10, 15, 20, 25, 30, 40, 100))]
by_day <- d[, .(days = .N, burn_hours = round(mean(BurnHours), 1), cells_median = as.numeric(median(CellsBurned)),
                cells_p90 = as.numeric(quantile(CellsBurned, .9)), max_ros_median = round(median(MaxRos), 2),
                max_intensity_median = round(median(MaxIntensity)), max_intensity_p90 = round(quantile(MaxIntensity, .9))), by = fwi_bin][order(fwi_bin)]
cat("daily fire behavior by FWI bin:\n"); print(by_day)
ev <- rd(file.path(run, "ember-events-log.csv"))
if (nrow(ev)) { cat("end reasons:\n"); print(ev[, .N, by = EndReason]) ; cat("crown fire share of burned cells: passive", round(weighted.mean(ev$FractionPassive, ev$Cells), 3), "active", round(weighted.mean(ev$FractionActive, ev$Cells), 3), "\n") }
fwrite(by_fwi, file.path(run, sprintf("ignitions_by_fwi_%s.csv", label)))
fwrite(by_day, file.path(run, sprintf("daily_by_fwi_%s.csv", label)))
