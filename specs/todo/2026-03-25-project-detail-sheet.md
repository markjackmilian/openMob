# Project Detail Sheet

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-25                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

A new bottom sheet modal (full-screen) accessible from the flyout drawer header allows the user to inspect all available information about the currently active project. The sheet aggregates data from multiple server endpoints (`/project`, `/vcs`, `/path`, `/config`) and from the local SQLite `ProjectPreference` store. It also allows the user to override the default AI model for the project (stored locally only), without modifying the global server configuration.

---

## Scope

### In Scope
- Addition of a second icon button in `FlyoutHeaderView` (next to the existing "New Chat" button), visible only when a project is active, that opens the Project Detail Sheet.
- New `ProjectDetailSheet` bottom sheet (full-screen modal) in the MAUI project.
- New `ProjectDetailViewModel` in `openMob.Core` that aggregates data from: `IProjectService`, `IOpencodeApiClient` (VCS, Path, Config endpoints), and `IProjectPreferenceService`.
- Read-only display of: project name, worktree path, VCS type, active git branch, working directory, config path, creation date, initialization date.
- Display of the effective default model for the project: local override (`ProjectPreference.DefaultModelId`) takes precedence over the server global (`ConfigDto.Model`).
- "Change Model" action that opens the existing `ModelPickerSheet` via `IAppPopupService.ShowModelPickerAsync`; on selection, persists via `ProjectPreferenceService.SetDefaultModelAsync` and updates the UI.
- "Reset to server default" action (visible only when a local override is active) that calls `ProjectPreferenceService.ClearDefaultModelAsync` and updates the UI.
- Loading indicator (`IsLoading`) during the initial data fetch.
- Graceful degradation: if any individual endpoint fails, the corresponding fields show "—" and the rest of the sheet renders normally.
- Registration of `ProjectDetailViewModel` in the DI container (`CoreServiceExtensions`).
- Addition of `ShowProjectDetailAsync(string projectId)` to `IAppPopupService` and its MAUI implementation.

### Out of Scope
- Writing any data to the server via `PUT /config` (model change is local-only).
- Modifying the worktree path or renaming the project.
- VCS operations (checkout branch, commit, push, pull).
- Deleting the project from this sheet.
- Managing `ThinkingLevel`, `AutoAccept`, or `AgentName` preferences (already handled in the Context Sheet).
- Displaying sessions count or session history.
- Showing the project detail for a project that is not the currently active one.

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The `FlyoutHeaderView` XAML must include a second `ImageButton` rendered in `Grid.Column="2"` (or equivalent layout), using a TablerIcons glyph appropriate for "project info / settings" (icon TBD — open question OQ-001). The button is bound to a new `OpenProjectDetailCommand` on `FlyoutViewModel` and is only visible when `HasProject == true`.

2. **[REQ-002]** `FlyoutViewModel` must expose an `[AsyncRelayCommand]` named `OpenProjectDetailCommand` that calls `IAppPopupService.ShowProjectDetailAsync(activeProjectId, ct)`. The command must be guarded: it must not execute if `HasProject == false` or if `ActiveProjectId` is null/empty.

3. **[REQ-003]** `IAppPopupService` must be extended with a new method:
   ```csharp
   Task ShowProjectDetailAsync(string projectId, CancellationToken ct = default);
   ```
   The MAUI implementation resolves `ProjectDetailViewModel` from DI, calls `InitializeAsync(projectId, ct)`, then pushes `ProjectDetailSheet` modally via the UXDivers popup stack.

4. **[REQ-004]** `ProjectDetailViewModel` must expose a public `Task InitializeAsync(string projectId, CancellationToken ct)` method that triggers all data fetches in parallel using `Task.WhenAll`. The following calls must be made concurrently:
   - `IProjectService.GetProjectByIdAsync(projectId, ct)` → project metadata
   - `IOpencodeApiClient.GetVcsInfoAsync(ct)` → branch name
   - `IOpencodeApiClient.GetPathAsync(ct)` → working directory and config path
   - `IOpencodeApiClient.GetConfigAsync(ct)` → global default model
   - `IProjectPreferenceService.GetOrDefaultAsync(projectId, ct)` → local model override

