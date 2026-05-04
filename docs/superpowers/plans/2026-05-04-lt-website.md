# Limitless Trading Website Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deploy the Limitless Trading marketing website to limitlesstrading.com — an Astro + Tailwind static site served from a nginx Docker container on the existing Kubernetes cluster.

**Architecture:** Astro generates pure static HTML at build time. GitHub Actions builds the Astro project, packages `/dist` into a nginx Alpine Docker image, pushes to Docker Hub, then deploys to Kubernetes using the same `envsubst` manifest pattern as the existing LT API. All three k8s deploy secrets already exist in the repo.

**Tech Stack:** Astro 4, Tailwind CSS 3, MDX (Astro Content Collections), nginx Alpine (Docker), Kubernetes (nginx ingress), GitHub Actions.

---

## File Map

| File | Purpose |
|---|---|
| `src/LimitlessTrading/LimitlessTrading.Web/package.json` | npm project, Astro + Tailwind deps |
| `src/LimitlessTrading/LimitlessTrading.Web/astro.config.mjs` | Astro config — static output, Tailwind integration, MDX, content collections |
| `src/LimitlessTrading/LimitlessTrading.Web/tailwind.config.mjs` | Tailwind config — LimitlessSoft colour tokens |
| `src/LimitlessTrading/LimitlessTrading.Web/src/styles/global.css` | Tailwind directives + CSS custom properties |
| `src/LimitlessTrading/LimitlessTrading.Web/src/layouts/BaseLayout.astro` | HTML shell — `<head>`, global CSS, slot |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/Nav.astro` | Sticky nav bar |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/Footer.astro` | Site footer |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/Ticker.astro` | Animated stock ticker strip |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/HeroSection.astro` | Home hero — big type + CTAs |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/FeatureCard.astro` | Feature highlight card |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/DownloadCard.astro` | Platform download card |
| `src/LimitlessTrading/LimitlessTrading.Web/src/components/ReleaseEntry.astro` | Single changelog entry |
| `src/LimitlessTrading/LimitlessTrading.Web/src/content/config.ts` | Astro Content Collections schema for releases |
| `src/LimitlessTrading/LimitlessTrading.Web/src/content/releases/v1-0-0.mdx` | First release entry |
| `src/LimitlessTrading/LimitlessTrading.Web/src/data/releases.ts` | Download links and version info |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/index.astro` | Home page |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/downloads.astro` | Downloads page |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/whats-new.astro` | What's New page |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/features.astro` | Features page (stub) |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/demos.astro` | Demos page (stub) |
| `src/LimitlessTrading/LimitlessTrading.Web/src/pages/about.astro` | About page (stub) |
| `src/LimitlessTrading/LimitlessTrading.Web/Dockerfile` | nginx Alpine image — copy /dist |
| `src/LimitlessTrading/LimitlessTrading.Web/nginx.conf` | Custom nginx config for SPA-style 404 handling |
| `k8s/namespaces/lt-web-namespace.yaml` | Kubernetes namespace |
| `k8s/services/lt-web-service.yaml` | Kubernetes service (NodePort 80→80) |
| `k8s/deployments/lt-web-deployment.yaml` | Kubernetes deployment |
| `k8s/ingress/lt-web-ingress.yaml` | Kubernetes ingress — host: limitlesstrading.com |
| `.github/workflows/release-web.yml` | Triggers CI on push to master (web paths) |
| `.github/workflows/build-and-publish-web.yml` | Builds image, pushes to Docker Hub, calls deploy |
| `.github/workflows/deploy-web.yml` | Applies k8s manifests, restarts deployment |

---

## Task 1: Scaffold Astro project

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/package.json`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/astro.config.mjs`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/tailwind.config.mjs`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/tsconfig.json`

- [ ] **Step 1: Create the project directory and package.json**

```bash
mkdir -p src/LimitlessTrading/LimitlessTrading.Web
```

Create `src/LimitlessTrading/LimitlessTrading.Web/package.json`:

```json
{
  "name": "limitless-trading-web",
  "type": "module",
  "version": "0.1.0",
  "scripts": {
    "dev": "astro dev",
    "build": "astro build",
    "preview": "astro preview",
    "check": "astro check"
  },
  "dependencies": {
    "@astrojs/mdx": "^3.1.9",
    "@astrojs/tailwind": "^5.1.4",
    "astro": "^4.16.18",
    "tailwindcss": "^3.4.17"
  },
  "devDependencies": {
    "typescript": "^5.7.2"
  }
}
```

- [ ] **Step 2: Install dependencies**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm install
```

Expected: `node_modules/` created, no errors.

- [ ] **Step 3: Create astro.config.mjs**

```js
// src/LimitlessTrading/LimitlessTrading.Web/astro.config.mjs
import { defineConfig } from 'astro/config';
import tailwind from '@astrojs/tailwind';
import mdx from '@astrojs/mdx';

export default defineConfig({
  output: 'static',
  integrations: [tailwind(), mdx()],
});
```

- [ ] **Step 4: Create tailwind.config.mjs**

