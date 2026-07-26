const { test, expect } = require('@playwright/test');

test('accepts keyboard input and executes a command', async ({ page }) => {
  const browserErrors = [];
  page.on('console', message => {
    if (message.type() === 'error') browserErrors.push(message.text());
  });
  page.on('pageerror', error => browserErrors.push(error.message));

  await page.goto('/');

  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await page.keyboard.type('help');
  await expect(input).toHaveValue('help');
  await page.keyboard.press('Enter');

  await expect(page.locator('#terminal-output')).toContainText('AVAILABLE COMMANDS');
  expect(browserErrors).toEqual([]);
});
