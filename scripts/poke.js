#!/usr/bin/env node
// poke.js <pid> [--hash <sha1>] [--text "..."] [--keys "..."] ...
// Sends text and/or key presses to a console window.
// Supports repeated --text/--keys flags (order-preserving).
//
// Precheck: if --hash is provided, re-sniff and abort if it doesn't match.

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');

const PATH_CONSOLE = path.join(__dirname, 'consoles.json');

const TOOLS_DIR = path.join(__dirname, 'tools');
const FOREGROUND_EXE = path.join(TOOLS_DIR, 'Foreground', 'bin', 'Release', 'net8.0-windows', 'Foreground.exe');
const SNIFFER_EXE = path.join(TOOLS_DIR, 'Sniffer', 'bin', 'Release', 'net8.0-windows', 'Sniffer.exe');
const PY_POKE = path.join(TOOLS_DIR, 'poke', 'poke.py');

function execCapture(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, {
    encoding: 'utf8',
    windowsHide: true,
    ...opts,
  });
  return {
    code: r.status ?? 0,
    stdout: (r.stdout || '').trimEnd(),
    stderr: (r.stderr || '').trimEnd(),
  };
}

function sha1(text) {
  return crypto.createHash('sha1').update(text || '', 'utf8').digest('hex');
}

function readJson(filePath, fallback) {
  try {
    if (!fs.existsSync(filePath)) return fallback;
    const raw = fs.readFileSync(filePath, 'utf8');
    if (!raw.trim()) return fallback;
    return JSON.parse(raw);
  } catch {
    return fallback;
  }
}

function writeJson(filePath, value) {
  fs.writeFileSync(filePath, JSON.stringify(value, null, 2), 'utf8');
}

function sniffText(pid) {
  const outDir = path.join(__dirname, 'captures');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, `pid-${pid}.txt`);
  execCapture(SNIFFER_EXE, [String(pid), outPath]);
  let text = '';
  try { text = fs.readFileSync(outPath, 'utf8'); } catch { text = ''; }
  return { outPath, text, hash: sha1(text) };
}

function parseArgs(argv) {
  const out = { pid: null, actions: [], hash: null };
  const args = argv.slice(2);
  out.pid = Number(args.shift());

  while (args.length) {
    const a = args.shift();

    // Defensive: we've seen "{ENTER}" get mangled into a stray -encodedCommand token.
    if (a === '-encodedCommand') {
      out.actions.push({ kind: 'keys', value: '{ENTER}' });
      continue;
    }

    if (a === '--text') {
      out.actions.push({ kind: 'text', value: args.shift() ?? '' });
      continue;
    }

    if (a === '--keys') {
      out.actions.push({ kind: 'keys', value: args.shift() ?? '' });
      continue;
    }

    if (a === '--hash') {
      out.hash = (args.shift() ?? '').trim() || null;
      continue;
    }
  }

  return out;
}

const { pid, actions, hash } = parseArgs(process.argv);
if (!pid) {
  console.error('Usage: node poke.js <pid> [--hash <sha1>] [--text "..."] [--keys "..."] ...');
  process.exit(2);
}

if (hash) {
  const cur = sniffText(pid);
  if (cur.hash !== hash) {
    console.error(JSON.stringify({ ok: false, pid, error: 'HASH_MISMATCH', expected: hash, actual: cur.hash, outPath: cur.outPath }));
    process.exit(3);
  }
}

const fg = execCapture(FOREGROUND_EXE, [String(pid)]);

const pyArgs = [PY_POKE];
for (const a of actions) {
  if (a.kind === 'text') pyArgs.push('--text', a.value);
  if (a.kind === 'keys') pyArgs.push('--keys', a.value);
}
const pyRes = execCapture('python', pyArgs);

// Update status in consoles.json (best-effort)
const items = readJson(PATH_CONSOLE, []);
let found = false;
for (const it of items) {
  if (it.pid === pid) {
    it.status = 'poked';
    found = true;
  }
}
writeJson(PATH_CONSOLE, items);

console.log(JSON.stringify({ ok: true, pid, found, status: 'poked', res: { fg, pyRes } }));