```js
// src/LimitlessTrading/LimitlessTrading.Web/tailwind.config.mjs
/** @type {import('tailwindcss').Config} */
export default {
  content: ['./src/**/*.{astro,html,js,jsx,md,mdx,ts,tsx}'],
  theme: {
    extend: {
      colors: {
        'ls-bg': '#080808',
        'ls-blue': '#0057ff',
        'ls-blue-dim': 'rgba(0,87,255,0.08)',
        'ls-border': 'rgba(255,255,255,0.06)',
        'ls-text': 'rgba(255,255,255,0.55)',
        'ls-text-dim': 'rgba(255,255,255,0.25)',
      },
      fontFamily: {
        sans: ['"Helvetica Neue"', 'Helvetica', 'Arial', 'sans-serif'],
      },
      letterSpacing: {
        widest2: '0.2em',
        widest3: '0.3em',
      },
    },
  },
  plugins: [],
};
```

- [ ] **Step 5: Create tsconfig.json**

```json
{
  "extends": "astro/tsconfigs/strict",
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@components/*": ["src/components/*"],
      "@data/*": ["src/data/*"],
      "@layouts/*": ["src/layouts/*"]
    }
  }
}
```

- [ ] **Step 6: Create src directory structure**

```bash
mkdir -p src/LimitlessTrading/LimitlessTrading.Web/src/{pages,components,layouts,styles,content/releases,data}
```

- [ ] **Step 7: Create global CSS**

Create `src/LimitlessTrading/LimitlessTrading.Web/src/styles/global.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@layer base {
  html {
    background-color: #080808;
    color: #ffffff;
    scroll-behavior: smooth;
  }

  body {
    @apply font-sans antialiased;
  }
}

@layer utilities {
  .grid-bg {
    background-image:
      linear-gradient(rgba(255,255,255,0.03) 1px, transparent 1px),
      linear-gradient(90deg, rgba(255,255,255,0.03) 1px, transparent 1px);
    background-size: 80px 80px;
  }

  .text-outline {
    color: transparent;
    -webkit-text-stroke: 2px rgba(255,255,255,0.25);
  }
}
```

- [ ] **Step 8: Verify the scaffold builds**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run build 2>&1 | tail -5
```

Expected: build fails because there are no pages yet — that's fine. The error should be "No pages found" or similar, not a dependency error.

- [ ] **Step 9: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Scaffold LT website — Astro + Tailwind"
```

---

## Task 2: Base layout and shared components

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/layouts/BaseLayout.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/Nav.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/Footer.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/Ticker.astro`

- [ ] **Step 1: Create BaseLayout.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/layouts/BaseLayout.astro
import '../styles/global.css';
import Nav from '@components/Nav.astro';
import Footer from '@components/Footer.astro';

interface Props {
  title: string;
  description?: string;
}

const { title, description = 'Self-hosted trading dashboard for IBKR — build any UI, keep all your data.' } = Astro.props;
---

<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="description" content={description} />
    <title>{title} — Limitless Trading</title>
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
  </head>
  <body class="bg-ls-bg text-white min-h-screen flex flex-col">
    <Nav />
    <main class="flex-1">
      <slot />
    </main>
    <Footer />
  </body>
</html>
```

- [ ] **Step 2: Create Nav.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/Nav.astro
const links = [
  { label: 'Features', href: '/features' },
  { label: 'Demos', href: '/demos' },
  { label: "What's New", href: '/whats-new' },
  { label: 'Downloads', href: '/downloads' },
];
---

<header class="sticky top-0 z-50 border-b border-ls-border backdrop-blur-sm bg-ls-bg/90">
  <nav class="flex items-center justify-between px-12 py-6">
    <a href="/" class="text-sm font-bold tracking-[0.2em] uppercase text-white">
      LIMITLESS<span class="text-ls-blue">TRADING</span>
    </a>

    <ul class="flex gap-9 list-none">
      {links.map(link => (
        <li>
          <a
            href={link.href}
            class="text-xs tracking-[0.15em] uppercase text-ls-text hover:text-white transition-colors"
          >
            {link.label}
          </a>
        </li>
      ))}
    </ul>

    <a
      href="/downloads"
      class="bg-ls-blue text-white px-6 py-2.5 text-xs font-bold tracking-[0.15em] uppercase hover:bg-blue-500 transition-colors"
    >
      Download Free
    </a>
  </nav>
</header>
```

- [ ] **Step 3: Create Footer.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/Footer.astro
import { CURRENT_VERSION } from '@data/releases';
---

<footer class="border-t border-ls-border px-12 py-8">
  <div class="flex items-center justify-between">
    <a href="/" class="text-sm font-bold tracking-[0.2em] uppercase text-white">
      LIMITLESS<span class="text-ls-blue">TRADING</span>
    </a>

    <div class="flex gap-8 text-xs tracking-widest uppercase text-ls-text">
      <a href="/features" class="hover:text-white transition-colors">Features</a>
      <a href="/demos" class="hover:text-white transition-colors">Demos</a>
      <a href="/whats-new" class="hover:text-white transition-colors">What's New</a>
      <a href="/downloads" class="hover:text-white transition-colors">Downloads</a>
      <a href="/about" class="hover:text-white transition-colors">About</a>
    </div>

    <div class="flex items-center gap-6 text-xs text-ls-text-dim tracking-widest uppercase">
      <span>v{CURRENT_VERSION}</span>
      <a href="https://limitlesssoft.com" target="_blank" rel="noopener" class="hover:text-white transition-colors">
        LimitlessSoft
      </a>
    </div>
  </div>
</footer>
```

