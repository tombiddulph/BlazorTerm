const { test, expect } = require('@playwright/test');

test('renders privacy-filtered activity lines on desktop and mobile', async ({ page, request }) => {
  const browserErrors = [];
  page.on('console', message => {
    if (message.type() === 'error') browserErrors.push(message.text());
  });
  page.on('pageerror', error => browserErrors.push(error.message));

  const data = await (await request.get('/activity-map.json')).json();
  await page.goto('/activity-map');
  await expect(page.getByRole('heading', { name: 'Ground covered.' })).toBeVisible();
  const replay = page.locator('#activity-map-replay');
  await expect(replay).toBeVisible();
  await expect(replay).toBeDisabled();
  await expect(replay).toBeEnabled({ timeout: 10000 });
  await expect(page.locator('#map-activity-count')).toHaveText(data.activityCount.toLocaleString());
  await expect(page.locator('#map-distance-count')).toHaveText(
    `${data.distanceKilometers.toLocaleString()} km / ${Math.round(data.distanceKilometers * 0.621371).toLocaleString()} mi`);
  await expect(page.locator('#map-elevation-count')).toHaveText(
    `${data.elevationMeters.toLocaleString()} m / ${Math.round(data.elevationMeters * 3.28084).toLocaleString()} ft`);
  for (const sport of data.sports) {
    await expect(page.locator('#map-sport-breakdown')).toContainText(
      `${sport.name.replace(/([a-z])([A-Z])/g, '$1 $2')}${sport.count.toLocaleString()}`);
  }
  await expect(page.locator('.maplibregl-canvas')).toBeVisible();
  await expect(page.locator('#activity-map-status')).toBeHidden();

  await replay.click();
  await expect(replay).toHaveText('Tracing...');
  await expect(replay).toBeEnabled({ timeout: 10000 });
  await expect(page.locator('#map-activity-count')).toHaveText(data.activityCount.toLocaleString());

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.locator('#activity-map')).toBeVisible();
  expect(browserErrors).toEqual([]);
});

test('shows the complete map immediately for reduced motion', async ({ page, request }) => {
  const data = await (await request.get('/activity-map.json')).json();
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/activity-map');

  await expect(page.locator('#map-activity-count')).toHaveText(data.activityCount.toLocaleString());
  await expect(page.locator('#activity-map-replay')).toBeHidden();
});
