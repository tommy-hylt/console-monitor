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

### 0) Set working directory (cron uses an absolute path)

In this environment, the cron job sets the working directory explicitly (absolute path is OK for cron on this machine):

```powershell
Set-Location -LiteralPath 'C:\\Users\\User\\Desktop\\260209 ConsoleMonitor\\console-monitor\\scripts'
```

Notes:
- The runbook lives at: `..\agent\JOB.md` (this file). Don’t try to read `scripts\agent\JOB.md`.
- Prefer PowerShell-only syntax in cron. Avoid `cmd`-style chains like `cd ... && ...`.

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

1) (Best-effort) Capture a console screenshot:

```powershell
node capture.js <pid> --title "<title>"
```

If it succeeds, attach the PNG in the Telegram notification.

2) Send a Telegram notification (include `pid`, `title`, `path`, `time`, and a short excerpt if safe).
3) Mark handled (so we don’t spam):

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
