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
  await page.keyboard.type('help', { delay: 0 });
  await expect(input).toHaveValue('help');
  await page.keyboard.press('Enter');

  await expect(page.locator('#terminal-output')).toContainText('PROFILE');
  expect(browserErrors).toEqual([]);
});

test('preserves terminal output across circuit pause and resume', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('about', { delay: 0 });
  await expect(input).toHaveValue('about');
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

test('presents grouped command navigation', async ({ page }) => {
  await page.goto('/?cmd=help');

  const output = page.locator('#terminal-output');
  await expect(output).toContainText('PROFILE');
  await expect(output).toContainText('NAVIGATE');
  await expect(output).toContainText('SYSTEM');
  await expect(page.getByRole('log').getByRole('button', { name: 'resume', exact: true })).toBeVisible();
});

test('completes and executes a command with the keyboard', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('res', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('resume ');
  await input.press('Enter');

  await expect(page.locator('#terminal-output')).toContainText('EXPERIENCE');
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

test('shows live application telemetry', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('telemetry', { delay: 0 });
  await input.press('Enter');

  const output = page.locator('#terminal-output');
  await expect(output).toContainText('LIVE TELEMETRY');
  await expect(output).toContainText(/circuits\s+\d+ active/);
  await expect(output).toContainText(/last request\s+\d+\.\d ms/);
});

test('keeps the terminal contained and aligned on an ultrawide viewport', async ({ page }) => {
  await page.setViewportSize({ width: 2560, height: 1200 });
  await page.goto('/');

  const shell = await page.locator('.terminal-window').boundingBox();
  expect(shell.width).toBeLessThanOrEqual(1152);
  expect(Math.abs(shell.x - (2560 - shell.width) / 2)).toBeLessThanOrEqual(1);

  await page.getByRole('button', { name: 'help', exact: true }).click();
  await expect(page.locator('.help-description')).not.toHaveCount(0);
  const descriptionEdges = await page.locator('.help-description').evaluateAll(elements =>
    elements.map(element => Math.round(element.getBoundingClientRect().left)));
  expect(new Set(descriptionEdges).size).toBe(1);

  const valueEdges = await page.locator('.neofetch-info strong').evaluateAll(elements =>
    elements.map(element => Math.round(element.getBoundingClientRect().left)));
  expect(new Set(valueEdges).size).toBe(1);
});

test('applies the selected accent to the prompt', async ({ page }) => {
  await page.goto('/');
  const segment = page.locator('.context-segment').last();
  const green = await segment.evaluate(element => getComputedStyle(element).color);

  await page.locator('#terminal-input').pressSequentially('theme amber', { delay: 0 });
  await page.locator('#terminal-input').press('Enter');

  await expect(page.locator('.site-shell')).toHaveClass(/theme-amber/);
  const amber = await segment.evaluate(element => getComputedStyle(element).color);
  expect(amber).not.toBe(green);
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
