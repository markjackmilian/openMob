# Hide Unhandled SSE Event Cards — Context Sheet Option

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-31                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

The opencode server emits SSE events and message part types that the app does not recognise. These are currently rendered as orange **"⚠ Unhandled SSE event"** debug cards in the `ChatPage`, flooding the conversation with raw JSON noise. This feature adds a per-project toggle in the Context Sheet that controls whether those unhandled cards are shown or hidden. The default is **hidden**, so new installs and new projects get a clean chat experience out of the box.

---

## Scope

### In Scope
- New `ShowUnhandledSseEvents` (`bool`) field on the `ProjectPreference` entity, default `false` (hidden)
- sqlite-net-pcl schema evolution: `ALTER TABLE ADD COLUMN` for the new column
- New `SetShowUnhandledSseEventsAsync` method on `IProjectPreferenceService`
- `GetOrDefaultAsync` returns `ShowUnhandledSseEvents = false` when no preference row exists
- New observable property `ShowUnhandledSseEvents` on `ContextSheetViewModel`, auto-saved on change via the established `partial void On*Changed` pattern
- `ContextSheetViewModel.InitializeAsync` loads and populates the new property
- `ChatViewModel` receives `ProjectPreferenceChangedMessage` and updates its own `ShowUnhandledSseEvents` observable property
- `ChatViewModel` suppresses adding `UnknownEvent` / `UnknownPart` cards to the `Messages` collection when `ShowUnhandledSseEvents = false`
- Toggle UI control in the Context Sheet XAML with label "Show unhandled events"

### Out of Scope
- Hiding recognised-but-verbose part types (tool calls, reasoning, step-start/finish, etc.)
- A global (app-wide) setting — this is strictly per-project
- Retroactive removal of cards already present in the `Messages` collection before the toggle is changed (only future incoming events are affected)
- Server-side changes to opencode
- Persistence of the preference to the server

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The `ProjectPreference` entity (`src/openMob.Core/Data/Entities/ProjectPreference.cs`) must be extended with a new field:
   - `ShowUnhandledSseEvents` (`bool`) — default `false`

2. **[REQ-002]** sqlite-net-pcl schema evolution: because `CreateTableAsync<T>()` is called at every startup and automatically runs `ALTER TABLE ADD COLUMN` for missing columns, no explicit migration file is required. The new column must be declared with a default value of `0` (false) so existing rows are unaffected.

3. **[REQ-003]** `IProjectPreferenceService` must be extended with:
   - `Task<bool> SetShowUnhandledSseEventsAsync(string projectId, bool value, CancellationToken ct = default)` — upserts the `ShowUnhandledSseEvents` field for the given project.

4. **[REQ-004]** `GetOrDefaultAsync` must return `ShowUnhandledSseEvents = false` when no `ProjectPreference` row exists for the project (hidden by default).

5. **[REQ-005]** `ContextSheetViewModel` must expose a new observable property:
   - `ShowUnhandledSseEvents` (`bool`) — bound to the toggle in the Context Sheet UI.

6. **[REQ-006]** `ContextSheetViewModel.InitializeAsync` must load `ShowUnhandledSseEvents` from `GetOrDefaultAsync` and assign it to the observable property (protected by the existing `_isInitializing` guard to prevent a spurious DB write on load).

7. **[REQ-007]** The auto-save hook `partial void OnShowUnhandledSseEventsChanged(bool value)` must call `SetShowUnhandledSseEventsAsync` and then publish `ProjectPreferenceChangedMessage` on success, following the established fire-and-forget pattern (`_ = SaveShowUnhandledSseEventsAsync(value)`).

8. **[REQ-008]** `ChatViewModel` must update its own observable property `ShowUnhandledSseEvents` when it receives a `ProjectPreferenceChangedMessage`, reading the new value from the message payload (or re-loading from the service — consistent with the existing pattern for other preference fields).

9. **[REQ-009]** In `ChatViewModel.StartSseSubscriptionAsync`, the `case UnknownEvent e:` branch (and any equivalent `UnknownPart` handling path) must check `ShowUnhandledSseEvents` before adding a card to `Messages`. If `ShowUnhandledSseEvents == false`, the card must be silently suppressed. The existing `DebugLogger` log call must still execute regardless of the toggle value.

