/**
 * Export all brand payloads from the Zeus frontend repo into this backoffice's Data/Seeds.
 * Mirrors price-configurator/scripts/export-all-brands.mjs with a configurable frontend path.
 *
 * Usage:
 *   node scripts/sync-seeds-from-frontend.mjs
 *   PRICE_CONFIGURATOR_FRONTEND=D:\path\to\price-configurator node scripts/sync-seeds-from-frontend.mjs
 */
import { readFileSync, writeFileSync, mkdirSync } from 'fs'
import path from 'path'
import { fileURLToPath, pathToFileURL } from 'url'
import { execSync } from 'child_process'

const BRANDS = [
  { brand: 'magnet', language: 'en-GB' },
  { brand: 'invita', language: 'da-DK' },
  { brand: 'sigdal', language: 'nb-NO' },
  { brand: 'norema', language: 'nb-NO' },
  { brand: 'novart', language: 'fi-FI' },
  { brand: 'marbodal', language: 'sv-SE' },
]

const DEFAULT_FRONTEND = 'C:\\Niteco-Project\\Nobia\\price-configurator'
const backofficeRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)))
const frontendRoot = process.env.PRICE_CONFIGURATOR_FRONTEND || DEFAULT_FRONTEND
const outDir = path.join(backofficeRoot, 'price-configurator-back-office', 'Data', 'Seeds')

mkdirSync(outDir, { recursive: true })

console.log(`Frontend: ${frontendRoot}`)
console.log(`Output:   ${outDir}`)

for (const { brand, language } of BRANDS) {
  const bundlePath = path.join(frontendRoot, `.tmp-${brand}-bundle.mjs`)
  const indexPath = path.join(frontendRoot, 'src', 'brands', brand, 'index.js')

  execSync(
    `npx esbuild "${indexPath}" --bundle --platform=node --format=esm --outfile="${bundlePath}"`,
    { cwd: frontendRoot, stdio: 'inherit' }
  )

  const mod = await import(pathToFileURL(bundlePath).href)
  const sections = mod.default

  const messagesPath = path.join(frontendRoot, 'config', brand, 'messages.json')
  const messages = JSON.parse(readFileSync(messagesPath, 'utf8'))

  const payload = { sections, messages }
  const outPath = path.join(outDir, `${brand}-${language}.payload.json`)
  writeFileSync(outPath, JSON.stringify(payload, null, 2), 'utf8')
  const messagesOutPath = path.join(outDir, `${brand}-${language}.messages.json`)
  writeFileSync(messagesOutPath, JSON.stringify(messages, null, 2), 'utf8')
  console.log(`Wrote ${outPath} (${sections.length} sections, ${Object.keys(messages).length} messages)`)
  console.log(`Wrote ${messagesOutPath}`)
}
