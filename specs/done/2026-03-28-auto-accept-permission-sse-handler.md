# Auto-Accept Permission — SSE Handler

## Metadata
| Field       | Value                                              |
|-------------|----------------------------------------------------|
| Date        | 2026-03-28                                         |
| Status      | **Completed**                                      |
| Version     | 1.0                                                |
| Completed   | 2026-03-28                                         |
| Branch      | feature/auto-accept-permission-sse-handler (merged)|
| Merged into | develop                                            |

---

## Executive Summary

When the user enables the **Auto-Accept** toggle in the message composer, the app must automatically approve incoming `permission.asked` SSE events without showing a permission card to the user. Currently `AutoAccept` is persisted in `ProjectPreference` and displayed in the UI, but the SSE handler in `ChatViewModel` never reads it — every `permission.asked` event always renders an inline permission card regardless of the toggle state. This spec adds the auto-accept interception layer inside the existing `HandlePermissionRequested` method so that, when `AutoAccept == true`, the app silently calls `POST /permission/{requestID}/reply` with `{ "reply": "always" }` and suppresses the card.

---

## Scope

### In Scope
- Reading `ChatViewModel.AutoAccept` inside `HandlePermissionRequested` before deciding whether to show a card or auto-reply
- Calling `IOpencodeApiClient.ReplyToPermissionAsync` automatically with reply `"always"` when auto-accept is active
- Suppressing the inline permission card and not incrementing `_pendingPermissionCount` when auto-replying
- Emitting a `PermissionRepliedEvent`-equivalent state update so the SSE `permission.replied` handler can resolve any card that was already shown (edge case: auto-accept toggled on mid-session)
- Unit tests covering the auto-accept branch in `HandlePermissionRequested`

### Out of Scope
- Changing how `AutoAccept` is persisted (already handled by `session-context-sheet-3of3`)
- Changing the UI toggle itself (already handled by `session-context-sheet-3of3`)
- Showing any visual indicator that a permission was auto-accepted (no toast, no card)
- Auto-accepting permissions that arrived **before** the toggle was enabled (no retroactive replay)
- Directory-level auto-accept (opencode UI concept not present in openMob — openMob is always scoped to a single active project)
- Changing the `PromptInput` / `SendPromptRequest` wire format (auto-accept is client-side only, not a server parameter)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** When a `PermissionRequestedEvent` arrives via SSE and `ChatViewModel.AutoAccept` is `true`, the app MUST call `IOpencodeApiClient.ReplyToPermissionAsync` with reply value `"always"` immediately, without dispatching to the UI thread or adding any message to `Messages`.

2. **[REQ-002]** When auto-replying, the app MUST NOT increment `_pendingPermissionCount` and MUST NOT set `HasPendingPermissions = true`.

3. **[REQ-003]** When auto-replying, the app MUST NOT add a `ChatMessage` of kind `PermissionRequest` to the `Messages` collection.

4. **[REQ-004]** The auto-reply call MUST apply the same project-directory and session-ID filters already present in `HandlePermissionRequested` (as established by `sse-project-directory-propagation`). If the event does not belong to the current session/project, it is silently discarded regardless of `AutoAccept`.

5. **[REQ-005]** If the `ReplyToPermissionAsync` call fails (network error, server error), the failure MUST be captured via `SentryHelper.CaptureException` with context `"ChatViewModel.HandlePermissionRequested.AutoAccept"`. The failure MUST NOT surface an error to the user (silent fail — the server will re-ask or the session will time out naturally).

6. **[REQ-006]** When `AutoAccept` is `false` (or the event does not pass the session/project filter), `HandlePermissionRequested` MUST behave exactly as it does today — rendering the inline permission card and incrementing `_pendingPermissionCount`.

7. **[REQ-007]** The auto-reply MUST use reply value `"always"` (not `"once"`), matching the opencode UI behaviour (`respondOnce` in `permission.tsx` uses `"once"` for the listener path, but the intent of the toggle is permanent acceptance for the session — `"always"` is the correct semantic for a user-controlled toggle).