- [ ] **Step 4: Create Ticker.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/Ticker.astro
const tickers = [
  { sym: 'AAPL', change: '+2.34%', up: true },
  { sym: 'TSLA', change: '-1.12%', up: false },
  { sym: 'SPY',  change: '+0.47%', up: true },
  { sym: 'NVDA', change: '+3.81%', up: true },
  { sym: 'QQQ',  change: '-0.23%', up: false },
  { sym: 'MSFT', change: '+1.05%', up: true },
  { sym: 'AMZN', change: '+0.88%', up: true },
  { sym: 'IBKR', change: '+0.63%', up: true },
];

const doubled = [...tickers, ...tickers];
---

<div class="overflow-hidden border-y border-ls-border bg-white/[0.02] py-2.5">
  <div class="ticker-inner flex gap-12 whitespace-nowrap">
    {doubled.map((t, i) => (
      <>
        <span class="inline-flex items-center gap-2 text-[11px] tracking-[0.15em] uppercase">
          <span class="text-ls-text">{t.sym}</span>
          <span class={t.up ? 'text-ls-blue' : 'text-red-500'}>{t.change}</span>
        </span>
        <span class="text-ls-border text-xs">·</span>
      </>
    ))}
  </div>
</div>

<style>
  .ticker-inner {
    animation: ticker 25s linear infinite;
  }

  @keyframes ticker {
    0%   { transform: translateX(0); }
    100% { transform: translateX(-50%); }
  }
</style>
```

- [ ] **Step 5: Create a placeholder favicon**

Create `src/LimitlessTrading/LimitlessTrading.Web/public/favicon.svg`:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
  <rect width="32" height="32" fill="#080808"/>
  <text x="4" y="24" font-family="Helvetica Neue, sans-serif" font-weight="900" font-size="22" fill="#0057ff">L</text>
</svg>
```

- [ ] **Step 6: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add base layout and shared components"
```

---

## Task 3: Content collection schema and releases data

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/content/config.ts`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/content/releases/v1-0-0.mdx`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/data/releases.ts`

- [ ] **Step 1: Create the Content Collections config**

```typescript
// src/LimitlessTrading/LimitlessTrading.Web/src/content/config.ts
import { defineCollection, z } from 'astro:content';

const releases = defineCollection({
  type: 'content',
  schema: z.object({
    version: z.string(),
    date: z.string(),
    type: z.enum(['Major', 'Feature', 'Fix']),
    summary: z.string(),
  }),
});

export const collections = { releases };
```

- [ ] **Step 2: Create the first release MDX entry**

```mdx
---
version: "1.0.0"
date: "2026-05-04"
type: "Major"
summary: "Initial public release of Limitless Trading."
---

Limitless Trading is now available for download. Connect your IBKR TWS or Gateway, build custom dashboards with widgets and templates, and keep all your data on your own machine.

### What's included

- Real-time market data via IBKR TWS / IB Gateway
- Widget system with live expressions
- Template builder and gallery
- Watchlist with streaming quotes
- No subscriptions, no data sharing
```

Save to: `src/LimitlessTrading/LimitlessTrading.Web/src/content/releases/v1-0-0.mdx`

- [ ] **Step 3: Create releases.ts data file**

```typescript
// src/LimitlessTrading/LimitlessTrading.Web/src/data/releases.ts

export const CURRENT_VERSION = '1.0.0';

export interface PlatformRelease {
  platform: 'Windows' | 'macOS' | 'Linux';
  label: string;
  ext: string;
  size: string;
  url: string;
  checksum: string;
}

export const platformReleases: PlatformRelease[] = [
  {
    platform: 'Windows',
    label: 'Windows',
    ext: '.exe',
    size: '—',
    url: '#',
    checksum: '—',
  },
  {
    platform: 'macOS',
    label: 'macOS',
    ext: '.dmg',
    size: '—',
    url: '#',
    checksum: '—',
  },
  {
    platform: 'Linux',
    label: 'Linux',
    ext: '.AppImage',
    size: '—',
    url: '#',
    checksum: '—',
  },
];
```

> **Note:** Replace `url`, `size`, and `checksum` values when real binaries are ready. The `'#'` placeholder keeps the download buttons present but inactive at launch.

- [ ] **Step 4: Verify TypeScript types check**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run check 2>&1 | tail -10
```

Expected: No type errors related to the content collection or data file.

- [ ] **Step 5: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add content collection schema and releases data"
```

---

## Task 4: Home page

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/HeroSection.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/FeatureCard.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/index.astro`

- [ ] **Step 1: Create HeroSection.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/HeroSection.astro
---

<section class="relative px-12 pt-20 pb-16 min-h-[85vh] flex flex-col justify-between grid-bg">
  <div>
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-8">
      Belgrade, Serbia — Trading Software
    </p>
    <span class="absolute right-12 top-20 text-[11px] tracking-[0.2em] text-ls-text-dim">01 / 04</span>

    <h1 class="text-[clamp(64px,10vw,120px)] font-black leading-[0.92] uppercase tracking-[-2px] max-w-5xl">
      <span class="block text-white">YOUR</span>
      <span class="block text-white">TRADING</span>
      <span class="block text-ls-blue">DASHBOARD</span>
      <span class="block text-outline">WITHOUT LIMITS</span>
    </h1>
  </div>

  <div class="flex items-end justify-between gap-10 mt-16">
    <p class="max-w-sm text-sm leading-relaxed text-ls-text">
      Connect your broker API, build any UI with widgets and templates, share with the community,
      and keep all your data on your own machine. No subscriptions. No data sharing. No limits.
    </p>
    <div class="flex items-center gap-4 flex-shrink-0">
      <a href="/downloads" class="bg-ls-blue text-white px-8 py-3.5 text-xs font-bold tracking-[0.2em] uppercase hover:bg-blue-500 transition-colors">
        Download Free
      </a>
      <a href="/demos" class="text-xs tracking-[0.15em] uppercase text-ls-text border-b border-ls-border pb-0.5 hover:text-white hover:border-white transition-colors">
        Watch Demo →
      </a>
    </div>
  </div>
</section>
```

