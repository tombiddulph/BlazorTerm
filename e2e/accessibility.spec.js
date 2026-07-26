const { test, expect } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;

const routes = ['/', '/resume', '/projects', '/projects/property-resolvers', '/timeline', '/contact'];

for (const route of routes) {
  test(`${route} has no automated accessibility violations`, async ({ page }) => {
    await page.goto(route);
    if (route === '/') await expect(page.locator('#terminal-input')).toBeFocused();

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
}
