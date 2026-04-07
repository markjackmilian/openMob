# Question Event Parsing Fix & Session-Load Recovery

## Metadata
| Field       | Value                                              |
|-------------|----------------------------------------------------|
| Date        | 2026-04-07                                         |
| Status      | **Completed**                                      |
| Version     | 1.0                                                |
| Completed   | 2026-04-08                                         |
| Branch      | feature/question-event-fix-session-recovery (merged)|
| Merged into | develop                                            |

---

## Executive Summary

The opencode server emits a `question.asked` SSE event when the AI agent needs user input, but the app's `ChatEventParser` only matches the string `"question"` — causing the event to fall through to `UnknownEvent` and never render the interactive question card. Additionally, the server's payload format (`questions[]` array with structured option objects) differs from what the parser expects (flat `question` string + `options[]` string array). As a result, the user sees only a generic tool call card labelled "question" with a spinner, with no way to respond — blocking the entire agent session. This spec fixes the SSE event type mapping, adapts the payload parser to the real server schema, migrates from the legacy `/tui/control/*` endpoints to the dedicated `/question/*` API, and adds question recovery at session load (app reopen, session switch) and SSE reconnect.

**Supersedes:** `2026-04-02-question-message-tui-control.md` — that spec was written before the server source was analysed and contains incorrect assumptions about the wire format and API endpoints.

---

## Scope

### In Scope
- **SSE event type fix:** `ChatEventParser` must recognise `"question.asked"` as the primary event type, keeping `"question"` as a legacy alias
- **Payload format adaptation:** `ParseQuestionRequested` must handle the real server schema: `questions[]` array of objects with `question`, `header`, `options[].label`, `multiple`, `custom` fields
- **New API client methods:** `GetPendingQuestionsAsync()` calling `GET /question` and `ReplyToQuestionAsync()` calling `POST /question/:requestID/reply` with `{ answers: [["<answer>"]] }` format
- **Session-load recovery:** `LoadMessagesAsync` must fetch pending questions after loading message history (covers app reopen, session switch)
- **Reconnect recovery:** On `Lost -> Healthy` SSE transition, pending questions must be recovered (same pattern as `auto-accept-reconnect-replay`)
- **Tool call card suppression:** When a question card exists for a given question, the corresponding tool call card with `toolName == "question"` should be visually suppressed or hidden
- **Legacy endpoint removal:** Remove or deprecate `GetNextTuiControlAsync` and `RespondToTuiControlAsync` if not used elsewhere; replace with new `/question/*` methods
- **Unit tests** for all new and modified parsing, ViewModel handling, and recovery logic

### Out of Scope
- Multi-question support: the server supports `questions[]` with multiple items, but the UI renders only the first question — multi-question is deferred to a future spec
- `question.replied` and `question.rejected` SSE events — not needed for the current flow (the client is the one replying)
- Auto-answering questions (no AutoAccept equivalent)
- Persisting questions/answers to local SQLite
- Server-side changes to opencode
- Markdown rendering of question text

---

## Functional Requirements

> Requirements are numbered for traceability.

### SSE Event Type Fix

1. **[REQ-001]** `ChatEventParser` must map the SSE event type string `"question.asked"` to `ParseQuestionRequested`. The existing `"question"` mapping must be retained as a legacy alias.

2. **[REQ-002]** `ParseQuestionRequested` must be rewritten to handle the real server payload format:
   - The `properties` object contains: `id` (string), `sessionID` (string), `questions` (array of question objects), `tool` (optional object with `messageID` and `callID`)
   - Each question object contains: `question` (string), `header` (string), `options` (array of `{ label, description }`), `multiple` (optional bool), `custom` (optional bool, default `true`)
   - The parser must extract the **first** question from the `questions` array
   - `Question` text comes from `questions[0].question`
   - `Options` is built from `questions[0].options[].label` (string array)
   - `AllowFreeText` comes from `questions[0].custom` (default `true` when absent)
   - On any parse failure (missing `id`, `sessionID`, or empty `questions` array), fall back to `UnknownEvent`

