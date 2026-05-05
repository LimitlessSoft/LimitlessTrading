# About Modal — License Section Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a license information section to the About modal in the Workstation app, showing plan tier, email, and a link to the License modal.

**Architecture:** All changes are confined to `PageNavMenu.jsx`. The `useLicense()` hook already provides `isPro`, `status`, and `customerEmail` — no new data fetching needed. The license block is inserted between the version line and the Divider, mirroring the existing Device Info box style.

**Tech Stack:** React, Chakra UI, `useLicense()` context hook

---

### Task 1: Expand `useLicense` destructure and add the License section

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Fe/src/components/PageNavMenu.jsx`

The About modal body currently looks like this (lines ~316–335):

```jsx
<ModalBody pb={6}>
  <Text color="gray.300" mb={4}>
    Trading analysis toolkit for Interactive Brokers.
  </Text>
  <Text fontSize="sm" color="gray.500" mb={4}>
    Version{' '}
    <Text as="span" color="gray.300">{appVersion ?? '—'}</Text>
  </Text>
  <Divider borderColor="gray.600" mb={4} />
  <Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="wider" mb={2}>
    Device Info
  </Text>
  <Box bg="gray.900" borderRadius="md" p={3} fontSize="xs" color="gray.300">
    {Object.entries(getDeviceInfo()).map(([key, value]) => (
      <Flex key={key} gap={2} mb={1}>
        <Text color="gray.500" minW="140px" flexShrink={0}>{key}</Text>
        <Code bg="transparent" color="gray.200" fontSize="xs" wordBreak="break-all">{value}</Code>
      </Flex>
    ))}
  </Box>
</ModalBody>
```

- [ ] **Step 1: Update `useLicense` destructure**

Find line 83:
```jsx
const { isPro } = useLicense()
```
Replace with:
```jsx
const { isPro, status: licenseStatus, customerEmail } = useLicense()
```

- [ ] **Step 2: Insert the License section into the About modal body**

After the version `<Text>` block and before the `<Divider>`, insert:

```jsx
<Divider borderColor="gray.600" mb={4} />
<Text fontSize="xs" fontWeight="semibold" color="gray.400" textTransform="uppercase" letterSpacing="wider" mb={2}>
  License
</Text>
<Box bg="gray.900" borderRadius="md" p={3} fontSize="xs" color="gray.300" mb={2}>
  <Flex gap={2} mb={1}>
    <Text color="gray.500" minW="140px" flexShrink={0}>plan</Text>
    <Code bg="transparent" fontSize="xs" color={licenseStatus === 'loading' ? 'gray.400' : isPro ? 'green.300' : 'gray.200'}>
      {licenseStatus === 'loading' ? '—' : isPro ? 'Pro' : 'Free'}
    </Code>
  </Flex>
  <Flex gap={2}>
    <Text color="gray.500" minW="140px" flexShrink={0}>email</Text>
    <Code bg="transparent" color="gray.200" fontSize="xs" wordBreak="break-all">
      {customerEmail ?? '—'}
    </Code>
  </Flex>
</Box>
<Text
  fontSize="xs"
  color="blue.400"
  cursor="pointer"
  mb={4}
  onClick={() => { onAboutClose(); onLicenseOpen() }}
>
  Manage license →
</Text>
```

The existing `<Divider borderColor="gray.600" mb={4} />` before Device Info stays in place.

- [ ] **Step 3: Verify the modal renders correctly**

Start the workstation dev server:
```bash
cd src/LT.Workstation/LT.Workstation.Fe && npm run dev
```
Open the app, click the hamburger menu → About. Confirm:
- "License" section heading appears between version and Device Info
- Plan shows "Pro" in green (if active) or "Free" in gray
- Email shows the customer email or "—"
- "Manage license →" link is visible

- [ ] **Step 4: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/components/PageNavMenu.jsx
git commit -m "Add license section to About modal"
```

---

### Task 2: Update clipboard copy to include license info

**Files:**
- Modify: `src/LT.Workstation/LT.Workstation.Fe/src/components/PageNavMenu.jsx`

The copy button handler (lines ~299–304) currently builds:
```js
navigator.clipboard.writeText(`Version: ${appVersion ?? '—'}\n${text}`)
```

- [ ] **Step 1: Update the clipboard handler**

Find the `onClick` of the copy `IconButton` in the About modal header:
```jsx
onClick={() => {
  const info = getDeviceInfo()
  const text = Object.entries(info).map(([k, v]) => `${k}: ${v}`).join('\n')
  navigator.clipboard.writeText(`Version: ${appVersion ?? '—'}\n${text}`)
  toast({ title: 'Copied to clipboard', status: 'success', duration: 2000, isClosable: true })
}}
```

Replace with:
```jsx
onClick={() => {
  const info = getDeviceInfo()
  const deviceText = Object.entries(info).map(([k, v]) => `${k}: ${v}`).join('\n')
  const licenseText = `License: ${isPro ? 'Pro' : 'Free'}\nEmail: ${customerEmail ?? '—'}`
  navigator.clipboard.writeText(`Version: ${appVersion ?? '—'}\n${licenseText}\n${deviceText}`)
  toast({ title: 'Copied to clipboard', status: 'success', duration: 2000, isClosable: true })
}}
```

- [ ] **Step 2: Verify clipboard output**

Click the copy icon in the About modal header. Paste into a text editor. Confirm the pasted text starts with:
```
Version: x.x.x
License: Pro   (or Free)
Email: user@example.com   (or —)
userAgent: ...
...
```

- [ ] **Step 3: Commit**

```bash
git add src/LT.Workstation/LT.Workstation.Fe/src/components/PageNavMenu.jsx
git commit -m "Include license info in About modal clipboard copy"
```
