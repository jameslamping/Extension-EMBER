#!/usr/bin/env Rscript
# Pool the logs of replicate runs of one variant (runs/<variant>_s<seed>/) into
# runs/<variant>_pooled/ so score_run.R can treat them as one long run. Years are offset
# so they stay unique; event IDs are offset likewise.
#
# Usage: Rscript pool_replicates.R <calibration_dir> <variant> <seed> [<seed> ...]
suppressMessages(library(data.table))
args <- commandArgs(trailingOnly = TRUE)
if (length(args) < 3) stop("usage: pool_replicates.R <calibration_dir> <variant> <seed> ...")
cal <- args[1]; variant <- args[2]; seeds <- args[-(1:2)]
out <- file.path(cal, "runs", paste0(variant, "_pooled")); dir.create(out, showWarnings = FALSE, recursive = TRUE)
rd <- function(f) { d <- fread(f, strip.white = TRUE); d[, which(names(d) == "" | grepl("^V\\d+$", names(d))) := NULL]; d }
logs <- c("ember-summary-log.csv", "ember-events-log.csv", "ember-daily-log.csv", "ember-ignitions-log.csv")
pooled <- setNames(vector("list", length(logs)), logs)
yoff <- 0; eoff <- 0
for (s in seeds) {
  d <- file.path(cal, "runs", sprintf("%s_s%s", variant, s))
  if (!file.exists(file.path(d, logs[1]))) { cat("missing", d, "\n"); next }
  summ <- rd(file.path(d, logs[1])); ny <- max(summ$SimulationYear)
  for (l in logs) {
    x <- rd(file.path(d, l))
    if ("SimulationYear" %in% names(x)) x[, SimulationYear := SimulationYear + yoff]
    if ("EventID" %in% names(x)) x[, EventID := EventID + eoff]
    x[, seed := s]
    pooled[[l]] <- rbind(pooled[[l]], x, fill = TRUE)
  }
  ev <- rd(file.path(d, logs[2]))
  yoff <- yoff + ny; eoff <- eoff + (if (nrow(ev)) max(ev$EventID) else 0)
  cat(sprintf("pooled %s: %d years, %d fires\n", d, ny, nrow(ev)))
}
for (l in logs) fwrite(pooled[[l]], file.path(out, l))
cat("wrote", out, "with", yoff, "years\n")