5. **[REQ-005]** `ProjectDetailViewModel` must expose the following read-only observable properties (all `string?`, displayed as "—" when null or on fetch failure):
   - `ProjectName` — derived from `ProjectDto.Worktree` via `ProjectNameHelper.ExtractFromWorktree`
   - `WorktreePath` — `ProjectDto.Worktree` (full path)
   - `VcsType` — `ProjectDto.Vcs` (e.g. `"git"`)
   - `GitBranch` — `VcsInfoDto.Branch`
   - `WorkingDirectory` — `PathDto.Directory`
   - `ConfigPath` — `PathDto.Config`
   - `CreatedAt` — `ProjectDto.Time.Created` formatted as a human-readable local date/time string
   - `InitializedAt` — `ProjectDto.Time.Initialized` formatted as above, or `"—"` if null

6. **[REQ-006]** `ProjectDetailViewModel` must expose the following observable properties for the model section:
   - `EffectiveModelId` (`string?`) — `ProjectPreference.DefaultModelId` if set, otherwise `ConfigDto.Model`
   - `IsModelOverridden` (`bool`) — `true` when `ProjectPreference.DefaultModelId` is not null/empty
   - `ModelSourceLabel` (`string`) — `"Project override"` when `IsModelOverridden == true`, `"Server default"` otherwise

7. **[REQ-007]** `ProjectDetailViewModel` must expose a `[AsyncRelayCommand]` named `ChangeModelCommand` that calls `IAppPopupService.ShowModelPickerAsync` with a callback. The callback must:
   a. Call `IProjectPreferenceService.SetDefaultModelAsync(projectId, selectedModelId, ct)`.
   b. Update `EffectiveModelId`, `IsModelOverridden`, and `ModelSourceLabel` on the UI thread.

8. **[REQ-008]** `ProjectDetailViewModel` must expose a `[AsyncRelayCommand]` named `ResetModelCommand` that:
   a. Calls `IProjectPreferenceService.ClearDefaultModelAsync(projectId, ct)`.
   b. Sets `EffectiveModelId` to the server global model (`ConfigDto.Model`).
   c. Sets `IsModelOverridden = false` and updates `ModelSourceLabel`.
   The command must only be executable when `IsModelOverridden == true`.

9. **[REQ-009]** `ProjectDetailViewModel` must expose `IsLoading` (`bool`) set to `true` for the entire duration of `InitializeAsync` and `false` on completion (success or failure). The sheet UI must show a loading indicator while `IsLoading == true`.

10. **[REQ-010]** If any individual API call inside `InitializeAsync` throws or returns a failure result, the corresponding properties must be set to `null` (rendered as "—" in the UI). The failure of one endpoint must not prevent the display of data from other endpoints. All exceptions must be captured via `SentryHelper.CaptureException`.

11. **[REQ-011]** The `ProjectDetailSheet` must include a "Close" button (top-right) that calls `IAppPopupService.PopPopupAsync`. The sheet must also support swipe-down dismissal (standard UXDivers bottom sheet behaviour).

12. **[REQ-012]** `ProjectDetailViewModel` must be registered in `CoreServiceExtensions.AddOpenMobCore()` as **Transient** (consistent with other sheet ViewModels in the project).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `FlyoutHeaderView.xaml` | Modified — add second `ImageButton` | Adjust `ColumnDefinitions` to accommodate the new button |
| `FlyoutViewModel.cs` | Modified — add `OpenProjectDetailCommand` and `ActiveProjectId` property | `ActiveProjectId` must be tracked (currently only `HasProject` bool is exposed) |
| `IAppPopupService.cs` | Modified — add `ShowProjectDetailAsync` method | Interface in `openMob.Core` |
| `MauiPopupService.cs` (MAUI impl) | Modified — implement `ShowProjectDetailAsync` | Resolves `ProjectDetailViewModel` from DI, calls `InitializeAsync`, pushes sheet |
| `ProjectDetailViewModel.cs` | New — `openMob.Core/ViewModels/` | Transient, aggregates 5 data sources |
| `ProjectDetailSheet.xaml` + `.xaml.cs` | New — `openMob/Views/Sheets/` | Full-screen bottom sheet, `x:DataType="ProjectDetailViewModel"` |
| `CoreServiceExtensions.cs` | Modified — register `ProjectDetailViewModel` as Transient | |

