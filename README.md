# console-monitor

Windows ConsoleMonitor toolset.

## Folders

- `scripts/` — main Node scripts (see `scripts/README.md`)
- `loop/` — optional refresh loop runner
- `agent/` — OpenClaw runbooks (cron procedure, setup)

## Notes

This repo intentionally does **not** commit runtime state/output files (e.g. `scripts/consoles.json`, `scripts/hanging.json`, captures, or build `bin/obj`).
