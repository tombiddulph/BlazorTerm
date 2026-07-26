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

test('preserves terminal output across circuit pause and resume', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('about');
  await input.press('Enter');
  await expect(page.locator('#terminal-output')).toContainText('ABOUT');

  await page.evaluate(() => window.Blazor.pauseCircuit());
  await page.evaluate(() => window.Blazor.resumeCircuit());

  await expect(page.locator('#terminal-output')).toContainText('ABOUT');
  await expect(input).toBeEditable();
});

test('runs suggested commands without typing', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('#terminal-input')).toBeFocused();
  await page.getByRole('button', { name: 'projects', exact: true }).click();

  await expect(page.locator('#terminal-output')).toContainText('SELECTED PROJECTS');
});

test('opens the static resume with the gui command', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('gui');
  await input.press('Enter');

  await expect(page).toHaveURL(/\/resume$/);
  await expect(page.getByRole('heading', { name: 'Experience' })).toBeVisible();
});

test.describe('without JavaScript', () => {
  test.use({ javaScriptEnabled: false });

  test('shows the semantic resume', async ({ page }) => {
    await page.goto('/');

    const fallback = page.locator('.noscript-content');
    await expect(fallback).toBeVisible();
    await expect(fallback).toContainText('Experience');
    await expect(fallback).toContainText('NewDay');
    await expect(fallback).toContainText('Selected projects');
  });
});
