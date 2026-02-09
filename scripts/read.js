#!/usr/bin/env node
// read.js --status <status> [--status <status> ...]
// Writes filtered items to (status).json files in this folder.
// Example: node read.js --status hanging --status handled

const fs = require('fs');
const path = require('path');

const PATH_CONSOLE = path.join(__dirname, 'consoles.json');

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

function parseArgs(argv) {
  const out = { statuses: [] };
  const args = argv.slice(2);
  while (args.length) {
    const a = args.shift();
    if (a === '--status') {
      const v = (args.shift() ?? '').trim();
      if (v) out.statuses.push(v);
    }
  }
  return out;
}

const { statuses } = parseArgs(process.argv);
if (!statuses.length) {
  console.error('Usage: node read.js --status <status> [--status <status> ...]');
  process.exit(2);
}

const items = readJson(PATH_CONSOLE, []);

// Write one file per requested status: <status>.json
for (const status of statuses) {
  const filtered = items.filter((x) => x.status === status);
  const outPath = path.join(__dirname, `${status}.json`);
  fs.writeFileSync(outPath, JSON.stringify(filtered, null, 2), 'utf8');
}

