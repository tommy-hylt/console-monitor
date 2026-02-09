# ConsoleMonitor / agent / JOB.md

This file teaches OpenClaw how to run ConsoleMonitor.

It includes:
- what ConsoleMonitor is
- procedures for the 30-minute cron job

## What ConsoleMonitor is

- Console state is stored in `scripts/consoles.json`.
- `scripts/read.js --status hanging` writes `scripts/hanging.json`.
- A console becomes `status: "hanging"` when the buffer hash has not changed for `HANG_MINUTES` (default 15).
- Status changes:
  - `ignore.js` => `ignored`
  - `handle.js` => `handled` (used to ack/not-spam)
  - `poke.js` => `poked` (used when we send a nudge)

## Procedures

### 0) Locate the project (no hardcoded path)

From the cron job, first locate this project folder by searching for `agent/JOB.md`.

PowerShell snippet:

```powershell
$job = Get-ChildItem -Path $env:USERPROFILE -Recurse -File -Filter JOB.md -ErrorAction SilentlyContinue |
  Where-Object { $_.FullName -like '*ConsoleMonitor*\\agent\\JOB.md' } |
  Select-Object -First 1
if (-not $job) { throw 'ConsoleMonitor not found (JOB.md missing)' }
$root = Split-Path -Parent (Split-Path -Parent $job.FullName)  # ...\ConsoleMonitor
$scripts = Join-Path $root 'scripts'
Set-Location -LiteralPath $scripts
```

### 1) Produce hanging list

```powershell
node read.js --status hanging
```

This produces: `hanging.json` in the scripts folder.

### 2) For each hanging console, decide AI vs non-AI

Inspect each entry:

- `pid`, `title`, `path`, and (sometimes) `text`.

Heuristics to classify as **AI / interactive**:

- Title contains: `claude`, `gemini`, `codex`, `openai`, `anthropic`.
- Process path contains: `claude`, `gemini`, `codex`, `openai`, `anthropic`.
- Buffer text contains obvious AI-agent prompts like: `Claude`, `Gemini`, `Codex`, `OpenAI`, `Anthropic`, `tool`, `function call`, etc.

If it **does NOT** look like an AI / interactive agent console:

```powershell
node ignore.js <pid>
```

If it **DOES** look like an AI / interactive agent console:

1) Send a Telegram notification (include `pid`, `title`, `path`, `time`, and a short excerpt if safe).
2) Mark handled (so we don’t spam):

```powershell
node handle.js <pid>
```

### Telegram alert format

Include:
- `pid`
- `title`
- `path`
- `time` (last-change time)
- `hash`
- optional short excerpt (first ~200–300 chars) if it doesn’t leak secrets

Note:
- The cron job does **not** poke automatically.
- Any later poke/ignore actions are handled manually outside this cron run.
