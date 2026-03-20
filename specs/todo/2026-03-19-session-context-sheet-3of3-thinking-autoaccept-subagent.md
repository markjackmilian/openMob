# Session Context Sheet — Thinking Level, Auto-Accept & Invoke Subagent

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-19                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

This spec covers the three remaining functional sections of the Session Context Sheet: **Thinking Level** (a three-way segmented control: Low / Medium / High), **Auto-Accept** (a boolean toggle), and **Invoke Subagent** (a one-shot action that opens a subagent picker and dispatches an invocation request to the server). All three sections are currently placeholder-only. Thinking level and auto-accept are persisted locally per project via `ProjectPreference`. Subagent invocation requires API research to determine the correct endpoint. This spec depends on `session-context-sheet-1of3-core` and `session-context-sheet-2of3-agent-model` being implemented first.

---

## Scope

### In Scope
- `ContextSheetViewModel.ThinkingLevel` property wired to persistence via `IProjectPreferenceService.SetThinkingLevelAsync` and `ProjectPreferenceChangedMessage`
- `ContextSheetViewModel.AutoAccept` property wired to persistence via `IProjectPreferenceService.SetAutoAcceptAsync` and `ProjectPreferenceChangedMessage`
- `ChatViewModel` updated to expose `ThinkingLevel` and `AutoAccept` observable properties, loaded from `ProjectPreference` and updated via `ProjectPreferenceChangedMessage`
- Extension of `IAgentService` with `GetSubagentAgentsAsync()` — returns only agents with `Mode == "subagent"` or `Mode == "all"`
- `IAppPopupService.ShowSubagentPickerAsync(Action<string> onSubagentSelected, CancellationToken ct)` — opens `AgentPickerSheet` in subagent mode
- `AgentPickerViewModel` extended with a `PickerMode` enum property (`Primary` / `Subagent`) to switch list source and sheet title
- `ContextSheetViewModel.InvokeSubagentCommand` — opens subagent picker, on selection dispatches the invocation request
- Technical analysis of the opencode API to determine the correct subagent invocation endpoint (open question — see below)

### Out of Scope
- UI/XAML of the Context Sheet (covered by `om-mobile-ui`)
- Thinking level support detection per model (future spec — models are untyped `JsonElement`, no capability flags available)
- Subagent activity indicator in the message flow (covered by `chat-page-redesign` spec REQ-032/033)
- Auto-accept enforcement logic on the server side (the toggle is a local preference; server-side behaviour depends on the opencode API)
- Thinking level and auto-accept shown in the context status bar (covered by `chat-page-redesign` spec REQ-019/020)

---

## Functional Requirements

> Requirements are numbered for traceability.

### Thinking Level

1. **[REQ-001]** `ContextSheetViewModel` must expose a `ChangeThinkingLevelCommand` (`[RelayCommand]`) that accepts a `ThinkingLevel` parameter. When invoked:
   - Set `ThinkingLevel = value` on the ViewModel
   - Call `await _preferenceService.SetThinkingLevelAsync(CurrentProjectId, value, ct)`
   - On success: publish `ProjectPreferenceChangedMessage`
   - On failure: set `ErrorMessage` (non-blocking, no rollback)

2. **[REQ-002]** The three valid values for `ThinkingLevel` are defined by the `ThinkingLevel` enum (introduced in `session-context-sheet-1of3-core`): `Low = 0`, `Medium = 1`, `High = 2`. The default value when no preference exists is `Medium`.

3. **[REQ-003]** `ChatViewModel` must be extended with:
   - `ThinkingLevel` (`ThinkingLevel`) — loaded from `IProjectPreferenceService.GetOrDefaultAsync` during `LoadContextAsync`; updated when a `ProjectPreferenceChangedMessage` arrives for the current project

### Auto-Accept

4. **[REQ-004]** `ContextSheetViewModel` must expose a `ToggleAutoAcceptCommand` (`[AsyncRelayCommand]`) that toggles `AutoAccept` (i.e., sets it to `!AutoAccept`). When invoked:
   - Capture the previous value
   - Set `AutoAccept = !AutoAccept` on the ViewModel
   - Call `await _preferenceService.SetAutoAcceptAsync(CurrentProjectId, AutoAccept, ct)`
   - On success: publish `ProjectPreferenceChangedMessage`
   - On failure: revert `AutoAccept` to its previous value and set `ErrorMessage`

   > Note: Auto-accept is the only preference where a rollback on failure is required, because a toggle that silently fails would leave the UI in a misleading state.

