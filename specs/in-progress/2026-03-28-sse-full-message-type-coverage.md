# SSE Full Message Type Coverage

## Metadata
| Field   | Value                                      |
|---------|--------------------------------------------|
| Date    | 2026-03-28                                 |
| Status  | In Progress                                |
| Version | 1.0                                        |

---

## Executive Summary

The opencode server emits a richer set of SSE events and message part types than the app currently handles. Several event types are silently dropped — including `permission.replied` (which leaves permission cards permanently stuck in "Pending"), `message.removed`, `message.part.removed`, `session.created`, and `session.deleted`. Additionally, non-text part types (`tool`, `reasoning`, `step-start`, `step-finish`, `subtask`, `agent`, `compaction`, `snapshot`, `patch`, `retry`) are ignored, so tool calls and AI reasoning are never shown to the user. This spec closes all these gaps — except `file` parts (images/PDFs), which are deferred to a dedicated spec.

---

## Scope

### In Scope

- **`permission.replied`** SSE event: parse and handle to auto-resolve permission cards server-side (e.g. auto-approved by rule, rejected in cascade, or resolved by another client).
- **`message.removed`** SSE event: parse and remove the corresponding `ChatMessage` from the collection.
- **`message.part.removed`** SSE event: parse and remove a specific part (tool call or reasoning block) from the corresponding `ChatMessage`.
- **`session.created`** SSE event: parse and notify `FlyoutViewModel` to add the new session to the drawer list in real-time.
- **`session.deleted`** SSE event: parse and notify `FlyoutViewModel` to remove the session from the drawer list in real-time.
- **`tool` part type** (`ToolPart`): render tool call cards inline in the chat with states `pending`, `running`, `completed`, `error`.
- **`reasoning` part type** (`ReasoningPart`): render AI thinking/reasoning text inline, collapsible.
- **`step-start` / `step-finish` part types**: render as lightweight step separators (step-start) and cost/token summaries (step-finish).
- **`subtask` part type** (`SubtaskPart`): render as a read-only subagent invocation label.
- **`agent` part type** (`AgentPart`): render as a read-only agent mention label.
- **`compaction` part type** (`CompactionPart`): render as a context-compaction notice.
- **`snapshot` / `patch` / `retry` part types**: silently ignore (no UI needed) but log via `DebugLogger` in DEBUG builds.
- **`UnknownEvent` logging**: emit a `OM_SSE` log entry with the raw event type string instead of silently dropping.
- **`permission.updated`** (legacy alias): remap to `permission.replied` handling for backward compatibility.

### Out of Scope

- `file` part type (images, PDFs) — dedicated spec required.
- `session.diff` SSE event — no chat UI impact.
- Server-side changes to opencode.
- Persistence of tool call results to SQLite.
- Pagination or lazy-loading of tool call outputs.
- Markdown rendering of tool call output text.

---

## Functional Requirements

> Requirements are numbered for traceability.

### SSE Event: `permission.replied`

1. **[REQ-001]** `ChatEventParser` must map the SSE event type `permission.replied` to a new `PermissionRepliedEvent` record containing: `SessionId` (string), `RequestId` (string, from `requestID` JSON field), `Reply` (string: `"once"` | `"always"` | `"reject"`).

2. **[REQ-002]** `ChatEventParser` must also map `permission.updated` (the legacy alias already in the parser) to the same `PermissionRepliedEvent` type, reading `permissionID` as `RequestId` and defaulting `Reply` to `"once"` if the field is absent. The existing `PermissionUpdatedEvent` record and `ChatEventType.PermissionUpdated` enum value are removed and replaced.

3. **[REQ-003]** `ChatViewModel.StartSseSubscriptionAsync` must add a `case PermissionRepliedEvent e:` branch that calls a new `HandlePermissionReplied(e)` handler.

4. **[REQ-004]** `HandlePermissionReplied` must: (a) apply the standard project-directory and session-ID filters; (b) find the `ChatMessage` with `MessageKind == PermissionRequest` and `RequestId == e.RequestId`; (c) if found and still `Pending`, call `ResolvePermissionRequest(e.RequestId, e.Reply, replyLabel)` on the UI thread via `_dispatcher.Dispatch`. The `replyLabel` is derived from `e.Reply` using the same mapping already in `ReplyToPermissionAsync` (`"always"` → `"Always"`, `"once"` → `"Once"`, `"reject"` → `"Deny"`).

