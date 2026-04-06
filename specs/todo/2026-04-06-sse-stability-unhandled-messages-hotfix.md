# SSE Stream Stability & Unhandled Message Types Hotfix

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-04-06                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

The app freezes and becomes unusable after SSE stream failures because `IsAiResponding` is never reset, locking the user out of the input field indefinitely. Additionally, the `_inFlightPermissionReplies` HashSet is accessed concurrently from multiple threads without synchronisation, risking silent data corruption. Several message part types defined by the opencode server API (notably `file`) are silently dropped, and the SSE event dispatch switch lacks a `default:` case, causing unmatched events to fall through without any handling. This hotfix addresses all identified stability and correctness issues in the SSE processing pipeline.

---

## Scope

### In Scope
- Reset `IsAiResponding` to `false` when the SSE stream fails and reconnection is exhausted
- Reset `IsStreaming` on all in-flight messages when the SSE subscription terminates abnormally
- Replace the non-thread-safe `HashSet<string>` (`_inFlightPermissionReplies`) with `ConcurrentDictionary<string, byte>`
- Add an explicit `case ServerConnectedEvent:` (no-op with DEBUG logging) in the ViewModel SSE switch
- Add a `default:` case in the ViewModel SSE switch to catch any future event types that are not `UnknownEvent`
- Add a final `else` branch with DEBUG logging in `HandleMessagePartUpdated` for unrecognised part types
- Handle the `file` part type with a textual placeholder in the chat (e.g. "[Attached file: filename.ext]")
- Ensure the user can always send messages after an SSE failure (input field never stays permanently disabled)

### Out of Scope
- Full rendering of `file` parts (image previews, PDF viewers, download buttons) — only a text placeholder is added
- Refactoring delta text accumulation from `string +=` to `StringBuilder`
- Batching UI thread dispatches to reduce GC pressure during high-frequency streaming
- Any new chat features or UX changes beyond the fixes described above
- Changes to the opencode server API or `ChatEventParser`

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** When the SSE stream fails and the reconnection loop in `ChatService.SubscribeToEventsAsync` is exhausted (or throws a non-cancellation exception), the `catch` block in `ChatViewModel.StartSseSubscriptionAsync` **MUST** set `IsAiResponding = false`.

2. **[REQ-002]** When the SSE subscription terminates abnormally (non-cancellation exception), all messages in the `Messages` collection that currently have `IsStreaming == true` **MUST** be set to `IsStreaming = false`. This prevents infinite spinner states on partially-streamed messages.

3. **[REQ-003]** The `_inFlightPermissionReplies` field in `ChatViewModel` **MUST** be replaced with a `ConcurrentDictionary<string, byte>` (or equivalent thread-safe structure). All call sites (`HandlePermissionRequested`, `ReplyToPermissionAsync`, and the auto-accept `Task.Run` callback) **MUST** be updated to use the new API (`TryAdd`, `TryRemove`).

4. **[REQ-004]** The `switch (chatEvent)` statement in `StartSseSubscriptionAsync` **MUST** include:
   - An explicit `case ServerConnectedEvent:` branch that performs a no-op (with a `DEBUG`-only log statement).
   - A `default:` branch that logs the unhandled event type in `DEBUG` builds and is a silent no-op in Release builds.

5. **[REQ-005]** The `HandleMessagePartUpdated` method **MUST** include a final `else` branch after all recognised part types. This branch **MUST**:
   - In `DEBUG` builds: log the unrecognised part type and part ID via `DebugLogger`.
   - In Release builds: silently ignore the part (no crash, no user-visible error).

6. **[REQ-006]** The `HandleMessagePartUpdated` method **MUST** handle the `file` part type explicitly. When a `file` part is received:
   - If the part has a non-null `Text` field containing a filename, display a placeholder: `"[Attached file: {filename}]"`.
   - If no filename is available, display: `"[Attached file]"`.
   - The placeholder text **MUST** be appended to the existing message's `TextContent` (or set if empty).