3. **[REQ-003]** `QuestionRequestedEvent` must be extended with an optional `ToolCallId` property (`string?`) extracted from `tool.callID` in the payload. This is used to correlate with the tool call card for suppression (REQ-010).

### New API Endpoints

4. **[REQ-004]** `IOpencodeApiClient` must expose:
   ```
   Task<OpencodeResult<IReadOnlyList<QuestionRequestDto>>> GetPendingQuestionsAsync(CancellationToken ct = default);
   ```
   Implementation calls `GET /question` and deserialises the response as an array of `QuestionRequestDto`.

5. **[REQ-005]** A new DTO `QuestionRequestDto` must be added in `Infrastructure/Http/Dtos/Opencode/` with fields matching the server's `Question.Request` schema:
   - `Id` (`string`) — the question request identifier
   - `SessionId` (`string`, JSON: `sessionID`) — the session identifier
   - `Questions` (`IReadOnlyList<QuestionInfoDto>`) — the questions array
   - `Tool` (`QuestionToolRefDto?`) — optional tool reference with `MessageId` and `CallId`

6. **[REQ-006]** A new DTO `QuestionInfoDto` must be added with fields:
   - `Question` (`string`) — the question text
   - `Header` (`string`) — short label
   - `Options` (`IReadOnlyList<QuestionOptionDto>`) — available choices
   - `Multiple` (`bool?`) — allow multiple selections
   - `Custom` (`bool?`) — allow free-text (default `true`)

7. **[REQ-007]** A new DTO `QuestionOptionDto` must be added with fields:
   - `Label` (`string`) — display text
   - `Description` (`string`) — explanation

8. **[REQ-008]** `IOpencodeApiClient` must expose:
   ```
   Task<OpencodeResult<bool>> ReplyToQuestionAsync(string requestId, IReadOnlyList<string> answers, CancellationToken ct = default);
   ```
   Implementation calls `POST /question/{requestId}/reply` with body `{ "answers": [["<answer1>", ...]] }`. The outer array has one element per question (we always send one since we only handle the first question).

9. **[REQ-009]** The existing `RespondToTuiControlAsync` and `GetNextTuiControlAsync` methods must be removed from `IOpencodeApiClient` and `OpencodeApiClient` if they are not used by any other feature. If they are still referenced, they must be marked `[Obsolete]` with a message pointing to the new methods.

### Session-Load Recovery

10. **[REQ-010]** `ChatViewModel.LoadMessagesAsync` must, after populating `Messages` and before starting the SSE subscription, call a new `RecoverPendingQuestionsAsync` method that:
    - Calls `GetPendingQuestionsAsync` with a 2-second timeout (linked `CancellationTokenSource`)
    - Filters results by `CurrentSessionId` (client-side, `StringComparison.Ordinal`)
    - For each pending question not already in `Messages` (duplicate guard by `QuestionId`), creates a `ChatMessage.CreateQuestionRequest` card and adds it to `Messages`
    - Failures are captured via `SentryHelper.CaptureException` — no user-visible error

11. **[REQ-011]** The existing `RecoverPendingQuestionAsync` method (which uses `/tui/control/next`) must be replaced by the new `RecoverPendingQuestionsAsync` (which uses `GET /question`).

### Reconnect Recovery

12. **[REQ-012]** When `IHeartbeatMonitorService.HealthStateChanged` fires with a transition from `Lost` or `Degraded` to `Healthy`, `ChatViewModel` must call `RecoverPendingQuestionsAsync` (same method as REQ-010). This follows the pattern established in `auto-accept-reconnect-replay`.

### Answer Submission

13. **[REQ-013]** `ChatViewModel.AnswerQuestionAsync` must be updated to call `ReplyToQuestionAsync(questionId, new[] { answer })` instead of `RespondToTuiControlAsync(questionId, answer)`.

### Tool Call Card Suppression

14. **[REQ-014]** When `HandleMessagePartUpdated` processes a `type == "tool"` part with `toolName == "question"`, it must check if a `ChatMessage` with `MessageKind == QuestionRequest` exists in `Messages` that has a matching correlation. If a match is found, the tool call card should be marked as hidden (e.g., set a flag on `ToolCallInfo` like `IsHidden = true`).