5. **[REQ-005]** If `HandlePermissionReplied` receives `reply == "reject"`, it must resolve **all** remaining `Pending` permission cards in the current session (not just the one matching `RequestId`), because the server rejects all pending permissions in the same session on a single reject. Each resolved card calls `DecrementPendingPermissions()`.

### SSE Event: `message.removed`

6. **[REQ-006]** `ChatEventParser` must map `message.removed` to a new `MessageRemovedEvent` record containing: `SessionId` (string, from `sessionID`), `MessageId` (string, from `messageID`).

7. **[REQ-007]** `ChatViewModel` must handle `MessageRemovedEvent`: apply project-directory and session-ID filters, then on the UI thread find and remove the `ChatMessage` with matching `Id` from `Messages`, call `RecalculateGrouping()` and `UpdateIsEmpty()`.

### SSE Event: `message.part.removed`

8. **[REQ-008]** `ChatEventParser` must map `message.part.removed` to a new `MessagePartRemovedEvent` record containing: `SessionId` (string, from `sessionID`), `MessageId` (string, from `messageID`), `PartId` (string, from `partID`).

9. **[REQ-009]** `ChatViewModel` must handle `MessagePartRemovedEvent`: apply project-directory and session-ID filters, then on the UI thread find the `ChatMessage` with matching `Id` and remove the `ToolCallInfo` or `ReasoningText` entry identified by `PartId`. If no matching part is found, the event is silently ignored.

### SSE Events: `session.created` / `session.deleted`

10. **[REQ-010]** `ChatEventParser` must map `session.created` to a new `SessionCreatedEvent` record containing: `SessionId` (string, from `sessionID`), `Session` (`SessionDto`, deserialised from the `info` field).

11. **[REQ-011]** `ChatEventParser` must map `session.deleted` to a new `SessionDeletedEvent` record containing: `SessionId` (string, from `sessionID`), `ProjectId` (string, from `info.projectID`).

12. **[REQ-012]** `ChatViewModel` must handle `SessionCreatedEvent` by publishing a `WeakReferenceMessenger` message of type `SessionCreatedMessage(SessionId, ProjectId)` so that `FlyoutViewModel` can prepend the new session to the drawer list without a full reload. No session-ID filter is applied (the event is global).

13. **[REQ-013]** `ChatViewModel` must handle `SessionDeletedEvent` by publishing the existing `SessionDeletedMessage(SessionId, ProjectId)` so that `FlyoutViewModel` removes the session from the drawer list. No session-ID filter is applied.

### Part Type: `tool`

14. **[REQ-014]** `PartDto` must be extended with a `State` property (`JsonElement?`, mapped from `"state"`) and a `CallId` property (string?, mapped from `"callID"`) and a `Tool` property (string?, mapped from `"tool"`), captured via `[JsonExtensionData]` or explicit mapping. The `ToolState` status is read from `state.status` (string: `"pending"` | `"running"` | `"completed"` | `"error"`).

15. **[REQ-015]** A new `ToolCallInfo` class (inheriting `ObservableObject`) must be introduced in `openMob.Core/Models/` with the following observable properties:
    - `PartId` (string, immutable)
    - `ToolName` (string, immutable)
    - `Status` (`ToolCallStatus` enum: `Pending`, `Running`, `Completed`, `Error`)
    - `Title` (string?, observable — populated from `state.title` when `running` or `completed`)
    - `Output` (string?, observable — populated from `state.output` when `completed`)
    - `ErrorText` (string?, observable — populated from `state.error` when `error`)
    - `DurationMs` (long?, observable — computed from `state.time.start` and `state.time.end` when `completed`)

16. **[REQ-016]** `ChatMessage` must expose a new `ObservableCollection<ToolCallInfo> ToolCalls` property (initialised empty) and a computed `bool HasToolCalls` (true when `ToolCalls.Count > 0`).

17. **[REQ-017]** `HandleMessagePartUpdated` in `ChatViewModel` must be extended: when `e.Part.Type == "tool"`, find or create a `ToolCallInfo` in the target `ChatMessage.ToolCalls` collection (matched by `PartId`), then update its `Status`, `Title`, `Output`, `ErrorText`, and `DurationMs` from the `state` JSON element.

18. **[REQ-018]** `HandleMessageUpdated` in `ChatViewModel` must be extended: when processing parts from `e.Message.Parts`, for each part with `type == "tool"`, apply the same `ToolCallInfo` upsert logic as REQ-017.

