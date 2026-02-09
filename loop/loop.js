#!/usr/bin/env node
// loop/loop.js
// Run scripts/refresh.js forever with a 10-minute sleep.

const path = require('path');
const { spawn } = require('child_process');

const SCRIPTS_DIR = path.join(__dirname, '..', 'scripts');
const REFRESH = path.join(SCRIPTS_DIR, 'refresh.js');

const SLEEP_MS = 10 * 60 * 1000; // fixed 10 minutes

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function runOnce() {
  return new Promise((resolve) => {
    const p = spawn(process.execPath, [REFRESH], {
      cwd: SCRIPTS_DIR,
      windowsHide: true,
      stdio: 'inherit',
    });
    p.on('exit', (code) => resolve(code ?? 0));
    p.on('error', () => resolve(1));
  });
}

function isoNow() {
  return new Date().toISOString();
}

(async function main() {
  console.log(`[ConsoleMonitor loop] started at ${isoNow()} (interval=${Math.round(SLEEP_MS / 60000)}m)`);

  // eslint-disable-next-line no-constant-condition
  while (true) {
    const started = Date.now();
    console.log(`[ConsoleMonitor loop] refresh starting ${isoNow()}`);

    let code = 0;
    try {
      code = await runOnce();
    } catch {
      code = 1;
    }

    const tookMs = Date.now() - started;
    console.log(`[ConsoleMonitor loop] refresh done ${isoNow()} (code=${code}, tookMs=${tookMs})`);
    console.log(`[ConsoleMonitor loop] sleeping ${Math.round(SLEEP_MS / 1000)}s`);

    await sleep(SLEEP_MS);
  }
})();
