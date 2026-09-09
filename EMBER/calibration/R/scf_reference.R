#!/usr/bin/env Rscript
# Reference statistics from a Social Climate Fire (SCF) run, for comparison with EMBER.
#
# Usage: Rscript scf_reference.R <scf_run_dir> <out_dir> [r0 r1 c0 c1]
#   r0 r1 c0 c1  optional 0-based row/column window (end exclusive) to restrict the
#                statistics to a sub-landscape; default is the whole map.
#
# Writes <out_dir>/scf_annual.csv  (cells burned per year inside the window)
#        <out_dir>/scf_events.csv  (fire pieces inside the window: event id, year, cells)
#        <out_dir>/scf_reference_summary.csv
suppressMessages({ library(terra); library(data.table) })

args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 2) stop("usage: scf_reference.R <scf_run_dir> <out_dir> [r0 r1 c0 c1]")
scf <- args[1]; out <- args[2]
dir.create(out, showWarnings = FALSE, recursive = TRUE)
win <- if (length(args) >= 6) as.integer(args[3:6]) else NULL

years <- integer(0)
y <- 1
while (file.exists(file.path(scf, sprintf("social-climate-fire/fire-severity-%d.tif", y)))) { years <- c(years, y); y <- y + 1 }
if (!length(years)) stop("no SCF severity maps found")

clip <- function(r) {
  if (is.null(win)) return(r)
  # terra rows/cols are 1-based; window is 0-based, end exclusive
  r[(win[1] + 1):win[2], (win[3] + 1):win[4], drop = FALSE]
}

annual <- data.table(); events <- data.table()
for (y in years) {
  sev <- clip(as.matrix(rast(file.path(scf, sprintf("social-climate-fire/fire-severity-%d.tif", y))), wide = TRUE))
  eid <- clip(as.matrix(rast(file.path(scf, sprintf("social-climate-fire/event-ID-%d.tif", y))), wide = TRUE))
  burned <- sum(sev > 1, na.rm = TRUE)
  annual <- rbind(annual, data.table(year = y, cells = burned))
  ids <- eid[!is.na(eid) & eid > 0]
  if (length(ids)) {
    tb <- as.data.table(table(ids))
    events <- rbind(events, data.table(year = y, event = as.integer(as.character(tb$ids)), cells = tb$N))
  }
}
active <- sum(clip(as.matrix(rast(file.path(scf, sprintf("social-climate-fire/fire-severity-%d.tif", years[1]))), wide = TRUE)) >= 1, na.rm = TRUE)

fwrite(annual, file.path(out, "scf_annual.csv"))
fwrite(events, file.path(out, "scf_events.csv"))

q <- function(x, p) if (length(x)) as.numeric(quantile(x, p)) else NA_real_
summary <- data.table(
  statistic = c("years", "active_cells", "cells_per_year_mean", "cells_per_year_median", "cells_per_year_p90", "cells_per_year_max",
                "fraction_years_with_fire", "annual_fraction_burned", "rotation_years",
                "fires_per_year", "fire_size_median", "fire_size_p90", "fire_size_max", "fraction_fires_gt10", "fraction_fires_gt100"),
  value = c(length(years), active, mean(annual$cells), median(annual$cells), q(annual$cells, .9), max(annual$cells),
            mean(annual$cells > 0), mean(annual$cells) / active, active / max(mean(annual$cells), 1e-9),
            nrow(events) / length(years), median(events$cells), q(events$cells, .9), max(events$cells, 0),
            mean(events$cells > 10), mean(events$cells > 100)))
fwrite(summary, file.path(out, "scf_reference_summary.csv"))
print(summary)
