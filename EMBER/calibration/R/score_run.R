#!/usr/bin/env Rscript
# Score an EMBER run against SCF reference statistics (from scf_reference.R).
#
# Usage: Rscript score_run.R <ember_run_dir> <reference_dir> <out_dir> [label]
#
# Produces <out_dir>/score_<label>.csv (side-by-side statistics), fire size and annual
# area distribution plots, and behavior diagnostics (ROS, intensity, duration, end reasons).
suppressMessages({ library(data.table); library(ggplot2) })

args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 3) stop("usage: score_run.R <ember_run_dir> <reference_dir> <out_dir> [label]")
run <- args[1]; ref <- args[2]; out <- args[3]; label <- if (length(args) >= 4) args[4] else basename(run)
dir.create(out, showWarnings = FALSE, recursive = TRUE)

rd <- function(f) { d <- fread(f, strip.white = TRUE); d[, which(names(d) == "" | grepl("^V\\d+$", names(d))) := NULL]; d }
summ <- rd(file.path(run, "ember-summary-log.csv"))
ev <- rd(file.path(run, "ember-events-log.csv"))
dl <- rd(file.path(run, "ember-daily-log.csv"))
ig <- rd(file.path(run, "ember-ignitions-log.csv"))
ref_summary <- fread(file.path(ref, "scf_reference_summary.csv"))
ref_annual <- fread(file.path(ref, "scf_annual.csv"))
ref_events <- fread(file.path(ref, "scf_events.csv"))
active <- ref_summary[statistic == "active_cells", value]

q <- function(x, p) if (length(x)) as.numeric(quantile(x, p)) else NA_real_
ember <- data.table(
  statistic = c("years", "active_cells", "cells_per_year_mean", "cells_per_year_median", "cells_per_year_p90", "cells_per_year_max",
                "fraction_years_with_fire", "annual_fraction_burned", "rotation_years",
                "fires_per_year", "fire_size_median", "fire_size_p90", "fire_size_max", "fraction_fires_gt10", "fraction_fires_gt100"),
  value = c(nrow(summ), active, mean(summ$CellsBurned), median(summ$CellsBurned), q(summ$CellsBurned, .9), max(summ$CellsBurned),
            mean(summ$CellsBurned > 0), mean(summ$CellsBurned) / active, active / max(mean(summ$CellsBurned), 1e-9),
            nrow(ev) / nrow(summ), median(ev$Cells), q(ev$Cells, .9), max(ev$Cells, 0), mean(ev$Cells > 10), mean(ev$Cells > 100)))
score <- merge(ref_summary, ember, by = "statistic", suffixes = c("_scf", "_ember"), sort = FALSE)
score[, ratio := value_ember / value_scf]
score[, label := label]
fwrite(score, file.path(out, sprintf("score_%s.csv", label)))
cat("\n==", label, "vs SCF ==\n"); print(score[, .(statistic, scf = signif(value_scf, 4), ember = signif(value_ember, 4), ratio = signif(ratio, 3))])

# observed record (FPA-FOD / MTBS, landscape-wide) scaled to this run's active area where the
# statistic is extensive (counts per year); intensive statistics compare directly
obs_dir <- file.path(dirname(ref), "observed")
if (file.exists(file.path(obs_dir, "fpa_fod_summary.csv"))) {
  fpa <- fread(file.path(obs_dir, "fpa_fod_summary.csv"))
  mtbs <- if (file.exists(file.path(obs_dir, "mtbs_summary.csv"))) fread(file.path(obs_dir, "mtbs_summary.csv")) else NULL
  g <- function(d, s) d[statistic == s, value]
  share <- active / g(fpa, "active_cells")
  fires_yr <- nrow(ev) / nrow(summ)
  light_yr <- sum(ev$Cause == "Lightning") / nrow(summ)
  obs <- data.table(
    statistic = c("fires_per_year", "lightning_fires_per_year", "fire_size_median", "fire_size_p90", "fraction_fires_gt10", "fraction_fires_gt100",
                  "annual_fraction_burned_fpa", "annual_fraction_burned_mtbs", "ignition_doy_median", "ignition_doy_p10", "ignition_doy_p90",
                  "fraction_area_high_severity_mtbs"),
    observed = c(g(fpa, "fires_per_year") * share, g(fpa, "lightning_fires_per_year") * share, g(fpa, "fire_size_median"), g(fpa, "fire_size_p90"),
                 g(fpa, "fraction_fires_gt10"), g(fpa, "fraction_fires_gt100"), g(fpa, "annual_fraction_burned"),
                 if (!is.null(mtbs)) g(mtbs, "annual_fraction_burned") else NA, g(fpa, "discovery_doy_median"), g(fpa, "discovery_doy_p10"), g(fpa, "discovery_doy_p90"),
                 if (!is.null(mtbs)) g(mtbs, "fraction_area_high") else NA),
    ember = c(fires_yr, light_yr, median(ev$Cells), q(ev$Cells, .9), mean(ev$Cells > 10), mean(ev$Cells > 100),
              mean(summ$CellsBurned) / active, mean(summ$CellsBurned) / active, median(ev$IgnitionDay), q(ev$IgnitionDay, .1), q(ev$IgnitionDay, .9),
              if (nrow(ev)) weighted.mean(ev$FractionHighSeverity, ev$Cells) else NA))
  obs[, ratio := ember / observed]
  obs[, label := label]
  fwrite(obs, file.path(out, sprintf("score_observed_%s.csv", label)))
  cat("\n==", label, "vs observed record (FPA-FOD / MTBS; counts scaled by area share", signif(share, 3), ") ==\n")
  print(obs[, .(statistic, observed = signif(observed, 4), ember = signif(ember, 4), ratio = signif(ratio, 3))])
}

