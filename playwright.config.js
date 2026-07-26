const { defineConfig } = require('@playwright/test');

module.exports = defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? 'github' : 'line',
  use: {
    baseURL: 'http://127.0.0.1:5201',
    browserName: 'chromium',
    trace: 'on-first-retry'
  },
  webServer: {
    command: 'dotnet run --no-build --configuration Release --urls http://127.0.0.1:5201',
    url: 'http://127.0.0.1:5201',
    reuseExistingServer: !process.env.CI,
    timeout: 120000
  }
});
