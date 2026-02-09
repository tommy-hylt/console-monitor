# ConsoleMonitor scripts

State is stored in `consoles.json` (in this folder).

## Prereqs

- `node` available (Node.js)
- `python` available (for `tools/poke/poke.py`)
- Python package: `pyautogui`
- Built tools present under `tools/`:
  - `tools/List/.../List.exe`
  - `tools/Sniffer/.../Sniffer.exe`
  - `tools/Foreground/.../Foreground.exe`
  - `tools/Screenshot/.../Screenshot.exe` (NEW)

## Files

- `refresh.js`
  - updates `consoles.json` by enumerating console-attached processes (via `tools/List`) and capturing buffers (via `tools/Sniffer`)
  - applies hanging detection
  - writes **only** `consoles.json` (quiet; no stdout summary)

- `read.js`
  - reads `consoles.json` and writes per-status JSON files: `(status).json`

- `poke.js`
  - sends text/keys to a target PID (Foreground + pyautogui)
  - optional precheck via `--hash`

- `handle.js`
  - marks an item as `handled`

- `ignore.js`
  - marks an item as `ignored`

- `capture.js` (NEW)
  - captures a screenshot PNG for a console PID into `scripts/captures/`

## State file

`consoles.json` structure:

```json
[{"pid":123,"title":"...","path":"...","text":"...","hash":"...","time":"...","status":"watching|hanging|handled|poked|ignored"}]
```

## Commands

Refresh:

```bat
cd /d <path-to-ConsoleMonitor>\scripts
node refresh.js
```

Read hanging (writes `hanging.json`):

```bat
node read.js --status hanging
```

Read handled + hanging (writes `handled.json` and `hanging.json`):

```bat
node read.js --status hanging --status handled
```

Note: `read.js` writes files; it does not print to stdout.

Poke (with optional hash precheck, repeated flags supported):

```bat
node poke.js <pid> --hash <sha1> --text "Abc" --keys "{ENTER}" --text "more" --keys "{TAB}"
```

Handle / Ignore:

```bat
node handle.js <pid>
node ignore.js <pid>
```
