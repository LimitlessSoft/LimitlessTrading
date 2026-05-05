#!/usr/bin/env node
// Bumps the Workstation version and syncs it across all three packages.
// Usage:
//   node scripts/set-workstation-version.js           bump patch (default)
//   node scripts/set-workstation-version.js --minor   bump minor
//   node scripts/set-workstation-version.js --major   bump major

import { readFileSync, writeFileSync } from 'fs'
import { resolve, dirname } from 'path'
import { fileURLToPath } from 'url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const root = resolve(__dirname, '..')

function bump(version, type) {
  let [major, minor, patch] = version.split('.').map(Number)
  if (type === 'major') { major++; minor = 0; patch = 0 }
  else if (type === 'minor') { minor++; patch = 0 }
  else { patch++ }
  return `${major}.${minor}.${patch}`
}

const args = process.argv.slice(2)
const type = args.includes('--major') ? 'major' : args.includes('--minor') ? 'minor' : 'patch'

const versionFile = resolve(root, 'src/LT.Workstation/version.json')
const { version: current } = JSON.parse(readFileSync(versionFile, 'utf8'))
const version = bump(current, type)

writeFileSync(versionFile, JSON.stringify({ version }, null, 2) + '\n')
console.log(`version.json: ${current} → ${version}`)

const packageFiles = [
  'src/LT.Workstation/LT.Workstation.App/package.json',
  'src/LT.Workstation/LT.Workstation.Fe/package.json',
  'src/LT.Workstation/LT.Workstation.Server/package.json',
]

for (const rel of packageFiles) {
  const file = resolve(root, rel)
  const pkg = JSON.parse(readFileSync(file, 'utf8'))
  pkg.version = version
  writeFileSync(file, JSON.stringify(pkg, null, 2) + '\n')
  console.log(`${rel}: → ${version}`)
}