- [ ] **Step 2: Create FeatureCard.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/FeatureCard.astro
interface Props {
  number: string;
  title: string;
  description: string;
}

const { number, title, description } = Astro.props;
---

<div class="bg-white/[0.02] border border-ls-border p-8 hover:border-ls-blue/50 hover:bg-ls-blue-dim transition-all">
  <p class="text-[10px] tracking-[0.2em] uppercase text-ls-text-dim mb-4">{number}</p>
  <h3 class="text-base font-black uppercase tracking-[-0.3px] text-white mb-3">{title}</h3>
  <p class="text-xs leading-relaxed text-ls-text">{description}</p>
</div>
```

- [ ] **Step 3: Create index.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/index.astro
import BaseLayout from '@layouts/BaseLayout.astro';
import Ticker from '@components/Ticker.astro';
import HeroSection from '@components/HeroSection.astro';
import FeatureCard from '@components/FeatureCard.astro';

const features = [
  {
    number: '01',
    title: 'Widgets & Expressions',
    description: 'Write expressions that fetch live data from your broker. Attach them to any widget — charts, tables, alerts, or custom displays you build yourself.',
  },
  {
    number: '02',
    title: 'Templates & Gallery',
    description: 'Save your dashboard layouts as templates. Share them with the community gallery, or import layouts built by other traders in seconds.',
  },
  {
    number: '03',
    title: 'Self-Hosted & Private',
    description: 'Everything runs on your machine. Market data, watchlists, preferences — none of it leaves your computer. No accounts, no cloud, no subscriptions.',
  },
];

const stats = [
  { value: '100%', label: 'Self-Hosted' },
  { value: '$0', label: 'Subscription Fee' },
  { value: '∞', label: 'Customization' },
  { value: 'IBKR+', label: 'Broker Support' },
];
---

<BaseLayout title="Home">
  <Ticker />
  <HeroSection />

  <!-- Stats bar -->
  <div class="grid grid-cols-4 border-t border-ls-border">
    {stats.map(stat => (
      <div class="px-12 py-7 border-r border-ls-border last:border-r-0">
        <p class="text-4xl font-black tracking-[-1px] text-white">
          {stat.value.includes('$') || stat.value.includes('%')
            ? stat.value
            : <>{stat.value}<span class="text-ls-blue"></span></>
          }
        </p>
        <p class="text-[10px] tracking-[0.2em] uppercase text-ls-text-dim mt-1">{stat.label}</p>
      </div>
    ))}
  </div>

  <!-- Feature highlights -->
  <section class="px-12 py-20">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">Why Limitless Trading</p>
    <h2 class="text-4xl font-black uppercase tracking-[-1px] text-white mb-12">
      BUILD ANY<br /><span class="text-ls-blue">DASHBOARD</span>
    </h2>
    <div class="grid grid-cols-3 gap-0.5">
      {features.map(f => <FeatureCard number={f.number} title={f.title} description={f.description} />)}
    </div>
  </section>

  <!-- Demo embed placeholder -->
  <section class="px-12 pb-20">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4 text-center">See it in action</p>
    <h2 class="text-3xl font-black uppercase tracking-[-1px] text-white mb-10 text-center">LIVE DEMO</h2>
    <div class="border border-ls-border bg-white/[0.02] flex items-center justify-center h-80 max-w-4xl mx-auto">
      <p class="text-xs tracking-[0.2em] uppercase text-ls-text-dim">Demo video coming soon</p>
    </div>
    <div class="text-center mt-8">
      <a href="/demos" class="text-xs tracking-[0.15em] uppercase text-ls-blue border-b border-ls-blue/40 pb-0.5 hover:border-ls-blue transition-colors">
        View all demos →
      </a>
    </div>
  </section>

  <!-- CTA banner -->
  <section class="border-t border-ls-border px-12 py-20 grid-bg">
    <div class="max-w-2xl">
      <h2 class="text-5xl font-black uppercase tracking-[-1.5px] text-white leading-[0.95] mb-8">
        NO SUBSCRIPTIONS.<br />
        NO DATA SHARING.<br />
        <span class="text-ls-blue">NO LIMITS.</span>
      </h2>
      <div class="flex gap-4 items-center">
        <a href="/downloads" class="bg-ls-blue text-white px-8 py-3.5 text-xs font-bold tracking-[0.2em] uppercase hover:bg-blue-500 transition-colors">
          Download Free
        </a>
        <a href="/whats-new" class="text-xs tracking-[0.15em] uppercase text-ls-text border-b border-ls-border pb-0.5 hover:text-white hover:border-white transition-colors">
          What's New →
        </a>
      </div>
    </div>
  </section>
</BaseLayout>
```

- [ ] **Step 4: Start dev server and verify the home page renders**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run dev
```

Open http://localhost:4321 and verify:
- Nav with logo and links appears
- Ticker animates across the page
- Hero shows large stacked type (YOUR / TRADING / DASHBOARD / WITHOUT LIMITS)
- Stats bar shows four columns
- Three feature cards render
- CTA banner at the bottom

Stop the server with Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add home page"
```