10. **[REQ-010]** The Context Sheet XAML must include a `Switch` (or equivalent toggle control) bound to `ContextSheetViewModel.ShowUnhandledSseEvents`, with a descriptive label "Show unhandled events".

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ProjectPreference.cs` | Add `ShowUnhandledSseEvents bool` property | sqlite-net-pcl auto-migrates on startup |
| `IProjectPreferenceService.cs` | Add `SetShowUnhandledSseEventsAsync` method | Follows existing `SetAutoAcceptAsync` pattern |
| `ProjectPreferenceService.cs` | Implement `SetShowUnhandledSseEventsAsync` | Upsert pattern, same as other Set* methods |
| `ContextSheetViewModel.cs` | Add `ShowUnhandledSseEvents` observable property + auto-save hook + load in `InitializeAsync` | `_isInitializing` guard required |
| `ChatViewModel.cs` | Add `ShowUnhandledSseEvents` observable property; update on `ProjectPreferenceChangedMessage`; filter in SSE switch | Load initial value in `InitializeAsync` or on first SSE subscription |
| `ContextSheet.xaml` | Add toggle row for "Show unhandled events" | Bound to `ContextSheetViewModel.ShowUnhandledSseEvents` |
| Unit tests | New tests for `ContextSheetViewModel` and `ChatViewModel` | See Acceptance Criteria |

### Dependencies
- `ProjectPreferenceChangedMessage` (already exists in `openMob.Core/Messages/`) — reused as-is; no new message type needed.
- `_isInitializing` guard pattern in `ContextSheetViewModel` — already established; must be applied to the new property.
- `DebugLogger` infrastructure — must continue to log unknown events regardless of the toggle state.
- sqlite-net-pcl `CreateTableAsync<T>()` auto-migration — no manual migration file needed; relies on the existing startup call in `AppDatabase`.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should toggling the switch retroactively remove already-rendered unhandled cards from the `Messages` collection? | Resolved | No — only future incoming SSE events are affected. Existing cards remain visible. |
| 2 | `Hide...` vs `Show...` naming for the property? | Resolved | `ShowUnhandledSseEvents` — semantically clearer (`false` = don't show, `true` = show). |
| 3 | Should `DebugLogger` still log unknown events even when the toggle hides the card? | Resolved | Yes — the log call is unconditional; only the UI card is suppressed. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a fresh install (no `ProjectPreference` row), when the `ChatPage` receives an `UnknownEvent` SSE, then no card is added to the `Messages` collection. *(REQ-004, REQ-009)*
- [ ] **[AC-002]** Given `ShowUnhandledSseEvents = false` (default), when an `UnknownEvent` or `UnknownPart` arrives via SSE, then no card is rendered in the chat. *(REQ-009)*
- [ ] **[AC-003]** Given `ShowUnhandledSseEvents = true`, when an `UnknownEvent` or `UnknownPart` arrives via SSE, then the orange debug card is rendered in the chat (existing behaviour). *(REQ-009)*
- [ ] **[AC-004]** Given the user enables the toggle in the Context Sheet, when the sheet is closed and a new unhandled event arrives, then the card appears in the chat. *(REQ-007, REQ-008, REQ-009)*
- [ ] **[AC-005]** Given the user disables the toggle, when the sheet is closed and a new unhandled event arrives, then no card appears. *(REQ-007, REQ-008, REQ-009)*
- [ ] **[AC-006]** Given the user sets the preference and restarts the app, when the `ChatPage` opens, then the preference is correctly restored. *(REQ-001, REQ-002, REQ-006)*
- [ ] **[AC-007]** Given two projects with different `ShowUnhandledSseEvents` values, when switching between projects, then each project's chat respects its own preference independently. *(REQ-001, REQ-008)*
- [ ] **[AC-008]** Given `ShowUnhandledSseEvents = false`, when an unknown event arrives, then `DebugLogger` still emits the `OM_SSE` log entry. *(REQ-009)*
- [ ] **[AC-009]** Given the Context Sheet is opened, then a "Show unhandled events" toggle is visible and reflects the current preference. *(REQ-010)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Schema evolution:** The project uses sqlite-net-pcl (not EF Core). Adding a column requires only adding the property to the entity class. `CreateTableAsync<T>()` is called at startup in `AppDatabase` and handles `ALTER TABLE ADD COLUMN` automatically. No migration file is needed. The property must have a default value of `false` so existing rows are unaffected.

- **Pattern to follow for `ProjectPreference` extension:** As established in `session-context-sheet-1of3-core` (2026-03-20), the `[Preserve(AllMembers = true)]` and `[Table("ProjectPreferences")]` attributes must be present. Enum properties must be stored as `int`; this property is `bool` and maps directly to `INTEGER`.

- **Pattern to follow for `IProjectPreferenceService`:** Follow `SetAutoAcceptAsync` exactly — upsert via `GetAsync` + insert-or-update, return `bool` success.

- **Pattern to follow for `ContextSheetViewModel`:** Follow the `AutoAccept` property implementation exactly:
  - `[ObservableProperty]` on `_showUnhandledSseEvents`
  - `partial void OnShowUnhandledSseEventsChanged(bool value)` with `_isInitializing` guard
  - Fire-and-forget: `_ = SaveShowUnhandledSseEventsAsync(value)`
  - Load in `InitializeAsync` inside the `_isInitializing = true` block

- **`ChatViewModel` integration:** `ChatViewModel` already subscribes to `ProjectPreferenceChangedMessage` in its constructor. The handler must read `ShowUnhandledSseEvents` from the message or re-call `GetOrDefaultAsync`. The initial value must be loaded during `ChatViewModel` initialisation (e.g., in `LoadSessionAsync` or equivalent startup path). The SSE switch `case UnknownEvent e:` is at approximately line 1076 of `ChatViewModel.cs` — the guard must be added there.

- **`DebugLogger` call must remain unconditional:** The existing `DebugLogger.Log("OM_SSE", ...)` call for unknown events (introduced in `sse-full-message-type-coverage`) must not be gated by `ShowUnhandledSseEvents`. Only the `Messages.Add(...)` call is conditional.

- **Related files:**
  - `src/openMob.Core/Data/Entities/ProjectPreference.cs`
  - `src/openMob.Core/Services/IProjectPreferenceService.cs`
  - `src/openMob.Core/Services/ProjectPreferenceService.cs`
  - `src/openMob.Core/ViewModels/ContextSheetViewModel.cs`
  - `src/openMob.Core/ViewModels/ChatViewModel.cs`
  - `src/openMob.Core/Messages/ProjectPreferenceChangedMessage.cs`
  - `src/openMob/Views/Popups/ContextSheet.xaml`
  - `tests/openMob.Tests/ViewModels/ContextSheetViewModelTests.cs`
  - `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs`
