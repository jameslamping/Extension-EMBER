# MORA full-landscape EMBER run

Same inputs as the SCF-calibrated historical run (bind-mounted at /mora), with EMBER in
place of Social Climate Fire. 491k active cells; NECN needs ~22 min per year on this
machine, so 80 years is roughly 30 hours. Start it with `./run.sh` (set CPUS / MEM env
vars as needed), then compare with

    python3 ../compare_scf.py . --window 0 777 0 855 --active 491438

Both runs draw random climate years, so compare distributions, not years.