### Dependencies
- `IProjectService` — already exists, no changes needed.
- `IOpencodeApiClient` — already exposes `GetVcsInfoAsync`, `GetPathAsync`, `GetConfigAsync`; no changes needed.
- `IProjectPreferenceService` — already exposes `GetOrDefaultAsync`, `SetDefaultModelAsync`, `ClearDefaultModelAsync`; no changes needed.
- `IAppPopupService.ShowModelPickerAsync` — already exists; reused as-is via callback pattern.
- `ProjectNameHelper.ExtractFromWorktree` — already exists in `openMob.Core.Helpers`.
- UXDivers popup stack — existing pattern used by all other sheets in the project.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Which TablerIcons glyph to use for the "project detail" button in the flyout header? Candidates: `IconKeys.InfoCircle`, `IconKeys.FolderOpen`, `IconKeys.Settings`. | Open | — |
| 2 | `GET /vcs` returns the branch for the **currently active project on the server**, not for an arbitrary project ID. If the project shown in the sheet is not the server-active project, the branch displayed may be incorrect. Should the branch field be shown only when the viewed project matches the server-active project, or always shown with a disclaimer? | Open | — |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a project is active in the flyout drawer, when the user opens the drawer, then a second icon button is visible next to the "New Chat" button. *(REQ-001)*
- [ ] **[AC-002]** Given no project is active, when the user opens the drawer, then the project detail button is hidden or disabled. *(REQ-001, REQ-002)*
- [ ] **[AC-003]** Given the project detail button is tapped, when the command executes, then a full-screen bottom sheet modal opens. *(REQ-002, REQ-003)*
- [ ] **[AC-004]** Given the sheet is opening, when data is being fetched, then a loading indicator is visible and no data fields are shown yet. *(REQ-009)*
- [ ] **[AC-005]** Given the sheet has loaded successfully, when the user views it, then all of the following are displayed: project name, worktree path, VCS type, git branch, working directory, config path, creation date. *(REQ-005)*
- [ ] **[AC-006]** Given `ProjectDto.Time.Initialized` is null, when the sheet loads, then the "Initialized" field shows "—". *(REQ-005)*
- [ ] **[AC-007]** Given `ProjectPreference.DefaultModelId` is set for the project, when the sheet loads, then `EffectiveModelId` shows the local override and `ModelSourceLabel` reads "Project override". *(REQ-006)*
- [ ] **[AC-008]** Given no local model override exists, when the sheet loads, then `EffectiveModelId` shows `ConfigDto.Model` and `ModelSourceLabel` reads "Server default". *(REQ-006)*
- [ ] **[AC-009]** Given the "Change Model" button is tapped, when the user selects a model in the picker, then `SetDefaultModelAsync` is called, `IsModelOverridden` becomes `true`, and the UI updates without closing the sheet. *(REQ-007)*
- [ ] **[AC-010]** Given `IsModelOverridden == true`, when the user taps "Reset to server default", then `ClearDefaultModelAsync` is called, `IsModelOverridden` becomes `false`, and `EffectiveModelId` reverts to the server global model. *(REQ-008)*
- [ ] **[AC-011]** Given `IsModelOverridden == false`, when the sheet is displayed, then the "Reset to server default" button is not visible. *(REQ-008)*
- [ ] **[AC-012]** Given `GET /vcs` returns an error, when the sheet loads, then the "Branch" field shows "—" and all other fields load normally. *(REQ-010)*
- [ ] **[AC-013]** Given the sheet is open, when the user taps the "Close" button or swipes down, then the sheet is dismissed. *(REQ-011)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- **`FlyoutViewModel` — `ActiveProjectId` tracking:** Currently `FlyoutViewModel` tracks `HasProject` (bool) and `ProjectSectionTitle` (string) but does not expose the raw project ID. `OpenProjectDetailCommand` needs the project ID to pass to `ShowProjectDetailAsync`. Either add a private `_activeProjectId` field populated during `LoadSessionsAsync` (from `_activeProjectService.GetActiveProjectAsync`), or expose it as an `[ObservableProperty]`. The latter is preferred for testability.
- **`FlyoutHeaderView.xaml` layout:** The current `Grid` has `ColumnDefinitions="*,Auto"`. Adding a third button requires either a third column (`*,Auto,Auto`) or grouping the two buttons in a horizontal `StackLayout` in `Grid.Column="1"`.
- **`InitializeAsync` vs `[RelayCommand]`:** The sheet ViewModel uses an explicit `InitializeAsync` method (called by `MauiPopupService` before pushing the sheet) rather than a `[RelayCommand]`, consistent with `ContextSheetViewModel` which also uses this pattern. Verify `ContextSheetViewModel` for the exact pattern to replicate.
- **`ModelPickerViewModel.OnModelSelected` callback pattern:** The existing `ShowModelPickerAsync` in `IAppPopupService` uses `Action<string>` callback. `ProjectDetailViewModel.ChangeModelCommand` must supply a lambda that captures `projectId` and calls `SetDefaultModelAsync` then updates observable properties. Since the callback is `Action<string>` (not async), the implementation must fire-and-forget the async work safely (use `Task.Run` or a dedicated private method with internal catch).
- **Parallel fetch with partial failure:** Use `Task.WhenAll` with individual `try/catch` per task (or `ContinueWith`) so that one failing endpoint does not cancel the others. Do not use a single outer `try/catch` around `Task.WhenAll` as that would mask partial results.
- **Date formatting:** `ProjectDto.Time.Created` and `Time.Initialized` are Unix timestamps in milliseconds. Use `DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("g")` (short date + short time, locale-aware) for display. Consider a dedicated converter or a helper method on the ViewModel.
- **OQ-001 (icon):** Pending user decision. Suggested default: `IconKeys.InfoCircle`. Implement with a `const` or resource so it can be changed without touching logic.
- **OQ-002 (branch scope):** Pending user decision. Suggested safe default: always show the branch returned by `GET /vcs` with no disclaimer (the server is single-project-context aware). If the user switches projects, the sheet would be re-opened for the new active project anyway.