5. **[REQ-005]** `ChatViewModel` must be extended with:
   - `AutoAccept` (`bool`) — loaded from `IProjectPreferenceService.GetOrDefaultAsync` during `LoadContextAsync`; updated when a `ProjectPreferenceChangedMessage` arrives for the current project

### Invoke Subagent

6. **[REQ-006]** `IAgentService` must be extended with:
   ```
   Task<IReadOnlyList<AgentDto>> GetSubagentAgentsAsync(CancellationToken ct = default)
   ```
   The implementation filters the result of `GetAgentsAsync()` keeping only entries where `Mode == "subagent"` or `Mode == "all"`.

7. **[REQ-007]** `AgentPickerViewModel` must be extended with a `PickerMode` enum property (`Primary` / `Subagent`):
   - When `PickerMode == Primary`: loads from `IAgentService.GetPrimaryAgentsAsync()`, sheet title = "Select Agent", prepends "Default" entry (as per `session-context-sheet-2of3-agent-model` spec)
   - When `PickerMode == Subagent`: loads from `IAgentService.GetSubagentAgentsAsync()`, sheet title = "Invoke Subagent", no "Default" entry (a subagent must be explicitly selected)

8. **[REQ-008]** `IAppPopupService` must be extended with:
   ```
   Task ShowSubagentPickerAsync(Action<string> onSubagentSelected, CancellationToken ct = default)
   ```
   The MAUI implementation resolves `AgentPickerSheet` from DI, sets `PickerMode = Subagent` and `OnAgentSelected = onSubagentSelected` on `AgentPickerViewModel`, then pushes it modally.

9. **[REQ-009]** `ContextSheetViewModel` must expose an `InvokeSubagentCommand` (`[AsyncRelayCommand]`) that:
   - Calls `_popupService.ShowSubagentPickerAsync(OnSubagentSelected)`
   - `OnSubagentSelected(agentName)` dispatches the subagent invocation request (see REQ-010)
   - Sets `IsBusy = true` during dispatch, `IsBusy = false` on completion or error
   - On error: sets `ErrorMessage`

10. **[REQ-010]** Subagent invocation API call — **to be determined during Technical Analysis**. The implementer must:
    - Inspect the opencode API documentation / OpenAPI spec for an endpoint that triggers subagent invocation within a session
    - Candidate approaches (in priority order):
      1. A dedicated subagent invocation endpoint (e.g., `POST /session/{id}/subagent`)
      2. `SendPromptAsync` with an agent override parameter (check if `SendPromptRequest` supports an `agent` field)
      3. `UpdateConfigAsync` with a session-scoped agent override (least preferred — config is global)
    - The chosen approach must be documented in the Technical Analysis section of the implemented spec before coding begins
    - If no suitable endpoint is found, `InvokeSubagentCommand` must display an `ErrorMessage` = "Subagent invocation not supported by this server version" and take no further action

