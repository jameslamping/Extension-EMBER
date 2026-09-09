#!/usr/bin/env Rscript
# Observed weather on the days of the large recorded fires versus the fire-season climate of
# the landscape, from a cache of GRIDMET daily point data (id, long, lat, date, tmax, tmin,
# rmin, rmax, ws [km/h], prec). Use it to decide whether the extreme fires of the record were
# wind- or drought-driven before choosing WindGustFactor and the burn-period parameters.
# Usage: Rscript fire_day_weather.R <gridmet_dir> <ecoregion_map.tif> <geomac_fires.csv> <out_dir> [first_year] [last_year]
suppressMessages({ library(data.table); library(terra) })
args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 4) stop("usage: fire_day_weather.R <gridmet_dir> <ecoregion_map.tif> <geomac_fires.csv> <out_dir> [first_year] [last_year]")
gdir <- args[1]; ecomap <- args[2]; fires_csv <- args[3]; out <- args[4]
y0 <- if (length(args) >= 5) as.integer(args[5]) else 1992; y1 <- if (length(args) >= 6) as.integer(args[6]) else 2021
dir.create(out, showWarnings = FALSE, recursive = TRUE)
w <- rast(ecomap); w[w == 0] <- NA
e <- as.vector(ext(project(trim(w), "EPSG:4326")))
files <- list.files(gdir, pattern = "\\.rds$", full.names = TRUE)
yr_of <- as.integer(sub(".*_(\\d{4})\\.rds$", "\\1", files))
files <- files[yr_of >= y0 & yr_of <= y1]
x <- rbindlist(lapply(files, function(f) as.data.table(readRDS(f))), fill = TRUE); x[, ws := as.numeric(ws)]
pts <- x[long >= e[1] - 0.02 & long <= e[2] + 0.02 & lat >= e[3] - 0.02 & lat <= e[4] + 0.02]
cat("GRIDMET points over the landscape:", uniqueN(pts$id), "\n")
d <- pts[, .(ws = mean(ws), rmin = mean(rmin), tmax = mean(tmax), prec = mean(prec)), by = date][order(date)]
s <- d[month(date) %in% 7:9]
clim <- data.table(statistic = c("ws_p50", "ws_p90", "ws_p99", "ws_max", "frac_days_ws_gt20", "frac_days_ws_gt30", "rmin_p10", "tmax_p90"),
                   value = c(quantile(s$ws, c(.5, .9, .99)), max(s$ws), mean(s$ws > 20), mean(s$ws > 30), quantile(s$rmin, .1), quantile(s$tmax, .9)))
cat("July-September daily climate over the landscape:\n"); print(clim, digits = 3)
fwrite(clim, file.path(out, "fire_season_climate.csv"))
f <- fread(fires_csv); f <- f[final_ha >= 50]
f[, first := as.Date(first)][, last := as.Date(last)]
rows <- lapply(seq_len(nrow(f)), function(i) {
  dd <- d[date >= f$first[i] - 3 & date <= f$last[i]]
  if (!nrow(dd)) return(NULL)
  data.table(fire = f$fire[i], first = f$first[i], last = f$last[i], final_ha = round(f$final_ha[i]),
             ws_max = round(max(dd$ws), 1), ws_p90 = round(quantile(dd$ws, .9), 1), rmin_min = round(min(dd$rmin)), tmax_max = round(max(dd$tmax), 1),
             dry_days = sum(dd$prec < 1), windy_days_gt20 = sum(dd$ws > 20))
})
fires <- rbindlist(rows)
cat("weather during the recorded fires (perimeter interval, from 3 days before the first perimeter):\n"); print(fires)
fwrite(fires, file.path(out, "fire_day_weather.csv"))
