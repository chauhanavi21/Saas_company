import { defineConfig } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

/**
 * Browser tests run against the real API and the real Angular app, with a throwaway SQLite database.
 *
 * The ports are fixed: the web app calls http://localhost:5000/api and the API only allows the origin
 * http://localhost:4200. Stop any other dev server on those ports first (the config refuses to reuse one,
 * so a test run can never touch real data).
 *
 * Playwright loads this file in several processes (the runner, each worker, the teardown), so a path
 * built from process.pid would differ in each. The first process picks the name and shares it through
 * the environment, so every later process, and the teardown that deletes the file, sees the same one.
 */
process.env['SAAS_E2E_DB'] ??= join(tmpdir(), `saasplatform-e2e-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}.db`);
export const database = process.env['SAAS_E2E_DB'];

export default defineConfig({
  testDir: './e2e',
  globalTeardown: './e2e/teardown.ts',
  // Every test registers its own company, so tests do not share state and can run side by side.
  fullyParallel: true,
  workers: 2,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI'] ? [['list'], ['github']] : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --no-launch-profile --project ../src/SaasPlatform.Api',
      url: 'http://localhost:5000/health',
      reuseExistingServer: false,
      timeout: 180_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: 'http://localhost:5000',
        ConnectionStrings__Default: `Data Source=${database}`,
        // Test-only signing key; never used outside this run.
        Jwt__Key: 'e2e-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz',
        // The suite signs in many times a minute, which the default 10-per-minute limit would block.
        // The limit itself is covered by the API tests.
        RateLimiting__AuthPermitLimit: '10000',
      },
    },
    {
      command: 'npx ng serve --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: false,
      timeout: 180_000,
    },
  ],
});
