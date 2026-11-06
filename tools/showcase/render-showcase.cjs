// Use the project's existing Playwright installation through NODE_PATH; no dependency installation.
const { chromium } = require('playwright');
const { pathToFileURL } = require('node:url');
const path = require('node:path');

(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'chrome' });
  try {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1200 }, deviceScaleFactor: 1 });
    for (const board of ['hero', 'journey', 'ai']) {
      const url = pathToFileURL(path.resolve(__dirname, '../../docs/showcase/layout.html'));
      url.searchParams.set('board', board);
      await page.goto(url.href);
      await page.evaluate(() => document.fonts.ready);
      await page.locator(`#${board} img`).evaluateAll(images => Promise.all(images.map(image => image.decode())));
      await page.locator(`#${board}`).screenshot({ path: path.resolve(__dirname, `../../docs/showcase/${board}-r41.png`) });
      console.log(`Rendered ${board}-r41.png from unchanged r41 screen captures.`);
    }
  } finally {
    await browser.close();
  }
})();
