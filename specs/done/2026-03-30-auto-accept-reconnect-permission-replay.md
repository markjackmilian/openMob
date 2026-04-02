# Auto-Accept — Pending Permission Replay on SSE Reconnect

## Metadata
| Field       | Value                                        |
|-------------|----------------------------------------------|
| Date        | 2026-03-30                                   |
| Status      | **Completed**                                |
| Version     | 1.0                                          |
| Completed   | 2026-03-31                                   |
| Branch      | feature/auto-accept-reconnect-replay (merged) |
| Merged into | develop                                      |

---

## Executive Summary

The existing auto-accept feature (`auto-accept-permission-sse-handler`) intercepts `permission.asked` SSE events and replies automatically when the `AutoAccept` toggle is enabled. However, on mobile the SSE connection can be lost while the app is in the background or the network is unavailable. During that window the opencode server may emit one or more `permission.asked` events that the client never receives. Because the server blocks indefinitely waiting for a reply (no server-side timeout exists — confirmed by reading `permission/index.ts`), the session freezes until the client reconnects and replies. This spec adds a **reconnect replay** step: when the `IHeartbeatMonitorService` transitions from `Lost` back to `Healthy` and `AutoAccept` is `true`, the app queries the server for all pending permissions on the active session and replies `"always"` to each one, unblocking the session automatically.

---

## Scope

### In Scope
- Hook into the existing `IHeartbeatMonitorService.HealthStateChanged` event inside `ChatViewModel` to detect the `Lost → Healthy` transition
- On reconnect, call `GET /permission` (via a new `GetPendingPermissionsAsync` method on `IOpencodeApiClient`) to retrieve all pending permission requests for the active session
- If `AutoAccept == true`, reply `"always"` to each pending permission using the existing `ReplyToPermissionAsync` method
- Reuse the existing `_inFlightPermissionReplies` guard to prevent duplicate replies
- Capture any reply failure via `SentryHelper.CaptureException` (silent fail — same policy as the existing auto-accept path)
- Unit tests covering the reconnect replay path in `ChatViewModelReconnectTests` (new test class) or `ChatViewModelSseTests` (extended)

### Out of Scope
- Changing the server-side permission timeout behaviour (server is open-source but out of scope for this project)
- Replaying permissions when `AutoAccept == false` (user must reply manually via the existing permission cards)
- Showing a visual indicator that a replay occurred (silent operation)
- Handling the case where the session itself has been aborted or deleted while the client was offline
- Polling for pending permissions at any time other than the `Lost → Healthy` transition
- Changes to `IHeartbeatMonitorService` interface or `HeartbeatMonitorService` implementation (the existing `HealthStateChanged` event is sufficient)
- Changes to the `heartbeat-monitor-footer` spec (already in-progress); this spec only adds a subscriber to the existing event

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `IOpencodeApiClient` MUST expose a new method:
   ```csharp
   Task<OpencodeResult<IReadOnlyList<PermissionRequestDto>>> GetPendingPermissionsAsync(
       string sessionId, CancellationToken ct = default);
   ```
   The implementation calls `GET /session/{sessionId}/permissions` and deserialises the response as a list of permission request objects. Each `PermissionRequestDto` MUST include at minimum: `Id` (string), `SessionId` (string), `Permission` (string), `Patterns` (string[]).

2. **[REQ-002]** `ChatViewModel` MUST subscribe to `IHeartbeatMonitorService.HealthStateChanged` during its initialisation (or when the SSE subscription starts) and unsubscribe on disposal.

3. **[REQ-003]** When `HealthStateChanged` fires with `ConnectionHealthState.Lost → ConnectionHealthState.Healthy` (i.e. the new state is `Healthy` and the previous state was `Lost` or `Degraded`), `ChatViewModel` MUST invoke a new internal method `ReplayPendingPermissionsAsync`.

