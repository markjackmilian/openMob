# Session Context Sheet — Agent & Model Selection

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-19                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

This spec covers the **Agent** and **Model** sections of the Session Context Sheet, plus the read-only **Project** section. It wires the existing `AgentPickerSheet` and `ModelPickerSheet` into the new `ContextSheetViewModel`, introduces primary-agent filtering in `IAgentService`, and ensures that every agent or model selection is immediately persisted to `ProjectPreference` and propagated to `ChatViewModel`. This spec depends on `session-context-sheet-1of3-core` being implemented first.

---

## Scope

### In Scope
- Extension of `IAgentService` with `GetPrimaryAgentsAsync()` — returns only agents with `Mode == "primary"` or `Mode == "all"`
- Wiring `AgentPickerSheet` to `ContextSheetViewModel` via callback pattern (matching the existing `ModelPickerSheet` pattern)
- `ContextSheetViewModel.SelectAgentCommand` — opens `AgentPickerSheet` (primary mode), receives selection via callback, persists via `IProjectPreferenceService.SetAgentAsync`, publishes `ProjectPreferenceChangedMessage`
- `ContextSheetViewModel.SelectModelCommand` — opens `ModelPickerSheet` (existing), receives selection via callback, persists via `IProjectPreferenceService.SetDefaultModelAsync`, publishes `ProjectPreferenceChangedMessage`
- `IAppPopupService.ShowAgentPickerAsync(Action<string?> onAgentSelected, CancellationToken ct)` — new method for primary-agent picker (distinct from any future subagent picker)
- Project section: read-only display of the current project name in `ContextSheetViewModel.ProjectName` (no tap action, no navigation)
- `ChatViewModel` receives `ProjectPreferenceChangedMessage` and updates `SelectedAgentName` / `SelectedAgentDisplayName` (new properties) in addition to the model properties already handled by the core spec

