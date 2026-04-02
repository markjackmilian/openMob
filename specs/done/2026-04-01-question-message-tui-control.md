# Question Message — TUI Control Handler

## Metadata
| Field       | Value                              |
|-------------|------------------------------------|
| Date        | 2026-04-01                         |
| Status      | **Completed**                      |
| Version     | 1.0                                |
| Completed   | 2026-04-02                         |
| Branch      | feature/question-tui-control (merged) |
| Merged into | develop                            |

---

## Executive Summary

The opencode server emits a `question` event via the SSE stream when the AI agent needs to ask the user a question before proceeding. This event is currently unrecognised and rendered as an orange "Received an unrecognized message from the server" fallback card. This feature adds full support for the `question` TUI control event: parsing the SSE payload, rendering an interactive inline card with predefined option buttons and an optional free-text field, submitting the user's answer to the server, and recovering any pending question when the user closes and reopens the session.

---

## Scope

### In Scope
- New `ChatEventType.QuestionRequested` enum value
- New `QuestionRequestedEvent` record with all relevant payload fields
- `ChatEventParser` extended to parse the `question` SSE event type
- New `MessageKind.QuestionRequest` on `ChatMessage` with associated properties: question text, options list, free-text flag, question ID, answer status
- `ChatViewModel` SSE handler for `QuestionRequestedEvent`: renders an inline question card
- `ChatViewModel` command `AnswerQuestionAsync(questionId, answer)`: submits the answer via `POST /tui/control/response` and resolves the card
- `IOpencodeApiClient` extended with `RespondToTuiControlAsync(string requestId, string body, CancellationToken ct)` mapping to `POST /tui/control/response`
- Pending question recovery on session open: `LoadMessagesAsync` polls `GET /tui/control/next` with a short timeout; if a pending `question` control request is found for the current session, it is injected into the `Messages` collection as a pending card
- UI: inline question card in `ChatPage` with question text, option chip buttons, optional free-text entry, and a submit button
- Card state transitions: `Pending` → `Resolved` (mirrors the permission card pattern)
- Duplicate guard: if a question card with the same `questionId` is already in `Messages`, a second SSE event for the same question is ignored

### Out of Scope
- Handling other TUI control types (e.g. `confirm`, `input`) — only `question` is in scope
- Auto-answering questions (no AutoAccept equivalent for questions)
- Persisting the question or answer to the local SQLite database
- Server-side changes to opencode
- Retroactive removal of already-rendered fallback cards for past `question` events

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `ChatEventType` must be extended with a new value `QuestionRequested`.

2. **[REQ-002]** A new sealed record `QuestionRequestedEvent : ChatEvent` must be added to `ChatEvent.cs` with the following properties:
   - `Id` (`string`, required) — the unique TUI control request identifier
   - `SessionId` (`string`, required) — the session this question belongs to
   - `Question` (`string`, required) — the question text to display to the user
   - `Options` (`IReadOnlyList<string>`, required, may be empty) — predefined answer options
   - `AllowFreeText` (`bool`, required) — whether the user may type a custom answer

3. **[REQ-003]** `ChatEventParser` must be extended to handle the `question` SSE event type string (exact wire value to be confirmed — see Open Questions). The parser must extract `id`, `sessionID`, `question`, `options`, and `allowFreeText` from the properties element. On any parse failure, it must fall back to `UnknownEvent` (consistent with all other parsers).

4. **[REQ-004]** `MessageKind` must be extended with a new value `QuestionRequest`.

5. **[REQ-005]** `ChatMessage` must be extended with the following observable properties (all nullable/default-safe for non-question messages):
   - `QuestionText` (`string`) — the question text
   - `QuestionOptions` (`IReadOnlyList<string>`) — predefined options
   - `QuestionAllowFreeText` (`bool`) — whether free-text entry is shown
   - `QuestionId` (`string`) — the TUI control request identifier
   - `QuestionStatus` (`QuestionStatus` enum: `Pending` / `Resolved`) — current state
   - `ResolvedAnswer` (`string?`) — the answer submitted by the user

6. **[REQ-006]** A new `QuestionStatus` enum must be added to `ChatMessage.cs` (or a dedicated file) with values `Pending` and `Resolved`.

7. **[REQ-007]** `ChatMessage` must expose a static factory method `CreateQuestionRequest(string id, string sessionId, string question, IReadOnlyList<string> options, bool allowFreeText)` that constructs a `ChatMessage` with `MessageKind.QuestionRequest`, `QuestionStatus.Pending`, and all question-specific fields populated.