---

## Task 5: What's New page

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/ReleaseEntry.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/whats-new.astro`

- [ ] **Step 1: Create ReleaseEntry.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/ReleaseEntry.astro
interface Props {
  version: string;
  date: string;
  type: 'Major' | 'Feature' | 'Fix';
  summary: string;
  isLatest: boolean;
}

const { version, date, type, summary, isLatest } = Astro.props;

const typeBg: Record<string, string> = {
  Major:   'bg-ls-blue/10 border-ls-blue/40 text-ls-blue',
  Feature: 'bg-white/5 border-white/10 text-white/50',
  Fix:     'bg-white/5 border-white/10 text-white/50',
};
---

<article class="border-b border-ls-border py-10 first:pt-0">
  <div class="flex items-start gap-6 mb-5">
    <h2 class={`text-2xl font-black uppercase tracking-[-0.5px] ${isLatest ? 'text-white' : 'text-white/40'}`}>
      v{version}
    </h2>

    {isLatest && (
      <span class="text-[10px] tracking-[0.2em] uppercase px-2.5 py-1 bg-ls-blue/10 border border-ls-blue/40 text-ls-blue">
        Latest
      </span>
    )}

    <span class={`text-[10px] tracking-[0.15em] uppercase px-2.5 py-1 border ${typeBg[type]}`}>
      {type}
    </span>

    <time class="text-xs tracking-widest uppercase text-ls-text-dim ml-auto">{date}</time>
  </div>

  <p class={`text-sm leading-relaxed mb-4 ${isLatest ? 'text-ls-text' : 'text-white/30'}`}>
    {summary}
  </p>

  <div class={`prose prose-invert prose-sm max-w-none ${isLatest ? '' : 'opacity-40'}`}>
    <slot />
  </div>
</article>
```

- [ ] **Step 2: Create whats-new.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/whats-new.astro
import { getCollection } from 'astro:content';
import BaseLayout from '@layouts/BaseLayout.astro';
import ReleaseEntry from '@components/ReleaseEntry.astro';

const releases = (await getCollection('releases')).sort((a, b) => {
  return b.data.version.localeCompare(a.data.version, undefined, { numeric: true });
});
---

<BaseLayout title="What's New" description="Release notes and changelog for Limitless Trading.">
  <div class="px-12 py-20 grid-bg">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">Changelog</p>
    <h1 class="text-5xl font-black uppercase tracking-[-1.5px] text-white mb-2">
      WHAT'S <span class="text-ls-blue">NEW</span>
    </h1>
    <p class="text-sm text-ls-text mb-16">Every release, every change.</p>

    <div class="max-w-3xl">
      {releases.map(async (release, i) => {
        const { Content } = await release.render();
        return (
          <ReleaseEntry
            version={release.data.version}
            date={release.data.date}
            type={release.data.type}
            summary={release.data.summary}
            isLatest={i === 0}
          >
            <Content />
          </ReleaseEntry>
        );
      })}
    </div>
  </div>
</BaseLayout>
```

- [ ] **Step 3: Verify in dev server**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run dev
```

Open http://localhost:4321/whats-new and verify:
- Page header shows "WHAT'S NEW" with blue accent
- The v1.0.0 release entry renders with "Latest" badge
- The MDX body (bullet list) renders below the summary

Stop the server with Ctrl+C.

- [ ] **Step 4: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add What's New page with content collection"
```

---

## Task 6: Downloads page

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/components/DownloadCard.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/downloads.astro`

- [ ] **Step 1: Create DownloadCard.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/components/DownloadCard.astro
import type { PlatformRelease } from '@data/releases';

interface Props {
  release: PlatformRelease;
}

const { release } = Astro.props;

const icons: Record<string, string> = {
  Windows: '⊞',
  macOS: '',
  Linux: '🐧',
};
---

<div class="bg-white/[0.02] border border-ls-border p-8 flex flex-col gap-6 hover:border-ls-blue/50 hover:bg-ls-blue-dim transition-all">
  <div>
    <p class="text-4xl mb-4">{icons[release.platform]}</p>
    <h3 class="text-xl font-black uppercase tracking-[-0.5px] text-white">{release.label}</h3>
    <p class="text-xs tracking-widest uppercase text-ls-text-dim mt-1">{release.ext}</p>
  </div>

  {release.size !== '—' && (
    <p class="text-xs text-ls-text">{release.size}</p>
  )}

  <a
    href={release.url}
    class={`text-center py-3 text-xs font-bold tracking-[0.2em] uppercase transition-colors ${
      release.url === '#'
        ? 'bg-white/5 text-white/30 cursor-not-allowed border border-ls-border'
        : 'bg-ls-blue text-white hover:bg-blue-500'
    }`}
    aria-disabled={release.url === '#'}
  >
    {release.url === '#' ? 'Coming Soon' : `Download ${release.ext}`}
  </a>