4. **[REQ-004]** `ReplayPendingPermissionsAsync` MUST:
   a. Return immediately (no-op) if `AutoAccept == false`.
   b. Return immediately (no-op) if `CurrentSessionId` is null or empty.
   c. Call `GetPendingPermissionsAsync(CurrentSessionId)`.
   d. For each returned permission request, call `ReplyToPermissionAsync(requestId, "always")` using the existing method, which already applies the `_inFlightPermissionReplies` duplicate guard.
   e. Run the replies sequentially (not in parallel) to avoid race conditions on the server's `approved` ruleset.

5. **[REQ-005]** If `GetPendingPermissionsAsync` returns a failure result (network error, non-2xx), the failure MUST be captured via `SentryHelper.CaptureException` with context `"ChatViewModel.ReplayPendingPermissionsAsync"`. No error is shown to the user.

6. **[REQ-006]** If any individual `ReplyToPermissionAsync` call fails during the replay loop, the failure MUST be captured via `SentryHelper.CaptureException` with context `"ChatViewModel.ReplayPendingPermissionsAsync.Reply"`. The loop MUST continue to the next pending permission (fail-and-continue, not fail-fast).

7. **[REQ-007]** `ReplayPendingPermissionsAsync` MUST be fire-and-forget from the `HealthStateChanged` handler (i.e. `_ = ReplayPendingPermissionsAsync(CancellationToken.None)`). It MUST NOT block the `HealthStateChanged` callback thread.

8. **[REQ-008]** The `HealthStateChanged` subscription in `ChatViewModel` MUST track the **previous** health state so that the replay is triggered only on a transition **from** `Lost` or `Degraded` **to** `Healthy`, not on every `Healthy` notification (e.g. repeated heartbeats while already healthy do not trigger replays).

9. **[REQ-009]** A new `PermissionRequestDto` sealed record MUST be added to `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` with the fields required by REQ-001. It MUST NOT duplicate or replace the existing `PermissionReplyRequest` or `PermissionResponseRequest` DTOs.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `IOpencodeApiClient.cs` | Modified | Add `GetPendingPermissionsAsync` method signature |
| `OpencodeApiClient.cs` | Modified | Implement `GetPendingPermissionsAsync` via `GET /permission` (global endpoint, filtered by sessionId client-side) |
| `ChatViewModel.cs` | Modified | Add `_previousHealthState` field; extend `OnHealthStateChanged` to track transitions and fire `ReplayPendingPermissionsAsync` |
| `PermissionRequestDto.cs` | Created | New response DTO for the pending permissions list endpoint |
| `ChatViewModelReconnectTests.cs` | Created | New test class covering REQ-003 through REQ-008 |

### Dependencies
- **`auto-accept-permission-sse-handler`** (done): `ReplyToPermissionAsync`, `_inFlightPermissionReplies`, `AutoAccept`, `HasPendingPermissions` all present in `ChatViewModel`.
- **`heartbeat-monitor-footer`** (in-progress): `IHeartbeatMonitorService` already injected into `ChatViewModel`; `HealthStateChanged` event already defined. This spec adds a subscriber — no changes to the service interface or implementation are needed.
- **`permission-request-inline-approval`** (done): `ReplyToPermissionAsync` internal method already handles the full reply flow including card resolution and `_pendingPermissionCount` management.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Does `GET /session/:id/permissions` exist in the opencode server API? | Resolved | **CORRECTED**: The actual endpoint is `GET /permission` (global, not session-scoped). Confirmed by reading `packages/opencode/src/server/routes/permission.ts` — the `PermissionRoutes` is mounted at `/permission` and `GET /` returns all pending permissions across all sessions as `Permission.Request[]`. The spec's assumed path `GET /session/:id/permissions` does NOT exist. The implementation must call `GET /permission` and filter client-side by `sessionID`. |
| 2 | Should the replay also resolve any permission cards already visible in the `Messages` collection (cards that appeared before the disconnect)? | Resolved | Yes — `ReplyToPermissionAsync` already calls `ResolvePermissionRequest` which updates the card state. No additional logic needed. |
| 3 | Should the replay be triggered on `Degraded → Healthy` as well as `Lost → Healthy`? | Resolved | Yes — both transitions should trigger the replay. During `Degraded` state the SSE connection may have already dropped silently. Replaying on any `→ Healthy` transition (from non-Healthy) is safe because the `_inFlightPermissionReplies` guard prevents duplicate replies. |
| 4 | What if `CurrentSessionId` changes between the `Lost` event and the `Healthy` event (user switched session while offline)? | Resolved | `ReplayPendingPermissionsAsync` reads `CurrentSessionId` at invocation time. If the session changed, the new session has no pending permissions (it was just loaded), so `GetPendingPermissionsAsync` returns an empty list — no-op. Safe. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given `AutoAccept == true` and the health state transitions from `Lost` to `Healthy`, when `HealthStateChanged` fires, then `GetPendingPermissionsAsync` is called for the active session and each returned permission is replied with `"always"`. *(REQ-003, REQ-004)*

