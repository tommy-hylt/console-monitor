#!/usr/bin/env node
// capture.js
// Capture a screenshot of a console window for a given PID.
//
// Usage:
//   node capture.js <pid>
//   node capture.js <pid> --out <path.png>
//
// Output:
//   Writes PNG, prints JSON to stdout.

import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { spawnSync } from 'child_process';

function parseArgs(argv) {
  const args = argv.slice(2);
  let pid = null;
  let outPath = null;
  let title = null;

  const positional = [];
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--out' && i + 1 < args.length) {
      outPath = args[++i];
    } else if (a === '--title' && i + 1 < args.length) {
      title = args[++i];
    } else {
      positional.push(a);
    }
  }

  if (positional[0]) pid = Number(positional[0]);
  return { pid, outPath, title };
}

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function toolExe() {
  return path.resolve(
    __dirname,
    'tools',
    'Screenshot',
    'bin',
    'Release',
    'net8.0-windows',
    'Screenshot.exe'
  );
}

function main() {
  const { pid, outPath, title } = parseArgs(process.argv);
  if (!pid || !Number.isFinite(pid) || pid <= 0) {
    console.error(JSON.stringify({ ok: false, error: 'Usage: node capture.js <pid> [--out out.png]' }, null, 2));
    process.exit(2);
  }

  const capturesDir = path.resolve(__dirname, 'captures');
  fs.mkdirSync(capturesDir, { recursive: true });
  const out = outPath ? path.resolve(outPath) : path.join(capturesDir, `pid-${pid}.png`);

  const jsonOut = out + '.json';

  const exe = toolExe();
  const exeArgs = ['--pid', String(pid), '--png', out, '--json', jsonOut];
  if (title) exeArgs.push('--title', String(title));

  const r = spawnSync(exe, exeArgs, {
    windowsHide: true,
    encoding: 'utf8',
  });

  const stdout = (r.stdout || '').trim();
  const stderr = (r.stderr || '').trim();

  let toolJson = null;
  try {
    toolJson = JSON.parse(fs.readFileSync(jsonOut, 'utf8'));
  } catch {}

  if (r.status !== 0) {
    console.error(
      JSON.stringify(
        {
          ok: false,
          pid,
          out,
          jsonOut,
          exitCode: r.status,
          stdout,
          stderr,
          tool: toolJson,
        },
        null,
        2
      )
    );
    process.exit(r.status ?? 1);
  }

  console.log(JSON.stringify({ ok: true, pid, out, jsonOut, tool: toolJson }, null, 2));
}

main();
