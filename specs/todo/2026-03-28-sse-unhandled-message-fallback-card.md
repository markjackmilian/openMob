# SSE Fallback Card — Unhandled Message Rendering

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-28                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

When the app receives an SSE event that cannot be handled — either because the event type is unknown or because the JSON payload is malformed — the current behaviour is a silent discard (with a debug-only Logcat entry). This feature replaces that silent discard with a visible **fallback card** rendered inline in the `ChatPage` message flow. In DEBUG builds the card exposes the raw JSON payload to aid future implementation; in Release builds it shows a generic, user-friendly notice.

---

## Scope

### In Scope
- Adding a `Fallback` value to the `SenderType` enum in `openMob.Core`
- Adding fallback-specific properties (`FallbackRawType`, `FallbackRawJson`) to `ChatMessage`
- Adding a `ChatMessage.CreateFallback()` static factory method
- Modifying `ChatViewModel.StartSseSubscriptionAsync` — `case UnknownEvent e:` — to create and append a fallback `ChatMessage` to `Messages`
- Preserving the existing `DebugLogger.WriteAction("OM_SSE", ...)` log call alongside the new card
- Adding a `FallbackMessageView` ContentView (MAUI project) to render the card
- Wiring the new view into the `ChatPage.xaml` `CollectionView` single-template via a visibility branch on `SenderType == Fallback`
- DEBUG build: card displays `FallbackRawType` and `FallbackRawJson` (pretty-printed)
- Release build: card displays a generic non-technical message only

### Out of Scope
- Modifications to `ChatEventParser` (already produces `UnknownEvent` correctly for all failure modes)
- Fallback cards for intentionally ignored part types (`snapshot`, `patch`, `retry`) — these remain silent discards
- Automatic Sentry reporting for unknown events
- Copy-to-clipboard or share actions on the fallback card
- Filtering or hiding fallback cards after the fact
- Pagination or grouping of multiple consecutive fallback cards

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The `SenderType` enum must gain a `Fallback` member to identify unhandled-message entries in the `Messages` collection.

2. **[REQ-002]** `ChatMessage` must expose two nullable string properties: `FallbackRawType` (the raw SSE event-type string from `UnknownEvent.RawType`) and `FallbackRawJson` (the pretty-printed JSON serialisation of `UnknownEvent.RawData`, or `null` if `RawData` is absent).

3. **[REQ-003]** A static factory method `ChatMessage.CreateFallback(string rawType, string? rawJson)` must be added, producing a `ChatMessage` with `SenderType = Fallback`, `MessageKind = Standard`, and the two fallback properties populated.

4. **[REQ-004]** In `ChatViewModel.StartSseSubscriptionAsync`, the `case UnknownEvent e:` branch must call `ChatMessage.CreateFallback(...)` and append the result to `Messages` via `_dispatcher.Dispatch(...)`, in addition to retaining the existing `#if DEBUG DebugLogger.WriteAction(...)` call.

5. **[REQ-005]** The fallback `ChatMessage` must be appended to `Messages` on the UI thread using the existing `IDispatcherService.Dispatch(...)` pattern, consistent with all other message mutations in `ChatViewModel`.

6. **[REQ-006]** In DEBUG builds, `FallbackRawJson` must be the pretty-printed (`JsonSerializerOptions.WriteIndented = true`) serialisation of `UnknownEvent.RawData`. If `RawData` is `null` or empty, `FallbackRawJson` must be `null`.

7. **[REQ-007]** In Release builds, `FallbackRawJson` must always be `null` and `FallbackRawType` must always be `null`. The `CreateFallback` factory must use `#if DEBUG / #else` to enforce this at compile time, ensuring zero sensitive data leaks in production builds.

8. **[REQ-008]** A new `FallbackMessageView` ContentView must be added to the MAUI project (`src/openMob/Views/Controls/`). It must expose `BindableProperty` entries for `RawType` (string), `RawJson` (string), and `IsDebugBuild` (bool, set once at construction from a compile-time constant).

9. **[REQ-009]** In DEBUG builds, `FallbackMessageView` must display:
   - A header label: *"⚠ Unhandled SSE event"*
   - A `RawType` label: *"Type: `<rawType>`"*
   - A scrollable/selectable `Label` or `Editor` (read-only) showing the pretty-printed JSON of `RawJson`, or *"(no payload)"* if `RawJson` is null

