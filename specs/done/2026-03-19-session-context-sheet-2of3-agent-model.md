# Session Context Sheet — Agent & Model Selection

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-19                                     |
| Status      | **Completed**                                  |
| Version     | 1.0                                            |
| Completed   | 2026-03-20                                     |
| Branch      | feature/session-context-sheet-agent-model (merged) |
| Merged into | develop                                        |

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

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-20

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/session-context-sheet-agent-model |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Services/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| MAUI service implementation | om-mobile-core | `src/openMob/Services/MauiPopupService.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

> **No om-mobile-ui work required.** The spec explicitly states that `AgentPickerSheet` XAML is reused as-is. The existing `AgentItem` record already has `Name`, `Description`, and `IsSelected` fields. The only XAML-adjacent change is that `AgentPickerSheet` binds to `AgentItem.Name` — the "Default" entry will use `Name = null` which requires a XAML `FallbackValue` already present (`FallbackValue='Agent'`). However, the spec requires the "Default" entry to display "Default" as its name — this means the `AgentItem` record needs a nullable `Name` and the XAML binding must handle null. **Decision**: Extend `AgentItem` to support a nullable `Name` and a `DisplayName` computed property, OR introduce a new `AgentPickerItem` record with `IsDefault` flag. Per spec notes, the latter is preferred. The XAML `DataTemplate` binds to `Name` — if we introduce `AgentPickerItem`, the XAML `x:DataType` must change. Since the spec says "no XAML changes expected", we will keep `AgentItem` but add `IsDefault` flag and change `Name` to nullable, with `DisplayName` computed. The XAML already uses `FallbackValue='Agent'` — we will update the XAML binding to use `DisplayName` instead of `Name`. This is a minimal XAML change (one binding update) that om-mobile-core can handle in the code-behind or om-mobile-ui can handle. **Final decision**: om-mobile-core handles the `AgentItem` model change and the single XAML binding update (from `Name` to `DisplayName`) since it is a 1-line change driven by the model change.

### Codebase Investigation Findings

**REQ-009 — DI registrations already exist:**
- `AgentPickerSheet` is already registered as `Transient` in `MauiProgram.cs` (line 86).
- `AgentPickerViewModel` is already registered as `Transient` in `CoreServiceExtensions.cs` (line 134).
- No new DI registrations needed.

**`AgentPickerViewModel` current state:**
- Already has `SelectedAgentName` as an `[ObservableProperty]` (line 38-39) — this satisfies the "input property" requirement of REQ-003.
- Already has `IsSubagentMode` property — the existing subagent mode uses `GetAgentsAsync()` (unfiltered). The new primary-agent mode (from `ContextSheetViewModel`) must use `GetPrimaryAgentsAsync()`.
- `LoadAgentsAsync` currently calls `_agentService.GetAgentsAsync()` — this must be changed to call `GetPrimaryAgentsAsync()` when NOT in subagent mode. **Decision**: Add a boolean `IsPrimaryMode` property (or reuse `!IsSubagentMode`) to switch between `GetPrimaryAgentsAsync()` and `GetAgentsAsync()`. Since subagent mode is the only other mode, `!IsSubagentMode` maps to primary mode. The `LoadAgentsAsync` method will call `GetPrimaryAgentsAsync()` when `!IsSubagentMode` and `GetAgentsAsync()` when `IsSubagentMode`.
- `SelectAgentAsync` currently uses `ArgumentException.ThrowIfNullOrWhiteSpace(agentName)` — this must be relaxed to allow `null` for the "Default" entry. The method signature must change to accept `string?`.
- The "Default" entry: since `AgentItem` currently has `string Name` (non-nullable), we need to either: (a) change `AgentItem.Name` to `string?` and add `DisplayName`, or (b) introduce a new `AgentPickerItem` record. **Decision**: Modify `AgentItem` to have `string? Name` and add `string DisplayName => Name ?? "Default"`. The XAML binding changes from `{Binding Name}` to `{Binding DisplayName}` — one line. The `IsDefault` flag is implicit: `Name is null`.

**`ContextSheetViewModel` current state:**
- Already has `SelectedAgentName`, `SelectedAgentDisplayName`, `SelectedModelId`, `SelectedModelDisplayName` as observable properties (lines 70-94).
- Already has `OpenAgentPickerAsync` command (lines 147-152) — a stub that does nothing. This must be replaced with a real `SelectAgentCommand` that calls `ShowAgentPickerAsync`.
- Already has `OpenModelPickerAsync` command (lines 159-166) — already wired to `ShowModelPickerAsync`. This must be renamed to `SelectModelCommand` per spec REQ-004, OR the existing command can be kept and the spec's naming is aspirational. **Decision**: The spec says `SelectAgentCommand` and `SelectModelCommand`. The existing commands are `OpenAgentPickerCommand` and `OpenModelPickerCommand`. We will rename them to match the spec. The XAML `ContextSheet.xaml` binds to these commands — the XAML must be updated accordingly. Since om-mobile-ui owns XAML, we need to check if `ContextSheet.xaml` already uses these command names.
- Already has `SaveAgentAsync` and `SaveModelAsync` private helpers that persist and publish the message — these are triggered by `OnSelectedAgentNameChanged` and `OnSelectedModelIdChanged` partial methods. The auto-save pattern is already in place. The spec's REQ-005 and REQ-006 describe the same flow. **No new save logic needed** — the existing auto-save via `OnSelectedAgentNameChanged` already calls `SaveAgentAsync` which calls `SetAgentAsync` and publishes the message.
- **Key insight**: The `OpenAgentPickerAsync` stub just needs to be replaced with a real implementation that calls `ShowAgentPickerAsync` with a callback that sets `SelectedAgentName`. The auto-save partial method will handle persistence automatically.

**`ChatViewModel` current state:**
- The `ProjectPreferenceChangedMessage` handler (lines 93-112) already updates `SelectedModelId` and `SelectedModelName` but does NOT update agent properties — because `SelectedAgentName` doesn't exist yet.
- `LoadContextAsync` (lines 273-329) loads `pref.DefaultModelId` but not `pref.AgentName`.
- Two new properties needed: `SelectedAgentName` (`string?`) and `SelectedAgentDisplayName` (`string`).

**`ContextSheet.xaml` command bindings:**
- Need to verify what command names the XAML currently uses for the Agent and Model rows.

### Files to Create

_(None — all changes are extensions to existing files)_

### Files to Modify

- `src/openMob.Core/Models/AgentItem.cs` — change `Name` to `string?`, add `DisplayName` computed property
- `src/openMob.Core/Services/IAgentService.cs` — add `GetPrimaryAgentsAsync` method signature
- `src/openMob.Core/Services/AgentService.cs` — implement `GetPrimaryAgentsAsync` with LINQ filter
- `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` — add `OnAgentSelected` callback, prepend "Default" entry, switch `LoadAgentsAsync` to use `GetPrimaryAgentsAsync` in non-subagent mode, relax `SelectAgentAsync` to accept `string?`
- `src/openMob.Core/Services/IAppPopupService.cs` — add `ShowAgentPickerAsync` method signature
- `src/openMob/Services/MauiPopupService.cs` — implement `ShowAgentPickerAsync`
- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — replace `OpenAgentPickerAsync` stub with real `SelectAgentCommand` wired to `ShowAgentPickerAsync`; rename `OpenModelPickerCommand` to `SelectModelCommand`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add `SelectedAgentName` and `SelectedAgentDisplayName` properties; update `LoadContextAsync` to load `AgentName`; update `ProjectPreferenceChangedMessage` handler to update agent properties
- `src/openMob/Views/Popups/AgentPickerSheet.xaml` — update `{Binding Name}` to `{Binding DisplayName}` in the agent name label (1-line change)
- `src/openMob/Views/Popups/ContextSheet.xaml` — update command bindings if they reference `OpenAgentPickerCommand` / `OpenModelPickerCommand` (verify first)

### Technical Dependencies

- `session-context-sheet-1of3-core` — **already merged** (confirmed: `ContextSheetViewModel`, `IProjectPreferenceService.SetAgentAsync`, `ProjectPreferenceChangedMessage` all exist in codebase)
- `IAgentService.GetPrimaryAgentsAsync` — new, introduced by this spec
- `IAppPopupService.ShowAgentPickerAsync` — new, introduced by this spec

### Technical Risks

- **`AgentItem.Name` nullability change**: `AgentItem` is a `sealed record` with `string Name`. Changing to `string?` is a breaking change for all existing consumers. The only consumer is `AgentPickerViewModel` which uses `a.Name == SelectedAgentName` for selection comparison — this works fine with nullable strings. The XAML `CommandParameter="{Binding Name}"` passes `null` for the Default entry, which is the desired behaviour.
- **`SelectAgentAsync` parameter type change**: Currently `string agentName` with `ThrowIfNullOrWhiteSpace`. Must change to `string? agentName` and remove the null guard. The XAML `CommandParameter="{Binding Name}"` will pass `null` for the Default entry.
- **Command rename in `ContextSheetViewModel`**: `OpenAgentPickerCommand` → `SelectAgentCommand`, `OpenModelPickerCommand` → `SelectModelCommand`. Any XAML binding to the old names will break. Must verify `ContextSheet.xaml` bindings.
- **No platform-specific concerns** — all changes are in Core or MAUI service layer, no iOS/Android conditionals needed.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/session-context-sheet-agent-model`
2. [om-mobile-core] Implement all Core and MAUI changes (all files listed above)
3. [om-tester] Write unit tests for `AgentService.GetPrimaryAgentsAsync`, `AgentPickerViewModel` callback/Default entry, `ContextSheetViewModel` agent/model commands, `ChatViewModel` agent properties
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

> Note: om-mobile-ui is not required for this spec. The single XAML binding change (`Name` → `DisplayName` in `AgentPickerSheet.xaml`) is handled by om-mobile-core as part of the model change. The `ContextSheet.xaml` command binding verification is also handled by om-mobile-core.

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-009]` requirements implemented
- [ ] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [ ] Unit tests written for all modified Services and ViewModels
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