15. **[REQ-015]** The `ChatPage.xaml` tool call card template must respect the `IsHidden` flag — when `true`, the tool call card is not visible. This can be achieved via a `DataTrigger` or a binding converter.

16. **[REQ-016]** If the question card does not yet exist when the tool call arrives (timing race), the tool call card is shown normally. When the question card is subsequently created (via SSE or recovery), the tool call card must be retroactively hidden.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatEventParser.cs` | Fix event type mapping + rewrite payload parser | `"question.asked"` + `"question"` alias; new payload format |
| `ChatEvent.cs` | Extend `QuestionRequestedEvent` with `ToolCallId` | New optional property |
| `IOpencodeApiClient.cs` | Add `GetPendingQuestionsAsync`, `ReplyToQuestionAsync`; remove/deprecate TUI control methods | 2 new + 2 removed |
| `OpencodeApiClient.cs` | Implement new methods, remove old ones | Follow existing patterns |
| `QuestionRequestDto.cs` | New DTO file | + `QuestionInfoDto`, `QuestionOptionDto`, `QuestionToolRefDto` |
| `ChatViewModel.cs` | Replace `RecoverPendingQuestionAsync`, update `AnswerQuestionAsync`, add reconnect recovery, add tool call suppression logic | ~80 lines changed |
| `ToolCallInfo.cs` | Add `IsHidden` observable property | For suppression |
| `ChatPage.xaml` | Add `DataTrigger` on `IsHidden` for tool call card | Minor XAML change |
| `TuiControlRequestDto.cs` | Remove if unused | Cleanup |
| Unit tests | Update parser tests, ViewModel tests, add recovery tests | Significant test changes |

### Dependencies
- As established in **SSE Project Directory Propagation**: all `ChatEvent` records include `ProjectDirectory` and all handlers filter by it before `SessionId`
- As established in **SSE Full Message Type Coverage**: `ChatEventParser` never propagates exceptions — malformed events fall to `UnknownEvent`
- As established in **Auto-Accept Reconnect Replay**: reconnect recovery follows the `Lost/Degraded -> Healthy` pattern via `IHeartbeatMonitorService.HealthStateChanged`
- As established in **Chat Conversation Loop**: all `Messages` collection mutations dispatched via `_dispatcher.Dispatch`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Are `GetNextTuiControlAsync` and `RespondToTuiControlAsync` used by any feature other than question recovery? | Resolved | **No.** Codebase analysis confirms both methods are only referenced from `ChatViewModel` for question handling (lines 800 and 2064) plus their test mocks. They can be safely removed. |
| 2 | The server sends both a `question.asked` SSE event and a `message.part.updated` tool call for the same question — which arrives first? | Resolved | **Indeterminate.** Order depends on server-side event bus timing. REQ-016 handles the timing race regardless of arrival order. |
| 3 | Should the `question` tool call card show a "completed" state after the question is answered, or remain hidden? | Resolved | Remain hidden — the question card already shows the resolved answer. Showing both would be redundant. |
| 4 | The server schema supports `multiple: true` (multi-select options). Should we handle it? | Resolved | Out of scope for v1. Only single-select is supported. `multiple` field is parsed but ignored. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a `question.asked` SSE event arrives for the current session, when processed, then an interactive question card appears with question text, option buttons, and (if `custom != false`) a free-text entry field. *(REQ-001, REQ-002)*
- [ ] **[AC-002]** Given a legacy `question` SSE event (old type string), when processed, then the question card renders identically to `question.asked`. *(REQ-001)*
- [ ] **[AC-003]** Given a tool call card with `toolName == "question"` and a corresponding question card exists, then the tool call card is hidden. *(REQ-014, REQ-015)*
- [ ] **[AC-004]** Given the user taps an option button on a pending question card, then `ReplyToQuestionAsync` is called with `POST /question/:id/reply` and the card transitions to `Resolved` showing the chosen answer. *(REQ-008, REQ-013)*
- [ ] **[AC-005]** Given the user types a free-text answer and taps "Send", then `ReplyToQuestionAsync` is called and the card resolves. *(REQ-008, REQ-013)*
- [ ] **[AC-006]** Given a question is answered, then `IsAiResponding` is set to `true`. *(existing REQ-017 from previous spec)*
- [ ] **[AC-007]** Given the user reopens the app with a session that has a pending question, when `LoadMessagesAsync` completes, then the pending question card is visible. *(REQ-010, REQ-011)*
- [ ] **[AC-008]** Given the user switches to a session with a pending question, when `LoadMessagesAsync` completes, then the pending question card is visible. *(REQ-010)*
- [ ] **[AC-009]** Given a `Lost -> Healthy` SSE reconnection with a pending question, then the question card is recovered automatically. *(REQ-012)*
- [ ] **[AC-010]** Given `GetPendingQuestionsAsync` returns no results, then no question card is injected and no error is shown. *(REQ-010)*
- [ ] **[AC-011]** Given a duplicate `question.asked` SSE event (same `id`), then it is silently ignored. *(existing duplicate guard)*
- [ ] **[AC-012]** Given `ReplyToQuestionAsync` fails, then the card remains `Pending`, the error is captured via Sentry, and the user can retry. *(REQ-013)*
- [ ] **[AC-013]** Given a `question.asked` event for a different session or project directory, then it is filtered out. *(existing filters)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Critical: Wire Format (Confirmed from Server Source)

The SSE envelope format is:
```json
{
  "directory": "/path/to/project",
  "payload": {
    "type": "question.asked",
    "properties": {
      "id": "question_01JR...",
      "sessionID": "session_01JR...",
      "questions": [
        {
          "question": "There are unstaged changes blocking the pull. You have two options:",
          "header": "Unstaged changes",
          "options": [
            { "label": "Stash changes", "description": "Stash and continue" },
            { "label": "Commit changes", "description": "Commit before pull" }
          ],
          "multiple": false,
          "custom": true
        }
      ],
      "tool": {
        "messageID": "msg_01JR...",
        "callID": "call_01JR..."
      }
    }
  }
}
```

The reply endpoint is `POST /question/{requestID}/reply` with body:
```json
{
  "answers": [["Stash changes"]]
}
```
The outer array corresponds to `questions[]` (one answer array per question). Each inner array contains the selected option labels (single-select = one element).

### Key Areas to Investigate

1. **`ChatEventParser.cs` line 83-114**: Add `"question.asked"` to the switch expression. Keep `"question"` as alias. Rewrite `ParseQuestionRequested` (lines 397-439) for the new payload format.

2. **`ChatViewModel.cs` lines 788-867**: Replace `RecoverPendingQuestionAsync` (uses `/tui/control/next`) with new `RecoverPendingQuestionsAsync` (uses `GET /question`).

3. **`ChatViewModel.cs` lines 2047-2099**: Update `AnswerQuestionAsync` to call `ReplyToQuestionAsync` instead of `RespondToTuiControlAsync`.

4. **`ChatViewModel.cs` reconnect handler**: Add `RecoverPendingQuestionsAsync` call in the `HealthStateChanged` handler, following the exact pattern from `auto-accept-reconnect-replay` (REQ-003/REQ-004 of that spec).

5. **`ChatViewModel.cs` lines 1511-1514**: In `HandleMessagePartUpdated`, when `type == "tool"`, check if `toolName == "question"` and if so, attempt to correlate with an existing question card and set `IsHidden`.

6. **`IOpencodeApiClient.cs` / `OpencodeApiClient.cs`**: Check if `RespondToTuiControlAsync` and `GetNextTuiControlAsync` are referenced anywhere else before removing.

### Suggested Implementation Approach

- Follow the **`auto-accept-reconnect-replay`** pattern for reconnect recovery (same `HealthStateChanged` hook, same fire-and-forget pattern, same Sentry error capture)
- Follow the **`GetPendingPermissionsAsync`** pattern for `GetPendingQuestionsAsync` (global GET endpoint, client-side session filter)
- The `QuestionRequestDto` and related DTOs should use `System.Text.Json` attributes (`[JsonPropertyName]`) consistent with all other DTOs in the project
- `ToolCallInfo.IsHidden` should be an `[ObservableProperty]` so the XAML `DataTrigger` reacts to changes

### Constraints to Respect

- `openMob.Core` has zero MAUI dependencies — all new DTOs, models, and logic must be pure .NET
- All `Messages` mutations via `_dispatcher.Dispatch`
- All new `ChatEvent` records must include `ProjectDirectory` (inherited from base)
- `ChatEventParser` never throws — malformed payloads fall to `UnknownEvent`
- `ConfigureAwait(false)` in service/API code; not in ViewModels

### Related Files

- `src/openMob.Core/Helpers/ChatEventParser.cs` — primary parser (line 83: switch, line 397: `ParseQuestionRequested`)
- `src/openMob.Core/Models/ChatEvent.cs` — `QuestionRequestedEvent` record
- `src/openMob.Core/Models/ChatMessage.cs` — `CreateQuestionRequest` factory
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — SSE handler (line 1301), recovery (line 788), answer (line 2047)
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — interface
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — implementation
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/TuiControlRequestDto.cs` — to be replaced
- `src/openMob.Core/Models/ToolCallInfo.cs` — add `IsHidden`
- `src/openMob/Views/Pages/ChatPage.xaml` — tool call card template (line 440)
- `tests/openMob.Tests/Helpers/ChatEventParserTests.cs`
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs`
- Server source: `packages/opencode/src/question/index.ts` — `Question.Request` schema, `Event.Asked` bus event
- Server source: `packages/opencode/src/tool/question.ts` — `QuestionTool` definition
- Server source: `packages/opencode/src/server/routes/question.ts` — REST endpoints

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-04-07

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix (SSE event type mismatch) + Feature (session recovery, tool call suppression) |
| Git Flow branch | feature/question-event-fix-session-recovery |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| SSE Event Parsing | om-mobile-core | `src/openMob.Core/Helpers/ChatEventParser.cs` |
| Event Models | om-mobile-core | `src/openMob.Core/Models/ChatEvent.cs` |
| DTOs | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` |
| API Client Interface | om-mobile-core | `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` |
| API Client Implementation | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` |
| ViewModel (recovery, answer, suppression) | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Tool Call Model | om-mobile-core | `src/openMob.Core/Models/ToolCallInfo.cs` |
| XAML Tool Call Template | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/QuestionRequestDto.cs` — DTO for `GET /question` response items (REQ-005)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/QuestionInfoDto.cs` — Nested DTO for question objects (REQ-006)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/QuestionOptionDto.cs` — Nested DTO for option objects (REQ-007)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/QuestionToolRefDto.cs` — Nested DTO for tool reference (REQ-005)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/QuestionReplyRequest.cs` — Request body for `POST /question/{id}/reply` (REQ-008)

