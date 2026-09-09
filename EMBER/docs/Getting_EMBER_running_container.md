# Getting EMBER running in a container (full MORA landscape)

Same pattern as Getting_LANDIS_running_container: build an image, start a named
container with the scenario folder bind-mounted, run LANDIS-II inside it and watch
the terminal. The EMBER image is the working climatelibrary:v6 image plus the EMBER
DLL, so NECN, the Climate Library and the outputs behave exactly as in the SCF runs.

# 0) Prerequisites (already in place on this Mac)
#    - Docker Desktop / OrbStack running
#    - base image climatelibrary:v6 present:      docker images | grep climatelibrary
#    - .NET SDK (10.x) on the Mac for the DLL build:  dotnet --version
#    - EMBER source:  /Users/jlamping/Documents/Reenvisioned_LANDIS_Fire/EMBER

# 1) Build the image  (about 1 minute; repeat after any change to EMBER/src)
cd /Users/jlamping/Documents/Reenvisioned_LANDIS_Fire/EMBER
./docker/build.sh
#    -> builds Landis.Extension.EMBER-v1.dll locally, layers it onto climatelibrary:v6,
#       registers it with Landis.Extensions.dll and patches Landis.Console.deps.json.
#       Result: image landis-ember:dev
docker images | grep landis-ember

#    optional: freeze the image used for a set of runs
docker tag landis-ember:dev landis-ember:v1

# 2) Scenario folder
#    A clean full-landscape run folder has been created next to the SCF run:
#      /Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/EMBER_Rep1/
#        Scenario.txt            80 years, NECN + EMBER + Output Biomass Community
#        extensions/EMBER.txt    calibrated pass-6 parameter set (see EMBER/calibration/README.md)
#        extensions/ember_species.csv, ember_fuelbeds.csv, NECN_succession_V8.txt, ...
#    The large MORA inputs (initial communities, soils, climate, ignition maps) are NOT
#    copied: the files reference them as /mora/..., which is the SCF run folder
#    mounted read-only (step 3). Rep1 is never written to.
#
#    To make another replicate:
cp -R /Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/EMBER_Rep1 /Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/EMBER_Rep2
#    (optionally set a different RandomNumberSeed in its Scenario.txt; leave it commented
#     for an independent draw of climate years)

# 3) Create the instance
#### Mac Desk
docker run -it --platform linux/amd64 \
  --mount type=bind,src="/Users/jlamping/Desktop/LANDIS/",dst=/scenarioFolder \
  --mount type=bind,src="/Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/Rep1",dst=/mora,readonly \
  --name EMBER1 \
  landis-ember:dev

#    optional limits, so the SCF containers keep their share of the machine:
#      add   --cpus=6 --memory=24g   before --name

# If already created but stopped, restart and attach to the terminal
docker start -ai EMBER1

# 4) Run inside the container
bash
cd /scenarioFolder/EMBER/MORA/Hist/EMBER_Rep1
dotnet $LANDIS_CONSOLE Scenario.txt

# Run all replicates in a directory
cd /scenarioFolder/EMBER/MORA/Hist
for d in EMBER_Rep*/ ; do (cd "$d" && dotnet $LANDIS_CONSOLE Scenario.txt); done

# Detach without stopping the run:  Ctrl-p Ctrl-q      Re-attach:  docker attach EMBER1
# Run in the background instead of an attached terminal:
docker start EMBER1 && docker exec -d EMBER1 bash -c 'cd /scenarioFolder/EMBER/MORA/Hist/EMBER_Rep1 && dotnet $LANDIS_CONSOLE Scenario.txt > run.log 2>&1'
tail -f /Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/EMBER_Rep1/run.log

# 5) What to expect in the terminal
#    Each year prints the NECN lines you know, then two EMBER lines, e.g.
#      EMBER year 12: loading weather, building fuels ...
#      EMBER year 12: 41 fires, 1206 cells burned (976.9 ha), largest 612.4 ha, mean FWI 14.2.
#    NECN is the cost: about 20-25 minutes per year for 491k cells on this machine
#    (EMBER adds seconds), so 80 years is roughly 30 hours.

# 6) Outputs (in the scenario folder)
#    ember-summary-log.csv     one row per year
#    ember-events-log.csv      one row per fire: cause, dates, cells, ROS, intensity, crown fire, mortality, emissions
#    ember-daily-log.csv       one row per fire-day
#    ember-ignitions-log.csv   attempts and fires by day
#    ember/                    maps: severity, day-of-burn, event-id, intensity, flame length,
#                              crown-fire class, canopy mortality, consumption, PM2.5,
#                              time-since-fire, fuel maps every FuelMapFrequency years
#    Metadata/                 the usual LANDIS metadata

# 7) Comparing with the SCF run when it is done
cd /Users/jlamping/Documents/Reenvisioned_LANDIS_Fire/EMBER/testing
python3 compare_scf.py /Users/jlamping/Desktop/LANDIS/EMBER/MORA/Hist/EMBER_Rep1 --window 0 777 0 855 --active 491438
#    and the calibration scripts (observed record, scoring) in EMBER/calibration/R,
#    e.g.  Rscript R/score_run.R <run_dir> <reference_dir> <out_dir> <label>

# 8) Housekeeping
docker ps -a                              # containers
docker stop EMBER1 ; docker rm EMBER1     # remove the instance (outputs stay on the Mac)
docker rmi landis-ember:dev               # remove the image (rebuild with step 1)
