#!/usr/bin/env node
// refresh.js
// - Enumerate console-attached processes (List.exe -> scripts/list.json)
// - Snapshot their buffers (Sniffer.exe)
// - Track change hashes + timestamps
// - Mark as hanging if unchanged for >= 15 minutes
//
// Writes state to: scripts/consoles.json

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');

const PATH_CONSOLE = path.join(__dirname, 'consoles.json');

const TOOLS_DIR = path.join(__dirname, 'tools');
const LIST_EXE = path.join(TOOLS_DIR, 'List', 'bin', 'Release', 'net8.0-windows', 'List.exe');
const SNIFFER_EXE = path.join(TOOLS_DIR, 'Sniffer', 'bin', 'Release', 'net8.0-windows', 'Sniffer.exe');

const LIST_JSON = path.join(__dirname, 'list.json');
const HANG_MINUTES = Number(process.env.HANG_MINUTES || 15);

function nowIso() {
  return new Date().toISOString();
}

function sha1(text) {
  return crypto.createHash('sha1').update(text || '', 'utf8').digest('hex');
}

function minutesBetween(isoA, isoB) {
  const a = new Date(isoA).getTime();
  const b = new Date(isoB).getTime();
  return (b - a) / 60000;
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

function refreshListJson() {
  const r = execCapture(LIST_EXE, [LIST_JSON]);
  if (r.code !== 0) throw new Error(`List.exe failed: ${r.stderr || r.stdout}`);
}

function captureConsoleText(pid) {
  const outDir = path.join(__dirname, 'captures');
  fs.mkdirSync(outDir, { recursive: true });
  const outPath = path.join(outDir, `pid-${pid}.txt`);
  execCapture(SNIFFER_EXE, [String(pid), outPath]);
  let text = '';
  try { text = fs.readFileSync(outPath, 'utf8'); } catch { text = ''; }
  return { outPath, text };
}

function main() {
  const now = nowIso();
  const prev = readJson(PATH_CONSOLE, []);
  const prevByPid = new Map(prev.map((x) => [x.pid, x]));

  refreshListJson();
  const consoles = JSON.parse(fs.readFileSync(LIST_JSON, 'utf8'));

  const next = [];
  for (const c of consoles) {
    const pid = Number(c.pid);
    const title = c.title || '';
    const procPath = c.path || '';

    const prevItem = prevByPid.get(pid);
    const prevStatus = prevItem?.status;

    // If ignored, don't recapture; carry forward previous snapshot.
    if (prevItem && prevStatus === 'ignored') {
      next.push({
        pid,
        title,
        path: procPath,
        text: prevItem.text || '',
        hash: prevItem.hash || sha1(prevItem.text || ''),
        time: prevItem.time || now,
        status: 'ignored',
      });
      continue;
    }

    const cap = captureConsoleText(pid);
    const text = cap.text || '';
    const hash = sha1(text);

    let time = prevItem?.time || now;
    let status = prevStatus || 'watching';

    if (prevItem) {
      if (prevItem.hash !== hash) {
        time = now;
        status = 'watching';
      } else {
        const mins = minutesBetween(time, now);
        if (mins >= HANG_MINUTES) {
          if (status === 'handled' || status === 'poked') {
            // keep as-is
          } else {
            status = 'hanging';
          }
        }
      }
    }

    next.push({ pid, title, path: procPath, text, hash, time, status });
  }

  next.sort((a, b) => {
    const pri = (s) => (s === 'hanging' ? 0 : s === 'handled' ? 1 : s === 'poked' ? 2 : s === 'watching' ? 3 : 4);
    const d = pri(a.status) - pri(b.status);
    if (d !== 0) return d;
    const t = (a.title || '').localeCompare(b.title || '');
    if (t !== 0) return t;
    return a.pid - b.pid;
  });

  writeJson(PATH_CONSOLE, next);

  // refresh.js is intentionally quiet: it only updates consoles.json.
  // Per-status JSON views are produced by read.js.
}


try {
  main();
} catch (e) {
  console.error(String(e && e.stack ? e.stack : e));
  process.exit(1);
}