### Files to Modify

- `src/openMob.Core/Helpers/ChatEventParser.cs` — Add `"question.asked"` switch arm; rewrite `ParseQuestionRequested` for new payload format (REQ-001, REQ-002)
- `src/openMob.Core/Models/ChatEvent.cs` — Add `ToolCallId` property to `QuestionRequestedEvent` (REQ-003)
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — Add `GetPendingQuestionsAsync`, `ReplyToQuestionAsync`; remove `GetNextTuiControlAsync`, `RespondToTuiControlAsync` (REQ-004, REQ-008, REQ-009)
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — Implement new methods; remove old TUI control methods (REQ-004, REQ-008, REQ-009)
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — Replace `RecoverPendingQuestionAsync` with `RecoverPendingQuestionsAsync`; update `AnswerQuestionAsync`; add reconnect recovery in `OnHealthStateChanged`; add tool call suppression in `HandleMessagePartUpdated` and `HandleQuestionRequested` (REQ-010–REQ-016)
- `src/openMob.Core/Models/ToolCallInfo.cs` — Add `[ObservableProperty] private bool _isHidden;` (REQ-014)
- `src/openMob/Views/Pages/ChatPage.xaml` — Add `IsVisible` binding or `DataTrigger` on tool call card `Border` for `IsHidden` (REQ-015)
- `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` — Add tests for `question.asked` and `question` event parsing with new payload format
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — Update all 17 existing question tests to use new API methods; add recovery and suppression tests

