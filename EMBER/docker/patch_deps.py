#!/usr/bin/env python3
"""Add an app-local assembly to a .NET deps.json so the default load context can resolve it.
Usage: patch_deps.py <deps.json> <AssemblyName> <version>"""
import json, sys

path, name, version = sys.argv[1], sys.argv[2], sys.argv[3]
with open(path) as f:
    d = json.load(f)
key = f"{name}/{version}"
for target, libs in d["targets"].items():
    libs[key] = {"runtime": {f"{name}.dll": {"assemblyVersion": version, "fileVersion": version}}}
    for k, v in libs.items():
        if k.startswith("Landis.Console/"):
            v.setdefault("dependencies", {})[name] = version
d["libraries"][key] = {"type": "reference", "serviceable": False, "sha512": ""}
with open(path, "w") as f:
    json.dump(d, f, indent=2)
print(f"added {key} to {path}")