19. **[REQ-019]** The XAML `ChatPage.xaml` must render `ToolCallInfo` items inside the assistant message bubble using a `CollectionView` or `BindableLayout` bound to `ToolCalls`. Each item shows: tool name, status icon/indicator, title (when available), output text (when `Completed`), error text (when `Error`). A `pending`/`running` state shows an `ActivityIndicator`.

### Part Type: `reasoning`

20. **[REQ-020]** `ChatMessage` must expose `ReasoningText` (string, `[ObservableProperty]`, initially empty) and `HasReasoning` (bool, computed from `ReasoningText.Length > 0`).

21. **[REQ-021]** `HandleMessagePartUpdated` must be extended: when `e.Part.Type == "reasoning"`, update `existing.ReasoningText` with the `text` field value.

22. **[REQ-022]** `HandleMessagePartDelta` must be extended: when `e.Field == "reasoning"` (in addition to the existing `"text"` check), append `e.Delta` to `existing.ReasoningText`.

23. **[REQ-023]** The XAML `ChatPage.xaml` must render a collapsible reasoning block above the main text content of assistant messages when `HasReasoning == true`. The block is collapsed by default and toggled by a "Show thinking" / "Hide thinking" label tap.

### Part Types: `step-start` / `step-finish`

24. **[REQ-024]** `ChatMessage` must expose `StepCount` (int, `[ObservableProperty]`, initially 0) and `LastStepCost` (decimal?, `[ObservableProperty]`, initially null).

25. **[REQ-025]** `HandleMessagePartUpdated` must be extended: when `e.Part.Type == "step-start"`, increment `existing.StepCount`. When `e.Part.Type == "step-finish"`, update `existing.LastStepCost` from the `cost` field in the part's `Extras`.

26. **[REQ-026]** The XAML `ChatPage.xaml` must render a lightweight step indicator below the assistant message bubble when `StepCount > 0`, showing the step count and cost (e.g. "3 steps · $0.0042").

### Part Types: `subtask`, `agent`, `compaction`

27. **[REQ-027]** `ChatMessage` must expose `SubtaskLabels` (`ObservableCollection<string>`, initially empty) for subtask parts, and `CompactionNotice` (string?, `[ObservableProperty]`, initially null) for compaction parts.

28. **[REQ-028]** `HandleMessagePartUpdated` must be extended:
    - `type == "subtask"`: append `"{agent}: {description}"` to `SubtaskLabels`.
    - `type == "agent"`: append the agent `name` to `SubtaskLabels` (reuse same collection).
    - `type == "compaction"`: set `CompactionNotice` to `"Context compacted"` (or `"Context auto-compacted"` if `auto == true`).

29. **[REQ-029]** The XAML `ChatPage.xaml` must render subtask labels as small chips above the message text, and the compaction notice as a full-width informational banner between messages.

### Part Types: `snapshot`, `patch`, `retry`

30. **[REQ-030]** `HandleMessagePartUpdated` must silently ignore parts with `type` in `{ "snapshot", "patch", "retry" }`. In DEBUG builds, a `DebugLogger` entry with tag `OM_SSE` must be emitted: `"[SSE] Ignored part type: {type} (partId={partId})"`.

### `UnknownEvent` Logging

31. **[REQ-031]** The `UnknownEvent` case in `StartSseSubscriptionAsync` (currently absent — the switch has no default) must be added. It must call `DebugLogger.Log("OM_SSE", $"[SSE] Unknown event type: '{e.RawType}'")` in DEBUG builds and do nothing in RELEASE builds.

---

## Functional Impacts

### Affected Components / Systems

| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatEventType.cs` | Add 6 values; remove `PermissionUpdated` | `PermissionReplied`, `MessageRemoved`, `MessagePartRemoved`, `SessionCreated`, `SessionDeleted` added |
| `ChatEvent.cs` | Add 5 new event records; remove `PermissionUpdatedEvent`; rename to `PermissionRepliedEvent` | All new records follow existing pattern with `ProjectDirectory` |
| `ChatEventParser.cs` | Add 5 new parse methods; remap `permission.updated` and `permission.replied` to `ParsePermissionReplied` | Remove `ParsePermissionUpdated`; add `ParseMessageRemoved`, `ParseMessagePartRemoved`, `ParseSessionCreated`, `ParseSessionDeleted` |
| `MessageDtos.cs` (`PartDto`) | Add `State` (`JsonElement?`), `CallId` (string?), `Tool` (string?) properties | Backward-compatible additions |
| `ToolCallInfo.cs` | **New file** in `openMob.Core/Models/` | `ObservableObject` subclass |
| `ChatMessage.cs` | Add `ToolCalls`, `HasToolCalls`, `ReasoningText`, `HasReasoning`, `StepCount`, `LastStepCost`, `SubtaskLabels`, `CompactionNotice` | All observable; constructor updated |
| `ChatViewModel.cs` | Add 5 new SSE handlers; extend `HandleMessagePartUpdated` and `HandleMessageUpdated` | `HandlePermissionReplied`, `HandleMessageRemoved`, `HandleMessagePartRemoved`, `HandleSessionCreated`, `HandleSessionDeleted` |
| `Messages/SessionCreatedMessage.cs` | **New file** | `sealed record SessionCreatedMessage(string SessionId, string ProjectId)` |
| `FlyoutViewModel.cs` | Subscribe to `SessionCreatedMessage`; prepend session to drawer list | Symmetric with existing `SessionDeletedMessage` handling |
| `ChatPage.xaml` | Add DataTemplates for tool calls, reasoning block, step indicator, subtask chips, compaction banner | UI-layer only |
| `ChatEventParserTests.cs` | Add tests for 5 new event types | |
| `ChatViewModelSseTests.cs` | Add tests for 5 new handlers + extended part handling | |

### Dependencies

- Existing `SessionDeletedMessage` in `openMob.Core/Messages/` — reused as-is for `session.deleted`.
- Existing `WeakReferenceMessenger.Default.Send(...)` pattern — reused for `SessionCreatedMessage`.
- Existing `ResolvePermissionRequest` / `DecrementPendingPermissions` helpers in `ChatViewModel` — reused for `HandlePermissionReplied`.
- Existing `DebugLogger` infrastructure — reused for unknown/ignored part logging.
- `FlyoutViewModel` — must subscribe to `SessionCreatedMessage` (symmetric with existing `SessionDeletedMessage` subscription).

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Does `ToolPart.state` always arrive complete in `message.part.updated`, or can it arrive partially during `running` (e.g. only `status` and `title`, no `output`)? | Open | To be confirmed via logcat during implementation. The handler must be defensive: read only the fields present. |
| 2 | Does `session.created` / `session.deleted` need a project-directory filter, or is it global? | Resolved | Global — no session-ID or directory filter. The `FlyoutViewModel` already filters by active project when rendering. |
| 3 | Does `ReasoningPart` text arrive via `message.part.delta` (field=`"reasoning"`) or only via `message.part.updated`? | Open | To be confirmed via logcat. REQ-022 covers both paths defensively. |
| 4 | When `permission.replied` with `reply == "reject"` arrives, does the server also send individual `permission.replied` events for each cascaded rejection, or only one? | Open | REQ-005 resolves all pending cards defensively regardless. If individual events also arrive, the `PermissionStatus == Resolved` guard in `ResolvePermissionRequest` prevents double-processing. |
| 5 | Does `FlyoutViewModel` currently use polling or SSE for session list updates? | Resolved | It uses `WeakReferenceMessenger` messages (`SessionTitleUpdatedMessage`, `SessionDeletedMessage`). `SessionCreatedMessage` follows the same pattern. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a `permission.replied` SSE event with `reply == "once"` or `"always"`, when received, then the matching permission card transitions to `Resolved` state with the correct label, without any user action. *(REQ-001, REQ-003, REQ-004)*
- [ ] **[AC-002]** Given a `permission.replied` SSE event with `reply == "reject"`, when received, then **all** `Pending` permission cards in the current session are resolved as "Deny". *(REQ-005)*
- [ ] **[AC-003]** Given a `message.removed` SSE event, when received, then the corresponding `ChatMessage` is removed from the `Messages` collection and grouping is recalculated. *(REQ-006, REQ-007)*
- [ ] **[AC-004]** Given a `message.part.removed` SSE event, when received, then the corresponding `ToolCallInfo` or reasoning entry is removed from the target `ChatMessage`. *(REQ-008, REQ-009)*
- [ ] **[AC-005]** Given a `session.created` SSE event, when received, then `FlyoutViewModel` prepends the new session to the drawer list without a full reload. *(REQ-010, REQ-012)*
- [ ] **[AC-006]** Given a `session.deleted` SSE event, when received, then `FlyoutViewModel` removes the session from the drawer list. *(REQ-011, REQ-013)*
- [ ] **[AC-007]** Given a `message.part.updated` with `type == "tool"` and `state.status == "pending"`, when received, then the assistant message shows a tool card with an `ActivityIndicator` and the tool name. *(REQ-014, REQ-015, REQ-016, REQ-017, REQ-019)*
- [ ] **[AC-008]** Given a `message.part.updated` with `type == "tool"` and `state.status == "completed"`, when received, then the tool card shows the tool name, title, and output text. *(REQ-017, REQ-019)*
- [ ] **[AC-009]** Given a `message.part.updated` with `type == "tool"` and `state.status == "error"`, when received, then the tool card shows the tool name and error text. *(REQ-017, REQ-019)*
- [ ] **[AC-010]** Given a `message.part.updated` with `type == "reasoning"`, when received, then the assistant message shows a collapsible "Show thinking" block. *(REQ-020, REQ-021, REQ-023)*
- [ ] **[AC-011]** Given a `message.part.delta` with `field == "reasoning"`, when received, then the reasoning text is appended incrementally. *(REQ-022)*
- [ ] **[AC-012]** Given a `message.part.updated` with `type == "step-finish"`, when received, then the step count and cost are shown below the assistant message bubble. *(REQ-024, REQ-025, REQ-026)*
- [ ] **[AC-013]** Given a `message.part.updated` with `type == "subtask"`, when received, then a chip with the agent name and description is shown above the message text. *(REQ-027, REQ-028, REQ-029)*
- [ ] **[AC-014]** Given a `message.part.updated` with `type == "compaction"`, when received, then a full-width "Context compacted" banner appears between messages. *(REQ-027, REQ-028, REQ-029)*
- [ ] **[AC-015]** Given a `message.part.updated` with `type` in `{ "snapshot", "patch", "retry" }`, when received in a DEBUG build, then a `OM_SSE` log entry is emitted and no UI change occurs. *(REQ-030)*
- [ ] **[AC-016]** Given any unrecognised SSE event type, when received in a DEBUG build, then a `OM_SSE` log entry with the raw type string is emitted. *(REQ-031)*
- [ ] **[AC-017]** All new `ChatEventParser` cases are covered by unit tests in `ChatEventParserTests.cs`. *(REQ-001, REQ-006, REQ-008, REQ-010, REQ-011)*
- [ ] **[AC-018]** All new `ChatViewModel` handlers are covered by unit tests in `ChatViewModelSseTests.cs`. *(REQ-003, REQ-007, REQ-009, REQ-012, REQ-013)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

1. **`permission.replied` wire format** — confirmed from server source (`src/permission/index.ts`): `{ sessionID, requestID, reply }` where `reply` is `"once" | "always" | "reject"`. The existing `ParsePermissionUpdated` reads `permissionID` — the new parser must read `requestID` instead. Both `permission.replied` and `permission.updated` must route to the same new `ParsePermissionReplied` method.

2. **`ToolPart` state JSON shape** — confirmed from server source (`src/session/message-v2.ts`):
   - `pending`: `{ status: "pending", input: {}, raw: string }`
   - `running`: `{ status: "running", input: {}, title?: string, metadata?: {}, time: { start: number } }`
   - `completed`: `{ status: "completed", input: {}, output: string, title: string, metadata: {}, time: { start, end, compacted? }, attachments?: [] }`
   - `error`: `{ status: "error", input: {}, error: string, metadata?: {}, time: { start, end } }`
   - The `PartDto.State` field should be kept as `JsonElement?` and parsed manually in the handler to avoid a complex DTO hierarchy.

3. **`PartDto` extension** — add `State` (`JsonElement?`, `[JsonPropertyName("state")]`), `CallId` (string?, `[JsonPropertyName("callID")]`), `Tool` (string?, `[JsonPropertyName("tool")]`) as optional positional parameters or `init` properties. The existing `[JsonExtensionData] Extras` already captures these, but explicit mapping is cleaner and avoids double-parsing.

4. **`ToolCallInfo` upsert pattern** — `HandleMessagePartUpdated` must find an existing `ToolCallInfo` in `ChatMessage.ToolCalls` by `PartId` (update in place) or create a new one (add to collection). Both paths must run on the UI thread via `_dispatcher.Dispatch`.

5. **`ReasoningPart` delta field name** — the server emits `message.part.delta` with `field: "text"` for text parts. It is **not confirmed** whether reasoning deltas use `field: "reasoning"` or `field: "text"` on a reasoning part. The implementation must check logcat during the first test run and adjust `HandleMessagePartDelta` accordingly. REQ-022 assumes `field: "reasoning"` as the most likely value.

6. **`session.created` / `session.deleted` payload** — confirmed from server source (`src/session/index.ts`): both events carry `{ sessionID, info: Session.Info }`. `Session.Info` maps to the existing `SessionDto`. `SessionDeletedEvent.ProjectId` is read from `info.projectID`.

7. **`FlyoutViewModel` subscription** — the existing `SessionDeletedMessage` subscription pattern must be replicated for `SessionCreatedMessage`. The new session must be prepended (not appended) to the drawer list to match the server's descending-by-updated-time ordering.

8. **`ChatEventType` enum cleanup** — `PermissionUpdated` is replaced by `PermissionReplied`. Any existing switch exhaustiveness checks or tests referencing `PermissionUpdated` / `PermissionUpdatedEvent` must be updated. The `PermissionUpdatedEvent` record is deleted.

9. **`StepFinishPart` cost field** — the `cost` field is a `number` (float) in the server schema. Map to `decimal` in C# via `JsonElement.GetDecimal()`. The `tokens` object (input, output, cache, reasoning) is available in `Extras` for future use but not required by this spec.

10. **`CompactionPart` placement** — compaction parts belong to a `user` message (the server injects them as user message parts). The compaction banner should be rendered as a special full-width item between the preceding assistant message and the compaction user message, not inside a bubble.

### Suggested implementation order

1. `ChatEventType.cs` — add/rename enum values
2. `ChatEvent.cs` — add/rename event records
3. `ChatEventParser.cs` — add/remap parse methods
4. `MessageDtos.cs` — extend `PartDto`
5. `ToolCallInfo.cs` — new model class
6. `ChatMessage.cs` — add new observable properties
7. `Messages/SessionCreatedMessage.cs` — new message record
8. `ChatViewModel.cs` — add handlers, extend existing handlers
9. `FlyoutViewModel.cs` — subscribe to `SessionCreatedMessage`
10. `ChatPage.xaml` — add DataTemplates
11. Unit tests — `ChatEventParserTests.cs` and `ChatViewModelSseTests.cs`

### Constraints to respect

- As established in **SSE Project Directory Propagation** (2026-03-21): all new `ChatEvent` records must include `string? ProjectDirectory { get; init; }` inherited from `ChatEvent`. All new `ChatViewModel` handlers must apply the project-directory filter before the session-ID filter.
- As established in **Chat Conversation Loop** (2026-03-16): all `Messages` collection mutations must be dispatched via `_dispatcher.Dispatch(...)` for UI thread safety.
- As established in **opencode-api-client** (2026-03-15): `ChatEventParser` never propagates exceptions — malformed or unrecognised events fall through to `UnknownEvent`.
- `openMob.Core` has zero MAUI dependencies. `ToolCallInfo`, `ChatMessage` extensions, and all new models must be pure .NET.
- `ToolCalls` and `SubtaskLabels` are `ObservableCollection<T>` so XAML `CollectionView`/`BindableLayout` bindings update automatically without replacing the collection reference.

### Related files

- `src/openMob.Core/Helpers/ChatEventParser.cs` — primary parser (441 lines)
- `src/openMob.Core/Models/ChatEvent.cs` — event records (191 lines)
- `src/openMob.Core/Models/ChatEventType.cs` — enum (32 lines)
- `src/openMob.Core/Models/ChatMessage.cs` — domain model (317 lines)
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` — `PartDto`, `MessageWithPartsDto` (62 lines)
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — SSE switch at line 1076, handlers from line 1119 (1872 lines total)
- `src/openMob.Core/Messages/SessionDeletedMessage.cs` — pattern to replicate for `SessionCreatedMessage`
- Server source reference: `packages/opencode/src/permission/index.ts` — `Permission.Event.Replied` wire format
- Server source reference: `packages/opencode/src/session/message-v2.ts` — `ToolPart`, `ReasoningPart`, `StepFinishPart`, `SubtaskPart`, `CompactionPart` schemas
- Server source reference: `packages/opencode/src/session/index.ts` — `Session.Event.Created`, `Session.Event.Deleted` wire format

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-28

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/sse-full-message-type-coverage |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Helpers/ChatEventParser.cs` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs`, `FlyoutViewModel.cs` |
| Models / Enums | om-mobile-core | `src/openMob.Core/Models/`, `src/openMob.Core/Messages/` |
| DTOs | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/Helpers/ChatEventParserTests.cs`, `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Models/ToolCallInfo.cs` — new `ObservableObject` subclass with `ToolCallStatus` enum and all observable properties for tool call state
- `src/openMob.Core/Messages/SessionCreatedMessage.cs` — new `sealed record SessionCreatedMessage(string SessionId, string ProjectId)` following the `SessionDeletedMessage` pattern

### Files to Modify

- `src/openMob.Core/Models/ChatEventType.cs` — add `PermissionReplied`, `MessageRemoved`, `MessagePartRemoved`, `SessionCreated`, `SessionDeleted`; remove `PermissionUpdated`
- `src/openMob.Core/Models/ChatEvent.cs` — add `PermissionRepliedEvent`, `MessageRemovedEvent`, `MessagePartRemovedEvent`, `SessionCreatedEvent`, `SessionDeletedEvent`; remove `PermissionUpdatedEvent`
- `src/openMob.Core/Helpers/ChatEventParser.cs` — add `ParsePermissionReplied`, `ParseMessageRemoved`, `ParseMessagePartRemoved`, `ParseSessionCreated`, `ParseSessionDeleted`; remap `permission.updated` and `permission.replied` to `ParsePermissionReplied`; remove `ParsePermissionUpdated`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/MessageDtos.cs` — extend `PartDto` with `State` (`JsonElement?`), `CallId` (string?), `Tool` (string?) explicit properties
- `src/openMob.Core/Models/ChatMessage.cs` — add `ToolCalls`, `HasToolCalls`, `ReasoningText`, `HasReasoning`, `StepCount`, `LastStepCost`, `SubtaskLabels`, `CompactionNotice`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add 5 new SSE handlers; extend `HandleMessagePartUpdated`, `HandleMessageUpdated`, `HandleMessagePartDelta`; add `UnknownEvent` case
- `src/openMob.Core/ViewModels/FlyoutViewModel.cs` — subscribe to `SessionCreatedMessage`; prepend session to drawer list
- `src/openMob/Views/Pages/ChatPage.xaml` — add tool call cards, reasoning block, step indicator, subtask chips, compaction banner to `MessageBlockView` or inline in `CollectionView` DataTemplate
- `tests/openMob.Tests/Helpers/ChatEventParserTests.cs` — add tests for 5 new event types + `permission.updated` remapping
- `tests/openMob.Tests/ViewModels/ChatViewModelSseTests.cs` — add tests for 5 new handlers + extended part handling

