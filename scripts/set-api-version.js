#!/usr/bin/env node
// Bumps the LT Public API version in the .csproj file.
// Usage:
//   node scripts/set-api-version.js           bump patch (default)
//   node scripts/set-api-version.js --minor   bump minor
//   node scripts/set-api-version.js --major   bump major

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

const csproj = resolve(root, 'src/LimitlessTrading/LimitlessTrading.Public/LT.Public.Api/LT.Public.Api/LT.Public.Api.csproj')
const content = readFileSync(csproj, 'utf8')

const match = content.match(/<Version>(.*?)<\/Version>/)
if (!match) {
  console.error('No <Version> tag found in csproj')
  process.exit(1)
}

const current = match[1]
const version = bump(current, type)
const updated = content.replace(/<Version>.*?<\/Version>/, `<Version>${version}</Version>`)
writeFileSync(csproj, updated)
console.log(`LT.Public.Api.csproj: ${current} → ${version}`)
