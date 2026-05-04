# Marketing Plan

ARTradingTools is a trading analysis toolkit targeting active retail traders and IBKR users. This document is a step-by-step execution plan for building a marketing presence online.

---

## Phase 1 — Foundation (Week 1–2)

Do this before posting anywhere. Rushing distribution before the foundation is ready wastes momentum.

### Step 1: Define your positioning statement

Write one sentence that answers: who is this for, what does it do, and why is it better than alternatives?

Example:
> "ARTradingTools is a self-hosted trading dashboard for Interactive Brokers users — real-time watchlists, custom alerts, and candlestick charts with no monthly fees."

Put this sentence everywhere: GitHub README, landing page headline, Reddit bio, Twitter bio.

### Step 2: Clean up the GitHub repository

The GitHub repo is your product page for developers. It must be ready before you drive any traffic.

- [ ] Write a complete `README.md`:
  - One-paragraph description using your positioning statement
  - Animated GIF or screenshot of the dashboard in action (most important thing on the page)
  - Feature list (watchlist, alerts, charts, IBKR integration, template widgets)
  - Prerequisites (Node.js, IBKR TWS, .NET 9)
  - Quick-start install instructions — keep it under 10 steps
  - Link to `docs/` for deeper documentation
- [ ] Add a `LICENSE` file (MIT is fine for visibility)
- [ ] Add topics/tags to the repo: `trading`, `interactive-brokers`, `ibkr`, `dashboard`, `react`, `fintech`
- [ ] Pin the repo to your GitHub profile

### Step 3: Create a demo GIF or video

This is the highest-leverage content asset you will make. One short recording will be reused everywhere.

- Record a 30–60 second screen capture showing:
  - The live watchlist streaming
  - An alert triggering
  - Switching between chart views
- Tools: OBS (free), Loom, or macOS built-in screen recording
- Convert to GIF for the README (use ezgif.com or ffmpeg)
- Keep the full MP4 for YouTube and Twitter

### Step 4: Set up a simple landing page

You do not need a custom design. Use one of these zero-cost options:

- **GitHub Pages** — free, just add an `index.html` or use Jekyll
- **Carrd.co** — drag-and-drop, free tier is enough
- **Notion public page** — fastest to set up

The landing page needs only four things:
1. Headline (your positioning statement)
2. The demo video or GIF
3. A short feature list
4. A call to action: "Star on GitHub" and/or an email signup form