### Technical Dependencies

- `PermissionUpdatedEvent` is currently referenced in `ChatEventParser.cs` (`ParsePermissionUpdated`) and `ChatEvent.cs`. Both must be updated atomically — the enum value removal will cause compile errors if the event record is not also removed.
- `PartDto` is a positional `sealed record`. Adding new properties requires adding them as `init`-only properties (not positional parameters) to avoid breaking existing call sites. The `[JsonExtensionData] Extras` dict already captures `state`, `callID`, `tool` — the new explicit properties will shadow them cleanly.
- `DebugLogger` does **not** have a generic `Log(tag, message)` method. The spec's `REQ-030` and `REQ-031` reference `DebugLogger.Log("OM_SSE", ...)` which does not exist. The correct approach is `DebugLogger.WriteAction("OM_SSE", message)` (the raw delegate) or `DebugLogger.LogSse(...)`. **Agents must use `DebugLogger.WriteAction("OM_SSE", ...)` directly** for the unknown-event and ignored-part-type cases, wrapped in `#if DEBUG`.
- `FlyoutViewModel` currently subscribes to `SessionDeletedMessage` and triggers a full `LoadSessionsCommand.ExecuteAsync(null)` reload. For `SessionCreatedMessage`, the spec requires a **prepend** (not a full reload). The `FlyoutViewModel` must construct a `SessionItem` from the `SessionCreatedMessage` data and call `_dispatcher.Dispatch(() => Sessions.Insert(0, newItem))`. The `SessionCreatedMessage` must carry enough data to build a `SessionItem` — specifically `SessionId`, `ProjectId`, `Title`, and `UpdatedAt`. Since `SessionCreatedEvent` carries the full `SessionDto`, the message record should include the `SessionDto` or at minimum `Title` and `UpdatedAt`.
  - **Decision**: `SessionCreatedMessage` will carry `SessionId`, `ProjectId`, `Title` (string), and `UpdatedAt` (DateTimeOffset) to avoid exposing `SessionDto` in the Messages layer.