### Files to Delete

- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/TuiControlRequestDto.cs` — Replaced by `QuestionRequestDto` (REQ-009)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/Requests/TuiControlResponseRequest.cs` — Replaced by `QuestionReplyRequest` (REQ-009)

### Technical Dependencies

- **No new NuGet packages required** — all work uses existing `System.Text.Json`, `CommunityToolkit.Mvvm`, and project infrastructure
- **No schema migrations** — no SQLite changes
- **No DI registration changes** — `IOpencodeApiClient` is already registered; new methods are added to the existing interface
- **opencode server API endpoints used:**
  - `GET /question` — returns array of pending question requests (new)
  - `POST /question/{requestId}/reply` — submits answer (new)
  - `GET /tui/control/next` — removed (legacy)
  - `POST /tui/control/response` — removed (legacy)
- **Prerequisite features (all already implemented):**
  - SSE Project Directory Propagation — `ProjectDirectory` on all `ChatEvent` records
  - SSE Full Message Type Coverage — `ChatEventParser` error handling pattern
  - Auto-Accept Reconnect Replay — `HealthStateChanged` reconnect recovery pattern
  - Chat Conversation Loop — `_dispatcher.Dispatch` pattern for `Messages` mutations

### Technical Risks

- **Breaking change to API client interface:** Removing `GetNextTuiControlAsync` and `RespondToTuiControlAsync` is a breaking interface change. Mitigated by: both methods are only used by question handling code that is being rewritten in the same PR.
- **Existing test breakage:** All 17 existing question tests in `ChatViewModelSseTests.cs` reference the old TUI control methods. They must all be updated in the same commit to avoid a broken test state.
- **Timing race (REQ-016):** The tool call card suppression requires bidirectional correlation — both the tool call handler and the question handler must check for the other. This is a subtle concurrency concern but is mitigated by all mutations running on the dispatcher thread.
- **Server compatibility:** The new `/question/*` endpoints must exist on the server. The spec confirms these are available in the current opencode server source.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/question-event-fix-session-recovery` from `develop`
2. **[om-mobile-core]** Implement all changes:
   - New DTOs (`QuestionRequestDto`, `QuestionInfoDto`, `QuestionOptionDto`, `QuestionToolRefDto`, `QuestionReplyRequest`)
   - Extend `QuestionRequestedEvent` with `ToolCallId`
   - Add `IsHidden` to `ToolCallInfo`
   - Rewrite `ParseQuestionRequested` for new payload format + add `"question.asked"` switch arm
   - Add `GetPendingQuestionsAsync` and `ReplyToQuestionAsync` to API client; remove TUI control methods
   - Replace `RecoverPendingQuestionAsync` with `RecoverPendingQuestionsAsync` in `ChatViewModel`
   - Update `AnswerQuestionAsync` to use `ReplyToQuestionAsync`
   - Add reconnect recovery in `OnHealthStateChanged`
   - Add tool call suppression logic in `HandleMessagePartUpdated` and `HandleQuestionRequested`
   - Delete `TuiControlRequestDto.cs` and `TuiControlResponseRequest.cs`
3. ⟳ **[om-mobile-ui]** Add `IsHidden` visibility binding on tool call card template in `ChatPage.xaml` (can start once `ToolCallInfo.IsHidden` property is defined in step 2)
4. **[om-tester]** Write/update unit tests for all modified code
5. **[om-reviewer]** Full review against spec
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-016]` requirements implemented
- [ ] All `[AC-001]` through `[AC-013]` acceptance criteria satisfied
- [ ] Unit tests written for all new and modified Services, ViewModels, and Helpers
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