### Out of Scope
- UI/XAML of the Context Sheet rows (covered by `om-mobile-ui`)
- Subagent filtering and invocation (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Thinking level and auto-accept (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Project switching from within the Context Sheet (the Project row is informational only; switching happens via the drawer's `ProjectSwitcherSheet`)
- `AgentPickerSheet` UI changes (the existing sheet is reused as-is; only the data source and callback wiring change)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `IAgentService` must be extended with:
   ```
   Task<IReadOnlyList<AgentDto>> GetPrimaryAgentsAsync(CancellationToken ct = default)
   ```
   The implementation filters the result of `GetAgentsAsync()` keeping only entries where `Mode == "primary"` or `Mode == "all"`. The existing `GetAgentsAsync()` method is preserved unchanged (used by other consumers).

2. **[REQ-002]** `IAppPopupService` must be extended with:
   ```
   Task ShowAgentPickerAsync(Action<string?> onAgentSelected, CancellationToken ct = default)
   ```
   - `onAgentSelected` receives the selected agent name, or `null` if the user selects "Default" (if a Default option is present) or dismisses without selecting.
   - The MAUI implementation resolves `AgentPickerSheet` from DI, sets the callback on `AgentPickerViewModel`, and pushes it modally.
   - The agent list shown is populated from `IAgentService.GetPrimaryAgentsAsync()`.

3. **[REQ-003]** `AgentPickerViewModel` must be extended to support a callback pattern:
   - New property: `OnAgentSelected: Action<string?>?`
   - When the user selects an agent, `SelectAgentCommand` invokes `OnAgentSelected(agentName)` before calling `PopPopupAsync`.
   - A "Default" entry must be prepended to the list (hardcoded, not from the API), representing `AgentName = null`. Selecting it invokes `OnAgentSelected(null)`.
   - The currently selected agent (passed in via a new `SelectedAgentName` property on the ViewModel) is shown with a checkmark.

4. **[REQ-004]** `ContextSheetViewModel` must expose:
   - `SelectAgentCommand` (`[AsyncRelayCommand]`) — calls `_popupService.ShowAgentPickerAsync(OnAgentSelected)` where `OnAgentSelected` is a private method that sets `SelectedAgentName`, triggers persistence, and publishes the message.
   - `SelectModelCommand` (`[AsyncRelayCommand]`) — calls `_popupService.ShowModelPickerAsync(OnModelSelected)` (existing method), where `OnModelSelected` sets `SelectedModelId`, triggers persistence, and publishes the message.

5. **[REQ-005]** When `OnAgentSelected(agentName)` is invoked on `ContextSheetViewModel`:
   - Set `SelectedAgentName = agentName` (observable property, triggers `SelectedAgentDisplayName` recomputation)
   - Call `await _preferenceService.SetAgentAsync(CurrentProjectId, agentName, ct)`
   - On success: publish `ProjectPreferenceChangedMessage` with the updated preference
   - On failure: set `ErrorMessage` (non-blocking, no rollback)

6. **[REQ-006]** When `OnModelSelected(modelId)` is invoked on `ContextSheetViewModel`:
   - Set `SelectedModelId = modelId` (observable property, triggers `SelectedModelDisplayName` recomputation)
   - Call `await _preferenceService.SetDefaultModelAsync(CurrentProjectId, modelId, ct)`
   - On success: publish `ProjectPreferenceChangedMessage` with the updated preference
   - On failure: set `ErrorMessage` (non-blocking, no rollback)

7. **[REQ-007]** `ChatViewModel` must be extended with two new observable properties:
   - `SelectedAgentName` (`string?`) — the raw agent name from preference (`null` = Default)
   - `SelectedAgentDisplayName` (`string`) — computed: `SelectedAgentName ?? "Default"`
   These are populated during `LoadContextAsync` from `IProjectPreferenceService.GetOrDefaultAsync` and updated when a `ProjectPreferenceChangedMessage` arrives.

8. **[REQ-008]** The **Project section** of `ContextSheetViewModel` is read-only:
   - `ProjectName` is loaded during `InitializeAsync` and never changes while the sheet is open.
   - No command is exposed for the Project row.
   - No navigation to `ProjectSwitcherSheet` is triggered from the Context Sheet.

9. **[REQ-009]** `AgentPickerSheet` must be registered in `MauiProgram.cs` as `Transient` (if not already). `AgentPickerViewModel` must be registered in `AddOpenMobCore()` as `Transient` (if not already).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `IAgentService.cs` | **Extended** | New `GetPrimaryAgentsAsync()` method |
| `AgentService.cs` | **Extended** | Implementation of `GetPrimaryAgentsAsync()` with LINQ filter |
| `AgentPickerViewModel.cs` | **Extended** | Add `OnAgentSelected` callback, `SelectedAgentName` input property, "Default" entry prepended to list |
| `IAppPopupService.cs` | **Extended** | New `ShowAgentPickerAsync` method |
| `MauiPopupService.cs` | **Extended** | Implementation of `ShowAgentPickerAsync` |
| `ContextSheetViewModel.cs` | **Extended** | Add `SelectAgentCommand`, `SelectModelCommand`, `OnAgentSelected`, `OnModelSelected` |
| `ChatViewModel.cs` | **Extended** | New `SelectedAgentName`, `SelectedAgentDisplayName` properties; load from preference in `LoadContextAsync`; update from message |

### Dependencies
- `session-context-sheet-1of3-core` — must be implemented first (`ContextSheetViewModel` base, `IProjectPreferenceService` extensions, `ProjectPreferenceChangedMessage`)
- `IAgentService` / `AgentService` — extended by this spec
- `AgentPickerSheet` / `AgentPickerViewModel` — extended by this spec
- `IAppPopupService` / `MauiPopupService` — extended by this spec
- `IProjectPreferenceService.SetAgentAsync` — introduced in core spec

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the "Default" entry in `AgentPickerSheet` always be present, or only when the project has no custom agent set? | Resolved | Always present as the first entry. It allows the user to explicitly reset to the default agent. |
| 2 | What is the display label for the "Default" agent entry? | Resolved | "Default" — consistent with the current placeholder text shown in the Context Sheet screenshot. |
| 3 | If `GetPrimaryAgentsAsync()` returns an empty list (no primary agents configured on the server), should the Agent row still be tappable? | Resolved | Yes — the picker opens and shows only the "Default" entry. The user can always reset to Default. |
| 4 | Should `ChatViewModel.SelectedAgentName` be shown anywhere in the existing UI (e.g., context status bar)? | Resolved | Not in this spec. The context status bar currently shows model name only (per `chat-page-redesign` spec REQ-019/020). Agent display in the status bar is a future enhancement. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the server has agents with `Mode="primary"`, `Mode="subagent"`, and `Mode="all"`, when `GetPrimaryAgentsAsync()` is called, then only agents with `Mode="primary"` or `Mode="all"` are returned. *(REQ-001)*
- [ ] **[AC-002]** Given the Context Sheet is open, when the Agent row is tapped, then `AgentPickerSheet` opens showing a "Default" entry first, followed by all primary agents, with the currently selected agent checked. *(REQ-002, REQ-003)*
- [ ] **[AC-003]** Given `AgentPickerSheet` is open, when the user selects an agent (or "Default"), then the sheet dismisses, `ContextSheetViewModel.SelectedAgentName` is updated, the preference is persisted, and a `ProjectPreferenceChangedMessage` is published. *(REQ-004, REQ-005)*
- [ ] **[AC-004]** Given the Context Sheet is open, when the Model row is tapped, then `ModelPickerSheet` opens (existing behaviour); on selection, `ContextSheetViewModel.SelectedModelId` is updated, the preference is persisted, and a `ProjectPreferenceChangedMessage` is published. *(REQ-004, REQ-006)*
- [ ] **[AC-005]** Given `ChatViewModel` is active and a `ProjectPreferenceChangedMessage` arrives with a new `AgentName`, then `ChatViewModel.SelectedAgentName` and `SelectedAgentDisplayName` are updated without a page reload. *(REQ-007)*
- [ ] **[AC-006]** Given the Context Sheet is open, when viewing the Project row, then the current project name is displayed and the row is not tappable / does not trigger any navigation. *(REQ-008)*
- [ ] **[AC-007]** Given a fresh project with no stored preference, when the Context Sheet opens, then the Agent row shows "Default" and the Model row shows "No model". *(REQ-003, REQ-007)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate

1. **`AgentPickerViewModel` callback wiring**: The existing `AgentPickerSheet` uses `OnAppearing` to load agents via `IAgentService.GetAgentsAsync()`. After this spec, it must call `GetPrimaryAgentsAsync()` instead. Verify whether `AgentPickerSheet` is currently used anywhere else in the app (e.g., from `ChatViewModel.ShowMoreMenuAsync`) — if so, the existing call sites must be updated or a mode parameter introduced to switch between full and primary-only lists.

2. **"Default" entry construction**: The "Default" entry should be a synthetic `AgentDto`-like display model (or a simple wrapper) with `Name = null` and `DisplayName = "Default"`. Do not add a fake `AgentDto` with `Name = "Default"` to avoid confusion with a real agent named "Default". Consider a dedicated `AgentPickerItem` display model with `IsDefault` flag.

3. **`MauiPopupService.ShowAgentPickerAsync` vs existing agent picker usage**: Check `ChatViewModel.ShowMoreMenuAsync` — the "Change agent" option currently has a comment `// Signal intent — the View layer handles the AgentPickerSheet popup`. Verify if the MAUI layer (`ChatPage.xaml.cs`) has any handler for this. If so, consolidate to use `ShowAgentPickerAsync` from `IAppPopupService`.

4. **`ContextSheetViewModel` callback threading**: The `OnAgentSelected` and `OnModelSelected` callbacks are invoked from the MAUI UI thread (inside `SelectAgentCommand` on the picker ViewModel). The subsequent `SetAgentAsync` / `SetDefaultModelAsync` calls are async. Use `_ = Task.Run(async () => { ... })` or simply `await` directly since the callback is already on the UI thread and the ViewModel's `[AsyncRelayCommand]` handles the async context.

### Constraints to Respect

- **`GetPrimaryAgentsAsync` must not mutate the existing `GetAgentsAsync` behaviour** — other consumers (e.g., future subagent picker) rely on the unfiltered list.
- **Callback pattern for pickers**: Follow the `ShowModelPickerAsync(Action<string> onModelSelected)` pattern exactly. Do not use `Task<string?>` return for full-page sheets — this would require blocking the caller until the sheet is dismissed, which conflicts with MAUI's modal navigation model.
- **Layer separation**: `AgentPickerItem` display model (if introduced) must live in `openMob.Core`. The MAUI `AgentPickerSheet` binds to it via `x:DataType`.
- **No duplicate registrations**: Verify `AgentPickerViewModel` and `AgentPickerSheet` DI registrations before adding new ones.

### Related Files and Modules

| File | Relevance |
|------|-----------|
| `src/openMob.Core/Services/IAgentService.cs` | Add `GetPrimaryAgentsAsync` |
| `src/openMob.Core/Services/AgentService.cs` | Implement filter |
| `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` | Add callback + Default entry |
| `src/openMob/Views/Popups/AgentPickerSheet.xaml` | Verify binding, no XAML changes expected |
| `src/openMob.Core/Services/IAppPopupService.cs` | Add `ShowAgentPickerAsync` |
| `src/openMob/Services/MauiPopupService.cs` | Implement `ShowAgentPickerAsync` |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Add commands and callbacks (from core spec) |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Add agent properties, update `LoadContextAsync` |

### References to Past Decisions

- As established in **app-navigation-structure** (2026-03-15): `AgentPickerSheet`, `ModelPickerSheet`, and `ProjectSwitcherSheet` exist as modal pages. `ModelPickerSheet` uses the `Action<string>` callback pattern — this spec replicates it for `AgentPickerSheet`.
- As established in **chat-page-redesign** (2026-03-18): REQ-026 specifies that the Agent row opens `AgentPickerSheet` (existing) and the Model row opens `ModelPickerSheet` (existing). This spec implements that wiring.
- As established in **session-context-sheet-1of3-core** (2026-03-19): `ProjectPreferenceChangedMessage` is the propagation mechanism. `ContextSheetViewModel.InitializeAsync` loads all preferences including `AgentName` and `DefaultModelId`.
