# Project List — One-Tap Active Project Selection

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | Draft                        |
| Version | 1.0                          |

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
| `ProjectsListViewModel` | Modified | Add `SelectProjectCommand`, `ActiveProjectId` property, remove detail navigation |
| `ProjectsListPage.xaml` | Modified | Bind tap to `SelectProjectCommand`, add badge to item template, remove navigation gesture |
| Project detail ViewModel | Deleted | Entire file removed |
| Project detail Page (XAML) | Deleted | Entire file removed |
| Shell route registration | Modified | Remove route for project detail page |
| `FlyoutViewModel` | Modified | Subscribe to `ActiveProjectChangedMessage` (verify or add) |
| `ChatViewModel` | Modified | Subscribe to `ActiveProjectChangedMessage`, implement session redirect logic |
| `ActiveProjectChangedMessage` | Created (if absent) | `sealed record` in `openMob.Core/Messages/` |
| `IActiveProjectService` | No change | `SetActiveProjectAsync` already exists |

### Dependencies
- `IActiveProjectService.SetActiveProjectAsync` — already implemented; single entry point for all project changes (established in `last-active-project-restore`)
- `WeakReferenceMessenger` — established cross-ViewModel communication pattern (established in `drawer-sessions-delete-refactor` and `session-context-sheet-1of3-core`)
- `FlyoutViewModel` — already Singleton, already reacts to session-level messages; must be extended or verified for project-level changes
- `ISessionService` — needed by `ChatViewModel` to query the most recent session for the new active project

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Does `ActiveProjectChangedMessage` already exist in the codebase? | Open | To be verified during technical analysis; create if absent |
| 2 | Does `FlyoutViewModel` already subscribe to an active-project-change event? | Open | To be verified; if yes, confirm it covers this trigger; if no, add subscription |
| 3 | What is the exact route/page name for the project detail page to be deleted? | Open | To be verified in `AppShell.xaml` and routing registration during technical analysis |

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
- **`ActiveProjectChangedMessage`**: verify existence in `openMob.Core/Messages/`. If absent, create as `sealed record ActiveProjectChangedMessage(string ProjectId)`. If present, reuse as-is.
- **`FlyoutViewModel`** is a Singleton registered in `CoreServiceExtensions`. It already subscribes to `SessionDeletedMessage` and `CurrentSessionChangedMessage`. Check whether it already handles an active-project-change signal; if not, add subscription to `ActiveProjectChangedMessage`. Follow the existing `IDisposable` + `WeakReferenceMessenger.Default.UnregisterAll(this)` pattern for cleanup.
- **`ChatViewModel`** session redirect: use `ISessionService` to query sessions for the new project id, order by most recent, take first. If result is empty, navigate to new session. Ensure navigation happens on the UI thread (use `IDispatcherService` if needed, consistent with `FlyoutViewModel` pattern).
- **Project detail page deletion**: locate the page in `AppShell.xaml` (likely a `ShellContent` or `Routing.RegisterRoute` call) and remove both the route registration and the files. Verify no other ViewModel or service holds a reference to the deleted ViewModel.
- **`ProjectsListViewModel.SelectProjectCommand`**: guard with `if (projectId == ActiveProjectId) return;`. After `SetActiveProjectAsync`, update `ActiveProjectId` locally and publish `ActiveProjectChangedMessage`. Use `[AsyncRelayCommand]`.
- **Badge binding**: `ActiveProjectId` on the ViewModel; in the XAML `DataTemplate`, bind badge visibility to a converter comparing `item.Id` with the parent ViewModel's `ActiveProjectId` (e.g. `EqualityToVisibilityConverter` or `BoolToVisibilityConverter` with a multi-binding or a computed property on the item model).
- **Messenger cleanup**: `ProjectsListViewModel` publishes but does not subscribe — no `IDisposable` needed for messaging on this ViewModel unless it subscribes to other messages. `ChatViewModel` already implements `IDisposable`; add `ActiveProjectChangedMessage` unregistration alongside existing cleanup.
- **Related files to investigate**: `AppShell.xaml`, `AppShell.xaml.cs`, `CoreServiceExtensions.cs`, `FlyoutViewModel.cs`, `ChatViewModel.cs`, `ProjectsListViewModel.cs` (or equivalent), `IActiveProjectService.cs`, `ActiveProjectService.cs`, `ISessionService.cs`.
