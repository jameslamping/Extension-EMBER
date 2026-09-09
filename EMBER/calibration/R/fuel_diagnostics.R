#!/usr/bin/env Rscript
# Fuel/canopy diagnostics for one run: distribution of CBH, CBD, canopy cover, fine fuel
# over the active cells, plus the critical surface intensity for crown-fire initiation
# and the 10-m wind needed for active crowning (Cruz 2005) at each cell.
# Usage: Rscript fuel_diagnostics.R <run_dir> <year>
suppressMessages({ library(terra); library(data.table) })
args <- commandArgs(trailingOnly = TRUE); run <- args[1]; yr <- as.integer(args[2])
r <- function(n) rast(file.path(run, "ember", sprintf("%s-%d.tif", n, yr)))
v <- function(n) as.vector(values(r(n)))
cbh <- v("canopy-base-height-dm") / 10; cbd <- v("canopy-bulk-density-x1000") / 1000
cc <- v("canopy-cover-pct"); cfl <- v("canopy-fuel-load-g-m2") / 1000
fine <- tryCatch(v("fuel-fine-g-m2"), error = function(e) rep(NA_real_, length(cbh)))
d <- data.table(cbh, cbd, cc, cfl, fine)[cfl > 0 | cc > 0]
q <- function(x) round(quantile(x, c(.1, .25, .5, .75, .9), na.rm = TRUE), 3)
cat("active cells with canopy:", nrow(d), "\n")
for (v in names(d)) { cat(sprintf("%-6s", v)); print(q(d[[v]])) }
# Van Wagner critical surface intensity at foliar moisture 100 %
d[, I0 := (0.010 * cbh * (460 + 25.9 * 100))^1.5]
# Cruz et al. 2005 active crown ROS (m/min): 11.02 U10^0.90 CBD^0.19 exp(-0.17 EFFM); critical R = 3/CBD
u <- seq(5, 80, 5)
need <- sapply(seq_len(nrow(d)), function(i) { R <- 11.02 * u^0.9 * d$cbd[i]^0.19 * exp(-0.17 * 8); u[which(R >= 3 / d$cbd[i])[1]] })
d[, u_active := need]
cat("critical surface intensity I0 (kW/m):"); print(q(d$I0))
cat("fraction of cells with I0 < 2000 kW/m:", round(mean(d$I0 < 2000), 3), "\n")
cat("10-m wind (km/h) needed for active crown fire at 8 % fine moisture:"); print(q(d$u_active))
cat("fraction with CBD >= 0.10:", round(mean(d$cbd >= 0.1), 3), "  >= 0.05:", round(mean(d$cbd >= 0.05), 3), "\n")