10. **[REQ-010]** In Release builds, `FallbackMessageView` must display only a single label: *"Received an unrecognized message from the server."* No technical data must be visible.

11. **[REQ-011]** `FallbackMessageView` must be visually distinct from standard message blocks. It must use a warning-style appearance: a `Border` with `ColorWarning` (or the closest available semantic token) as the left-border accent, and a subtly tinted background, consistent with the left-border-bar pattern established by `MessageBlockView`.

12. **[REQ-012]** The `ChatPage.xaml` single `DataTemplate` must include a new visibility branch: when `SenderType == Fallback`, the `FallbackMessageView` is shown and the standard `MessageBlockView` (and all other sections) are hidden for that item.

13. **[REQ-013]** The `SenderType`-to-visibility conversion for the fallback branch must follow the existing converter adapter pattern: pure logic in `openMob.Core`, thin `IValueConverter` wrapper in the MAUI project.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SenderType.cs` (Core) | Modified — add `Fallback` member | Existing members (`User`, `Agent`, `Subagent`) unchanged |
| `ChatMessage.cs` (Core) | Modified — add `FallbackRawType`, `FallbackRawJson` properties + `CreateFallback()` factory | Properties are `null` on all non-fallback messages |
| `ChatViewModel.cs` (Core) | Modified — `case UnknownEvent e:` appends a fallback message | `DebugLogger` call retained |
| `FallbackMessageView.xaml/.cs` (MAUI) | New file — ContentView for fallback card rendering | Lives in `src/openMob/Views/Controls/` |
| `ChatPage.xaml` (MAUI) | Modified — new visibility branch in `CollectionView` DataTemplate | No structural changes to existing branches |
| Converter (Core + MAUI) | New — `SenderTypeToFallbackVisibilityConverter` (or reuse existing `SenderType` converters if applicable) | Follows existing adapter pattern |
| `ChatEventParser.cs` (Core) | **Not modified** | Already produces `UnknownEvent` for all failure modes |

### Dependencies
- `UnknownEvent.RawType` (string) and `UnknownEvent.RawData` (JsonElement?) — already present on the record, no changes needed
- `IDispatcherService` — already injected in `ChatViewModel`, no changes needed
- `DebugLogger.WriteAction` — retained as-is alongside the new card logic
- `MessageBlockView` left-border-bar visual pattern — `FallbackMessageView` must visually align with this established pattern
- `chat-page-redesign` feature (currently in-progress on `feature/chat-page-redesign`) — introduces `SenderType` enum and `MessageBlockView`; this feature depends on those being merged or must be developed on top of that branch

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should `Fallback` be added to the existing `SenderType` enum, or should a separate discriminator property be used on `ChatMessage`? | Resolved | Add `Fallback` to `SenderType` — consistent with the existing pattern; no new discriminator needed |
| 2 | Should this feature branch off `feature/chat-page-redesign` (which introduces `MessageBlockView` and the new `SenderType` usage) or off `develop`? | Open | Depends on merge order of `chat-page-redesign`; to be decided by `om-orchestrator` at implementation time |
| 3 | Is there a `ColorWarning` semantic token already defined in the MAUI `ResourceDictionary`? | Open | `om-orchestrator` must verify in `src/openMob/Resources/Styles/` before implementation |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given an SSE stream that delivers an event with an unrecognised type, when `ChatViewModel` processes it, then a new item with `SenderType = Fallback` appears at the bottom of the `Messages` collection. *(REQ-001, REQ-004, REQ-005)*

- [ ] **[AC-002]** Given a DEBUG build and a fallback card in the message list, when the user views the chat, then the card displays the raw event type string and the pretty-printed JSON payload (or "(no payload)" if absent). *(REQ-002, REQ-006, REQ-008, REQ-009)*

- [ ] **[AC-003]** Given a Release build and a fallback card in the message list, when the user views the chat, then the card displays only "Received an unrecognized message from the server." with no technical data. *(REQ-007, REQ-010)*

- [ ] **[AC-004]** Given any build, when a fallback card is rendered, then the `DebugLogger.WriteAction("OM_SSE", ...)` log entry is also emitted (DEBUG only, as before). *(REQ-004)*

- [ ] **[AC-005]** Given an SSE event of type `snapshot`, `patch`, or `retry`, when `ChatViewModel` processes it, then no fallback card is added to `Messages` (these remain silent discards). *(Scope — Out of Scope)*

- [ ] **[AC-006]** Given a Release build, when `ChatMessage.CreateFallback(...)` is called, then `FallbackRawType` and `FallbackRawJson` are both `null` — verified by unit test. *(REQ-007)*

- [ ] **[AC-007]** Given a DEBUG build, when `ChatMessage.CreateFallback("unknown.type", "{\"foo\":1}")` is called, then `FallbackRawType == "unknown.type"` and `FallbackRawJson` contains the pretty-printed JSON — verified by unit test. *(REQ-003, REQ-006)*

- [ ] **[AC-008]** Given the `ChatPage` CollectionView, when an item has `SenderType = Fallback`, then `FallbackMessageView` is visible and `MessageBlockView` (and all other standard sections) are hidden for that item. *(REQ-012)*

- [ ] **[AC-009]** `ChatEventParser` has zero modifications — its existing test suite passes unchanged. *(Scope)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- Verify whether `feature/chat-page-redesign` (currently in-progress) has already been merged into `develop` before branching. If not, this feature must be developed on top of `feature/chat-page-redesign` to avoid conflicts on `SenderType`, `MessageBlockView`, and `ChatPage.xaml`.
- Verify the existence of a `ColorWarning` semantic color token in `src/openMob/Resources/Styles/Colors.xaml` (or equivalent). If absent, define one following the `Color` prefix + semantic name convention (`ColorWarning`).
- Inspect `src/openMob/Views/Controls/` for existing `ContentView` controls to confirm the correct file/namespace pattern for `FallbackMessageView`.
- Check whether any existing `SenderType`-based `IValueConverter` can be extended, or whether a new `SenderTypeToFallbackVisibilityConverter` is needed.

### Suggested implementation approach
1. **Core first:** Add `Fallback` to `SenderType`, add `FallbackRawType`/`FallbackRawJson` to `ChatMessage`, add `CreateFallback()` factory with `#if DEBUG` guard on payload population.
2. **ViewModel:** Modify `case UnknownEvent e:` in `ChatViewModel.StartSseSubscriptionAsync` — call `CreateFallback(e.RawType, rawJson)` and dispatch-append to `Messages`. Retain existing `DebugLogger` call.
3. **Tests:** Add unit tests for `CreateFallback()` in DEBUG and Release configurations; add a `ChatViewModel` test asserting that an `UnknownEvent` produces a fallback `ChatMessage` in `Messages`.
4. **MAUI UI:** Create `FallbackMessageView` ContentView; add the visibility branch in `ChatPage.xaml`; add/extend the converter.

