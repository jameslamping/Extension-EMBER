#!/usr/bin/env Rscript
# Observed burned area and severity targets from MTBS annual dNBR mosaics for a LANDIS-II
# landscape. MTBS maps fires larger than ~400 ha in the western US, so these targets describe
# the large-fire regime; combine with FPA-FOD (fpa_fod_targets.R) for fire counts.
#
# Usage: Rscript mtbs_targets.R <mtbs_dir> <ecoregion_map.tif> <out_dir>
#
# dNBR thresholds follow the common MTBS convention: unburned/low < 100, low 100-269,
# moderate 270-439, high >= 440 (Key & Benson 2006 approximate breaks).
suppressMessages({ library(terra); library(data.table); library(ggplot2) })

args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 3) stop("usage: mtbs_targets.R <mtbs_dir> <ecoregion_map.tif> <out_dir>")
mdir <- args[1]; ecomap <- args[2]; out <- args[3]
dir.create(out, showWarnings = FALSE, recursive = TRUE)

eco <- rast(ecomap)
active <- eco > 0
active[!active] <- NA
n_active <- global(active, "sum", na.rm = TRUE)[[1]]
cell_ha <- prod(res(eco)) / 1e4

files <- sort(list.files(mdir, pattern = "dnbr\\.tif$", full.names = TRUE))
rows <- list()
for (f in files) {
  year <- as.integer(substr(basename(f), 12, 15))
  r <- rast(f)
  r <- project(r, eco, method = "near")
  r <- mask(r, active)
  v <- values(r); v <- v[!is.na(v)]
  burned <- sum(v >= 100)
  rows[[length(rows) + 1]] <- data.table(year = year, cells_burned = burned, cells_low = sum(v >= 100 & v < 270),
    cells_moderate = sum(v >= 270 & v < 440), cells_high = sum(v >= 440), mean_dnbr_burned = if (burned) mean(v[v >= 100]) else NA_real_)
  cat(sprintf("%d: %6d cells burned (%.0f ha), high %.2f\n", year, burned, burned * cell_ha, if (burned) sum(v >= 440) / burned else 0))
}
annual <- rbindlist(rows)
fwrite(annual, file.path(out, "mtbs_annual.csv"))
ny <- nrow(annual)
tot <- sum(annual$cells_burned)
summary <- data.table(
  statistic = c("years", "active_cells", "cells_per_year_mean", "cells_per_year_median", "cells_per_year_p90", "cells_per_year_max",
                "fraction_years_with_fire", "annual_fraction_burned", "rotation_years", "fraction_area_low", "fraction_area_moderate",
                "fraction_area_high", "mean_dnbr_burned"),
  value = c(ny, n_active, mean(annual$cells_burned), median(annual$cells_burned), as.numeric(quantile(annual$cells_burned, .9)), max(annual$cells_burned),
            mean(annual$cells_burned > 0), mean(annual$cells_burned) / n_active, n_active / max(mean(annual$cells_burned), 1e-9),
            sum(annual$cells_low) / max(tot, 1), sum(annual$cells_moderate) / max(tot, 1), sum(annual$cells_high) / max(tot, 1),
            weighted.mean(annual$mean_dnbr_burned, annual$cells_burned, na.rm = TRUE)))
fwrite(summary, file.path(out, "mtbs_summary.csv"))
print(summary)
p <- ggplot(annual, aes(year, cells_burned * cell_ha)) + geom_col() + labs(y = "MTBS burned area (ha)", title = "MTBS burned area on the landscape") + theme_minimal()
ggsave(file.path(out, "mtbs_annual_area.png"), p, width = 7, height = 3.5, dpi = 120)
cat("wrote", out, "\n")
