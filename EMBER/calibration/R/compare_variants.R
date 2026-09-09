#!/usr/bin/env Rscript
# Collect the scores of all variant runs into one table and one figure.
#
# Usage: Rscript compare_variants.R <calibration_dir> [variant ...]
# Reads runs/<variant>/score_<variant>.csv, score_observed_<variant>.csv, diagnostics_<variant>.csv
# and the reference folders; writes summary/variants_table.csv and summary/fire_size_cdf.png.
suppressMessages({ library(data.table); library(ggplot2) })

args <- commandArgs(trailingOnly = TRUE)
cal <- if (length(args) >= 1) args[1] else "."
variants <- if (length(args) >= 2) args[-1] else basename(list.dirs(file.path(cal, "runs"), recursive = FALSE))
out <- file.path(cal, "summary"); dir.create(out, showWarnings = FALSE)

keep_scf <- c("fires_per_year", "cells_per_year_mean", "cells_per_year_max", "annual_fraction_burned", "rotation_years",
              "fire_size_median", "fire_size_p90", "fire_size_max", "fraction_fires_gt10", "fraction_fires_gt100")
keep_diag <- c("fires_per_attempt", "days_active_median", "days_active_p90", "mean_ros_median_m_min", "max_intensity_p90_kW_m",
               "fraction_cells_active_crown", "canopy_mortality_mean", "cells_per_fire_day_median")
keep_obs <- c("fires_per_year", "annual_fraction_burned_fpa", "annual_fraction_burned_mtbs", "fraction_fires_gt10", "fraction_fires_gt100",
              "fraction_area_high_severity_mtbs", "ignition_doy_median")

tab <- list(); sizes <- list()
for (v in variants) {
  d <- file.path(cal, "runs", v)
  sf <- file.path(d, sprintf("score_%s.csv", v)); if (!file.exists(sf)) next
  s <- fread(sf)[statistic %in% keep_scf, .(statistic, value = value_ember)]
  if (!exists("scf_col")) scf_col <- fread(sf)[statistic %in% keep_scf, .(statistic, value = value_scf)]
  o <- fread(file.path(d, sprintf("score_observed_%s.csv", v)))
  obs_col <- o[statistic %in% keep_obs, .(statistic = paste0("obs:", statistic), value = observed)]
  s <- rbind(s, o[statistic %in% keep_obs, .(statistic = paste0("obs:", statistic), value = ember)])
  g <- fread(file.path(d, sprintf("diagnostics_%s.csv", v)))[statistic %in% keep_diag, .(statistic = paste0("diag:", statistic), value)]
  s <- rbind(s, g); s[, variant := v]; tab[[v]] <- s
  ev <- fread(file.path(d, "ember-events-log.csv"), strip.white = TRUE)
  if (nrow(ev)) sizes[[v]] <- data.table(source = paste("EMBER", v), cells = ev$Cells)
}
if (!length(tab)) stop("no scored variants found")
long <- rbindlist(tab)
wide <- dcast(long, statistic ~ variant, value.var = "value")
refs <- rbind(scf_col[, .(statistic, SCF = value)], obs_col[, .(statistic, SCF = NA_real_)])
wide <- merge(refs, wide, by = "statistic", all = TRUE)
obs_vals <- rbindlist(lapply(variants, function(v) { f <- file.path(cal, "runs", v, sprintf("score_observed_%s.csv", v)); if (file.exists(f)) fread(f)[statistic %in% keep_obs, .(statistic = paste0("obs:", statistic), observed)] }))
obs_vals <- unique(obs_vals)
wide <- merge(wide, obs_vals, by = "statistic", all.x = TRUE)
setcolorder(wide, c("statistic", "observed", "SCF"))
wide <- wide[order(!grepl("^obs", statistic), !grepl("^diag", statistic), statistic)]
fwrite(wide, file.path(out, "variants_table.csv"))
print(wide, digits = 3)

ref_events <- fread(file.path(cal, "reference/window/scf_events.csv"))
fpa <- file.path(cal, "reference/observed/fpa_fod_fires.csv")
all <- rbind(data.table(source = "SCF (window)", cells = ref_events$cells), rbindlist(sizes))
if (file.exists(fpa)) all <- rbind(all, data.table(source = "FPA-FOD record", cells = fread(fpa)$size_cells))
p <- ggplot(all, aes(cells, colour = source)) + stat_ecdf(linewidth = 0.9) + scale_x_log10() +
  labs(x = "fire size (cells)", y = "cumulative fraction of fires", title = "Fire size distribution by variant") + theme_minimal()
ggsave(file.path(out, "fire_size_cdf.png"), p, width = 7, height = 4.5, dpi = 120)
cat("wrote", out, "\n")
