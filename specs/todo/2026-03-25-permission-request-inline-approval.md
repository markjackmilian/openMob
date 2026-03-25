# Permission Request Handling — Inline Chat Approval

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-25                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

When the opencode server requires user approval for a tool call (e.g. running a bash command, editing a file), it broadcasts a `permission.asked` SSE event. Currently the app ignores this event, causing the session to appear frozen. This feature intercepts `permission.asked` events and renders an inline permission card in the chat with three actionable choices — **Always**, **Once**, **Deny** — and sends the user's reply to `POST /permission/{requestID}/reply`.

---

## Scope

### In Scope
- Full parsing of the `permission.asked` SSE payload into a typed `PermissionRequestedEvent` (all fields: `id`, `sessionID`, `permission`, `patterns`, `metadata`, `always`, `tool`)
- Injection of a permission card as an inline `ChatMessage` into the `Messages` collection
- Display of permission type (`permission` field) and requested values (`patterns` field) on the card
- Three reply actions: **Always** (`always`), **Once** (`once`), **Deny** (`reject`)
- `POST /permission/{requestID}/reply` API call on user action
- Visual "resolved" state on the card after reply (action label shown, buttons disabled)
- Non-blocking visual indicator when at least one permission card is pending
- Support for multiple simultaneous permission cards (each independent)
- New `ReplyToPermissionAsync` method on `IOpencodeApiClient` / `OpencodeApiClient`

### Out of Scope
- Handling of the `permission.replied` SSE event (post-reply broadcast)
- Polling `GET /permission` to restore pending permissions on app restart
- Local persistence of "always" approval rules (managed server-side)
- Optional `message` feedback field on Deny (always sent without message body)
- Filtering permission cards by active session (all sessions shown)
- Cleanup of server-side pending permissions on chat abandonment

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The `ChatEventParser` must parse the SSE event type `permission.asked` and produce a `PermissionRequestedEvent` containing: `Id` (string, `^per.*`), `SessionId` (string), `Permission` (string), `Patterns` (string[]), `Metadata` (Dictionary<string, object>), `Always` (string[]), and optional `Tool` (with `MessageId` and `CallId`).

2. **[REQ-002]** The `ChatViewModel` must handle `PermissionRequestedEvent` in its SSE switch and inject a new `ChatMessage` of kind `PermissionRequest` into the `Messages` collection via `_dispatcher.Dispatch`.

3. **[REQ-003]** The permission `ChatMessage` must expose: the permission type label, the list of requested patterns, the `RequestId` needed to reply, and a mutable `PermissionStatus` property (`Pending` / `Resolved`) with the chosen reply value.

4. **[REQ-004]** `IOpencodeApiClient` must declare a method `ReplyToPermissionAsync(string requestId, string reply, CancellationToken ct)` where `reply` is one of `"once"`, `"always"`, `"reject"`.

5. **[REQ-005]** `OpencodeApiClient` must implement `ReplyToPermissionAsync` by calling `POST /permission/{requestId}/reply` with JSON body `{ "reply": "<value>" }` and the standard `x-opencode-directory` header.

6. **[REQ-006]** `ChatViewModel` must expose a reply command (e.g. `ReplyToPermissionCommand(string requestId, string reply)`) that: calls `ReplyToPermissionAsync`, updates the matching `ChatMessage.PermissionStatus` to `Resolved`, stores the chosen reply label on the message, and decrements the pending permission count.

7. **[REQ-007]** `ChatViewModel` must expose an observable property `HasPendingPermissions` (bool) that is `true` when at least one permission card in `Messages` has `PermissionStatus == Pending`, and `false` otherwise.

8. **[REQ-008]** The chat UI must display a non-blocking visual indicator (e.g. a banner above the input bar or a status label) bound to `HasPendingPermissions`, visible when `true` and hidden when `false`. The message input field must remain enabled.

9. **[REQ-009]** Multiple `permission.asked` events must each produce an independent permission card; all cards are simultaneously visible and actionable.

