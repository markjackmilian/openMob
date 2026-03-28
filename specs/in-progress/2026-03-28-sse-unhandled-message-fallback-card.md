# SSE Fallback Card — Unhandled Message Rendering

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-28                   |
| Status  | In Progress                  |
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
| 2 | Should this feature branch off `feature/chat-page-redesign` (which introduces `MessageBlockView` and the new `SenderType` usage) or off `develop`? | Resolved | `feature/chat-page-redesign` is NOT present in the remote branches list; `MessageBlockView` and the new `SenderType` are already merged into `develop`. Branch directly from `develop`. |
| 3 | Is there a `ColorWarning` semantic token already defined in the MAUI `ResourceDictionary`? | Resolved | Yes — `ColorWarningLight` (#FF9F0A) and `ColorWarningDark` (#FFD60A) are defined in `Colors.xaml`. Also `ColorWarningContainerLight`/`Dark` for tinted backgrounds. |

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

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-28

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/sse-fallback-card |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Pre-Analysis Findings

**Q2 — Branch base:** `feature/chat-page-redesign` is NOT present in the remote branch list. `MessageBlockView`, `SenderType` (with `User`/`Agent`/`Subagent`), and the redesigned `ChatPage.xaml` are already merged into `develop`. Branch directly from `develop`.

**Q3 — ColorWarning token:** `ColorWarningLight` (#FF9F0A) and `ColorWarningDark` (#FFD60A) already exist in `Colors.xaml`. `ColorWarningContainerLight`/`Dark` are also available for the tinted background. No new color tokens needed.

**Converter strategy:** A new `SenderTypeToFallbackVisibilityConverter` (Core pure logic + MAUI wrapper) is required. The existing `SenderTypeToColorKeyConverter` and `SenderTypeToLabelConverter` do not handle the `Fallback` case for visibility. A dedicated converter is cleaner than extending the existing ones with a boolean output.

**ChatMessage constructor:** The constructor is `internal`, so `CreateFallback()` can call it directly. Two new nullable string properties (`FallbackRawType`, `FallbackRawJson`) will be added as `[ObservableProperty]` fields (though they won't change after creation, the pattern is consistent). The constructor will gain two optional parameters.

**ChatViewModel `case UnknownEvent e:`:** Currently at line 1186. The `_dispatcher` field and `Messages` collection are already available. The `CurrentSessionId` is available for the fallback message's `sessionId`.

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Models / Enum | om-mobile-core | `src/openMob.Core/Models/SenderType.cs`, `ChatMessage.cs` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Core Converter | om-mobile-core | `src/openMob.Core/Converters/SenderTypeToFallbackVisibilityConverter.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Controls/FallbackMessageView.xaml/.cs` |
| MAUI Converter | om-mobile-ui | `src/openMob/Converters/SenderTypeToFallbackVisibilityConverter.cs` |
| ChatPage wiring | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml` |
| DI registration | om-mobile-ui | `src/openMob/MauiProgram.cs` (converter registration if needed) |
| Unit Tests | om-tester | `tests/openMob.Tests/Models/ChatMessageTests.cs`, `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs`, `tests/openMob.Tests/Converters/SenderTypeToFallbackVisibilityConverterTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Converters/SenderTypeToFallbackVisibilityConverter.cs` — pure logic converter: returns `true` when `SenderType == Fallback`, `false` otherwise
- `src/openMob/Converters/SenderTypeToFallbackVisibilityConverter.cs` — thin MAUI `IValueConverter` wrapper delegating to Core converter
- `src/openMob/Views/Controls/FallbackMessageView.xaml` — ContentView XAML for fallback card
- `src/openMob/Views/Controls/FallbackMessageView.xaml.cs` — code-behind with BindableProperties
- `tests/openMob.Tests/Converters/SenderTypeToFallbackVisibilityConverterTests.cs` — converter unit tests

### Files to Modify

- `src/openMob.Core/Models/SenderType.cs` — add `Fallback` member with XML doc
- `src/openMob.Core/Models/ChatMessage.cs` — add `FallbackRawType`/`FallbackRawJson` `[ObservableProperty]` fields + `CreateFallback()` factory + constructor parameters
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — modify `case UnknownEvent e:` to call `CreateFallback` and dispatch-append to `Messages`
- `src/openMob/Views/Pages/ChatPage.xaml` — add `FallbackMessageView` visibility branch in DataTemplate; hide `MessageBlockView` and surrounding sections when `SenderType == Fallback`
- `src/openMob/MauiProgram.cs` — register `SenderTypeToFallbackVisibilityConverter` in XAML resources (if not auto-discovered)
- `tests/openMob.Tests/Models/ChatMessageTests.cs` — add `CreateFallback` tests
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — add `UnknownEvent` → fallback card test

### Technical Dependencies

- `UnknownEvent.RawType` (string, required) and `UnknownEvent.RawData` (JsonElement?, optional) — already on the record
- `IDispatcherService.Dispatch(Action)` — already injected in `ChatViewModel`
- `ColorWarningLight`/`Dark` and `ColorWarningContainerLight`/`Dark` — already in `Colors.xaml`
- `MessageBlockView` left-bar pattern — `FallbackMessageView` mirrors this with warning colors
- No new NuGet packages required

### Technical Risks

- **`#if DEBUG` in tests:** Unit tests always run in DEBUG configuration, so `CreateFallback` will always populate `FallbackRawType`/`FallbackRawJson` in the test runner. The Release-build behavior (null fields) must be documented and tested via a separate test that verifies the compile-time guard logic comment, or accepted as a known limitation.
- **ChatPage.xaml DataTemplate complexity:** The existing DataTemplate already has compaction banner, subtask chips, reasoning block, MessageBlockView, and tool call cards. The fallback branch must hide ALL of these sections (not just MessageBlockView) when `SenderType == Fallback`. Use a wrapping `VerticalStackLayout` with `IsVisible` bound to the inverse of the fallback converter.
- **Converter registration:** MAUI converters used in XAML must be registered in `App.xaml` or `MauiProgram.cs` resource dictionaries, or declared as `StaticResource` in the page. Check existing pattern in `ChatPage.xaml`.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/sse-fallback-card`
2. [om-mobile-core] Add `Fallback` to `SenderType`, add properties + `CreateFallback()` to `ChatMessage`, add `SenderTypeToFallbackVisibilityConverter` (Core), modify `ChatViewModel` `case UnknownEvent e:`
3. ⟳ [om-mobile-ui] Create `FallbackMessageView`, add MAUI converter wrapper, wire `ChatPage.xaml` (can start once ViewModel binding surface is confirmed — no new ViewModel properties needed, only `SenderType` and `FallbackRawType`/`FallbackRawJson` on `ChatMessage`)
4. [om-tester] Write unit tests for `CreateFallback()`, `SenderTypeToFallbackVisibilityConverter`, and `ChatViewModel` fallback behavior
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-013]` requirements implemented
- [ ] All `[AC-001]` through `[AC-009]` acceptance criteria satisfied
- [ ] Unit tests written for `CreateFallback()`, converter, and `ChatViewModel` fallback path
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
