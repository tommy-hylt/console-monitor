#!/usr/bin/env node
// handle.js <pid>
// Marks a console entry as handled.

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

function writeJson(filePath, value) {
  fs.writeFileSync(filePath, JSON.stringify(value, null, 2), 'utf8');
}

const pid = Number(process.argv[2]);
if (!pid) {
  console.error('Usage: node handle.js <pid>');
  process.exit(2);
}

const items = readJson(PATH_CONSOLE, []);
let found = false;
for (const item of items) {
  if (item.pid === pid) {
    item.status = 'handled';
    found = true;
  }
}

writeJson(PATH_CONSOLE, items);
console.log(JSON.stringify({ ok: true, pid, found, status: 'handled' }));