- `ChatMessage.HasToolCalls` and `HasReasoning` are computed properties that depend on observable collections/properties. They cannot use `[ObservableProperty]`. They must be implemented as standard C# properties with `OnPropertyChanged` notifications triggered from `ToolCalls.CollectionChanged` and the `ReasoningText` setter respectively.
- `ToolCalls` and `SubtaskLabels` are `ObservableCollection<T>` — they must be initialised in the `ChatMessage` constructor (not as field initialisers) to avoid null reference issues.
- The `MessageBlockView` control currently receives all `ChatMessage` properties as `BindableProperty` parameters. Adding `ToolCalls`, `HasToolCalls`, `ReasoningText`, `HasReasoning`, `StepCount`, `LastStepCost`, `SubtaskLabels`, `CompactionNotice` as new `BindableProperty` entries on `MessageBlockView` is the cleanest approach. Alternatively, the `DataTemplate` in `ChatPage.xaml` can be changed to bind directly to `ChatMessage` with `x:DataType="models:ChatMessage"` — this is already the case (`x:DataType="models:ChatMessage"` is set on the DataTemplate). The `MessageBlockView` approach is preferred for encapsulation.
- `CompactionNotice` rendering: compaction parts belong to user messages. The `MessageBlockView` must check `CompactionNotice` and render a full-width banner **instead of** the normal user bubble when `CompactionNotice` is not null.

