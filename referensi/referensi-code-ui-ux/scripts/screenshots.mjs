// Screenshot every screen at a pinned demo time. Usage: node scripts/screenshots.mjs [baseUrl] [filter]
import { chromium } from 'playwright'
import { mkdirSync } from 'node:fs'

const base = process.argv[2] ?? 'http://localhost:5173'
const filter = process.argv[3] ?? ''
const outDir = 'docs/screenshots'
mkdirSync(outDir, { recursive: true })

const PHONE = { width: 390, height: 844 }
const DESKTOP = { width: 1440, height: 900 }

/** @type {{name:string, path:string, viewport:{width:number,height:number}, actions?: (page: import('playwright').Page) => Promise<void>}[]} */
const SHOTS = [
  { name: 'D1-now-peek', path: '/?t=tue-0830&paused', viewport: PHONE },
  { name: 'D1-now-half', path: '/?t=tue-0830&paused&sheet=half', viewport: PHONE },
  { name: 'D1-now-full', path: '/?t=tue-0830&paused&sheet=full', viewport: PHONE },
  { name: 'D2-zone-A1', path: '/zone/A1?t=tue-0830&paused', viewport: PHONE },
  { name: 'D2-zone-S1-nodata', path: '/zone/S1?t=tue-0850&paused', viewport: PHONE },
  { name: 'D3-plan', path: '/plan?t=tue-0830&paused', viewport: PHONE },
  { name: 'D3-plan-full', path: '/plan?t=tue-0830&paused&sheet=full', viewport: PHONE },
  { name: 'D4-best', path: '/best?t=tue-0830&paused', viewport: PHONE },
  { name: 'D5-code', path: '/code?t=tue-0830&paused', viewport: PHONE },
  { name: 'D6-saved', path: '/saved?t=tue-0830&paused', viewport: PHONE },
  { name: 'D7-how-we-pick', path: '/how-we-pick?t=tue-0830&paused', viewport: PHONE },
  { name: 'D1-now-evening', path: '/?t=tue-2200&paused&sheet=half', viewport: PHONE },
  { name: 'D1-desktop', path: '/?t=tue-0830&paused&sheet=half', viewport: DESKTOP },
  { name: 'O1-overview', path: '/ops?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O2-live-map', path: '/ops/map?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O2-live-map-forecast', path: '/ops/map?t=tue-0830&paused&layer=forecast', viewport: DESKTOP },
  { name: 'O3-zones', path: '/ops/zones?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O4-zone-inspector', path: '/ops/zones/A1?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O5-alerts', path: '/ops/alerts?t=tue-0850&paused', viewport: DESKTOP },
  { name: 'O6-devices', path: '/ops/devices?t=tue-0850&paused', viewport: DESKTOP },
  { name: 'O7-analytics', path: '/ops/analytics?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O7-forecast-health', path: '/ops/analytics/forecast?t=tue-0830&paused', viewport: DESKTOP },
  { name: 'O8-settings', path: '/ops/settings?t=tue-0830&paused', viewport: DESKTOP },
]

// wait for the dev server
for (let i = 0; i < 60; i++) {
  try {
    const r = await fetch(base)
    if (r.ok) break
  } catch {
    /* not up yet */
  }
  await new Promise((r) => setTimeout(r, 500))
}

const browser = await chromium.launch()
try {
  for (const shot of SHOTS) {
    if (filter && !shot.name.includes(filter)) continue
    const context = await browser.newContext({ viewport: shot.viewport, deviceScaleFactor: 2 })
    const page = await context.newPage()
    const errors = []
    page.on('pageerror', (e) => errors.push(e.message))
    page.on('console', (m) => {
      if (m.type() === 'error') errors.push(m.text())
    })
    try {
      await page.goto(base + shot.path, { waitUntil: 'networkidle', timeout: 30000 })
    } catch (e) {
      errors.push(`goto failed: ${e.message.split('\n')[0]}`)
    }
    await page.waitForTimeout(1200)
    if (shot.actions) await shot.actions(page)
    const mounted = await page.evaluate(() => (document.querySelector('#root')?.children.length ?? 0) > 0).catch(() => false)
    if (!mounted) errors.push('page is blank (#root empty)')
    await page.screenshot({ path: `${outDir}/${shot.name}.png` })
    console.log(`${shot.name.padEnd(24)} ${errors.length ? 'ERRORS: ' + errors.slice(0, 3).join(' | ').slice(0, 300) : 'ok'}`)
    if (errors.length) process.exitCode = 1
    await context.close()
  }
} finally {
  await browser.close()
}