</div>
```

- [ ] **Step 2: Create downloads.astro**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/downloads.astro
import BaseLayout from '@layouts/BaseLayout.astro';
import DownloadCard from '@components/DownloadCard.astro';
import { platformReleases, CURRENT_VERSION } from '@data/releases';

const requirements = [
  'Interactive Brokers TWS or IB Gateway (paper or live)',
  'Windows 10+, macOS 12+, or Ubuntu 20.04+',
  'Node.js 18+ (bundled in installer)',
  '4 GB RAM minimum recommended',
];

const steps = [
  'Download the installer for your platform',
  'Run the installer and follow the prompts',
  'Open Interactive Brokers TWS or IB Gateway',
  'Launch Limitless Trading — it connects automatically',
];

const checksums = platformReleases.map(r => ({ platform: r.platform, checksum: r.checksum }));
---

<BaseLayout title="Downloads" description="Download Limitless Trading for Windows, macOS, or Linux.">
  <div class="px-12 py-20 grid-bg">

    <!-- Header -->
    <div class="mb-16">
      <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">Current Release</p>
      <h1 class="text-5xl font-black uppercase tracking-[-1.5px] text-white mb-2">
        DOWNLOAD <span class="text-ls-blue">v{CURRENT_VERSION}</span>
      </h1>
      <p class="text-sm text-ls-text">Free. Self-hosted. No account required.</p>
    </div>

    <!-- Platform cards -->
    <div class="grid grid-cols-3 gap-0.5 mb-20 max-w-4xl">
      {platformReleases.map(r => <DownloadCard release={r} />)}
    </div>

    <!-- Requirements -->
    <div class="mb-16 max-w-2xl">
      <h2 class="text-lg font-black uppercase tracking-[-0.3px] text-white mb-6">Requirements</h2>
      <ul class="flex flex-col gap-3">
        {requirements.map(req => (
          <li class="flex items-start gap-3 text-sm text-ls-text">
            <span class="text-ls-blue mt-0.5 flex-shrink-0">—</span>
            {req}
          </li>
        ))}
      </ul>
    </div>

    <!-- Quick start -->
    <div class="mb-16 max-w-2xl">
      <h2 class="text-lg font-black uppercase tracking-[-0.3px] text-white mb-6">Quick Start</h2>
      <ol class="flex flex-col gap-4">
        {steps.map((step, i) => (
          <li class="flex items-start gap-4">
            <span class="text-xs font-black tracking-widest text-ls-blue flex-shrink-0 mt-0.5">
              0{i + 1}
            </span>
            <span class="text-sm text-ls-text">{step}</span>
          </li>
        ))}
      </ol>
    </div>

    <!-- Checksums (collapsible) -->
    <div class="max-w-2xl">
      <details class="border border-ls-border">
        <summary class="px-6 py-4 text-xs font-bold tracking-[0.2em] uppercase text-ls-text cursor-pointer hover:text-white transition-colors">
          SHA256 Checksums
        </summary>
        <div class="px-6 pb-6 pt-2 flex flex-col gap-3">
          {checksums.map(c => (
            <div class="flex items-start gap-4">
              <span class="text-[10px] tracking-widest uppercase text-ls-text-dim w-20 flex-shrink-0 mt-0.5">
                {c.platform}
              </span>
              <code class="text-xs font-mono text-ls-text break-all">
                {c.checksum === '—' ? 'Available with first binary release' : c.checksum}
              </code>
            </div>
          ))}
        </div>
      </details>
    </div>

  </div>
</BaseLayout>
```

- [ ] **Step 3: Verify in dev server**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run dev
```

Open http://localhost:4321/downloads and verify:
- Three platform cards show "Coming Soon" buttons (since URLs are `#`)
- Requirements list renders
- Quick start numbered steps render
- Checksums section is collapsible and shows "Available with first binary release"

Stop the server with Ctrl+C.

- [ ] **Step 4: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add Downloads page"
```

---

## Task 7: Scaffold post-launch pages

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/features.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/demos.astro`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/src/pages/about.astro`

- [ ] **Step 1: Create features.astro stub**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/features.astro
import BaseLayout from '@layouts/BaseLayout.astro';
---

<BaseLayout title="Features">
  <div class="px-12 py-20 grid-bg min-h-[60vh] flex flex-col justify-center">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">Platform</p>
    <h1 class="text-5xl font-black uppercase tracking-[-1.5px] text-white mb-4">
      FEATURES
    </h1>
    <p class="text-sm text-ls-text max-w-md">Full feature breakdown coming soon.</p>
  </div>
</BaseLayout>
```

- [ ] **Step 2: Create demos.astro stub**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/demos.astro
import BaseLayout from '@layouts/BaseLayout.astro';
---

<BaseLayout title="Demos">
  <div class="px-12 py-20 grid-bg min-h-[60vh] flex flex-col justify-center">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">See it in action</p>
    <h1 class="text-5xl font-black uppercase tracking-[-1.5px] text-white mb-4">
      DEMOS
    </h1>
    <p class="text-sm text-ls-text max-w-md">Demo recordings coming soon.</p>
  </div>
</BaseLayout>
```

- [ ] **Step 3: Create about.astro stub**

```astro
---
// src/LimitlessTrading/LimitlessTrading.Web/src/pages/about.astro
import BaseLayout from '@layouts/BaseLayout.astro';
---

<BaseLayout title="About">
  <div class="px-12 py-20 grid-bg min-h-[60vh] flex flex-col justify-center">
    <p class="text-[11px] tracking-[0.3em] uppercase text-ls-text-dim mb-4">The story</p>
    <h1 class="text-5xl font-black uppercase tracking-[-1.5px] text-white mb-4">
      ABOUT
    </h1>
    <p class="text-sm text-ls-text max-w-lg mb-6">
      Limitless Trading is built by <a href="https://limitlesssoft.com" target="_blank" rel="noopener" class="text-ls-blue hover:underline">LimitlessSoft</a> — a software engineering company from Belgrade, Serbia.
    </p>
    <p class="text-sm text-ls-text max-w-lg">
      We built Limitless Trading because traders deserve full control over their tools — their data, their UI, their dashboard. No subscriptions. No cloud. No limits.
    </p>
  </div>