- [ ] **[AC-002]** Given `AutoAccept == false` and the health state transitions from `Lost` to `Healthy`, when `HealthStateChanged` fires, then `GetPendingPermissionsAsync` is NOT called and no reply is sent. *(REQ-004a)*

- [ ] **[AC-003]** Given `AutoAccept == true` and the health state is already `Healthy` and another `Healthy` notification arrives (no state change), when `HealthStateChanged` fires, then `ReplayPendingPermissionsAsync` is NOT invoked. *(REQ-008)*

- [ ] **[AC-004]** Given `AutoAccept == true` and `GetPendingPermissionsAsync` returns two pending permissions, when the replay runs, then `ReplyToPermissionAsync` is called twice sequentially with `"always"` for each permission ID. *(REQ-004d, REQ-004e)*

- [ ] **[AC-005]** Given `AutoAccept == true` and `GetPendingPermissionsAsync` fails with a network error, when the replay runs, then the exception is captured via `SentryHelper.CaptureException` with context `"ChatViewModel.ReplayPendingPermissionsAsync"` and no error is shown to the user. *(REQ-005)*

- [ ] **[AC-006]** Given `AutoAccept == true` and the first of two pending permissions fails to reply, when the replay runs, then the failure is captured via Sentry and the second permission is still replied to. *(REQ-006)*

- [ ] **[AC-007]** Given `AutoAccept == true` and a permission card is already visible in `Messages` (rendered before the disconnect), when the replay replies to that permission, then the card transitions to `Resolved` state (via the existing `ResolvePermissionRequest` path). *(REQ-004d)*

- [ ] **[AC-008]** Given the same `requestId` is returned by `GetPendingPermissionsAsync` and is also in `_inFlightPermissionReplies` (concurrent auto-accept from SSE), when the replay runs, then no duplicate API call is made. *(REQ-004d — reuse of existing guard)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

1. **Verify `GET /session/:id/permissions` endpoint**: Before implementing `GetPendingPermissionsAsync`, fetch the live OpenAPI spec from the running server at `GET /doc` (or inspect `packages/opencode/src/server/` in the opencode GitHub repo) to confirm the exact path, HTTP method, and response schema for listing pending permissions. The server source shows `Permission.list()` exists and is exposed via the HTTP layer — confirm the route matches `GET /session/{id}/permissions` or adjust accordingly.

2. **`HealthStateChanged` subscription point in `ChatViewModel`**: The service is already injected (`IHeartbeatMonitorService _heartbeatMonitor`). The subscription should be added in `StartSseSubscriptionAsync` (alongside the SSE loop start) and removed in `Dispose()`. A `_previousHealthState` field (type `ConnectionHealthState`, default `Healthy`) must be added to track transitions.