### Suggested implementation approach
1. Add `ActiveProjectId` (`string?`) as `[ObservableProperty]` to `FlyoutViewModel`; populate it alongside `HasProject` in `LoadSessionsAsync`.
2. Create `ProjectDetailViewModel` in `openMob.Core/ViewModels/ProjectDetailViewModel.cs` — Transient, constructor-injected with `IProjectService`, `IOpencodeApiClient`, `IProjectPreferenceService`, `IAppPopupService`, `IDispatcherService`.
3. Create `ProjectDetailSheet.xaml` in `openMob/Views/Sheets/` following the same structure as `ContextSheet.xaml` (full-screen, scrollable content, close button).
4. Add `ShowProjectDetailAsync` to `IAppPopupService` and implement in `MauiPopupService`.
5. Add `OpenProjectDetailCommand` to `FlyoutViewModel` and the second button to `FlyoutHeaderView.xaml`.
6. Register `ProjectDetailViewModel` as Transient in `CoreServiceExtensions.AddOpenMobCore()`.

### Constraints to respect
- `openMob.Core` must have zero MAUI dependencies — `ProjectDetailViewModel` must not reference any MAUI types.
- All UI thread mutations must go through `IDispatcherService.Dispatch(...)`.
- `IOpencodeApiClient` is injected directly into the ViewModel (not via a service wrapper) for the VCS, Path, and Config calls, consistent with how other ViewModels access lower-level endpoints when no dedicated service wrapper exists.
- `async void` is forbidden; the `OnModelSelected` callback must handle async work via a fire-and-forget pattern with internal exception handling.
- All public and internal members must have `/// <summary>` XML documentation.

### Related files or modules
- `src/openMob.Core/ViewModels/FlyoutViewModel.cs` — modified
- `src/openMob/Views/Controls/FlyoutHeaderView.xaml` — modified
- `src/openMob.Core/Services/IAppPopupService.cs` — modified
- `src/openMob/Services/MauiPopupService.cs` — modified (MAUI impl)
- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — reference for `InitializeAsync` pattern
- `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` — reference for `OnModelSelected` callback pattern
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — `GetVcsInfoAsync`, `GetPathAsync`, `GetConfigAsync`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/VcsInfoDto.cs` — `Branch`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/PathDto.cs` — `Directory`, `Config`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ConfigDto.cs` — `Model`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/ProjectDto.cs` — `Worktree`, `Vcs`, `VcsDir`, `Time`
- `src/openMob.Core/Services/IProjectPreferenceService.cs` — `GetOrDefaultAsync`, `SetDefaultModelAsync`, `ClearDefaultModelAsync`
- `src/openMob.Core/Helpers/ProjectNameHelper.cs` — `ExtractFromWorktree`
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — DI registration