11. **[REQ-011]** After a successful subagent invocation dispatch, the Context Sheet remains open (it does not auto-dismiss). The user closes it manually by dismissing the bottom sheet.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ContextSheetViewModel.cs` | **Extended** | Add `ChangeThinkingLevelCommand`, `ToggleAutoAcceptCommand`, `InvokeSubagentCommand` |
| `ChatViewModel.cs` | **Extended** | Add `ThinkingLevel`, `AutoAccept` properties; load from preference; update from message |
| `IAgentService.cs` | **Extended** | New `GetSubagentAgentsAsync()` method |
| `AgentService.cs` | **Extended** | Implementation of `GetSubagentAgentsAsync()` with LINQ filter |
| `AgentPickerViewModel.cs` | **Extended** | Add `PickerMode` enum; switch list source and title based on mode |
| `IAppPopupService.cs` | **Extended** | New `ShowSubagentPickerAsync` method |
| `MauiPopupService.cs` | **Extended** | Implementation of `ShowSubagentPickerAsync` |
| `IOpencodeApiClient.cs` | **Possibly extended** | May need a new method for subagent invocation (TBD in Technical Analysis) |
| `OpencodeApiClient.cs` | **Possibly extended** | Implementation of subagent invocation endpoint |

### Dependencies
- `session-context-sheet-1of3-core` — must be implemented first (`ThinkingLevel` enum, `ProjectPreference` fields, `IProjectPreferenceService` extensions, `ProjectPreferenceChangedMessage`, `ContextSheetViewModel` base)
- `session-context-sheet-2of3-agent-model` — must be implemented first (`AgentPickerViewModel` callback pattern, `PickerMode` builds on it)
- `IAgentService` — extended by this spec (and by `session-context-sheet-2of3-agent-model`)
- opencode API documentation — required to determine subagent invocation endpoint (REQ-010)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Which opencode API endpoint handles subagent invocation within a session? | **Open** | Must be investigated during Technical Analysis. See REQ-010 for candidate approaches and fallback behaviour. |
| 2 | Does the opencode server use the `AutoAccept` preference to automatically approve tool calls / file writes, or is it purely a client-side UX hint? | **Open** | To be verified against opencode API. If server-side: `UpdateConfigAsync` may need to be called with the auto-accept value when the session starts. If client-side only: the preference is stored locally and used by the app to auto-dismiss confirmation dialogs (future spec). |
| 3 | Should `ThinkingLevel` affect the prompt sent to the model (e.g., as a system prompt modifier or a model parameter), or is it a server-side config? | **Open** | Deferred to a future spec on thinking level model integration. For this spec, `ThinkingLevel` is stored locally only and has no effect on API calls. |
| 4 | If `GetSubagentAgentsAsync()` returns an empty list, should `InvokeSubagentCommand` be disabled or should it open an empty picker? | Resolved | Disable `InvokeSubagentCommand` (via `CanExecute`) when the subagent list is empty. Show a tooltip or subtitle "No subagents available" on the row. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the Context Sheet is open with `ThinkingLevel = Medium`, when the user selects "Low", then `ContextSheetViewModel.ThinkingLevel` becomes `Low`, `SetThinkingLevelAsync` is called, and a `ProjectPreferenceChangedMessage` is published. *(REQ-001, REQ-002)*
- [ ] **[AC-002]** Given `ChatViewModel` is active and a `ProjectPreferenceChangedMessage` arrives with `ThinkingLevel = High`, then `ChatViewModel.ThinkingLevel` is updated to `High` without a page reload. *(REQ-003)*
- [ ] **[AC-003]** Given the Context Sheet is open with `AutoAccept = false`, when the toggle is switched on, then `AutoAccept` becomes `true`, `SetAutoAcceptAsync` is called, and a `ProjectPreferenceChangedMessage` is published. *(REQ-004)*
- [ ] **[AC-004]** Given `SetAutoAcceptAsync` fails (DB error), when the toggle is switched, then `AutoAccept` is reverted to its previous value and `ErrorMessage` is set. *(REQ-004)*
- [ ] **[AC-005]** Given the server has agents with `Mode="subagent"` and `Mode="all"`, when `GetSubagentAgentsAsync()` is called, then only those agents are returned; agents with `Mode="primary"` are excluded. *(REQ-006)*
- [ ] **[AC-006]** Given the Context Sheet is open and subagents are available, when "Invoke Subagent" is tapped, then `AgentPickerSheet` opens with title "Invoke Subagent", no "Default" entry, and only subagent-mode agents listed. *(REQ-007, REQ-008)*
- [ ] **[AC-007]** Given `AgentPickerSheet` is open in subagent mode and the user selects an agent, then the sheet dismisses and the subagent invocation request is dispatched to the API. *(REQ-009, REQ-010)*
- [ ] **[AC-008]** Given no subagents are available from the API, when the Context Sheet is open, then the "Invoke Subagent" row is visually disabled and `InvokeSubagentCommand` cannot be executed. *(REQ-004 open question resolution)*
- [ ] **[AC-009]** Given a fresh project with no stored preference, when the Context Sheet opens, then `ThinkingLevel = Medium` and `AutoAccept = false` are shown. *(REQ-002, REQ-004 — defaults from core spec)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate

1. **Subagent invocation endpoint (critical — REQ-010)**: Before writing any code for `InvokeSubagentCommand`, the implementer must:
   - Fetch the opencode OpenAPI spec from the running server (`GET /api` or check `src/openMob.Core/Infrastructure/Http/` for any existing OpenAPI-derived client)
   - Search for endpoints containing "subagent", "agent", or "invoke" in the path or operation ID
   - Check `SendPromptRequest` for an `agent` or `subagent` field that could override the active agent per-message
   - If a dedicated endpoint exists, add it to `IOpencodeApiClient` following the `OpencodeResult<T>` pattern
   - Document the chosen approach in the Technical Analysis section before implementation

2. **`AutoAccept` rollback pattern**: The `ToggleAutoAcceptCommand` must capture the previous value before toggling, then restore it on failure. Since `[ObservableProperty]` source generators fire `PropertyChanged` immediately on assignment, the rollback must re-assign the old value (which will fire another `PropertyChanged` — this is acceptable and expected).

3. **`AgentPickerViewModel.PickerMode` and `OnAppearing`**: The `PickerMode` must be set before the sheet is pushed (in `MauiPopupService`), because `OnAppearing` triggers `LoadAgentsAsync` which reads `PickerMode` to decide which service method to call. Verify the order of operations in `MauiPopupService.ShowSubagentPickerAsync`.

4. **`InvokeSubagentCommand` CanExecute**: Use `[AsyncRelayCommand(CanExecute = nameof(CanInvokeSubagent))]` where `CanInvokeSubagent` returns `!IsBusy && SubagentAgents.Count > 0`. Load `SubagentAgents` during `InitializeAsync` alongside the other preferences. This requires `ContextSheetViewModel` to call `IAgentService.GetSubagentAgentsAsync()` at init time.

5. **`ThinkingLevel` and `AutoAccept` in `ChatViewModel`**: These properties are loaded during `LoadContextAsync` from `GetOrDefaultAsync`. They are also updated via `ProjectPreferenceChangedMessage`. Ensure the message handler runs on the main thread (use `MainThread.BeginInvokeOnMainThread` if the message is published from a background thread).

### Constraints to Respect

- **`ThinkingLevel` is local-only for this spec**: Do not call `UpdateConfigAsync` or any server API when thinking level changes. The integration with the model/server is deferred to a future spec.
- **`AutoAccept` is local-only for this spec**: Same constraint. The server-side behaviour of auto-accept is an open question (OQ-002) to be resolved in a future spec.
- **`AgentPickerViewModel` must remain a single class**: Do not create separate `SubagentPickerViewModel` and `PrimaryAgentPickerViewModel` classes. Use the `PickerMode` enum to switch behaviour within the single existing ViewModel.
- **No `async void`**: `ToggleAutoAcceptCommand` rollback logic must be inside an `[AsyncRelayCommand]`, not `async void`.
- **`ConfigureAwait(false)`**: Required on all `await` calls in `openMob.Core`.

### Related Files and Modules

| File | Relevance |
|------|-----------|
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Add 3 new commands |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Add `ThinkingLevel`, `AutoAccept` properties |
| `src/openMob.Core/Services/IAgentService.cs` | Add `GetSubagentAgentsAsync` |
| `src/openMob.Core/Services/AgentService.cs` | Implement filter |
| `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` | Add `PickerMode` enum |
| `src/openMob.Core/Services/IAppPopupService.cs` | Add `ShowSubagentPickerAsync` |
| `src/openMob/Services/MauiPopupService.cs` | Implement `ShowSubagentPickerAsync` |
| `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` | Possibly add subagent invocation method |
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | Possibly implement subagent invocation |
| `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/` | Possibly add subagent request/response DTOs |

### References to Past Decisions

- As established in **chat-page-redesign** (2026-03-18): REQ-026 specifies Thinking Level as a segmented control (Low/Medium/High) and Auto-Accept as a toggle, both applied immediately with no save button. REQ-031 specifies Invoke Subagent as a one-shot action opening `AgentPickerSheet` in subagent mode.
- As established in **session-context-sheet-1of3-core** (2026-03-19): `ThinkingLevel` enum (`Low=0`, `Medium=1`, `High=2`) and `AutoAccept` bool are stored in `ProjectPreference`. `ProjectPreferenceChangedMessage` is the propagation mechanism. Default values: `ThinkingLevel.Medium`, `AutoAccept = false`.
- As established in **session-context-sheet-2of3-agent-model** (2026-03-19): `AgentPickerViewModel` callback pattern (`OnAgentSelected: Action<string?>?`) and `PickerMode` enum are introduced in that spec. This spec extends `PickerMode` with the `Subagent` value.
- As established in **opencode-api-client** (2026-03-15): All API calls use `OpencodeResult<T>` pattern. New endpoints must follow the same pattern. `SendPromptRequest` carries optional `ModelId` and `ProviderId` — check if an `AgentName` field can be added for subagent invocation.
