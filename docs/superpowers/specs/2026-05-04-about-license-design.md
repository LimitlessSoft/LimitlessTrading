# About Modal — License Section

**Date:** 2026-05-04  
**Status:** Approved

## Goal

Add a license information section to the About modal in the Workstation app so users can see their current plan and associated email without opening the full License modal.

## Location

`src/LT.Workstation/LT.Workstation.Fe/src/components/PageNavMenu.jsx` — the About modal (lines ~287–338).

## Layout

The license block is inserted between the version line and the existing Divider + Device Info block:

```
[Version line]

[License label — uppercase xs]
[gray.900 box]
  Plan    Pro / Free
  Email   user@example.com / —

[Manage license → link]

[Divider]

[Device Info label — uppercase xs]
[gray.900 box — existing]
  ...device rows...
```

## License Section Details

- Section label: `"License"` — matches the existing Device Info label style (`xs`, `semibold`, `gray.400`, `uppercase`, `letterSpacing="wider"`, `mb={2}`)
- Box: `bg="gray.900"`, `borderRadius="md"`, `p={3}`, `fontSize="xs"` — identical to Device Info box
- **Plan row:**
  - Label: `"plan"` in `gray.500`, `minW="140px"`
  - Value: `"Pro"` colored `green.300` when active, `"Free"` in `gray.200` otherwise
  - When `status === 'loading'`: show `"—"` in gray
- **Email row:**
  - Label: `"email"` in `gray.500`, `minW="140px"`
  - Value: `customerEmail` in `gray.200`, or `"—"` in gray when null
- **Manage license link:** `"Manage license →"` below the box, `fontSize="xs"`, `color="blue.400"`, `cursor="pointer"`, `mt={2}`. On click: close About modal (`onAboutClose()`), open License modal (`onLicenseOpen()`).

## Data Source

`useLicense()` is already imported and called in `PageNavMenu`. It provides `{ isPro, status, customerEmail }` — no new fetching required.

## Copy-to-Clipboard

The existing copy button in the About modal header copies version + device info. Update it to also prepend license tier and email:

```
Version: x.x.x
License: Pro
Email: user@example.com
[...device info...]
```

## Out of Scope

- No changes to LicenseModal, LicenseContext, or the server license route.
- No new components — all changes are inline within the About modal JSX in PageNavMenu.jsx.