7. **[REQ-007]** After the SSE subscription terminates (both normal cancellation and abnormal failure), the `CanSendMessage` computed property **MUST** return `true` (assuming other preconditions like `CurrentSessionId != null` are met). The user **MUST NOT** be permanently locked out of the input field.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatViewModel.cs` — `StartSseSubscriptionAsync` | Modified | Add `IsAiResponding = false` and `IsStreaming` reset in catch block |
| `ChatViewModel.cs` — SSE switch statement | Modified | Add `case ServerConnectedEvent:` and `default:` branches |
| `ChatViewModel.cs` — `_inFlightPermissionReplies` | Modified | Replace `HashSet<string>` with `ConcurrentDictionary<string, byte>` |
| `ChatViewModel.cs` — `HandleMessagePartUpdated` | Modified | Add `file` part handling and final `else` branch with logging |
| `ChatViewModel.cs` — `HandlePermissionRequested` | Modified | Update to use `ConcurrentDictionary` API |
| `ChatViewModel.cs` — `ReplyToPermissionAsync` | Modified | Update to use `ConcurrentDictionary` API |

### Dependencies
- No new NuGet packages required (`ConcurrentDictionary` is in `System.Collections.Concurrent`).
- No database schema changes.
- No changes to the opencode server API contract.
- No changes to `ChatEventParser`, `ChatService`, or any other service.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Is the GC-every-second loop caused by the `IsAiResponding` deadlock or by a separate issue? | Open | Likely caused by the stuck state preventing proper SSE teardown. REQ-001/REQ-002 should resolve it. If GC pressure persists after the fix, a follow-up investigation is needed. |
| 2 | Should the `file` part placeholder be interactive (tappable) in the future? | Resolved | No — out of scope for this hotfix. A future spec can add file preview/download. |
| 3 | Should `ErrorMessage` be set when the SSE stream fails after reconnect exhaustion? | Resolved | Yes — the existing `ErrorMessage` assignment in the catch block is kept. The fix only adds the missing `IsAiResponding` reset alongside it. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the SSE stream fails and reconnection is exhausted, when the user views the chat, then `IsAiResponding` is `false` and the message input field is enabled. *(REQ-001, REQ-007)*
- [ ] **[AC-002]** Given a message was in streaming state (`IsStreaming == true`) when the SSE connection dropped, when reconnection fails, then the message shows `IsStreaming = false` (no infinite spinner). *(REQ-002)*
- [ ] **[AC-003]** Given the opencode server sends a `file` part type in a `message.part.updated` event, when the event reaches the ViewModel, then a textual placeholder (e.g. "[Attached file: image.png]") appears in the chat message. *(REQ-006)*
- [ ] **[AC-004]** Given the opencode server sends an unknown/future part type, when the event reaches `HandleMessagePartUpdated`, then it is logged in DEBUG builds and silently ignored in Release builds — no crash, no exception. *(REQ-005)*
- [ ] **[AC-005]** Given two threads access `_inFlightPermissionReplies` concurrently (SSE background thread adding, UI thread removing), when they operate simultaneously, then no `InvalidOperationException` or state corruption occurs. *(REQ-003)*
- [ ] **[AC-006]** Given a `ServerConnectedEvent` arrives in the ViewModel SSE switch, when it is processed, then it matches its own explicit `case` branch (not `UnknownEvent` or `default`). *(REQ-004)*
- [ ] **[AC-007]** Given the app is running normally with an active SSE connection, when logcat is monitored, then Explicit GC events do NOT occur every second (the stuck-state loop is resolved). *(REQ-001, REQ-002)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Key areas to investigate:**
  - `ChatViewModel.StartSseSubscriptionAsync()` — lines 1146–1230: the `catch (Exception ex)` block at line 1221 sets `ErrorMessage` but does NOT reset `IsAiResponding`. Add `IsAiResponding = false;` and iterate `Messages` to reset `IsStreaming`.
  - `ChatViewModel._inFlightPermissionReplies` — line 82: replace `HashSet<string>` with `ConcurrentDictionary<string, byte>`. Update `Add()` → `TryAdd(key, 0)`, `Remove()` → `TryRemove(key, out _)`, `Contains()` → `ContainsKey()`.
  - `ChatViewModel.StartSseSubscriptionAsync()` switch — lines 1158–1214: add `case ServerConnectedEvent:` before the `UnknownEvent` cases, and add a `default:` case at the end.
  - `ChatViewModel.HandleMessagePartUpdated()` — lines 1368–1456: add `else if (type == "file")` handling and a final `else` with `#if DEBUG` logging.

- **Suggested implementation approach:**
  - All changes are confined to `ChatViewModel.cs` — no other files need modification.
  - The `IsAiResponding` reset should be done via `_dispatcher.Dispatch()` to ensure thread-safe UI property updates.
  - The `IsStreaming` reset loop should also run inside `_dispatcher.Dispatch()` to avoid cross-thread collection access.
  - For the `file` part placeholder, extract the filename from `e.Part.Extras` if available (the opencode `FilePart` schema has a `filename` field), or fall back to `e.Part.Text`.

- **Constraints to respect:**
  - All UI-bound property mutations must go through `_dispatcher.Dispatch()`.
  - No `async void` — all async paths must be properly awaited or fire-and-forget with try/catch.
  - `ConcurrentDictionary` is already available in `System.Collections.Concurrent` — no new using required if `GlobalUsings.cs` covers it.
  - Maintain the existing defensive pattern: never crash on unexpected SSE data.

- **Related files:**
  - `src/openMob.Core/ViewModels/ChatViewModel.cs` — primary target (all changes)
  - `src/openMob.Core/Models/ChatEvent.cs` — reference for `ServerConnectedEvent` type
  - `src/openMob.Core/Helpers/ChatEventParser.cs` — reference only (no changes needed)
  - `src/openMob.Core/Services/ChatService.cs` — reference for SSE reconnect logic (no changes needed)
  - `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` — reference for `PartDto` fields (filename extraction)

- **GC loop investigation:**
  - The GC-every-second pattern in logcat is almost certainly caused by the app being stuck in an inconsistent state after SSE failure: `IsAiResponding == true` prevents user interaction, but the SSE loop has exited, creating a deadlock. The .NET runtime's GC runs Explicit collections when it detects idle threads with pending finalizers. Fixing REQ-001/REQ-002 should eliminate this pattern. If it persists, a separate investigation into timer/polling loops is warranted.

- **opencode server API reference (v1.3.17):**
  - All valid `MessageV2.Part` types: `text`, `subtask`, `reasoning`, `file`, `tool`, `step-start`, `step-finish`, `snapshot`, `patch`, `agent`, `retry`, `compaction`.
  - The `file` part schema includes: `type: "file"`, `mime: string`, `filename?: string`, `url: string`, `source?: FilePartSource`.
  - On the SSE wire, `file` parts arrive as `message.part.updated` events with `part.type == "file"`.