</BaseLayout>
```

- [ ] **Step 4: Run a full production build to verify all pages compile**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run build 2>&1 | tail -15
```

Expected output includes:
```
✓ Built in ...
dist/index.html
dist/downloads/index.html
dist/whats-new/index.html
dist/features/index.html
dist/demos/index.html
dist/about/index.html
```

No errors or warnings about missing types or broken imports.

- [ ] **Step 5: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/
git commit -m "Add stub pages for features, demos, about"
```

---

## Task 8: Dockerfile and nginx config

**Files:**
- Create: `src/LimitlessTrading/LimitlessTrading.Web/nginx.conf`
- Create: `src/LimitlessTrading/LimitlessTrading.Web/Dockerfile`

- [ ] **Step 1: Create nginx.conf**

```nginx
# src/LimitlessTrading/LimitlessTrading.Web/nginx.conf
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ $uri.html /404.html =404;
    }

    gzip on;
    gzip_types text/plain text/css application/javascript application/json image/svg+xml;
    gzip_min_length 1024;
}
```

- [ ] **Step 2: Create Dockerfile**

```dockerfile
# src/LimitlessTrading/LimitlessTrading.Web/Dockerfile
FROM node:20-alpine AS builder

WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
```

- [ ] **Step 3: Build the Docker image locally to verify**

```bash
docker build -f src/LimitlessTrading/LimitlessTrading.Web/Dockerfile -t lt-web:local src/LimitlessTrading/LimitlessTrading.Web
```

Expected: Image builds successfully. Last line should be something like:
```
Successfully built <id>
Successfully tagged lt-web:local
```

- [ ] **Step 4: Run the image and verify the site serves**

```bash
docker run -p 8090:80 lt-web:local
```

Open http://localhost:8090 in a browser. Verify the home page loads — nav, ticker, hero. Stop the container with Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add src/LimitlessTrading/LimitlessTrading.Web/Dockerfile src/LimitlessTrading/LimitlessTrading.Web/nginx.conf
git commit -m "Add Dockerfile and nginx config"
```

---

## Task 9: Kubernetes manifests

**Files:**
- Create: `k8s/namespaces/lt-web-namespace.yaml`
- Create: `k8s/services/lt-web-service.yaml`
- Create: `k8s/deployments/lt-web-deployment.yaml`
- Create: `k8s/ingress/lt-web-ingress.yaml`

- [ ] **Step 1: Create lt-web-namespace.yaml**

```yaml
# k8s/namespaces/lt-web-namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: lt-web-namespace
```

- [ ] **Step 2: Create lt-web-service.yaml**

```yaml
# k8s/services/lt-web-service.yaml
apiVersion: v1
kind: Service
metadata:
  name: ${DEPLOY_ENV}-lt-web-service
  namespace: lt-web-namespace
spec:
  selector:
    app: ${DEPLOY_ENV}-lt-web
  ports:
    - protocol: TCP
      port: 80
      targetPort: 80
  type: NodePort
```

- [ ] **Step 3: Create lt-web-deployment.yaml**

```yaml
# k8s/deployments/lt-web-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ${DEPLOY_ENV}-lt-web-deployment
  namespace: lt-web-namespace
spec:
  replicas: 1
  selector:
    matchLabels:
      app: ${DEPLOY_ENV}-lt-web
  template:
    metadata:
      labels:
        app: ${DEPLOY_ENV}-lt-web
    spec:
      containers:
      - name: ${DEPLOY_ENV}-lt-web
        image: limitlesssoft/lt-web:${DEPLOY_ENV}
        imagePullPolicy: Always
        ports:
        - containerPort: 80
        resources:
          requests:
            cpu: "50m"
            memory: "32Mi"
          limits:
            cpu: "200m"
            memory: "64Mi"

      imagePullSecrets:
      - name: regcred
```

- [ ] **Step 4: Create lt-web-ingress.yaml**

```yaml
# k8s/ingress/lt-web-ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: lt-web-ingress
  namespace: lt-web-namespace
  annotations:
    kubernetes.io/ingress.class: nginx
spec:
  rules:
  - host: limitlesstrading.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: ${DEPLOY_ENV}-lt-web-service
            port:
              number: 80
```

> **Note:** The ingress host is `limitlesstrading.com` (not the `*.aleksaristic.com` pattern) — this is the public domain, so no env-substituted subdomain is needed. The service name still uses `${DEPLOY_ENV}` so staging and production deployments use different services.

- [ ] **Step 5: Commit**

```bash
git add k8s/namespaces/lt-web-namespace.yaml k8s/services/lt-web-service.yaml k8s/deployments/lt-web-deployment.yaml k8s/ingress/lt-web-ingress.yaml
git commit -m "Add k8s manifests for lt-web"
```

---

## Task 10: GitHub Actions CI/CD workflows

**Files:**
- Create: `.github/workflows/release-web.yml`
- Create: `.github/workflows/build-and-publish-web.yml`
- Create: `.github/workflows/deploy-web.yml`

- [ ] **Step 1: Create release-web.yml**