### Technical Risks

- **Breaking change: `PermissionUpdated` enum removal** — any existing tests that reference `ChatEventType.PermissionUpdated` or `PermissionUpdatedEvent` will fail to compile. The `ChatEventParserTests.cs` file must be checked for such references and updated.
- **`PartDto` is a positional record** — adding new properties as `init`-only (not positional) is safe, but the `[JsonExtensionData]` dict currently captures `state`, `callID`, `tool`. Once explicit properties are added, the `Extras` dict will no longer contain those keys. Any code reading `Extras["state"]` will break. A search confirms no existing code reads these keys from `Extras` — safe to proceed.
- **`HasToolCalls` and `HasReasoning` notification** — `ChatMessage` uses `[ObservableProperty]` source generators. Computed properties that depend on other observable properties require manual `OnPropertyChanged` calls. `HasReasoning` must be raised in the `ReasoningText` setter (via `partial void OnReasoningTextChanged`). `HasToolCalls` must be raised via `ToolCalls.CollectionChanged` subscription in the constructor.
- **`FlyoutViewModel` `SessionCreatedMessage` handler** — the handler must guard against `_disposeCts.IsCancellationRequested` (same as `SessionDeletedMessage`). Unlike `SessionDeletedMessage` which triggers a full reload, this handler does a direct `Sessions.Insert(0, ...)` — it must also check that the session belongs to the currently active project (`ActiveProjectId`) before inserting.
- **`MessageBlockView` BindableProperty explosion** — adding 8 new `BindableProperty` entries to `MessageBlockView` is significant. Consider whether the DataTemplate should instead bind directly to `ChatMessage` (already typed via `x:DataType`) and pass the whole object. The current architecture passes individual properties. For this spec, the individual-property approach is maintained for consistency.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/sse-full-message-type-coverage`
2. [om-mobile-core] Implement all Core changes: `ChatEventType`, `ChatEvent`, `ChatEventParser`, `PartDto`, `ToolCallInfo`, `ChatMessage`, `SessionCreatedMessage`, `ChatViewModel`, `FlyoutViewModel`
3. ⟳ [om-mobile-ui] Implement XAML changes in `ChatPage.xaml` — can start once `ChatMessage` binding surface is defined (after step 2 defines the new properties)
4. [om-tester] Write unit tests for `ChatEventParser` and `ChatViewModel` SSE handlers — must wait for step 2
5. [om-reviewer] Full review against spec — must wait for steps 2, 3, 4
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-031]` requirements implemented
- [ ] All `[AC-001]` through `[AC-018]` acceptance criteria satisfied
- [ ] Unit tests written for all new `ChatEventParser` cases and `ChatViewModel` handlers
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