8. **[REQ-008]** The `_inFlightPermissionReplies` guard (already present in `ChatViewModel` to prevent duplicate concurrent API calls) MUST also be applied to the auto-accept path to prevent duplicate auto-replies for the same `requestID`.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatViewModel.cs` | Modified | `HandlePermissionRequested` gains an `AutoAccept` branch before the existing card-rendering path |
| `IOpencodeApiClient.cs` | None | `ReplyToPermissionAsync` already exists (from `permission-request-inline-approval`) |
| `OpencodeApiClient.cs` | None | No changes needed |
| `ChatMessage.cs` | None | No new message kind needed |
| `ChatPage.xaml` | None | No UI changes |
| `ChatViewModelSseTests.cs` | Modified | 6 new test methods for the auto-accept branch |

### Dependencies
- **`permission-request-inline-approval`** (done): `ReplyToPermissionAsync`, `PermissionRequestedEvent`, `_pendingPermissionCount`, `_inFlightPermissionReplies`, and `HasPendingPermissions` all present.
- **`session-context-sheet-3of3`** (done): `ChatViewModel.AutoAccept` present.
- **`sse-project-directory-propagation`** (done): project-directory filter pattern preserved.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the auto-reply use `"always"` or `"once"`? | Resolved | `"always"` — the toggle represents a persistent session-level decision. `"once"` would require re-approval on every tool call. |
| 2 | Should a failed auto-reply silently fail or surface an error? | Resolved | Silent fail with Sentry capture. The user enabled auto-accept intentionally; surfacing an error for every failed auto-reply would be disruptive. |
| 3 | Should auto-accept apply to all permission types or only specific ones (e.g. only `edit`)? | Resolved | All permission types — the toggle is a blanket "trust this session" switch, matching the opencode UI behaviour. |
| 4 | What happens if `AutoAccept` is toggled on mid-session while a permission card is already visible? | Resolved | Out of scope. The existing card remains visible and must be resolved manually. Auto-accept only applies to future `permission.asked` events. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives for the current session/project, when `HandlePermissionRequested` executes, then `ReplyToPermissionAsync` is called with `reply = "always"` and no `ChatMessage` is added to `Messages`. *(REQ-001, REQ-003)*

- [x] **[AC-002]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives, when `HandlePermissionRequested` executes, then `_pendingPermissionCount` remains unchanged and `HasPendingPermissions` is not set to `true`. *(REQ-002)*

- [x] **[AC-003]** Given `AutoAccept == false` and a `PermissionRequestedEvent` arrives, when `HandlePermissionRequested` executes, then behaviour is identical to the pre-existing implementation (card rendered, `_pendingPermissionCount` incremented). *(REQ-006)*

- [x] **[AC-004]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives for a **different** session or project directory, when `HandlePermissionRequested` executes, then the event is silently discarded (no auto-reply, no card). *(REQ-004)*

- [x] **[AC-005]** Given `AutoAccept == true` and `ReplyToPermissionAsync` throws an exception, when `HandlePermissionRequested` executes, then the exception is captured via `SentryHelper.CaptureException` with context `"ChatViewModel.HandlePermissionRequested.AutoAccept"` and no error is shown to the user. *(REQ-005)*

- [x] **[AC-006]** Given `AutoAccept == true` and the same `requestID` arrives twice concurrently (race condition), when `HandlePermissionRequested` executes for the second event, then `_inFlightPermissionReplies` prevents a duplicate API call. *(REQ-008)*

- [x] **[AC-007]** All new code paths are covered by unit tests in `ChatViewModelSseTests.cs` using NSubstitute mocks and FluentAssertions. *(all REQs)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- `ChatViewModel.HandlePermissionRequested` — the exact insertion point for the `AutoAccept` branch. The branch must be inserted **after** the project-directory and session-ID filters but **before** the `_dispatcher.Dispatch` call that adds the permission card.
- `_inFlightPermissionReplies` — already a `HashSet<string>` in `ChatViewModel`. The auto-accept path must add the `requestID` to this set before the async call and remove it in a `finally` block, identical to the manual reply path.
- `ChatViewModel.AutoAccept` — an `[ObservableProperty]` bool introduced by `session-context-sheet-3of3`. Reading it from a background SSE thread is safe because `bool` reads are atomic on all .NET platforms.

### Constraints to respect
- `HandlePermissionRequested` is called from the SSE background task — no `_dispatcher.Dispatch` needed for the auto-accept path (no UI mutation).
- `ConfigureAwait(false)` is mandatory in Core library code (established project convention).
- `async void` is forbidden — use `Task.Run` + fire-and-forget with `_ =` assignment.
- The `_inFlightPermissionReplies` set is accessed from the SSE background thread. Since `HandlePermissionRequested` is always called sequentially (single SSE consumer loop), no additional locking is needed.
- Reply value must be `"always"` (string literal), not an enum.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-28

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature (behaviour extension) |
| Git Flow branch | `feature/auto-accept-permission-sse-handler` |
| Branches from | `develop` |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files Modified

- `src/openMob.Core/ViewModels/ChatViewModel.cs` — auto-accept branch added inside `HandlePermissionRequested`
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — 6 new tests for AC-001 through AC-006

### Code Inspection Findings

1. **`PermissionRequestedEvent.Id` — not `RequestId`**: The spec's suggested code snippet used `e.RequestId`, but the actual model declares the field as `Id`. Implementation correctly uses `e.Id`.

2. **No session-ID filter in `HandlePermissionRequested`**: The handler only applies the `ProjectDirectory` filter (no `SessionId` check). The auto-accept branch follows the same pattern — no new session-ID filter added.

3. **`_inFlightPermissionReplies.Add()` return value**: Used as `if (!_inFlightPermissionReplies.Add(requestId)) return;` — atomic add+check, same pattern as `ReplyToPermissionAsync`.

4. **`_inFlightPermissionReplies` guard confirmed effective**: `om-tester` verified that the `Task.Run` is still in-flight when a second sequential event arrives within the 200ms window, so the guard correctly blocks the duplicate. Test uses `Received(1)` to pin this behaviour.

### Technical Dependencies

- No new NuGet packages
- No schema changes
- No new interfaces
- All referenced APIs already present

### Review Outcome

- **om-reviewer verdict:** ⚠️ Approved with remarks
- **Critical findings:** 0
- **Major findings:** 0
- **Minor findings:** 2 (both resolved)
  - [m-001] XML doc `<param>` gap — resolved by om-mobile-core
  - [m-002] Misleading test name — resolved by om-tester (renamed + assertion corrected to `Received(1)`)

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-008]` requirements implemented
- [x] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [x] Unit tests written for all new code paths (63/63 pass)
- [x] `om-reviewer` verdict: ⚠️ Approved with remarks
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