8. **[REQ-008]** `IOpencodeApiClient` must be extended with:
   ```
   Task<OpencodeResult<bool>> RespondToTuiControlAsync(string requestId, string body, CancellationToken ct = default);
   ```
   This maps to `POST /tui/control/response` with body `{ "requestID": "<id>", "body": "<answer>" }`.

9. **[REQ-009]** `OpencodeApiClient` must implement `RespondToTuiControlAsync` following the existing `ReplyToPermissionAsync` pattern (POST with JSON body, `OpencodeResult<bool>` return, retry logic).

10. **[REQ-010]** `IOpencodeApiClient` must be extended with:
    ```
    Task<OpencodeResult<TuiControlRequestDto?>> GetNextTuiControlAsync(CancellationToken ct = default);
    ```
    This maps to `GET /tui/control/next`. Returns `null` (wrapped in `OpencodeResult`) when no control request is pending (HTTP 204 or empty body).

11. **[REQ-011]** A new DTO `TuiControlRequestDto` must be added in the Opencode DTOs folder with fields:
    - `Id` (`string`) — the control request identifier
    - `SessionId` (`string`) — the session identifier
    - `Type` (`string`) — the control type (e.g. `"question"`)
    - `Body` (`JsonElement`) — the raw control-type-specific payload

12. **[REQ-012]** `ChatViewModel.StartSseSubscriptionAsync` must handle `QuestionRequestedEvent`:
    - Filter by `ProjectDirectory` and `SessionId` (same pattern as `PermissionRequestedEvent`)
    - If a question card with the same `QuestionId` already exists in `Messages`, silently ignore the event (duplicate guard)
    - Otherwise, call `ChatMessage.CreateQuestionRequest(...)` and add the card to `Messages` on the UI thread
    - Increment a `_pendingQuestionCount` counter and set `HasPendingQuestions = true`

13. **[REQ-013]** `ChatViewModel` must expose:
    - `HasPendingQuestions` (`bool`, `[ObservableProperty]`) — `true` when at least one question card is pending
    - `_pendingQuestionCount` (private `int`) — counter for pending question cards

14. **[REQ-014]** `ChatViewModel` must expose an `[AsyncRelayCommand]` `AnswerQuestionAsync(string[] args)` that:
    - Guards against duplicate concurrent calls for the same `questionId` via `_inFlightQuestionAnswers` (`HashSet<string>`)
    - Calls `_apiClient.RespondToTuiControlAsync(questionId, answer, ct)`
    - On success: resolves the question card (sets `QuestionStatus = Resolved`, `ResolvedAnswer = answer`), decrements `_pendingQuestionCount`, updates `HasPendingQuestions`
    - On failure: captures the exception via `SentryHelper.CaptureException` and does not resolve the card (user can retry)

15. **[REQ-015]** `ChatViewModel.LoadMessagesAsync` must, after successfully loading the message history and before starting the SSE subscription, call `GetNextTuiControlAsync` with a short timeout (≤ 2 seconds). If the result is a `question` control request whose `SessionId` matches `CurrentSessionId` and no card with that `QuestionId` already exists in `Messages`, it must inject a pending question card into `Messages`. This ensures the question is visible when the user reopens a session that was waiting for an answer.

16. **[REQ-016]** The `ChatPage` XAML must include a `DataTemplate` for `MessageKind.QuestionRequest` that renders:
    - The question text (styled as an assistant message bubble)
    - A horizontal/wrapping list of option buttons (one per entry in `QuestionOptions`); tapping an option calls `AnswerQuestionCommand` with the option text as the answer
    - A free-text `Entry` field and a "Send" button, visible only when `QuestionAllowFreeText = true`; submitting calls `AnswerQuestionCommand` with the entered text
    - When `QuestionStatus = Resolved`: the card becomes read-only, the chosen answer is displayed as a label, and the input controls are hidden