# behavior diagnostics (EMBER only)
diag <- data.table(
  statistic = c("ignition_attempts_per_year", "fires_per_attempt", "days_active_median", "days_active_p90", "days_active_max",
                "mean_ros_median_m_min", "max_ros_p90_m_min", "mean_intensity_median_kW_m", "max_intensity_p90_kW_m",
                "fraction_cells_passive_crown", "fraction_cells_active_crown", "canopy_mortality_mean", "cells_per_fire_day_median",
                "cells_per_fire_day_p90", "burn_hours_median", "ignition_fwi_median", "end_weather", "end_fuel_limited", "end_season_end"),
  value = c(sum(ig$LightningAttempts + ig$AccidentalAttempts) / nrow(summ), nrow(ev) / max(1, sum(ig$LightningAttempts + ig$AccidentalAttempts)),
            median(ev$DaysActive), q(ev$DaysActive, .9), max(ev$DaysActive, 0), median(ev$MeanRos), q(ev$MaxRos, .9), median(ev$MeanIntensity),
            q(ev$MaxIntensity, .9), weighted.mean(ev$FractionPassive, ev$Cells), weighted.mean(ev$FractionActive, ev$Cells),
            weighted.mean(ev$MeanCanopyMortality, ev$Cells), median(dl$CellsBurned), q(dl$CellsBurned, .9), median(dl$BurnHours),
            median(ev$IgnitionFWI), mean(ev$EndReason == "Weather"), mean(ev$EndReason == "FuelLimited"), mean(ev$EndReason == "SeasonEnd")))
diag[, label := label]
fwrite(diag, file.path(out, sprintf("diagnostics_%s.csv", label)))
cat("\n== behavior diagnostics ==\n"); print(diag[, .(statistic, value = signif(value, 4))])

# plots: fire size CDF and annual area distribution
sizes <- rbind(data.table(model = "SCF", cells = ref_events$cells), data.table(model = paste("EMBER", label), cells = ev$Cells))
p1 <- ggplot(sizes, aes(cells, colour = model)) + stat_ecdf(linewidth = 0.9) + scale_x_log10() +
  labs(x = "fire size (cells)", y = "cumulative fraction of fires", title = "Fire size distribution") + theme_minimal()
annual <- rbind(data.table(model = "SCF", cells = ref_annual$cells), data.table(model = paste("EMBER", label), cells = summ$CellsBurned))
p2 <- ggplot(annual, aes(cells + 1, colour = model)) + stat_ecdf(linewidth = 0.9) + scale_x_log10() +
  labs(x = "cells burned per year + 1", y = "cumulative fraction of years", title = "Annual area burned") + theme_minimal()
ggsave(file.path(out, sprintf("fire_size_cdf_%s.png", label)), p1, width = 6, height = 4, dpi = 120)
ggsave(file.path(out, sprintf("annual_area_cdf_%s.png", label)), p2, width = 6, height = 4, dpi = 120)
if (nrow(ev)) {
  p3 <- ggplot(ev, aes(IgnitionFWI, Cells)) + geom_point(alpha = .6) + scale_y_log10() +
    labs(x = "FWI at ignition", y = "fire size (cells)", title = "Fire size vs ignition FWI") + theme_minimal()
  ggsave(file.path(out, sprintf("size_vs_fwi_%s.png", label)), p3, width = 6, height = 4, dpi = 120)
}
cat("\nwrote", out, "\n")
