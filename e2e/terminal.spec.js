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

  await expect(page.locator('#terminal-output')).toContainText('HELP / COMMAND GROUPS');
  await expect(input).toHaveValue('');
  expect(browserErrors).toEqual([]);
});

test('only shows the blinking cursor while input has focus', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  const cursor = page.locator('.active-prompt .cursor');
  await expect(input).toBeFocused();
  await expect(cursor).toHaveCSS('animation-name', 'blink');

  await page.locator('.new-tab').click();
  await expect(input).not.toBeFocused();
  await expect(cursor).toHaveCSS('animation-name', 'none');
  await expect(cursor).toHaveCSS('opacity', '0');

  await page.locator('.active-prompt').click();
  await expect(input).toBeFocused();
  await expect(cursor).toHaveCSS('animation-name', 'blink');
});

test('plays the trace topology boot once per session without blocking input', async ({ page }) => {
  await page.goto('/');
  const overlay = page.locator('#terminal-boot');
  const input = page.locator('#terminal-input');
  await expect(overlay).toBeVisible();
  await expect(overlay).toHaveClass(/is-running/);
  await expect(input).toBeFocused();
  await input.pressSequentially('help', { delay: 0 });
  await expect(input).toHaveValue('help');
  await expect(overlay).toBeHidden({ timeout: 4000 });

  await page.reload();
  await page.waitForTimeout(150);
  await expect(overlay).toBeHidden();
});

test('skips the trace topology boot for reduced motion', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await expect(page.locator('#terminal-boot')).toBeHidden();
});

test('preserves terminal output across circuit pause and resume', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('about', { delay: 0 });
  await expect(input).toHaveValue('about');
  await input.press('Enter');
  await expect(page.locator('#terminal-output')).toContainText('ABOUT');
  await input.pressSequentially('cd projects', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.active-prompt .path-segment')).toHaveText('~/projects');

  await page.evaluate(() => window.Blazor.pauseCircuit());
  await page.evaluate(() => window.Blazor.resumeCircuit());

  await expect(page.locator('#terminal-output')).toContainText('ABOUT');
  await expect(page.locator('.active-prompt .path-segment')).toHaveText('~/projects');
  await expect(input).toBeEditable();
});

test('runs suggested commands without typing', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('unfinished draft', { delay: 0 });
  await page.getByRole('button', { name: 'projects', exact: true }).click();

  await expect(page.locator('#terminal-output')).toContainText('SELECTED PROJECTS');
  await expect(input).toHaveValue('');
});