17. **[REQ-017]** When `QuestionStatus` transitions to `Resolved`, `IsAiResponding` must be set to `true` (the agent will resume processing after receiving the answer), consistent with the behaviour after a permission reply.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatEventType.cs` | Add `QuestionRequested` value | Enum extension |
| `ChatEvent.cs` | Add `QuestionRequestedEvent` record | New sealed record |
| `ChatEventParser.cs` | Parse `question` event type | New `ParseQuestionRequested` private method |
| `ChatMessage.cs` | Add `MessageKind.QuestionRequest`, `QuestionStatus` enum, 6 new observable properties, `CreateQuestionRequest` factory | Follows `CreatePermissionRequest` pattern |
| `IOpencodeApiClient.cs` | Add `RespondToTuiControlAsync` and `GetNextTuiControlAsync` | Two new interface methods |
| `OpencodeApiClient.cs` | Implement both new methods | POST + GET, follow existing patterns |
| `TuiControlRequestDto.cs` | New DTO file | In `Infrastructure/Http/Dtos/Opencode/` |
| `TuiControlResponseRequest.cs` | New request record | In `Infrastructure/Http/Dtos/Opencode/Requests/` |
| `ChatViewModel.cs` | Handle `QuestionRequestedEvent` in SSE switch, add `AnswerQuestionAsync` command, add `HasPendingQuestions`, add recovery call in `LoadMessagesAsync` | ~260 lines of new code |
| `ChatPage.xaml` | New `DataTemplate` for `MessageKind.QuestionRequest` | Option chips + free-text entry + resolved state |
| `ChatPage.xaml.cs` | `OnOptionChipTapped`, `OnQuestionSendTapped`, `ViewModel` property | Thin UI glue — no business logic |
| Unit tests | 15 new tests in `ChatViewModelSseTests.cs` (80 total passing) | All AC covered |

### Dependencies
- `PermissionRequestedEvent` / `CreatePermissionRequest` pattern — the question card follows the same inline card architecture established for permissions
- `_inFlightPermissionReplies` pattern — reused as `_inFlightQuestionAnswers` for duplicate guard
- `HasPendingPermissions` / `_pendingPermissionCount` pattern — reused for questions
- `ReplyToPermissionAsync` pattern — `AnswerQuestionAsync` follows the same structure
- `LoadMessagesAsync` recovery path — analogous to `ReplayPendingPermissionsAsync` but synchronous and scoped to a single control request

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | What is the exact SSE event type string for the question event? | Open | Assumed `"question"` — must be confirmed via live server inspection |
| 2 | What is the exact JSON shape of the `question` payload? | Open | Assumed `{ id, sessionID, question, options, allowFreeText }` — must be confirmed |
| 3 | Does `GET /tui/control/next` return HTTP 204 or long-poll? | Open | Assumed long-poll; 2-second timeout CTS applied |
| 4 | Is `allowFreeText` optional? | Open | Assumed optional, defaults to `true` when absent |
| 5 | Can multiple `question` events arrive simultaneously? | Open | Assumed one at a time; duplicate guard handles edge cases |

---

## Acceptance Criteria

- [x] **[AC-001]** Given a `question` SSE event arrives for the current session, when the event is processed, then an inline question card appears in the `Messages` collection with the question text, option buttons, and (if `allowFreeText = true`) a free-text entry field. *(REQ-003, REQ-012, REQ-016)*
- [x] **[AC-002]** Given a question card is displayed, when the user taps one of the predefined option buttons, then `AnswerQuestionAsync` is called with that option text, `RespondToTuiControlAsync` is called on the API client, and the card transitions to `Resolved` showing the chosen answer. *(REQ-014, REQ-016)*
- [x] **[AC-003]** Given a question card is displayed with `allowFreeText = true`, when the user types a custom answer and taps "Send", then `AnswerQuestionAsync` is called with the typed text and the card resolves. *(REQ-014, REQ-016)*
- [x] **[AC-004]** Given a question card is displayed with `allowFreeText = false`, then the free-text entry and "Send" button are not visible. *(REQ-016)*
- [x] **[AC-005]** Given a question card has been resolved, then all option buttons and the free-text entry are hidden, and the resolved answer label is displayed. *(REQ-016)*
- [x] **[AC-006]** Given `AnswerQuestionAsync` is called, when the API call succeeds, then `IsAiResponding` is set to `true`. *(REQ-017)*
- [x] **[AC-007]** Given the user closes and reopens a session that has a pending question, when `LoadMessagesAsync` completes, then the pending question card is visible in the `Messages` collection. *(REQ-015)*
- [x] **[AC-008]** Given `LoadMessagesAsync` calls `GetNextTuiControlAsync` and the server has no pending control request, then no question card is injected and no error is shown. *(REQ-015)*
- [x] **[AC-009]** Given a `question` SSE event arrives with the same `questionId` as a card already in `Messages`, then the duplicate event is silently ignored. *(REQ-012)*
- [x] **[AC-010]** Given `AnswerQuestionAsync` is called concurrently twice for the same `questionId`, then only the first call proceeds; the second is silently dropped. *(REQ-014)*
- [x] **[AC-011]** Given `AnswerQuestionAsync` fails (API error), then the question card remains in `Pending` state and the error is captured via Sentry. *(REQ-014)*
- [x] **[AC-012]** Given a `question` SSE event arrives for a different session or project directory, then the event is filtered out and no card is added. *(REQ-012)*
- [x] **[AC-013]** Given a `question` SSE event arrives, then `HasPendingQuestions` is `true`. Given the user answers the question, then `HasPendingQuestions` returns to `false`. *(REQ-013)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-04-01

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | `feature/question-tui-control` |
| Branches from | `develop` |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Models / Enums | om-mobile-core | `src/openMob.Core/Models/` |
| SSE Parser | om-mobile-core | `src/openMob.Core/Helpers/ChatEventParser.cs` |
| API Client Interface | om-mobile-core | `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` |
| API Client Implementation | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` |
| DTO | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` |
| ViewModel | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files Created

- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/TuiControlRequestDto.cs` — new DTO for `GET /tui/control/next` response
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/TuiControlResponseRequest.cs` — request body for `POST /tui/control/response`

### Files Modified

- `src/openMob.Core/Models/ChatEventType.cs` — add `QuestionRequested` enum value
- `src/openMob.Core/Models/ChatEvent.cs` — add `QuestionRequestedEvent` sealed record
- `src/openMob.Core/Helpers/ChatEventParser.cs` — add `"question"` case + `ParseQuestionRequested` method
- `src/openMob.Core/Models/ChatMessage.cs` — add `MessageKind.QuestionRequest`, `QuestionStatus` enum, 6 observable properties, `CreateQuestionRequest` factory, update constructor
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — add `RespondToTuiControlAsync` and `GetNextTuiControlAsync` interface methods
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — implement both new methods
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add `QuestionRequestedEvent` SSE handler, `AnswerQuestionAsync` command, `HasPendingQuestions` property, `_pendingQuestionCount` counter, `_inFlightQuestionAnswers` set, recovery call in `LoadMessagesAsync`, `ResetQuestionState` helper, `SetSession` reset call, `_inFlightPermissionReplies.Clear()` in `Dispose()`
- `src/openMob/Views/Pages/ChatPage.xaml` — add `QuestionRequest` DataTemplate inside the existing CollectionView DataTemplate
- `src/openMob/Views/Pages/ChatPage.xaml.cs` — `ViewModel` property, `OnOptionChipTapped`, `OnQuestionSendTapped`
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — 15 new tests (80 total passing)

### Technical Dependencies

- The `PermissionRequest` inline card pattern (already implemented) is the direct template for this feature. No new architectural patterns are introduced.
- opencode server API endpoints:
  - `POST /tui/control/response` — submit answer to a TUI control request
  - `GET /tui/control/next` — poll for the next pending TUI control request (assumed long-poll; 2-second timeout applied)
- No new NuGet packages required.
- Wire format for the `question` SSE event is assumed as:
  - Event type string: `"question"` (in `payload.type`)
  - Properties: `{ "id": string, "sessionID": string, "question": string, "options": string[], "allowFreeText": bool? }`
  - `allowFreeText` defaults to `true` if absent
  - The `POST /tui/control/response` body is `{ "requestID": "<id>", "body": "<answer>" }`

### Key Technical Decisions

1. **`AnswerQuestionCommand` accepts `string[]`** — MAUI `CommandParameter` supports only one object. The command accepts `string[]` where `[0]`=questionId and `[1]`=answer. The UI layer uses code-behind `Clicked` handlers to construct and pass the array. See ADR `adr-maui-multi-param-command.md`.

2. **`allowFreeText` parsing** — Both `ChatEventParser.ParseQuestionRequested` and `ChatViewModel.RecoverPendingQuestionAsync` use explicit `JsonValueKind.True` / `JsonValueKind.False` checks. Malformed values fall through to the `true` default.

3. **Recovery timeout** — A 2-second `CancellationTokenSource` linked to the caller's `ct` is used in `RecoverPendingQuestionAsync`.

4. **XAML DataTrigger sequence for free-text grid** — `IsVisible="False"` default + three triggers: show when `QuestionAllowFreeText==True`, hide when `QuestionAllowFreeText==False`, hide when `QuestionStatus==Resolved`.

5. **`_inFlightPermissionReplies.Clear()` added to `Dispose()`** — Pre-existing omission surfaced during review; fixed for parity.

### Review Summary

| Round | Critical | Major | Minor | Verdict |
|-------|----------|-------|-------|---------|
| Round 1 | 0 | 3 | 6 | 🔴 Changes required |
| Round 2 | 0 | 0 | 0 | ✅ Approved |

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-017]` requirements implemented
- [x] All `[AC-001]` through `[AC-013]` acceptance criteria satisfied
- [x] Unit tests written for all new ViewModel logic (80 tests passing)
- [x] `om-reviewer` verdict: ✅ Approved
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
