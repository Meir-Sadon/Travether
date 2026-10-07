import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests for the core flows (PLAN.md §7, step 1.15). They run the real API against a
 * PostGIS database and the Vite dev server, and drive Chromium like a traveler on a phone.
 * See README.md in this folder.
 */
const apiPort = 5080
const webPort = 5173
const database =
  process.env.E2E_DATABASE_URL ?? 'Host=localhost;Port=5433;Database=travether_e2e;Username=travether;Password=travether'

export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  workers: process.env.CI ? 2 : 3,
  retries: process.env.CI ? 1 : 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: `http://localhost:${webPort}`,
    ...devices['Pixel 7'],
    locale: 'en-GB',
    timezoneId: 'Asia/Bangkok',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    launchOptions: process.env.E2E_CHROMIUM ? { executablePath: process.env.E2E_CHROMIUM } : {},
  },
  webServer: [
    {
      command: 'dotnet run --project ../backend/src/Travether.Api --no-launch-profile',
      url: `http://127.0.0.1:${apiPort}/api/health`,
      timeout: 240_000,
      reuseExistingServer: !process.env.CI,
      stdout: 'ignore',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: `http://127.0.0.1:${apiPort}`,
        ConnectionStrings__Default: database,
        Database__MigrateOnStartup: 'true',
        Notifications__RunJobs: 'false',
        RateLimits__AuthPerMinute: '100000',
        RateLimits__SignupsPerIpPerDay: '100000',
        RateLimits__JoinRequestsPerHour: '100000',
        RateLimits__MessagesPerMinute: '100000',
        RateLimits__ReportsPerDay: '100000',
        RateLimits__ExportsPerDay: '100000',
      },
    },
    {
      command: `npm run dev -- --port ${webPort} --strictPort`,
      cwd: '../frontend',
      url: `http://localhost:${webPort}`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: { VITE_API_PROXY: `http://127.0.0.1:${apiPort}` },
    },
  ],
})