```yaml
# .github/workflows/release-web.yml
name: Release production lt-web

on:
  workflow_dispatch:
  push:
    branches: [ "master" ]
    paths:
      - 'src/LimitlessTrading/LimitlessTrading.Web/**'
      - 'k8s/deployments/lt-web-deployment.yaml'
      - 'k8s/services/lt-web-service.yaml'
      - 'k8s/ingress/lt-web-ingress.yaml'
      - 'k8s/namespaces/lt-web-namespace.yaml'

jobs:
  web:
    uses: AleksaRistic216/LimitlessTrading/.github/workflows/build-and-publish-web.yml@master
    secrets: inherit
    with:
      env: production
```

- [ ] **Step 2: Create build-and-publish-web.yml**

```yaml
# .github/workflows/build-and-publish-web.yml
name: Build and push lt-web docker image, then start deploying action

on:
  workflow_call:
    inputs:
      env:
        required: true
        type: string

jobs:
  build-and-push-web:
    environment: ${{ inputs.env }}
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3

    - name: Build docker image
      run: docker build --file src/LimitlessTrading/LimitlessTrading.Web/Dockerfile -t limitlesssoft/lt-web:${{ inputs.env }} src/LimitlessTrading/LimitlessTrading.Web

    - name: Login to Docker Hub
      uses: docker/login-action@v3
      with:
        registry: https://index.docker.io/v1/
        username: ${{ secrets.DOCKER_HUB_USERNAME }}
        password: ${{ secrets.DOCKER_HUB_PASSWORD }}

    - name: Push docker image
      run: docker push limitlesssoft/lt-web:${{ inputs.env }}

  deploy:
    uses: AleksaRistic216/LimitlessTrading/.github/workflows/deploy-web.yml@master
    needs: [ build-and-push-web ]
    secrets: inherit
    with:
      env: ${{ inputs.env }}
```

- [ ] **Step 3: Create deploy-web.yml**

```yaml
# .github/workflows/deploy-web.yml
name: Deploy lt-web to cluster

on:
  workflow_call:
    inputs:
      env:
        required: true
        type: string

jobs:
  apply-resources:
    runs-on: ubuntu-latest
    environment: ${{ inputs.env }}
    steps:
    - uses: actions/checkout@v3

    - name: Set K8s context
      uses: ossrs/k8s-set-context-action@v1
      with:
        kubeconfig: '${{ secrets.KUBE_CONFIG_PURE }}'

    - name: Deploy lt-web to cluster
      run: |-
        export DEPLOY_ENV=${{ inputs.env }}
        envsubst < k8s/namespaces/lt-web-namespace.yaml | kubectl apply -f -
        envsubst < k8s/services/lt-web-service.yaml | kubectl apply -f -
        envsubst < k8s/deployments/lt-web-deployment.yaml | kubectl apply -f -
        envsubst < k8s/ingress/lt-web-ingress.yaml | kubectl apply -f -
        kubectl rollout restart -n lt-web-namespace deployment ${{ inputs.env }}-lt-web-deployment
```

- [ ] **Step 4: Verify workflow files are valid YAML**

```bash
python3 -c "
import yaml, sys
for f in ['.github/workflows/release-web.yml', '.github/workflows/build-and-publish-web.yml', '.github/workflows/deploy-web.yml']:
    yaml.safe_load(open(f))
    print(f'OK: {f}')
"
```

Expected:
```
OK: .github/workflows/release-web.yml
OK: .github/workflows/build-and-publish-web.yml
OK: .github/workflows/deploy-web.yml
```

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/release-web.yml .github/workflows/build-and-publish-web.yml .github/workflows/deploy-web.yml
git commit -m "Add CI/CD workflows for lt-web"
```

---

## Task 11: Final production build verification

- [ ] **Step 1: Run full production build**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run build 2>&1
```

Expected: Clean build with all 6 pages output to `dist/`. No TypeScript errors, no broken imports.

- [ ] **Step 2: Run Astro type check**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run check 2>&1
```

Expected: `0 errors, 0 warnings`.

- [ ] **Step 3: Preview production build**

```bash
cd src/LimitlessTrading/LimitlessTrading.Web && npm run preview
```

Open http://localhost:4321 and verify all six pages load:
- `/` — Home (ticker, hero, stats, features, demo placeholder, CTA)
- `/downloads` — Platform cards, requirements, quick start, checksums
- `/whats-new` — v1.0.0 entry with "Latest" badge
- `/features` — Stub page
- `/demos` — Stub page
- `/about` — Stub with LimitlessSoft link

Check that nav links between pages work. Stop the server.

- [ ] **Step 4: Add .superpowers to .gitignore**

Add to `.gitignore`:

```
.superpowers/
```

- [ ] **Step 5: Final commit**

```bash
git add .gitignore
git commit -m "Ignore .superpowers brainstorm files"
```

---

## Post-Launch: Updating content

### Adding a new release to What's New

1. Create `src/LimitlessTrading/LimitlessTrading.Web/src/content/releases/v{version}.mdx` using the same frontmatter schema as `v1-0-0.mdx`.
2. Update `CURRENT_VERSION` and the download URL/size/checksum in `src/LimitlessTrading/LimitlessTrading.Web/src/data/releases.ts`.
3. Push to master — CI deploys automatically.

### Adding a demo video

Add a `<video>` or `<iframe>` embed into `src/LimitlessTrading/LimitlessTrading.Web/src/pages/demos.astro` replacing the stub content.