test('presents grouped command navigation', async ({ page }) => {
  await page.goto('/?cmd=help');

  const output = page.locator('#terminal-output');
  await expect(output).toContainText('HELP / COMMAND GROUPS');
  await expect(page.getByRole('log').getByRole('button', { name: 'help profile', exact: true })).toBeVisible();
  await page.getByRole('log').getByRole('button', { name: 'help profile', exact: true }).click();
  await expect(output).toContainText('HELP / PROFILE');
  await expect(page.getByRole('log').getByRole('button', { name: 'resume', exact: true }).last()).toBeVisible();
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

test('completes help groups', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('help filt', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('help filters ');
  await input.press('Enter');
  await expect(page.locator('.history-entry').filter({ hasText: 'help filters' })).toContainText('HELP / FILTERS');
});

test('completes manual names and tour controls', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('man gr', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('man grep ');
  await input.press('Enter');
  await expect(page.locator('.history-entry').filter({ hasText: 'man grep' })).toContainText('SYNOPSIS');

  await input.pressSequentially('tour prev', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('tour previous ');
});

test('navigates and persists the guided tour without blocking commands', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('tour', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('log')).toContainText('TOUR 1/6 / PROFILE');
  await page.getByRole('log').getByRole('button', { name: 'next', exact: true }).click();
  await expect(page.getByRole('log')).toContainText('TOUR 2/6 / PROJECTS');

  await input.pressSequentially('whoami', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').last()).toContainText('whoamitom');
  await expect(input).toBeFocused();
  await input.pressSequentially('tour next', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('log')).toContainText('TOUR 3/6 / PIPES');

  await page.evaluate(() => window.Blazor.pauseCircuit());
  await page.evaluate(() => window.Blazor.resumeCircuit());
  await expect(input).toBeFocused();
  await expect(input).toBeEditable();
  await input.pressSequentially('tour', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').last()).toContainText('TOUR 3/6 / PIPES');

  await input.pressSequentially('tour previous', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').last()).toContainText('TOUR 2/6 / PROJECTS');
  await input.pressSequentially('tour 6', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').last()).toContainText('TOUR 6/6 / FINISH');
  await input.pressSequentially('tour stop', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').last()).toContainText('Tour stopped');
});

test('completes filters and highlights grep matches', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('resume | gr', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('resume | grep ');
  await input.pressSequentially('-i azure');
  await input.press('Enter');

  const latestEntry = page.locator('.history-entry').filter({ hasText: 'resume | grep -i azure' });
  await expect(latestEntry).toContainText('Azure');
  await expect(latestEntry.locator('.match-highlight').first()).toHaveText(/azure/i);
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

test('discovers and opens the activity map from the terminal', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('map');
  await input.press('Enter');

  await expect(page).toHaveURL(/\/activity-map$/);
  await expect(page.getByRole('heading', { name: 'Ground covered.' })).toBeVisible();
  await expect(page.locator('.maplibregl-canvas')).toBeVisible();
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

test('shows accessible disabled states for live commands', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();

  await input.pressSequentially('kubectl get pods', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('log')).toContainText('live cluster view is disabled');

  await input.pressSequentially('rides', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('log')).toContainText('Strava integration is disabled');
  await expect(page.getByRole('log')).toHaveAttribute('aria-live', 'polite');
});

test('renders a responsive command trace', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('trace resume', { delay: 0 });
  await input.press('Enter');

  const latestEntry = page.locator('.history-entry').filter({ hasText: 'trace resume' });
  await expect(latestEntry).toContainText('TRACE');
  await expect(latestEntry).toContainText('command.resume');
  await expect(latestEntry).toContainText('content.load');
  await expect(latestEntry.locator('.trace-bar').first()).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(latestEntry.locator('.trace-bar').first()).toBeHidden();
  await expect(latestEntry.locator('.trace-duration').first()).toBeVisible();
});

test('navigates the virtual filesystem and completes nested paths', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();

  await input.pressSequentially('cd projects', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.active-prompt .path-segment')).toHaveText('~/projects');

  await input.pressSequentially('pwd', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.history-entry').filter({ hasText: 'pwd' })).toContainText('/home/tom/projects');

  await input.pressSequentially('cd ~', { delay: 0 });
  await input.press('Enter');
  await expect(page.locator('.active-prompt .path-segment')).toHaveText('~');
  await input.pressSequentially('cat projects/service-bus-explorer/REA', { delay: 0 });
  await input.press('Tab');
  await expect(input).toHaveValue('cat projects/service-bus-explorer/README.md ');
  await input.press('Enter');
  await expect(page.locator('.history-entry').filter({ hasText: 'cat projects/service-bus-explorer/README.md' })).toContainText('SERVICE BUS EMULATOR EXPLORER');
});

test('keeps the terminal contained and aligned on an ultrawide viewport', async ({ page }) => {
  await page.setViewportSize({ width: 2560, height: 1200 });
  await page.goto('/');
  await expect(page.locator('#terminal-input')).toBeFocused();

  const shell = await page.locator('.terminal-window').boundingBox();
  expect(shell.width).toBeLessThanOrEqual(1442);
  expect(shell.width).toBeGreaterThanOrEqual(1300);
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
  await expect(page.locator('#terminal-input')).toBeFocused();
  const segment = page.locator('.context-segment').last();
  const green = await segment.evaluate(element => getComputedStyle(element).color);

  await page.locator('#terminal-input').pressSequentially('theme amber', { delay: 0 });
  await page.locator('#terminal-input').press('Enter');

  await expect(page.locator('.site-shell')).toHaveClass(/theme-amber/);
  const amber = await segment.evaluate(element => getComputedStyle(element).color);
  expect(amber).not.toBe(green);
});

test('persists each extended theme across a new page session', async ({ page, context }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  const palettes = [];

  for (const theme of ['nord', 'solarized', 'dracula']) {
    await input.pressSequentially(`theme ${theme}`, { delay: 0 });
    await input.press('Enter');
    await expect(page.locator('.site-shell')).toHaveClass(new RegExp(`theme-${theme}`));
    palettes.push(await page.locator('.site-shell').evaluate(element => {
      const style = getComputedStyle(element);
      return [style.getPropertyValue('--accent'), style.getPropertyValue('--page-bg'), style.getPropertyValue('--terminal-bg')].join('|');
    }));
  }

  expect(new Set(palettes).size).toBe(3);
  await expect.poll(() => page.evaluate(() => localStorage.getItem('blazorterm-theme'))).toBe('theme-dracula');

  const freshPage = await context.newPage();
  await freshPage.goto('/');
  await expect(freshPage.locator('html')).toHaveAttribute('data-terminal-theme', 'theme-dracula');
  await expect(freshPage.locator('.site-shell')).toHaveClass(/theme-dracula/);
});

test('vim traps shell commands, offers a delayed hint, and exits only with :q!', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();

  await input.pressSequentially('vim', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('region', { name: 'Vim interaction mode' })).toBeVisible();
  await expect(input).toHaveAttribute('aria-label', /colon q exclamation mark/);

  await input.pressSequentially('pwd', { delay: 0 });
  await input.press('Enter');
  await expect(input).toHaveValue('');
  await expect(page.locator('.vim-message')).toContainText('Not an editor command: pwd');
  await expect(page.locator('.history-entry').filter({ hasText: 'pwd' })).toHaveCount(0);
  await expect(page.locator('.vim-message')).toContainText('type :q!', { timeout: 12000 });

  await input.pressSequentially(':q!', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('region', { name: 'Vim interaction mode' })).toBeHidden();
  await expect(input).toHaveValue('');

  await input.pressSequentially('uptime', { delay: 0 });
  await input.press('Enter');
  await expect(page.getByRole('log')).toContainText('UPTIME');
  await input.press('ArrowUp');
  await expect(input).toHaveValue('uptime');
  await input.press('ArrowUp');
  await expect(input).toHaveValue('vim');
});

test('renders cowsay with an accessible alternative and clears command input', async ({ page }) => {
  await page.goto('/');
  const input = page.locator('#terminal-input');
  await expect(input).toBeFocused();
  await input.pressSequentially('cowsay ship it', { delay: 0 });
  await input.press('Enter');

  await expect(input).toHaveValue('');
  await expect(page.getByRole('img', { name: 'A cow says: ship it' })).toBeVisible();
  await expect(page.locator('.ascii-art')).toContainText('< ship it >');
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
