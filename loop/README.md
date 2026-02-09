# ConsoleMonitor / loop

This folder contains the optional loop runner.

- `loop.js` runs `..\scripts\refresh.js` every 10 minutes.

## Run

Recommended: run the top-level helper cmd:

```bat
cd /d <path-to-ConsoleMonitor>
console-monitor-loop.cmd
```

Or directly:

```bat
cd /d <path-to-ConsoleMonitor>\loop
node loop.js
```

## Interval

Fixed at 10 minutes (not configurable).