3. **`ReplayPendingPermissionsAsync` fire-and-forget pattern**: Use `_ = ReplayPendingPermissionsAsync(CancellationToken.None)` from the event handler. The method must be `private async Task` (not `async void`). Wrap the entire body in a `try/catch` to prevent unobserved task exceptions.

4. **`PermissionRequestDto` schema**: The opencode server's `Permission.Request` type (from `permission/index.ts`) has: `id`, `sessionID`, `permission`, `patterns` (string[]), `metadata` (object), `always` (string[]), `tool?` (object with `messageID`, `callID`). The DTO needs at minimum `Id` and the fields needed to call `ReplyToPermissionAsync`. Map field names using `[JsonPropertyName]` to match the server's camelCase JSON.

5. **Sequential vs parallel replies**: Use a `foreach` loop (not `Task.WhenAll`) to reply sequentially. The server's `reply` handler in `permission/index.ts` mutates the shared `approved` ruleset — parallel replies could cause race conditions on the server side.

6. **`_inFlightPermissionReplies` reuse**: `ReplyToPermissionAsync(string requestId, string reply, CancellationToken)` already adds to `_inFlightPermissionReplies` before the API call and removes in `finally`. Calling it from `ReplayPendingPermissionsAsync` automatically inherits this guard — no additional locking needed.

### Constraints to respect
- `ConfigureAwait(false)` on all `await` calls in `openMob.Core`.
- No `async void` — use `_ =` fire-and-forget assignment.
- `HealthStateChanged` is raised from the `PeriodicTimer` background thread — the handler must not perform UI mutations directly. `ReplayPendingPermissionsAsync` calls `ReplyToPermissionAsync` which already dispatches UI updates via `_dispatcher.Dispatch`.
- The `_inFlightPermissionReplies` set is accessed from the SSE background thread and now also from the reconnect replay path. Since both paths call `ReplyToPermissionAsync` which is the single entry point for the set, no additional locking is needed (the set is always accessed from a single logical flow at a time).

### Related files or modules
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add `HealthStateChanged` subscription, `_previousHealthState` field, `ReplayPendingPermissionsAsync` method
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — add `GetPendingPermissionsAsync` signature
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — implement `GetPendingPermissionsAsync`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/PermissionRequestDto.cs` — new file
- `tests/openMob.Tests/ViewModels/ChatViewModelReconnectTests.cs` — new test class

### References to past decisions
- As established in **`auto-accept-permission-sse-handler`**: `ReplyToPermissionAsync` is the single entry point for all permission replies; it handles `_inFlightPermissionReplies`, card resolution, and Sentry capture. The replay path must go through this method, not bypass it.
- As established in **`heartbeat-monitor-footer`**: `IHeartbeatMonitorService` is injected into `ChatViewModel` and `HealthStateChanged` is the canonical event for health state transitions. No changes to the service are needed.
- As established in **`permission-request-inline-approval`**: `ResolvePermissionRequest` is called by `ReplyToPermissionAsync` on success — permission cards already visible in `Messages` will be automatically resolved when the replay replies to them.
- As established in **`sse-project-directory-propagation`**: the `x-opencode-directory` header is injected globally by `OpencodeApiClient.ExecuteAsync` — `GetPendingPermissionsAsync` inherits this automatically.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-31

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/auto-accept-reconnect-replay |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Infrastructure/Http/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Data / DTOs | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/PermissionRequestDto.cs` — new response DTO for `GET /permission`

### Files to Modify

- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — add `GetPendingPermissionsAsync` method signature
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — implement `GetPendingPermissionsAsync` calling `GET /permission`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add `_previousHealthState` field; extend `OnHealthStateChanged` to track transitions and fire `ReplayPendingPermissionsAsync`; add `ReplayPendingPermissionsAsync` method

### Critical API Endpoint Correction

> **The spec's assumed endpoint `GET /session/:id/permissions` does NOT exist.**

After reading the opencode server source (`packages/opencode/src/server/routes/permission.ts`), the actual endpoint is:

```
GET /permission
```

This is a **global** endpoint (mounted at `/permission` in `InstanceRoutes`) that returns **all** pending permissions across all sessions as `Permission.Request[]`. The response includes `sessionID` on each item, so the client must filter by `CurrentSessionId` after fetching.

The `Permission.Request` schema (from `permission/index.ts`):
```typescript
{
  id: PermissionID,       // string
  sessionID: SessionID,   // string
  permission: string,
  patterns: string[],
  metadata: Record<string, any>,
  always: string[],
  tool?: { messageID: MessageID, callID: string }
}
```

The `PermissionRequestDto` must map `sessionID` (camelCase) → `SessionId` (PascalCase) using `[JsonPropertyName("sessionID")]`.

### Technical Dependencies

- `auto-accept-permission-sse-handler` (done): `ReplyToPermissionAsync`, `_inFlightPermissionReplies`, `AutoAccept` all present in `ChatViewModel`
- `heartbeat-monitor-footer` (done/merged): `IHeartbeatMonitorService` injected into `ChatViewModel`; `HealthStateChanged` event already subscribed in constructor (line 126 of `ChatViewModel.cs`)
- `permission-request-inline-approval` (done): `ReplyToPermissionAsync` handles full reply flow
- No new NuGet packages required

### Existing Subscription — Important Finding

The `ChatViewModel` constructor **already subscribes** to `HealthStateChanged` at line 126:
```csharp
_heartbeatMonitor.HealthStateChanged += OnHealthStateChanged;
```

And `Dispose()` already unsubscribes:
```csharp
_heartbeatMonitor.HealthStateChanged -= OnHealthStateChanged;
```

**REQ-002 is already satisfied.** The implementation only needs to:
1. Add a `_previousHealthState` field (default `Healthy`)
2. Extend the existing `OnHealthStateChanged` method to track the previous state and fire `ReplayPendingPermissionsAsync` on `→ Healthy` transitions from non-Healthy states

### Technical Risks

- **Thread safety of `_previousHealthState`**: The field is written from the `PeriodicTimer` background thread (via `OnHealthStateChanged`). Since `OnHealthStateChanged` is always called from the same timer thread (single-threaded timer), no locking is needed. However, marking it `volatile` is a safe precaution.
- **Global endpoint returns all sessions**: `GET /permission` returns permissions for all sessions. The client must filter by `CurrentSessionId`. This is safe — the filter is applied before calling `ReplyToPermissionAsync`.
- **No breaking changes**: This is purely additive — new method on interface, new DTO, new field + method in ViewModel.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/auto-accept-reconnect-replay`
2. [om-mobile-core] Implement `PermissionRequestDto`, `GetPendingPermissionsAsync` on interface + client, extend `ChatViewModel` with `_previousHealthState` + `ReplayPendingPermissionsAsync`
3. [om-tester] Write `ChatViewModelReconnectTests` (after om-mobile-core completes)
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [x] REQ-002 already satisfied (subscription exists in constructor)
- [ ] REQ-001: `GetPendingPermissionsAsync` added to `IOpencodeApiClient` and implemented in `OpencodeApiClient`
- [ ] REQ-003, REQ-008: `_previousHealthState` field added; `OnHealthStateChanged` extended to detect `→ Healthy` transitions
- [ ] REQ-004: `ReplayPendingPermissionsAsync` implemented with all sub-requirements
- [ ] REQ-005: Sentry capture on `GetPendingPermissionsAsync` failure
- [ ] REQ-006: Sentry capture per-reply failure with fail-and-continue
- [ ] REQ-007: Fire-and-forget pattern used
- [ ] REQ-009: `PermissionRequestDto` created in correct namespace
- [ ] All `[AC-001]` through `[AC-008]` acceptance criteria satisfied
- [ ] Unit tests written for all new paths in `ChatViewModelReconnectTests`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