For email capture use [Mailchimp](https://mailchimp.com) (free up to 500 contacts) or [Beehiiv](https://beehiiv.com) (free, newsletter-first).

---

## Phase 2 — First Distribution (Week 3–4)

### Step 5: Post on Reddit

Reddit is the fastest way to reach IBKR traders. Post on these subreddits in this order:

1. **r/interactivebrokers** — most targeted, users already use IBKR
2. **r/algotrading** — algo and quant traders, appreciate technical depth
3. **r/trading** — broader audience, lower conversion but higher reach
4. **r/Python** or **r/webdev** — if you angle it as a dev project

Post format that works:
```
Title: "I built a self-hosted trading dashboard for IBKR — real-time watchlist, 
        alerts, and charts with no subscription fees [OC]"

Body:
- 2–3 sentences on what it does and why you built it
- Embed the GIF or screenshot directly in the post
- Link to GitHub
- End with: "Happy to answer questions about the setup or how it connects to TWS"
```

Rules:
- Post on weekdays between 8–10am EST for best visibility
- Do not post to multiple subreddits on the same day — wait 48 hours between posts
- Engage in comments for the first 2 hours after posting
- Do not lead with "I'm looking for feedback" — lead with the value

### Step 6: Post on Twitter/X

Twitter has an active fintech and dev-trader community (fintwit). It compounds slowly but is worth starting now.

- Create or clean up your account with a clear bio: "Building trading tools for IBKR users | @ARTradingTools"
- Tweet the demo video with a short description thread:
  - Tweet 1: hook — "I built a self-hosted IBKR trading dashboard. Here's what it looks like:"
  - Tweet 2: key features (bullet list)
  - Tweet 3: link to GitHub and landing page
- Tag relevant accounts: `@IBKR`, `@TradingView` if referencing their charts library
- Use hashtags: `#trading`, `#IBKR`, `#algotrading`, `#fintech`, `#buildinpublic`

Post the thread once. Do not spam. Then engage by replying to other trading-related tweets to build presence organically over the following weeks.

### Step 7: Submit to Product Hunt

Product Hunt is good for a one-time launch spike and credibility.

- Create an account at producthunt.com and build reputation by upvoting other products for 1–2 weeks before your launch
- Schedule your launch for a Tuesday or Wednesday (highest traffic days)
- Your listing needs:
  - Tagline (60 characters max): "Self-hosted IBKR trading dashboard with real-time alerts"
  - Thumbnail image (240x240)
  - Gallery screenshots (1270x760)
  - The demo video
- Ask friends, colleagues, or Reddit followers to upvote on launch day — the first 2 hours determine your ranking

---

## Phase 3 — Content & Compounding (Month 2–3)

This phase builds durable traffic that does not require you to keep posting.

### Step 8: Start a YouTube channel

YouTube is the best long-term channel for a dev tool because videos rank on Google.

Video ideas in order of production effort (start easy):

1. **"How I set up ARTradingTools with IBKR TWS"** — screen recording walkthrough, 5–10 min
2. **"How I track my watchlist in real-time with my own dashboard"** — demo + explanation
3. **"Building a custom trading alert system in JavaScript"** — technical deep-dive
4. **"My IBKR trading setup in 2026"** — broader appeal, can go viral in fintwit

Upload schedule: 1 video every 2 weeks is enough to start. Consistency matters more than frequency.

### Step 9: Write one SEO article

A single well-written article on a high-intent keyword can drive consistent traffic for years.

Target keyword: **"ibkr trading dashboard"** or **"interactive brokers custom dashboard"**

Article outline:
- Why the default IBKR TWS interface is limiting
- What a custom dashboard can do (real-time data, alerts, custom charts)
- How to set up ARTradingTools step by step
- Screenshots throughout

Publish options:
- Your own blog (GitHub Pages + Jekyll, or Hashnode for free hosting)
- Dev.to (built-in audience, good for developer-angled content)
- Medium (large finance audience)

### Step 10: Build an email list

Email is the only channel you own. Every other platform can de-rank or ban you.

- Add the email signup form from Step 4 everywhere (README, landing page, YouTube description)
- Send one email per month maximum:
  - New features added
  - An interesting thing you learned while building
  - Link to any new content (video, article)
- Keep it short — 3–5 paragraphs max

---

## Phase 4 — Paid Tier & Monetization (Month 3+)

Once you have an audience, you can introduce a paid offering.

Options in order of complexity:

| Model | What it means | Effort |
|---|---|---|
| **Donations** | GitHub Sponsors or Buy Me a Coffee link | Very low |
| **Pro features** | Cloud sync, mobile view, extra widgets — paid tier | Medium |
| **Hosted version** | You run the infrastructure, users pay monthly | High |
| **One-time license** | Pay once for commercial use or extra features | Low |

Start with donations. Add a GitHub Sponsors button and a Ko-fi link once you have GitHub stars coming in. Graduate to a paid tier only after you understand which features users actually want.

---

## Metrics to Track

Check these weekly once you start distributing:

| Metric | Where to find it | Target (Month 1) |
|---|---|---|
| GitHub stars | GitHub repo | 50+ |
| Reddit post upvotes | Reddit analytics | 100+ per post |
| Landing page unique visitors | Google Analytics or Plausible | 200+ |
| Email subscribers | Mailchimp / Beehiiv | 50+ |
| YouTube views | YouTube Studio | 500+ |

---

## Priority Order (What to Do First)

If you can only do one thing per week, follow this sequence:

1. **Week 1** — Write the README + record the demo GIF
2. **Week 2** — Set up landing page with email capture
3. **Week 3** — Post on r/interactivebrokers
4. **Week 4** — Post on r/algotrading + Twitter thread
5. **Month 2** — Record first YouTube video
6. **Month 2** — Submit to Product Hunt
7. **Month 3** — Publish first SEO article
8. **Month 3+** — Add donation link, evaluate paid tier
