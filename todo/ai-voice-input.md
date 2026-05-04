# AI Assistant Voice Input

Add voice recording to the AI chat panel so the user can speak a message instead of typing it. Recorded audio is transcribed to text and placed in the input field for review before sending — mirroring how whisper.cpp works. The goal is to bundle whisper.cpp with the app so no external cloud STT service is needed.

## Approach

### Option A — Bundled whisper.cpp (preferred)

Ship the `whisper` binary and a model file (`ggml-base.en.bin`, ~75 MB) alongside the app (e.g. `anal/ARAnalysis/bin/`). On first run, download them via a setup script if not present.

Backend exposes `POST /api/ai/transcribe`:
1. Accepts a `.wav` or `.webm` audio blob (multipart via `multer`)
2. Converts to 16 kHz mono WAV via `ffmpeg` if needed
3. Shells out to the whisper binary: `whisper --model base.en --output-txt --file <path>`
4. Returns `{ "text": "..." }`

Follow the same subprocess pattern used in `server/ai/providers/claude-cli.js`.

### Option B — Browser Web Speech API (fallback)

Use the built-in `SpeechRecognition` API — no backend needed, but Chromium-only and requires internet on some platforms.

## Frontend (`AIChatPanel.jsx`)

- Add a microphone icon button next to the send button
- While recording: button turns red / shows stop icon, display a "Recording…" indicator
- On stop: POST the audio blob to `/api/ai/transcribe`, receive transcript, populate the input field
- User reviews/edits the transcript before sending — do not auto-submit

## Open questions

- Commit binary + model to the repo, or download on first use via a setup script?
- Expose a settings option to select a larger model (e.g. `base` → `small` → `medium`) for better accuracy?

## Files

| File | Change |
|------|--------|
| `src/components/AIChatPanel.jsx` | Mic button + recording UI + POST to transcribe |
| `server/routes/ai.js` (or new `server/routes/transcribe.js`) | `POST /api/ai/transcribe` endpoint |
| `server/ai/whisper.js` | Whisper subprocess helper |
| `docs/ai-assistant.md` | Document the feature and setup steps |
