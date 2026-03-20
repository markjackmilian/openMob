# Session Context Sheet — Core Infrastructure

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-19                                     |
| Status      | **Completed**                                  |
| Version     | 1.0                                            |
| Completed   | 2026-03-20                                     |
| Branch      | feature/session-context-sheet-core (merged)    |
| Merged into | develop                                        |

---

## Executive Summary

The Session Context Sheet is a bottom sheet modal opened from the chat page header's edit button. It exposes per-project session settings (agent, model, thinking level, auto-accept) that are currently placeholder-only. This spec covers the **core infrastructure layer**: extending the `ProjectPreference` entity and service to store all session-level settings per project, defining the global default values used when no project preference exists, and introducing the `ContextSheetViewModel` with its popup service integration. Subsequent vertical specs build on this foundation for each individual section.

---

## Scope

### In Scope
- Extension of `ProjectPreference` entity with new fields: `AgentName` (string?), `ThinkingLevel` (enum: Low/Medium/High), `AutoAccept` (bool)
- EF Core migration for the new `ProjectPreference` columns
- Extension of `IProjectPreferenceService` with new read/write methods for all preference fields
- Definition of global default values (Agent = "Default", ThinkingLevel = Medium, AutoAccept = false, Model = already handled)
- New `ContextSheetViewModel` in `openMob.Core`: loads preferences for the active project, exposes observable properties for all settings, auto-saves on every change
- New `IAppPopupService.ShowContextSheetAsync(string projectId, string sessionId)` method
- `ContextSheetViewModel` propagates changes to `ChatViewModel` via `WeakReferenceMessenger` (CommunityToolkit.Mvvm messaging)
- New message types: `ProjectPreferenceChangedMessage`
- `ChatViewModel` subscribes to `ProjectPreferenceChangedMessage` and updates its observable state accordingly
- Registration of `ContextSheetViewModel` in `AddOpenMobCore()` DI extension

### Out of Scope
- XAML / UI implementation of the Context Sheet (covered by `om-mobile-ui`)
- Agent selection logic (spec `session-context-sheet-2of3-agent-model`)
- Model selection logic (spec `session-context-sheet-2of3-agent-model`)
- Thinking level segmented control logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Auto-accept toggle logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Invoke Subagent logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Thinking level support detection per model (future spec)
- Server-side persistence of any preference (all storage is local SQLite)

---

## Functional Requirements

1. **[REQ-001]** The `ProjectPreference` entity must be extended with three new nullable/defaulted fields:
   - `AgentName` (`string?`) — name of the selected primary agent; `null` means "Default"
   - `ThinkingLevel` (`ThinkingLevel` enum) — values: `Low`, `Medium`, `High`; default `Medium`
   - `AutoAccept` (`bool`) — default `false`

2. **[REQ-002]** A new EF Core migration must be created to add the three columns to the `ProjectPreferences` table. Existing rows must receive the default values (`AgentName = NULL`, `ThinkingLevel = 1` (Medium), `AutoAccept = 0`).

3. **[REQ-003]** `IProjectPreferenceService` must be extended with the following methods:
   - `Task<ProjectPreference> GetOrDefaultAsync(string projectId, CancellationToken ct)` — returns the stored preference for the project, or a new `ProjectPreference` populated with global defaults if none exists. Never returns `null`.
   - `Task<bool> SetAgentAsync(string projectId, string? agentName, CancellationToken ct)` — upsert `AgentName`
   - `Task<bool> SetThinkingLevelAsync(string projectId, ThinkingLevel level, CancellationToken ct)` — upsert `ThinkingLevel`
   - `Task<bool> SetAutoAcceptAsync(string projectId, bool autoAccept, CancellationToken ct)` — upsert `AutoAccept`
   - `Task<bool> ClearDefaultModelAsync(string projectId, CancellationToken ct)` — sets `DefaultModelId = null`
   - The existing `SetDefaultModelAsync` is preserved unchanged.

4. **[REQ-004]** Global default values (used when no `ProjectPreference` row exists for a project) are:
   - `AgentName`: `null` (displayed as "Default" in the UI)
   - `ThinkingLevel`: `ThinkingLevel.Medium`
   - `AutoAccept`: `false`
   - `DefaultModelId`: `null` (displayed as "No model" in the UI — already existing behaviour)

5. **[REQ-005]** `ContextSheetViewModel` observable properties:
   - `ProjectName` (`string`) — read-only display name of the active project
   - `SelectedAgentName` (`string?`) — current agent name (`null` = Default)
   - `SelectedAgentDisplayName` (`string`) — computed: `SelectedAgentName ?? "Default"`
   - `SelectedModelId` (`string?`) — current model ID
   - `SelectedModelDisplayName` (`string`) — computed: extracted model name or "No model"
   - `ThinkingLevel` (`ThinkingLevel`) — current thinking level
   - `AutoAccept` (`bool`) — current auto-accept state
   - `IsBusy` (`bool`) — true while loading preferences
   - `ErrorMessage` (`string?`) — set on save failure, not blocking

6. **[REQ-006]** `ContextSheetViewModel.InitializeAsync(string projectId, string sessionId)` sets `IsBusy`, loads project name and preferences, populates all properties, clears `IsBusy`.

7. **[REQ-007]** Auto-save on every property change via `partial void On*Changed` pattern. On success: publish `ProjectPreferenceChangedMessage`. On failure: set `ErrorMessage` (no rollback).

8. **[REQ-008]** `ProjectPreferenceChangedMessage` — sealed record in `openMob.Core/Messages/`.

9. **[REQ-009]** `ChatViewModel` subscribes to `ProjectPreferenceChangedMessage` in constructor, unregisters in `Dispose()`.

