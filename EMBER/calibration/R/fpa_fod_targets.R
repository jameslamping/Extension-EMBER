#!/usr/bin/env Rscript
# Observed fire-regime targets for a LANDIS-II landscape from the FPA-FOD wildfire record
# (Short 2022, 1992-2020): fires per year by cause, fire size distribution, seasonality.
#
# Usage: Rscript fpa_fod_targets.R <fpa_fod.gdb> <ecoregion_map.tif> <out_dir> [cell_length_m] [scf_run_dir]
#
# The landscape is the set of active cells (map value > 0) of the LANDIS-II ecoregion map;
# fires whose reported location falls on an active cell are counted. Sizes are converted
# from acres to cells so they compare with model output. If an SCF run directory is given,
# its events log is summarized alongside.
suppressMessages({ library(sf); library(terra); library(data.table); library(ggplot2) })

args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 3) stop("usage: fpa_fod_targets.R <fpa_fod.gdb> <ecoregion_map.tif> <out_dir> [cell_length_m] [scf_run_dir]")
gdb <- args[1]; ecomap <- args[2]; out <- args[3]
cell_m <- if (length(args) >= 4) as.numeric(args[4]) else 90
scf <- if (length(args) >= 5) args[5] else NA
dir.create(out, showWarnings = FALSE, recursive = TRUE)
cell_ha <- cell_m^2 / 1e4

eco <- rast(ecomap)
active_cells <- global(eco > 0, "sum", na.rm = TRUE)[[1]]
bb <- st_bbox(st_transform(st_as_sfc(st_bbox(eco)), 4269))
wkt <- st_as_text(st_as_sfc(bb))

cat("reading FPA-FOD points inside the landscape bounding box ...\n")
fires <- st_read(gdb, layer = "Fires", wkt_filter = wkt, quiet = TRUE)
fires <- st_transform(fires, crs(eco))
v <- terra::extract(eco, vect(fires))[, 2]
fires <- fires[!is.na(v) & v > 0, ]
d <- as.data.table(st_drop_geometry(fires))
setnames(d, tolower(names(d)))
d[, year := fire_year]
d[, size_ha := fire_size * 0.404686]
d[, size_cells := pmax(1, round(size_ha / cell_ha))]
d[, cause := fifelse(nwcg_general_cause == "Natural", "Lightning", "Human")]
d[, doy := discovery_doy]
years <- sort(unique(d$year)); ny <- diff(range(years)) + 1
cat(sprintf("%d fires on %d active cells (%.0f ha) over %d years (%d-%d)\n", nrow(d), active_cells, active_cells * cell_ha, ny, min(years), max(years)))

q <- function(x, p) as.numeric(quantile(x, p))
per_year <- d[, .(fires = .N, lightning = sum(cause == "Lightning"), human = sum(cause == "Human"),
                  cells = sum(size_cells), largest = max(size_cells)), by = year]
allyears <- data.table(year = min(years):max(years))
per_year <- merge(allyears, per_year, by = "year", all.x = TRUE)
for (c in c("fires", "lightning", "human", "cells", "largest")) per_year[is.na(get(c)), (c) := 0L]
fwrite(per_year, file.path(out, "fpa_fod_annual.csv"))
fwrite(d[, .(year, doy, cause, size_ha, size_cells, fire_name, nwcg_general_cause)], file.path(out, "fpa_fod_fires.csv"))

summary <- data.table(
  statistic = c("years", "active_cells", "fires_per_year", "lightning_fires_per_year", "human_fires_per_year",
                "cells_per_year_mean", "cells_per_year_median", "cells_per_year_p90", "cells_per_year_max",
                "annual_fraction_burned", "rotation_years", "fire_size_median", "fire_size_p90", "fire_size_max",
                "fraction_fires_gt10", "fraction_fires_gt100", "fraction_area_in_fires_gt100",
                "discovery_doy_p10", "discovery_doy_median", "discovery_doy_p90"),
  value = c(ny, active_cells, nrow(d) / ny, sum(d$cause == "Lightning") / ny, sum(d$cause == "Human") / ny,
            mean(per_year$cells), median(per_year$cells), q(per_year$cells, .9), max(per_year$cells),
            mean(per_year$cells) / active_cells, active_cells / max(mean(per_year$cells), 1e-9),
            median(d$size_cells), q(d$size_cells, .9), max(d$size_cells),
            mean(d$size_cells > 10), mean(d$size_cells > 100), sum(d$size_cells[d$size_cells > 100]) / sum(d$size_cells),
            q(d$doy, .1), median(d$doy), q(d$doy, .9)))
fwrite(summary, file.path(out, "fpa_fod_summary.csv"))
print(summary)

# optional: SCF events for comparison
if (!is.na(scf) && file.exists(file.path(scf, "socialclimatefire-events-log.csv"))) {
  ev <- fread(file.path(scf, "socialclimatefire-events-log.csv"), strip.white = TRUE)
  ev <- ev[, .(year = SimulationYear, size_cells = TotalSitesBurned, doy = InitialDayOfYear, cause = fifelse(IgnitionType == "Lightning", "Lightning", "Human"))]
  ny_scf <- max(ev$year)
  scf_summary <- data.table(statistic = summary$statistic, value = c(ny_scf, active_cells, nrow(ev) / ny_scf, sum(ev$cause == "Lightning") / ny_scf,
    sum(ev$cause == "Human") / ny_scf, NA, NA, NA, NA, NA, NA, median(ev$size_cells), q(ev$size_cells, .9), max(ev$size_cells),
    mean(ev$size_cells > 10), mean(ev$size_cells > 100), sum(ev$size_cells[ev$size_cells > 100]) / sum(ev$size_cells),
    q(ev$doy, .1), median(ev$doy), q(ev$doy, .9)))
  cmp <- merge(summary, scf_summary, by = "statistic", suffixes = c("_observed", "_scf"), sort = FALSE)
  fwrite(cmp, file.path(out, "fpa_fod_vs_scf.csv"))
  cat("\n== observed vs SCF ==\n"); print(cmp)
  sizes <- rbind(data.table(source = "FPA-FOD", cells = d$size_cells), data.table(source = "SCF run", cells = ev$size_cells))
  p <- ggplot(sizes, aes(cells, colour = source)) + stat_ecdf(linewidth = 0.9) + scale_x_log10() +
    labs(x = "fire size (cells)", y = "cumulative fraction of fires", title = "Fire size: FPA-FOD record vs SCF") + theme_minimal()
  ggsave(file.path(out, "fire_size_fpa_vs_scf.png"), p, width = 6, height = 4, dpi = 120)
  season <- rbind(data.table(source = "FPA-FOD", doy = d$doy), data.table(source = "SCF run", doy = ev$doy))
  p2 <- ggplot(season, aes(doy, colour = source)) + geom_density(linewidth = 0.9) + labs(x = "day of year of ignition", title = "Fire seasonality") + theme_minimal()
  ggsave(file.path(out, "seasonality_fpa_vs_scf.png"), p2, width = 6, height = 4, dpi = 120)
}
cat("wrote", out, "\n")
