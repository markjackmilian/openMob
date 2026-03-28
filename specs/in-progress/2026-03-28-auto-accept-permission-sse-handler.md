# Auto-Accept Permission — SSE Handler

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-28                   |
| Status  | In Progress                  |
| Version | 1.0                          |

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
| `ChatViewModelSseTests.cs` | Modified | New test class or new test methods for the auto-accept branch |

### Dependencies
- **`permission-request-inline-approval`** (in progress): must be merged first or developed in parallel on the same branch. `ReplyToPermissionAsync`, `PermissionRequestedEvent`, `_pendingPermissionCount`, `_inFlightPermissionReplies`, and `HasPendingPermissions` are all introduced by that spec. This spec adds a branch inside `HandlePermissionRequested` that those elements already define.
- **`session-context-sheet-3of3`** (in progress): introduces `ChatViewModel.AutoAccept` (bool property, `[ObservableProperty]`). This spec reads that property. Must be merged first or developed in parallel.
- **`sse-project-directory-propagation`** (done): the project-directory filter pattern is already established and must be preserved.

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

- [ ] **[AC-001]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives for the current session/project, when `HandlePermissionRequested` executes, then `ReplyToPermissionAsync` is called with `reply = "always"` and no `ChatMessage` is added to `Messages`. *(REQ-001, REQ-003)*

- [ ] **[AC-002]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives, when `HandlePermissionRequested` executes, then `_pendingPermissionCount` remains unchanged and `HasPendingPermissions` is not set to `true`. *(REQ-002)*

- [ ] **[AC-003]** Given `AutoAccept == false` and a `PermissionRequestedEvent` arrives, when `HandlePermissionRequested` executes, then behaviour is identical to the pre-existing implementation (card rendered, `_pendingPermissionCount` incremented). *(REQ-006)*

- [ ] **[AC-004]** Given `AutoAccept == true` and a `PermissionRequestedEvent` arrives for a **different** session or project directory, when `HandlePermissionRequested` executes, then the event is silently discarded (no auto-reply, no card). *(REQ-004)*

- [ ] **[AC-005]** Given `AutoAccept == true` and `ReplyToPermissionAsync` throws an exception, when `HandlePermissionRequested` executes, then the exception is captured via `SentryHelper.CaptureException` with context `"ChatViewModel.HandlePermissionRequested.AutoAccept"` and no error is shown to the user. *(REQ-005)*

- [ ] **[AC-006]** Given `AutoAccept == true` and the same `requestID` arrives twice concurrently (race condition), when `HandlePermissionRequested` executes for the second event, then `_inFlightPermissionReplies` prevents a duplicate API call. *(REQ-008)*

- [ ] **[AC-007]** All new code paths are covered by unit tests in `ChatViewModelSseTests.cs` using NSubstitute mocks and FluentAssertions. *(all REQs)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- `ChatViewModel.HandlePermissionRequested` — the exact insertion point for the `AutoAccept` branch. The branch must be inserted **after** the project-directory and session-ID filters but **before** the `_dispatcher.Dispatch` call that adds the permission card.
- `_inFlightPermissionReplies` — already a `HashSet<string>` in `ChatViewModel`. The auto-accept path must add the `requestID` to this set before the async call and remove it in a `finally` block, identical to the manual reply path.
- `ChatViewModel.AutoAccept` — an `[ObservableProperty]` bool introduced by `session-context-sheet-3of3`. Reading it from a background SSE thread is safe because `bool` reads are atomic on all .NET platforms.

### Suggested implementation approach

Inside `HandlePermissionRequested`, after the existing filters and before the `_dispatcher.Dispatch` block:

```csharp
// Auto-accept path [REQ-001 through REQ-008]
if (AutoAccept)
{
    var requestId = e.RequestId;
    if (_inFlightPermissionReplies.Contains(requestId))
        return; // [REQ-008] duplicate guard

    _inFlightPermissionReplies.Add(requestId);
    _ = Task.Run(async () =>
    {
        try
        {
            await _apiClient.ReplyToPermissionAsync(requestId, "always", CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SentryHelper.CaptureException(ex, new Dictionary<string, object>
            {
                ["context"] = "ChatViewModel.HandlePermissionRequested.AutoAccept",
                ["requestId"] = requestId,
                ["sessionId"] = CurrentSessionId ?? "null",
            });
        }
        finally
        {
            _inFlightPermissionReplies.Remove(requestId);
        }
    });
    return; // [REQ-003] do not fall through to card rendering
}
```