10. **[REQ-010]** `IAppPopupService.ShowContextSheetAsync(string projectId, string sessionId, CancellationToken ct = default)`.

11. **[REQ-011]** `ContextSheetViewModel` registered as Transient. `ContextSheet` page registered as Transient.

12. **[REQ-012]** `ChatViewModel.OpenContextSheetCommand` passes `CurrentProjectId` and `CurrentSessionId`.

---

## Acceptance Criteria

- [x] **[AC-001]** `GetOrDefaultAsync` returns defaults without throwing on fresh install.
- [x] **[AC-002]** Sheet shows `Default`, `No model`, `Medium`, `false` when no preference exists.
- [x] **[AC-003]** Preference change triggers `Set*Async` and publishes `ProjectPreferenceChangedMessage`.
- [x] **[AC-004]** `ChatViewModel` updates `SelectedModelId`/`SelectedModelName` on message receipt without page reload.
- [x] **[AC-005]** Header edit button opens sheet with preferences pre-loaded.
- [x] **[AC-006]** DB save failure sets `ErrorMessage`, does not roll back UI value.
- [x] **[AC-007]** Migration columns `AgentName` (TEXT NULL), `ThinkingLevel` (INTEGER DEFAULT 1), `AutoAccept` (INTEGER DEFAULT 0) present.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `ChatViewModel` base class change? | Resolved | Use `WeakReferenceMessenger.Default` directly — no base class change. |
| 2 | `ThinkingLevel` stored as int or string? | Resolved | Store as int (EF Core default). Low=0, Medium=1, High=2. |
| 3 | Is `sessionId` needed in `ShowContextSheetAsync`? | Resolved | Keep for future session-level overrides. `ContextSheetViewModel` ignores it for now. |

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-20

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/session-context-sheet-core |
| Branches from | develop |
| Estimated complexity | Medium |
| Agents involved | om-mobile-core, om-tester, om-reviewer |

### Files Created

| File | Description |
|------|-------------|
| `src/openMob.Core/Messages/ProjectPreferenceChangedMessage.cs` | Sealed record for WeakReferenceMessenger messaging |
| `src/openMob.Core/Data/Migrations/20260320000000_AddAgentThinkingAutoAcceptToProjectPreference.cs` | EF Core migration |
| `src/openMob.Core/Data/Migrations/20260320000000_AddAgentThinkingAutoAcceptToProjectPreference.Designer.cs` | Migration designer snapshot |

### Files Modified

| File | Change |
|------|--------|
| `src/openMob.Core/Data/Entities/ProjectPreference.cs` | Added `AgentName`, `ThinkingLevel`, `AutoAccept` |
| `src/openMob.Core/Data/AppDbContext.cs` | Fluent config for 3 new columns |
| `src/openMob.Core/Data/Migrations/AppDbContextModelSnapshot.cs` | Updated snapshot |
| `src/openMob.Core/Services/IProjectPreferenceService.cs` | Added 5 new method signatures |
| `src/openMob.Core/Services/ProjectPreferenceService.cs` | Implemented 5 new methods |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Full rewrite: `InitializeAsync`, auto-save, messaging, commands |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | WeakReferenceMessenger subscription, updated `OpenContextSheetCommand` |
| `src/openMob.Core/Services/IAppPopupService.cs` | Updated `ShowContextSheetAsync` signature |
| `src/openMob/Services/MauiPopupService.cs` | Updated `ShowContextSheetAsync` to call `InitializeAsync` |
| `src/openMob/Views/Popups/ContextSheet.xaml` | Fixed bindings: `AgentName`→`SelectedAgentDisplayName`, `ModelName`→`SelectedModelDisplayName` |
| `src/openMob/Views/Popups/ContextSheet.xaml.cs` | Removed `LoadContextCommand` call from `OnAppearing` |

### Test Files

| File | Action | Tests |
|------|--------|-------|
| `tests/openMob.Tests/ViewModels/ContextSheetViewModelTests.cs` | Full rewrite | 30+ tests |
| `tests/openMob.Tests/Services/ProjectPreferenceServiceTests.cs` | Extended | +28 tests |
| `tests/openMob.Tests/ViewModels/ChatViewModelPreferenceTests.cs` | New file | 8 tests |
| `tests/openMob.Tests/ViewModels/ChatViewModelRedesignTests.cs` | Added IDisposable | — |

**Final test count: 788 passing, 0 failing.**

### Key Implementation Decisions

1. **`GetOrDefaultAsync` purity** — returns a transient `new ProjectPreference { ... }` without inserting a DB row. Avoids polluting the DB with rows for projects the user never customised.

2. **`_isInitializing` guard** — prevents `partial void On*Changed` auto-save methods from firing during `InitializeAsync`. Set to `true` before property assignments, cleared in `finally`.

3. **`ClearDefaultModelAsync`** — added during fix loop (reviewer finding [M-001]). `SaveModelAsync` was silently discarding `null` model selections. A dedicated clear method was added to `IProjectPreferenceService` to handle this case explicitly.

4. **`ContextSheetViewModel` constructor** — takes `IProjectService`, `IProjectPreferenceService`, `IAppPopupService`. The old stub had these; the initial rewrite incorrectly dropped `IAppPopupService`, causing all picker commands to be missing. Restored in fix loop.

5. **WeakReferenceMessenger cleanup** — `WeakReferenceMessenger.Default.UnregisterAll(this)` is the first line of `ChatViewModel.Dispose()` to prevent stale registrations from receiving messages after the ViewModel is disposed.

6. **Fire-and-forget pattern** — `_ = SaveXxxAsync(...)` in `On*Changed` partial methods. `CancellationToken.None` passed explicitly to all service calls inside save helpers to document intent.