10. **[REQ-010]** If `ReplyToPermissionAsync` fails (network error, non-2xx response), the card must remain in `Pending` state and an error must be captured via `SentryHelper.CaptureException`.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatEventParser.cs` | Modify | Add full parsing of `permission.asked` payload into typed fields |
| `ChatEvent.cs` (`PermissionRequestedEvent`) | Modify | Expand from `RawPayload: JsonElement` to fully typed properties |
| `ChatMessage.cs` | Modify | Add `MessageKind` discriminator (or `IsPermissionRequest` flag), `PermissionStatus`, `PermissionType`, `PermissionPatterns`, `RequestId`, `ResolvedReply` |
| `ChatViewModel.cs` | Modify | Add `PermissionRequestedEvent` handler, `ReplyToPermissionCommand`, `HasPendingPermissions` property |
| `IOpencodeApiClient.cs` | Modify | Add `ReplyToPermissionAsync` method signature |
| `OpencodeApiClient.cs` | Modify | Implement `ReplyToPermissionAsync` with `POST /permission/{id}/reply` |
| `ChatPage.xaml` | Modify | Add `DataTemplate` for permission card; bind `HasPendingPermissions` to pending indicator |
| `ChatViewModelSseTests.cs` | Modify / New | Add tests for `PermissionRequestedEvent` handling and reply command |
| `ChatEventParserTests.cs` | Modify | Add test cases for `permission.asked` parsing |

### Dependencies
- Existing SSE infrastructure: `OpencodeApiClient.SubscribeToEventsAsync` → `ChatEventParser` → `ChatViewModel.StartSseSubscriptionAsync`
- `x-opencode-directory` header injection already handled globally in `OpencodeApiClient` (as established in the Global Directory Header Injection ADR)
- `_dispatcher.Dispatch` pattern already used for all UI mutations in `ChatViewModel`
- `SentryHelper.CaptureException` for error reporting (established pattern)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the Deny action expose an optional feedback message field to the user (maps to `message` in the request body)? | Open | Deferred — Deny always sent without `message` field for now |
| 2 | If the user navigates away from the chat without replying, the permission remains pending server-side until server timeout. Should the app attempt any cleanup (e.g. auto-reject on `OnDisappearing`)? | Open | Not in scope for v1.0 |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given an SSE `permission.asked` event arrives on the stream, when `ChatEventParser` processes it, then a `PermissionRequestedEvent` is produced with all typed fields correctly populated (id, sessionID, permission, patterns, always, tool). *(REQ-001)*

- [ ] **[AC-002]** Given a `PermissionRequestedEvent` is received by `ChatViewModel`, when the SSE handler runs, then a new `ChatMessage` of kind `PermissionRequest` appears at the bottom of the `Messages` collection with the correct `PermissionType` and `PermissionPatterns`. *(REQ-002, REQ-003)*

- [ ] **[AC-003]** Given a permission card is visible with `PermissionStatus == Pending`, when the user taps **Always**, then `POST /permission/{id}/reply` is called with `{ "reply": "always" }`, the card transitions to `Resolved` showing "Always" as the chosen action, and buttons are disabled. *(REQ-004, REQ-005, REQ-006)*

- [ ] **[AC-004]** Given a permission card is visible with `PermissionStatus == Pending`, when the user taps **Once**, then `POST /permission/{id}/reply` is called with `{ "reply": "once" }` and the card transitions to `Resolved`. *(REQ-004, REQ-005, REQ-006)*

- [ ] **[AC-005]** Given a permission card is visible with `PermissionStatus == Pending`, when the user taps **Deny**, then `POST /permission/{id}/reply` is called with `{ "reply": "reject" }` and the card transitions to `Resolved`. *(REQ-004, REQ-005, REQ-006)*

- [ ] **[AC-006]** Given at least one permission card has `PermissionStatus == Pending`, when the chat is visible, then `HasPendingPermissions` is `true` and the pending indicator is shown; the message input remains enabled. *(REQ-007, REQ-008)*

- [ ] **[AC-007]** Given all permission cards are resolved, when the last card transitions to `Resolved`, then `HasPendingPermissions` becomes `false` and the pending indicator disappears. *(REQ-007, REQ-008)*

- [ ] **[AC-008]** Given three `permission.asked` events arrive in quick succession, when they are processed, then three independent permission cards appear in `Messages`, each actionable independently. *(REQ-009)*

- [ ] **[AC-009]** Given `ReplyToPermissionAsync` throws a network exception, when the reply command executes, then the card remains in `Pending` state, the exception is captured via `SentryHelper.CaptureException`, and no crash occurs. *(REQ-010)*

- [ ] **[AC-010]** Given a `permission.asked` event with `sessionID` from a non-active session, when it arrives, then a permission card is still injected into the current chat view (no session filtering). *(REQ-002)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- `ChatEventParser.cs`: current `PermissionRequestedEvent` parsing uses `JsonElement RawPayload` — needs to be replaced with typed field extraction matching the `PermissionRequest` OpenAPI schema (`id`, `sessionID`, `permission`, `patterns`, `metadata`, `always`, `tool?`)
- `ChatMessage.cs`: evaluate whether to add a `MessageKind` enum (`Text`, `PermissionRequest`) or a simpler `bool IsPermissionRequest` flag. A `MessageKind` enum is preferred for extensibility (future system messages). Add observable properties: `PermissionStatus` (`Pending`/`Resolved`), `PermissionType` (string), `PermissionPatterns` (IReadOnlyList<string>), `RequestId` (string), `ResolvedReply` (string?)
- `ChatViewModel.cs`: `HasPendingPermissions` should be recomputed whenever any `ChatMessage.PermissionStatus` changes — consider using `ObservableCollection` change events or a counter field `_pendingPermissionCount` (int, incremented on card add, decremented on resolve) for O(1) updates
- `OpencodeApiClient.cs`: `ReplyToPermissionAsync` follows the same pattern as `ExecuteAsync` — use `PostAsync` with `StringContent` JSON body; `requestId` goes in the URL path, not the body

### Suggested implementation approach
1. Expand `PermissionRequestedEvent` record with typed properties (Core model layer)
2. Update `ChatEventParser` switch case for `permission.asked` to deserialise all fields
3. Add `MessageKind` enum and permission-related properties to `ChatMessage`
4. Add `ReplyToPermissionAsync` to `IOpencodeApiClient` + `OpencodeApiClient`
5. Add handler in `ChatViewModel.StartSseSubscriptionAsync` switch + `ReplyToPermissionCommand` + `HasPendingPermissions`
6. Add XAML `DataTemplate` for `MessageKind.PermissionRequest` in `ChatPage.xaml`
7. Add unit tests in `ChatEventParserTests` and `ChatViewModelSseTests`

### Constraints to respect
- Zero business logic in XAML code-behind — all state in `ChatViewModel`
- `_dispatcher.Dispatch` required for all `Messages` collection mutations (thread safety)
- `ConfigureAwait(false)` in `OpencodeApiClient` and service code
- `[ObservableProperty]` source generators for all new observable properties on `ChatMessage`
- `SentryHelper.CaptureException` on any caught exception in the reply command
- The `x-opencode-directory` header is already injected globally — no per-call header needed

### Related files or modules
- `src/openMob.Core/Models/ChatEvent.cs` — `PermissionRequestedEvent` record
- `src/openMob.Core/Helpers/ChatEventParser.cs` — SSE parsing switch
- `src/openMob.Core/Models/ChatMessage.cs` — domain message model
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — SSE loop (`StartSseSubscriptionAsync`), `Messages` collection
- `src/openMob.Core/Services/IOpencodeApiClient.cs` — interface
- `src/openMob.Core/Services/OpencodeApiClient.cs` — HTTP implementation
- `src/openMob/Views/ChatPage.xaml` — CollectionView DataTemplates
- `tests/openMob.Tests/ChatEventParserTests.cs` — parser unit tests
- `tests/openMob.Tests/ChatViewModelSseTests.cs` — ViewModel SSE tests

### Past decisions to honour
- As established in the **SSE Project Directory Propagation** spec, `ProjectDirectory` must be extracted and propagated on every event including `PermissionRequestedEvent`
- As established in the **Global Directory Header ADR**, `x-opencode-directory` is injected globally via `GetCachedWorktree()` — `ReplyToPermissionAsync` inherits this automatically
- As established in the **New Session Button** tech analysis, `POST` calls with no meaningful body use `PostAsync(url, null)` — here we DO have a body so use `StringContent` with `application/json`
- SSE `yield return` cannot be inside `try/catch` — the existing `SubscribeToEventsAsync` structure must not be altered; new event handling belongs in `ChatViewModel`, not in the parser
