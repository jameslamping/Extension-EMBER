#!/usr/bin/env Rscript
# Observed daily fire growth from GeoMAC historic perimeters (2000-2018) for fires that touch a
# LANDIS-II landscape: per fire, area inside the landscape at each perimeter date, daily growth
# increments, days between first and last perimeter. These are the targets for the burn-period
# and spread parameters (compare with ember-daily-log.csv).
#
# Usage: Rscript geomac_targets.R <geomac.gdb> <ecoregion_map.tif> <out_dir> [cell_length_m]
suppressMessages({ library(sf); library(terra); library(data.table) })
sf_use_s2(FALSE)

args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 3) stop("usage: geomac_targets.R <geomac.gdb> <ecoregion_map.tif> <out_dir> [cell_length_m]")
gdb <- args[1]; ecomap <- args[2]; out <- args[3]
cell_m <- if (length(args) >= 4) as.numeric(args[4]) else 90
dir.create(out, showWarnings = FALSE, recursive = TRUE)
cell_ha <- cell_m^2 / 1e4

eco <- rast(ecomap)
landscape <- as.polygons(eco > 0, dissolve = TRUE)
landscape <- st_as_sf(landscape[landscape[[1]] == 1, ])
landscape <- st_make_valid(landscape)
bb <- st_bbox(st_transform(landscape, 4269))
wkt <- st_as_text(st_as_sfc(bb))

layers <- st_layers(gdb)$name
layers <- layers[grepl("PERIM", layers)]
pick <- function(nms, patterns) { for (p in patterns) { h <- nms[grepl(p, nms, ignore.case = TRUE)]; if (length(h)) return(h[1]) }; NA }

rows <- list()
for (ly in layers) {
  year <- as.integer(regmatches(ly, regexpr("[0-9]{4}", ly)))
  p <- tryCatch(st_read(gdb, layer = ly, wkt_filter = wkt, quiet = TRUE), error = function(e) NULL)
  if (is.null(p) || nrow(p) == 0) next
  nm <- names(p)
  namecol <- pick(nm, c("^fire_?name$", "incident", "event_name", "^firename$", "name"))
  datecol <- pick(nm, c("perim_?date", "^date_?$", "repdate", "c_date", "date_uploa", "datecurren", "date"))
  if (is.na(namecol) || is.na(datecol)) { cat("skip", ly, ": no name/date column\n"); next }
  p <- st_transform(st_make_valid(p), st_crs(landscape))
  p <- suppressWarnings(st_intersection(p, landscape))
  if (nrow(p) == 0) next
  p$area_ha <- as.numeric(st_area(p)) / 1e4
  d <- as.data.table(st_drop_geometry(p))
  d[, fire := toupper(trimws(as.character(get(namecol))))]
  d[, date := as.Date(substr(as.character(get(datecol)), 1, 10))]
  d <- d[!is.na(date) & !is.na(fire) & area_ha > 0, .(area_ha = max(area_ha)), by = .(fire, date)]
  d[, year := year]
  rows[[length(rows) + 1]] <- d
  cat(sprintf("%d: %d perimeter-dates from %d fires inside the landscape\n", year, nrow(d), uniqueN(d$fire)))
}
perims <- rbindlist(rows)
if (!nrow(perims)) stop("no GeoMAC perimeters intersect the landscape")
setorder(perims, year, fire, date)
perims[, area_cum := cummax(area_ha), by = .(year, fire)]
perims[, growth_ha := c(area_cum[1], diff(area_cum)), by = .(year, fire)]
perims[, days_since_prev := c(NA, as.integer(diff(date))), by = .(year, fire)]
perims[, growth_ha_per_day := growth_ha / pmax(1, fifelse(is.na(days_since_prev), 1L, days_since_prev))]
fwrite(perims, file.path(out, "geomac_perimeters.csv"))

fires <- perims[, .(first = min(date), last = max(date), n_perims = .N, final_ha = max(area_cum),
                    final_cells = max(area_cum) / cell_ha, max_daily_growth_ha = max(growth_ha_per_day),
                    max_daily_growth_cells = max(growth_ha_per_day) / cell_ha, days_span = as.integer(max(date) - min(date)) + 1), by = .(year, fire)]
fwrite(fires, file.path(out, "geomac_fires.csv"))
print(fires)
q <- function(x, p) as.numeric(quantile(x, p, na.rm = TRUE))
summary <- data.table(
  statistic = c("fires_with_perimeters", "final_ha_median", "final_ha_max", "days_span_median", "days_span_max",
                "max_daily_growth_ha_median", "max_daily_growth_ha_p90", "max_daily_growth_ha_max",
                "max_daily_growth_cells_median", "max_daily_growth_cells_max"),
  value = c(nrow(fires), median(fires$final_ha), max(fires$final_ha), median(fires$days_span), max(fires$days_span),
            median(fires$max_daily_growth_ha), q(fires$max_daily_growth_ha, .9), max(fires$max_daily_growth_ha),
            median(fires$max_daily_growth_cells), max(fires$max_daily_growth_cells)))
fwrite(summary, file.path(out, "geomac_summary.csv"))
print(summary)
cat("wrote", out, "\n")
