# Limitless Trading Website — Design Spec

**Date:** 2026-05-04  
**Status:** Approved

## Overview

A new public-facing marketing and distribution website for Limitless Trading, deployed to the existing Kubernetes cabinet cluster. The site presents the platform, serves download binaries, shows demos, and publishes release notes. Visual style matches limitlesssoft.com — near-black background, massive display typography, electric blue (`#0057ff`) accent, subtle grid overlay.

**Domain:** limitlesstrading.com  
**Stack:** Astro + Tailwind CSS  
**Container:** nginx Alpine (static files)  
**Deployment:** Kubernetes (cabinet cluster), same pattern as the existing LT API

---

## Architecture

### Build & Deploy Pipeline

```
Astro source → GitHub Actions (astro build) → Docker image (nginx + /dist) → Docker Hub → Kubernetes → limitlesstrading.com
```

### Framework

**Astro + Tailwind CSS.** Astro builds to pure static HTML at deploy time — no JavaScript runtime in production, no React hydration cost. Tailwind implements the LimitlessSoft visual style. React components are used selectively for interactive elements (download buttons, video embeds). Astro Content Collections power the What's New changelog via MDX files.

### Container

nginx Alpine Docker image. The CI build runs `astro build`, then copies `/dist` into a minimal nginx Alpine image. No Node.js runtime in the container. Image pushed to Docker Hub as `limitlesssoft/lt-web:{env}`.

### Content — What's New

Each release gets an MDX file at `src/content/releases/v{version}.mdx`. Astro Content Collections generates the What's New page automatically. Adding a release = add one MDX file, push, CI deploys. No CMS required.

### Content — Downloads

Download links and version info stored in `src/data/releases.ts`. Updated manually alongside each MDX changelog entry. GitHub Releases API integration is a straightforward future upgrade with no structural changes needed.

### Kubernetes

Mirrors the existing LT API pattern exactly using `envsubst` for environment substitution:

| Manifest | Value |
|---|---|
| Namespace | `lt-web-namespace` |
| Image | `limitlesssoft/lt-web:{env}` |
| Host | `limitlesstrading.com` |
| Port | 80 → 80 (nginx) |

### CI/CD

Three GitHub Actions workflows, identical structure to the existing API workflows:

| File | Purpose |
|---|---|
| `release-web.yml` | Triggers on push to master (web paths) or `workflow_dispatch` |
| `build-and-publish-web.yml` | Runs `astro build`, builds and pushes Docker image, calls deploy |
| `deploy-web.yml` | Applies k8s manifests via `envsubst`, restarts deployment |

**No new secrets required.** `DOCKER_HUB_USERNAME`, `DOCKER_HUB_PASSWORD`, and `KUBE_CONFIG_PURE` already exist in the repo.

---

## Pages

### Launch Priority

#### Home (`/`)

| Section | Description |
|---|---|
| Nav | Logo left, links center (Features · Demos · What's New · Downloads), Download button right. Sticky on scroll with blur backdrop. |
| Ticker | Animated horizontal scroll of stock symbols with mock prices. Atmosphere, not live data. |
| Hero | Massive display type ("YOUR TRADING DASHBOARD / WITHOUT LIMITS"), positioning statement body, Download Free + Watch Demo CTAs, slide counter. |
| Stats Bar | 100% Self-Hosted · $0 Subscription · ∞ Customization · IBKR+. Horizontal separator. |
| Feature Highlights | Three feature cards: Widgets & Expressions · Templates & Gallery · Self-Hosted & Private. Each has title, description, and screenshot/GIF placeholder. |
| Demo Embed | Centered video or GIF embed. "See it in action" heading. Links to Demos page. |
| CTA Banner | Full-width: "No subscriptions. No data sharing. No limits." Download Free button + What's New link. |
| Footer | Logo, nav links, LimitlessSoft link, current version number. |

#### Downloads (`/downloads`)

| Section | Description |
|---|---|
| Header | Current version number, release date, brief description. |
| Platform Cards | Windows (.exe), macOS (.dmg), Linux (.AppImage / .deb). Each shows platform icon, file size, download button. |
| Requirements | Node.js version, IBKR TWS/Gateway, supported OS versions. |
| Quick Start | Numbered steps: Download → Install → Open TWS → Launch. Links to docs when available. |
| Checksum | Collapsible SHA256 checksums per platform. |

#### What's New (`/whats-new`)

| Section | Description |
|---|---|
| Release List | Reverse-chronological entries from Astro Content Collections. |
| Release Entry | Version, date, type badge (Major / Feature / Fix), MDX body. |
| Latest Badge | Most recent release marked with blue "Latest" badge. |

### After Launch

#### Features (`/features`)

Alternating left/right image+text sections per capability: Widgets & Expressions, Templates, Community Gallery, IBKR Integration, Self-Hosting. Includes a comparison table against TradingView and TWS.

#### Demos (`/demos`)

Grid of demo cards — title, description, embedded video or GIF. Populated as recordings are produced.

#### About (`/about`)

Origin story, self-hosted philosophy, zero-subscription commitment. Links to limitlesssoft.com.

---

## File Structure

```
src/
  LimitlessTrading/
    LimitlessTrading.Public/
      LimitlessTrading.Public.Fe/       # existing app — unchanged
    LimitlessTrading.Web/               # new
      src/
        pages/
          index.astro                   # Home
          downloads.astro
          whats-new.astro
          features.astro
          demos.astro
          about.astro
        components/
          Nav.astro
          Footer.astro
          Ticker.astro
          HeroSection.astro
          DownloadCard.astro
          ReleaseEntry.astro
        content/
          releases/
            v1-0-0.mdx                  # What's New entries
        data/
          releases.ts                   # download links + versions
        styles/
          global.css
      astro.config.mjs
      tailwind.config.mjs
      Dockerfile
      package.json

k8s/
  namespaces/lt-web-namespace.yaml
  deployments/lt-web-deployment.yaml
  services/lt-web-service.yaml
  ingresses/lt-web-ingress.yaml

.github/workflows/
  release-web.yml
  build-and-publish-web.yml
  deploy-web.yml
```

---

## Visual Style

Matches limitlesssoft.com:

- **Background:** `#080808` (near-black) with subtle grid overlay
- **Accent:** `#0057ff` (electric blue)
- **Typography:** Extra-bold uppercase display type, mixed fills — white filled, blue filled, outline/stroke
- **Ticker:** Animated horizontal scroll, atmospheric mock data
- **Buttons:** Sharp corners (no border-radius), uppercase labels, letter-spacing

---

## Deferred Decisions

- **Download source:** Currently manual config file (`src/data/releases.ts`). GitHub Releases API is a drop-in future upgrade.
- **Email capture:** Not in scope for launch. Can be added to the CTA banner later (Mailchimp / Beehiiv embed).
- **Demos content:** Page scaffolded at launch; populated once recordings are produced.
- **Features / About content:** Pages scaffolded; copy and screenshots filled in after launch.
