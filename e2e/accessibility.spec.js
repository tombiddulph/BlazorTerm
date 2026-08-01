const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;

const routes = ['/', '/?cmd=tour', '/?cmd=man%20grep', '/?cmd=kubectl%20get%20pods', '/?cmd=rides', '/?cmd=cowsay%20hello', '/?cmd=vim', '/resume', '/projects', '/projects/property-resolvers', '/timeline', '/activity-map', '/contact'];

for (const route of routes) {
  test(`${route} has no automated accessibility violations`, async ({ page }) => {
    await page.goto(route);
    if (route === '/') await expect(page.locator('#terminal-input')).toBeFocused();
    if (route === '/activity-map') await expect(page.locator('.maplibregl-canvas')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
}
