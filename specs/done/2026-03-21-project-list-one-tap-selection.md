# Project List — One-Tap Active Project Selection

## Metadata
| Field       | Value                                                |
|-------------|------------------------------------------------------|
| Date        | 2026-03-21                                           |
| Status      | **Completed**                                        |
| Version     | 1.0                                                  |
| Completed   | 2026-03-22                                           |
| Branch      | feature/project-list-one-tap-selection (merged)       |
| Merged into | develop                                              |

---

## Executive Summary

The current project selection flow requires three steps: open the project list, tap a project to navigate to its detail page, then tap an explicit "Set as active project" button. This feature simplifies the flow to a single tap: tapping a project in the list immediately sets it as the active project. A visual badge on the list item identifies the currently active project. The project detail page is removed entirely. The drawer and the chat page react automatically to the project change via messaging.

---

## Scope

### In Scope
- Tap on a project item in the project list → immediately sets it as the active project via `IActiveProjectService.SetActiveProjectAsync`
- Tap on the already-active project → no action
- Visual badge on the active project item in the project list
- Removal of the "Set as active project" button from the project detail page
- Removal of the tap-to-detail navigation from the project list
- Complete deletion of the project detail page (View + ViewModel + Shell route)
- Drawer auto-refresh after project change (header + session list)
- ChatPage auto-navigation after project change: navigate to the most recent session of the new active project, or to "New Session" if none exist
- Publication of `ActiveProjectChangedMessage` via `WeakReferenceMessenger` on project change (create if not already present)