### Constraints to respect
- `HandlePermissionRequested` is called from the SSE background task — no `_dispatcher.Dispatch` needed for the auto-accept path (no UI mutation).
- `ConfigureAwait(false)` is mandatory in Core library code (established project convention).
- `async void` is forbidden — use `Task.Run` + fire-and-forget with `_ =` assignment.
- The `_inFlightPermissionReplies` set is accessed from the SSE background thread. Since `HandlePermissionRequested` is always called sequentially (single SSE consumer loop), no additional locking is needed. If concurrent access becomes a concern in future, replace with `ConcurrentDictionary<string, byte>`.
- Reply value must be `"always"` (string literal), not an enum — `ReplyToPermissionAsync` already accepts a `string` parameter per the `permission-request-inline-approval` spec.

### Related files or modules
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — `HandlePermissionRequested` method, `AutoAccept` property, `_inFlightPermissionReplies` field, `_pendingPermissionCount` field
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — `ReplyToPermissionAsync` signature
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — existing SSE handler tests; new auto-accept tests go here
- `specs/in-progress/2026-03-25-permission-request-inline-approval.md` — defines `ReplyToPermissionAsync`, `PermissionRequestedEvent`, `_inFlightPermissionReplies`
- `specs/in-progress/2026-03-19-session-context-sheet-3of3-thinking-autoaccept-subagent.md` — defines `ChatViewModel.AutoAccept`
- `specs/done/2026-03-21-sse-project-directory-propagation.md` — defines the project-directory filter pattern

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

### Files to Create

_None_ — this spec only modifies existing files.

### Files to Modify

- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add auto-accept branch inside `HandlePermissionRequested`
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — add unit tests for the auto-accept branch (AC-001 through AC-006)

### Code Inspection Findings

> Critical findings from reading the actual source before writing the brief.

1. **`PermissionRequestedEvent.Id` — not `RequestId`**: The spec's suggested code snippet uses `e.RequestId`, but the actual model (`src/openMob.Core/Models/ChatEvent.cs` line 99) declares the field as `Id`. The correct reference is `e.Id`. The `PermissionId` property is a legacy alias for `Id` and should not be used. **The implementation must use `e.Id`.**

2. **No session-ID filter in current `HandlePermissionRequested`**: The current implementation (lines 1557–1576) only applies the `ProjectDirectory` filter — there is no `SessionId` check. The spec says to apply "the same project-directory and session-ID filters already present", but the session-ID filter is absent from this handler (unlike `HandleMessageUpdated`, `HandleSessionUpdated`, etc.). The auto-accept branch must therefore only apply the project-directory filter that is already present, consistent with the existing handler behaviour. No new session-ID filter should be added (that would be a behaviour change outside this spec's scope).

3. **`_inFlightPermissionReplies` is a `HashSet<string>`**: The `Add` method returns `bool` — use `if (!_inFlightPermissionReplies.Add(requestId)) return;` as the duplicate guard (same pattern as `ReplyToPermissionAsync` at line 1755).

4. **All dependencies are already present**: `AutoAccept` (line 287), `_inFlightPermissionReplies` (line 64), `_apiClient.ReplyToPermissionAsync` (line 1768), `HasPendingPermissions` (line 277), `_pendingPermissionCount` (line 58) — all exist in the current codebase. No new interfaces, services, or NuGet packages are needed.

5. **`HandlePermissionRequested` insertion point**: The auto-accept block must be inserted at line 1562, after the `ProjectDirectory` filter (lines 1559–1561) and before the `_dispatcher.Dispatch` call (line 1563).

### Technical Dependencies

- No new NuGet packages required
- No schema changes required
- No new interfaces required
- All referenced APIs (`ReplyToPermissionAsync`, `AutoAccept`, `_inFlightPermissionReplies`) are already present

### Technical Risks

- **`Task.Run` fire-and-forget**: The auto-accept path uses `_ = Task.Run(...)`. If the SSE loop is cancelled while the fire-and-forget task is in-flight, `CancellationToken.None` ensures the API call completes. This is intentional per the spec.
- **`_inFlightPermissionReplies` thread safety**: The set is accessed from the SSE background thread (sequential consumer loop) and from `ReplyToPermissionAsync` (which can be called from the UI thread). The fire-and-forget `Task.Run` lambda accesses the set from a thread-pool thread. This is a pre-existing concern in the codebase; the spec explicitly notes it is acceptable for the current sequential SSE consumer pattern.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/auto-accept-permission-sse-handler`
2. [om-mobile-core] Modify `HandlePermissionRequested` in `ChatViewModel.cs`
3. [om-tester] Write unit tests in `ChatViewModelSseTests.cs` (can start once step 2 is complete)
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-008]` requirements implemented
- [ ] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [ ] Unit tests written for all new code paths in `HandlePermissionRequested`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
