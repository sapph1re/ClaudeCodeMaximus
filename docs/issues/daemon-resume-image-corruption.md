# Daemon Bug: Session Resume Corrupts Historical Images

## Symptom

When a session contains a previously-sent image and you call `run.send` with `externalId` (existing session) — even with a brand new valid image — the API returns:

```
API Error: 400 {"type":"error","error":{"type":"invalid_request_error",
"message":"messages.N.content.0.image.source.base64.data: Image format image/png not supported"}}
```

The error path `messages.N.content.0` references the **historical** image at message index N, not the new one being sent.

## Reproduction

1. Send a message with an image attachment to a fresh session — works, Claude responds correctly. The image is stored in the JSONL.
2. Send a follow-up message with another image to the **same** session (with `externalId`).
3. The API rejects message N (the original image), not the new one.

This was reproduced via raw WebSocket calls (no GUI involved). Send the same valid PNG bytes:
- **Without `externalId`**: works, Claude describes the image
- **With `externalId` of an existing session that has prior images**: fails with the error above

## What's verified

- The PNG bytes themselves are valid (verified with `file`, magic byte check, and successful sends to fresh sessions)
- The JSON the GUI sends is identical to a working raw WebSocket call (same `media_type: "image/png"`, same `source.type: "base64"`, same data)
- The error reproduces from a Node.js script bypassing the GUI entirely

## Hypothesis

The persistent `claude` process resumed via `--resume <id>` reads the JSONL session file and replays the entire conversation history to the Anthropic API on the next turn. Something about how images are stored in the JSONL — or how the resume reconstructs them — produces a media type that the API rejects.

This could be:
1. Claude Code CLI storing images in JSONL with a different format than what gets sent
2. JSONL parser losing fidelity on the base64 data
3. A media type field getting normalized/lost during resume

Possibly worth checking: does this also reproduce when running `claude --resume <id>` directly from the terminal (no daemon involved)? If yes, it's a CLI bug; if no, it's a daemon bug in how it spawns/feeds the resumed process.

## Workaround for users

For now, image attachments work reliably **only in fresh sessions** (no historical images). Once a session has had image content, subsequent sends with new images fail. Starting a new session and sending the image there works.

## Reference

- Discovered in PR #7 (sapph1re/ClaudeCodeMaximus) attachments work
- Daemon version: tessyn@0.4.1