### Out of Scope
- Any modification to the drawer layout or session list behaviour beyond reacting to the project change
- Project creation or deletion
- Any other functionality previously exposed by the project detail page
- Badge visibility outside the project list (drawer, chat header, etc.)
- Animations or transitions on badge appearance

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Tapping a project item in the project list that is not currently active calls `IActiveProjectService.SetActiveProjectAsync(projectId)` and sets it as the active project.
2. **[REQ-002]** Tapping the already-active project item performs no action (command guard: if `projectId == ActiveProjectId`, return early).
3. **[REQ-003]** Each project item in the list displays a visual badge (e.g. checkmark or accent indicator) when it is the active project. The badge is hidden for all other items.
4. **[REQ-004]** The badge is rendered exclusively within the project list. No other screen or component displays this badge.
5. **[REQ-005]** The tap gesture on a project list item is intercepted by `SelectProjectCommand`. Navigation to the project detail page is removed.
6. **[REQ-006]** The project detail page (XAML View, code-behind, ViewModel, and Shell route registration) is deleted from the codebase.
7. **[REQ-007]** After `SetActiveProjectAsync` completes, `ProjectsListViewModel` publishes `ActiveProjectChangedMessage(string ProjectId)` via `WeakReferenceMessenger.Default`. If this message type already exists in the codebase, it is reused; otherwise it is created as a `sealed record` in `openMob.Core/Messages/`.
8. **[REQ-008]** `FlyoutViewModel` subscribes to `ActiveProjectChangedMessage` and reloads its session list and project header in response. If `FlyoutViewModel` already handles active project changes via another mechanism, that mechanism is verified to cover this new trigger.
9. **[REQ-009]** `ChatViewModel` subscribes to `ActiveProjectChangedMessage`. On receipt, it loads the most recent session (ordered by last updated/created date descending) belonging to the new active project and navigates to it. If the new active project has no sessions, it navigates to the "New Session" page for that project.
10. **[REQ-010]** `ProjectsListViewModel` exposes an `ActiveProjectId` (`string?`) observable property, populated on load and updated after each successful `SelectProjectCommand` execution, used for badge binding in the XAML template.
11. **[REQ-011]** `ProjectsListViewModel` loads the current active project id on initialisation (via `IActiveProjectService`) to correctly render the badge on first open.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ProjectsViewModel` | Modified | Add `SelectProjectCommand` one-tap logic, `ActiveProjectId` property, remove detail navigation |
| `ProjectsPage.xaml` | Modified | Bind tap to `SelectProjectCommand`, add badge to item template, remove navigation gesture |
| `ProjectDetailViewModel` | Deleted | Entire file removed |
| `ProjectDetailPage.xaml` + `.xaml.cs` | Deleted | Entire files removed |
| `ProjectDetailViewModelTests.cs` | Deleted | Tests for deleted ViewModel |
| Shell route registration | Modified | Remove route for project detail page |
| `FlyoutViewModel` | Verified | Already subscribes to `ActiveProjectChangedMessage` — no changes needed |
| `ChatViewModel` | Modified | Subscribe to `ActiveProjectChangedMessage`, implement session redirect logic |
| `ActiveProjectChangedMessage` | Already exists | `sealed record ActiveProjectChangedMessage(ProjectDto Project)` in `openMob.Core/Messages/` |
| `IActiveProjectService` | No change | `SetActiveProjectAsync` already exists and publishes `ActiveProjectChangedMessage` |

### Dependencies
- `IActiveProjectService.SetActiveProjectAsync` — already implemented; single entry point for all project changes (established in `last-active-project-restore`)
- `WeakReferenceMessenger` — established cross-ViewModel communication pattern (established in `drawer-sessions-delete-refactor` and `session-context-sheet-1of3-core`)
- `FlyoutViewModel` — already Singleton, already reacts to `ActiveProjectChangedMessage` (verified in codebase)
- `ISessionService` — needed by `ChatViewModel` to query the most recent session for the new active project

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Does `ActiveProjectChangedMessage` already exist in the codebase? | Resolved | Yes. `sealed record ActiveProjectChangedMessage(ProjectDto Project)` exists in `src/openMob.Core/Messages/ActiveProjectChangedMessage.cs`. It carries a `ProjectDto` (not just a string ID). |
| 2 | Does `FlyoutViewModel` already subscribe to an active-project-change event? | Resolved | Yes. `FlyoutViewModel` already subscribes to `ActiveProjectChangedMessage` in its constructor and reloads sessions in response. No additional subscription needed. |
| 3 | What is the exact route/page name for the project detail page to be deleted? | Resolved | Route: `"project-detail"` registered in `AppShell.xaml.cs` as `Routing.RegisterRoute("project-detail", typeof(ProjectDetailPage))`. Files: `ProjectDetailPage.xaml`, `ProjectDetailPage.xaml.cs`, `ProjectDetailViewModel.cs`, `ProjectDetailViewModelTests.cs`. Also registered as Transient in `MauiProgram.cs`: `builder.Services.AddTransient<ProjectDetailPage>()`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the project list is open and project A is active, when the user taps project B, then project B becomes the active project and its badge appears; project A's badge disappears. *(REQ-001, REQ-003)*
- [ ] **[AC-002]** Given project A is active, when the user taps project A in the list, then no state change occurs and no navigation happens. *(REQ-002)*
- [ ] **[AC-003]** Given the project list is open, when it loads, then the currently active project already shows the badge without requiring any tap. *(REQ-010, REQ-011)*
- [ ] **[AC-004]** Given the badge is visible in the project list, when the user navigates to any other screen, then no badge is shown elsewhere. *(REQ-004)*
- [ ] **[AC-005]** Given a new active project is selected and the drawer is opened, then the drawer header shows the new project name and the session list shows only that project's sessions. *(REQ-008)*
- [ ] **[AC-006]** Given a new active project is selected and the new project has at least one session, then ChatPage navigates to the most recent session of that project. *(REQ-009)*
- [ ] **[AC-007]** Given a new active project is selected and the new project has no sessions, then the app navigates to the "New Session" page for that project. *(REQ-009)*
- [ ] **[AC-008]** Given the project detail page previously existed, when the user is on the project list, then no navigation path to the project detail page exists. *(REQ-005, REQ-006)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **`IActiveProjectService.SetActiveProjectAsync`** is the established single entry point for all active project changes. It already handles SQLite persistence via `AppStateService`. Do not bypass it. (Established in `last-active-project-restore`.)
- **`ActiveProjectChangedMessage`**: verified — exists in `openMob.Core/Messages/` as `sealed record ActiveProjectChangedMessage(ProjectDto Project)`. Carries a full `ProjectDto`, not just a string ID.
- **`FlyoutViewModel`** is a Singleton registered in `CoreServiceExtensions`. It already subscribes to `ActiveProjectChangedMessage` and reloads sessions. No changes needed for REQ-008.
- **`ChatViewModel`** session redirect: use `ISessionService` to query sessions for the new project id, order by most recent, take first. If result is empty, navigate to new session. Ensure navigation happens on the UI thread (use `IDispatcherService` if needed, consistent with `FlyoutViewModel` pattern).
- **Project detail page deletion**: `ProjectDetailPage.xaml`, `ProjectDetailPage.xaml.cs` in `src/openMob/Views/Pages/`, `ProjectDetailViewModel.cs` in `src/openMob.Core/ViewModels/`, `ProjectDetailViewModelTests.cs` in `tests/openMob.Tests/ViewModels/`. Route `"project-detail"` in `AppShell.xaml.cs`. Transient registration in `MauiProgram.cs`.
- **`ProjectsViewModel.SelectProjectCommand`**: currently navigates to `"project-detail"`. Must be changed to: guard with `if (projectId == ActiveProjectId) return;`, call `SetActiveProjectAsync`, update `ActiveProjectId` locally, update `IsActive` on `ProjectItem` instances. No need to publish `ActiveProjectChangedMessage` manually — `ActiveProjectService.SetActiveProjectAsync` already publishes it.
- **Badge binding**: `ProjectsViewModel` already has `ActiveProjectId` as `[ObservableProperty]`. The `ProjectItem` record already has `IsActive` property. After `SetActiveProjectAsync`, rebuild the `Projects` collection or update `IsActive` on each item.
- **`ChatViewModel` subscription to `ActiveProjectChangedMessage`**: `ChatViewModel` already implements `IDisposable` and unregisters messages. Add subscription in constructor, unregister in `Dispose`. On receipt, use `ISessionService.GetLastSessionForProjectAsync` (already exists in interface) to get the most recent session, then navigate.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-22

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/project-list-one-tap-selection |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ProjectsViewModel.cs`, `ChatViewModel.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ProjectsPage.xaml` |
| DI / Routing | om-mobile-core | `src/openMob/MauiProgram.cs`, `src/openMob/AppShell.xaml.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — this feature only modifies and deletes existing files.

### Files to Modify

- `src/openMob.Core/ViewModels/ProjectsViewModel.cs` — Replace `SelectProjectAsync` navigation-to-detail with one-tap set-active logic; add guard for already-active project; update `ActiveProjectId` and `Projects` collection after selection
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — Subscribe to `ActiveProjectChangedMessage`; on receipt, load most recent session for new project via `ISessionService.GetLastSessionForProjectAsync` and navigate to it (or new session if none)
- `src/openMob/Views/Pages/ProjectsPage.xaml` — Update item template to show active badge (checkmark/accent indicator) bound to `IsActive` on `ProjectItem`; remove any navigation-to-detail gesture
- `src/openMob/AppShell.xaml.cs` — Remove `Routing.RegisterRoute("project-detail", typeof(ProjectDetailPage))`
- `src/openMob/MauiProgram.cs` — Remove `builder.Services.AddTransient<ProjectDetailPage>()` registration

### Files to Delete

- `src/openMob/Views/Pages/ProjectDetailPage.xaml` — Project detail page XAML
- `src/openMob/Views/Pages/ProjectDetailPage.xaml.cs` — Project detail page code-behind
- `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` — Project detail ViewModel
- `tests/openMob.Tests/ViewModels/ProjectDetailViewModelTests.cs` — Tests for deleted ViewModel

### Technical Dependencies

- `IActiveProjectService.SetActiveProjectAsync(projectId)` — already exists, already publishes `ActiveProjectChangedMessage`
- `ActiveProjectChangedMessage(ProjectDto Project)` — already exists in `openMob.Core/Messages/`
- `FlyoutViewModel` — already subscribes to `ActiveProjectChangedMessage` — no changes needed
- `ISessionService.GetLastSessionForProjectAsync` — already exists in interface, needed by `ChatViewModel` for session redirect
- `INavigationService` — already injected in `ChatViewModel`, needed for navigation after project change
- `IDispatcherService` — already injected in `ChatViewModel`, needed for UI-thread navigation

### Technical Risks

- **ProjectDetailPage deletion**: Must verify no other code references `ProjectDetailPage` or `ProjectDetailViewModel` before deletion. Search confirmed: only `AppShell.xaml.cs` route registration and `MauiProgram.cs` DI registration reference these types.
- **ChatViewModel message subscription**: `ChatViewModel` is Transient — each instance subscribes/unsubscribes independently. Must ensure `Dispose` unregisters `ActiveProjectChangedMessage` to avoid leaks.
- **Race condition on project switch**: If user rapidly switches projects, the `ChatViewModel` message handler must handle the case where navigation is already in progress. Use `CancellationToken` from the ViewModel's CTS.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/project-list-one-tap-selection`
2. [om-mobile-core] Modify `ProjectsViewModel.SelectProjectAsync` to one-tap logic; modify `ChatViewModel` to subscribe to `ActiveProjectChangedMessage`; delete `ProjectDetailViewModel.cs`; update `MauiProgram.cs` and `AppShell.xaml.cs`
3. ⟳ [om-mobile-ui] Update `ProjectsPage.xaml` badge binding; delete `ProjectDetailPage.xaml` + `.xaml.cs` (can start once ViewModel changes are defined)
4. [om-tester] Write unit tests for `ProjectsViewModel.SelectProjectCommand` and `ChatViewModel` project-change handler; delete `ProjectDetailViewModelTests.cs`
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-011]` requirements implemented
- [ ] All `[AC-001]` through `[AC-008]` acceptance criteria satisfied
- [ ] Unit tests written for `ProjectsViewModel` (one-tap selection, guard, badge) and `ChatViewModel` (project change handler)
- [ ] `ProjectDetailPage`, `ProjectDetailViewModel`, and associated tests deleted
- [ ] `om-reviewer` verdict: Approved or Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