### Constraints to respect
- `ChatEventParser` must **not** be modified (established constraint from `spec-chat-service-layer` and `spec-sse-message-content-fix`).
- `#if DEBUG` preprocessor guards must be used for all payload data — never `RuntimeInformation` or environment checks.
- All `Messages` mutations must go through `_dispatcher.Dispatch(...)` (established pattern from `spec-chat-conversation-loop`).
- `openMob.Core` must remain a pure `net10.0` library — `FallbackMessageView` lives in the MAUI project only.
- `DebugLogger.WriteAction` does **not** have a generic `Log(tag, message)` method — use `DebugLogger.WriteAction("OM_SSE", ...)` directly, wrapped in `#if DEBUG` (established in `tech-sse-full-message-type-coverage`).
- The `#if DEBUG` guard on `FallbackRawJson` population in `CreateFallback()` must be at **compile time** (preprocessor), not runtime, to guarantee zero data leakage in Release builds.

### Related files or modules
- `src/openMob.Core/Models/SenderType.cs` — add `Fallback`
- `src/openMob.Core/Models/ChatMessage.cs` — add properties + factory
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — modify `case UnknownEvent e:` (~line 1186)
- `src/openMob.Core/Models/ChatEvent.cs` — `UnknownEvent` record (read-only reference, no changes)
- `src/openMob/Views/Controls/` — new `FallbackMessageView.xaml` + `.cs`
- `src/openMob/Views/Pages/ChatPage.xaml` — add visibility branch in DataTemplate
- `src/openMob/Resources/Styles/Colors.xaml` — verify/add `ColorWarning`
- `tests/openMob.Tests/` — new tests for `CreateFallback()` and `ChatViewModel` fallback behaviour
