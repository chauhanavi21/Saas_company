import { existsSync, rmSync } from 'node:fs';
import { database } from '../playwright.config';

const FILES = ['', '-wal', '-shm'].map((suffix) => database + suffix);

/** Exit handlers must be synchronous, so wait with Atomics.wait, which sleeps without using the CPU. */
function sleep(ms: number) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

function remove(attempts: number) {
  for (let attempt = 0; attempt < attempts; attempt++) {
    for (const file of FILES) {
      try {
        rmSync(file, { force: true });
      } catch {
        /* still held open by the API; try again below */
      }
    }
    if (!FILES.some(existsSync)) return;
    sleep(200);
  }
}

/**
 * Removes the throwaway database (and SQLite's -wal and -shm side files) after the run.
 *
 * The API holds the file open until Playwright stops it, and this hook runs before that, so deleting here
 * fails on Windows. The exit handler runs after the servers have been told to stop, but the process can
 * take a moment to let go of the file, so it retries for up to ten seconds before giving up.
 */
export default function globalTeardown() {
  remove(1);
  process.once('exit', () => remove(50));
}
