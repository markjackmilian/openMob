# Drawer Navigation — Active Project Sessions & Session Delete Refactor

## Metadata
| Field       | Value                                              |
|-------------|----------------------------------------------------|
| Date        | 2026-03-21                                         |
| Status      | **Completed**                                      |
| Version     | 1.0                                                |
| Completed   | 2026-03-21                                         |
| Branch      | feature/drawer-sessions-delete-refactor (merged)   |
| Merged into | develop                                            |

---

## Executive Summary

The lateral drawer (hamburger menu) currently shows the app name and a flat list of sessions regardless of the active project. This feature corrects that by displaying the active project name as the drawer header and filtering the session list to show only sessions belonging to the active project. Additionally, the inline/swipe delete action on sessions is removed from the drawer and relocated to the existing Context Sheet, which already serves as the per-session settings modal accessible from the top-right of the chat page.

---

## Scope

### In Scope
- Replace the drawer header (currently shows app name) with the **active project name**
- Filter the drawer session list to show **only sessions belonging to the active project** (`CurrentProjectId`)
- Each session entry displays **session name only**
- **Highlight the currently open session** in the drawer list
- Tap on a session in the drawer → **navigate to that session's ChatPage** and **close the drawer**
- **Remove swipe-to-delete and any inline delete action** from the drawer session list
- Add a **"Delete Session" button** in the existing `ContextSheet` (top-right area of the chat page)
- Delete button triggers a **confirmation dialog** before proceeding
- On confirmed delete: close the Context Sheet, **navigate to the New Session page** for the active project
- The drawer session list **auto-refreshes** when a session is deleted (via `WeakReferenceMessenger`)

### Out of Scope
- Change project button in the drawer (already present, untouched)
- Settings button in the drawer (already present, untouched)
- Renaming a session
- Sorting or filtering sessions in the drawer
- Pagination of the session list
- Server-side session deletion
- Any change to the Context Sheet sections (agent, model, thinking level, auto-accept)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The drawer header displays the name of the active project (`CurrentProjectId`). If no project is active, the header shows a placeholder (e.g. "No project selected").

2. **[REQ-002]** The drawer session list is populated exclusively with sessions belonging to the active project. Sessions from other projects are never shown.

3. **[REQ-003]** Each item in the drawer session list displays only the session name (no date, no message count, no other metadata).

4. **[REQ-004]** The session currently open in `ChatPage` is visually highlighted in the drawer list (e.g. distinct background or accent color). If no session is open, no item is highlighted.

5. **[REQ-005]** Tapping a session item in the drawer navigates to the `ChatPage` for that session and closes the drawer. If the tapped session is already the active one, the drawer closes without re-navigating.

6. **[REQ-006]** Swipe-to-delete and any inline delete affordance (icon, button) are removed from the drawer session list. The only way to delete a session is via the Context Sheet.

7. **[REQ-007]** The `ContextSheetViewModel` exposes a `DeleteSessionCommand` (`[AsyncRelayCommand]`). The command is disabled (`CanExecute = false`) while `IsBusy` is `true`.

8. **[REQ-008]** Invoking `DeleteSessionCommand` presents a confirmation dialog with:
   - Title: "Delete session"
   - Body: "Are you sure you want to delete this session? This action cannot be undone."
   - Confirm action: "Delete" (destructive style)
   - Cancel action: "Cancel"

9. **[REQ-009]** If the user confirms deletion: the session is deleted via `ISessionService.DeleteSessionAsync`, the Context Sheet is dismissed, and the app navigates to the **New Session page** for the active project.

10. **[REQ-010]** If the user cancels the dialog, no action is taken and the Context Sheet remains open.

11. **[REQ-011]** After a session is successfully deleted, a `SessionDeletedMessage` is published via `WeakReferenceMessenger`. The drawer's ViewModel subscribes to this message and refreshes the session list automatically.

12. **[REQ-012]** The drawer ViewModel (`AppShellViewModel` or equivalent) loads the session list on drawer open and re-loads it when the active project changes.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| Drawer XAML (Shell flyout or custom) | Modify | Replace app name header with project name; filter session list |
| Drawer ViewModel (`AppShellViewModel` or equivalent) | Modify | Add `ActiveProjectName`, filter sessions by `CurrentProjectId`, subscribe to `SessionDeletedMessage` |
| `ContextSheet.xaml` | Modify | Add "Delete Session" button in top-right area |
| `ContextSheetViewModel` | Modify | Add `DeleteSessionCommand`, confirmation dialog call, post-delete navigation |
| `ISessionService` | Modify (if missing) | Ensure `DeleteSessionAsync(string sessionId, CancellationToken)` exists |
| `IAppPopupService` | Modify (if needed) | Add `ShowConfirmationDialogAsync` or reuse existing alert mechanism |
| `Messages/` folder | Modify | Add `SessionDeletedMessage` sealed record |
| Session list item XAML (drawer) | Modify | Remove swipe gesture / delete button |

### Dependencies
- `ContextSheetViewModel` already receives `projectId` and `sessionId` via `InitializeAsync` — `DeleteSessionCommand` can use these directly.
- `WeakReferenceMessenger` is already the established pattern for cross-ViewModel communication in this project (see ADR: WeakReferenceMessenger ViewModel Communication).
- `ISessionService` must expose `DeleteSessionAsync`; if not present it must be added.
- Navigation to the New Session page must reuse the existing Shell route for new session creation.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | After delete, where does the user go? | Resolved | Navigate to the New Session page for the active project |
| 2 | Should the drawer auto-refresh when a session is deleted? | Resolved | Yes, via `SessionDeletedMessage` on `WeakReferenceMessenger` |
| 3 | What is the exact ViewModel managing the drawer content? | Resolved | `FlyoutViewModel` (confirmed in codebase) |
| 4 | Does `ISessionService.DeleteSessionAsync` already exist? | Resolved | Yes — `Task<bool> DeleteSessionAsync(string id, CancellationToken ct = default)` already exists |
| 5 | Is there an existing confirmation dialog helper in `IAppPopupService`? | Resolved | Yes — `ShowConfirmDeleteAsync(string title, string message, CancellationToken ct)` already exists |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given an active project, when the drawer is opened, then the header displays the active project name. *(REQ-001)*
- [ ] **[AC-002]** Given an active project with 3 sessions and another project with 2 sessions, when the drawer is opened, then only the 3 sessions of the active project are listed. *(REQ-002)*
- [ ] **[AC-003]** Given the drawer is open, when the user taps a session that is not the current one, then the app navigates to that session's ChatPage and the drawer closes. *(REQ-005)*
- [ ] **[AC-004]** Given the drawer is open, when the user taps the already-active session, then the drawer closes without re-navigating. *(REQ-005)*
- [ ] **[AC-005]** Given the drawer session list, when the user attempts a swipe gesture on a session item, then no delete action is triggered. *(REQ-006)*
- [ ] **[AC-006]** Given the Context Sheet is open, when the user taps "Delete Session", then a confirmation dialog appears with "Delete" and "Cancel" actions. *(REQ-007, REQ-008)*
- [ ] **[AC-007]** Given the confirmation dialog is shown, when the user taps "Cancel", then no deletion occurs and the Context Sheet remains open. *(REQ-010)*
- [ ] **[AC-008]** Given the confirmation dialog is shown, when the user taps "Delete", then the session is deleted, the Context Sheet is dismissed, and the app navigates to the New Session page for the active project. *(REQ-009)*
- [ ] **[AC-009]** Given a session has just been deleted, when the drawer is opened, then the deleted session no longer appears in the list. *(REQ-011)*
- [ ] **[AC-010]** Given `IsBusy` is `true` on `ContextSheetViewModel`, then `DeleteSessionCommand.CanExecute` returns `false`. *(REQ-007)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Drawer ViewModel**: Identify the ViewModel currently bound to the Shell flyout or custom drawer. It likely already holds a session list — verify whether it is already filtered by project or shows all sessions globally.
- **Active project propagation**: The drawer ViewModel needs access to `CurrentProjectId`. Verify whether this is already injected (e.g. via `IProjectService` or a shared state service) or needs to be wired up.
- **`SessionDeletedMessage`**: New sealed record to add in `openMob.Core/Messages/`. Follow the same pattern as `ProjectPreferenceChangedMessage` (established in feature `session-context-sheet-1of3-core`).
- **`WeakReferenceMessenger` pattern**: As established in the ADR `adr-weakreferencemessenger-viewmodel-communication`, all subscriptions must be registered in the constructor and unregistered as the first line of `Dispose()`. The drawer ViewModel must follow this pattern.
- **`DeleteSessionCommand` in `ContextSheetViewModel`**: `sessionId` is already available via `InitializeAsync`. The command must guard against double-execution with `IsBusy`. Use `[AsyncRelayCommand]` with `CanExecute` tied to `!IsBusy`.
- **Confirmation dialog**: Check if `IAppPopupService` already exposes a generic `ShowConfirmationDialogAsync(string title, string message, string confirm, string cancel)` method. If not, add it — do not use `Application.Current.MainPage.DisplayAlert` directly from the ViewModel.
- **Post-delete navigation**: Identify the Shell route for the New Session page (e.g. `//NewSession`, `//Chat/New`, or similar). The navigation call must happen after the Context Sheet is dismissed to avoid stacking modal on top of a navigating page.
- **`ISessionService.DeleteSessionAsync`**: Verify existence. If missing, add `Task<bool> DeleteSessionAsync(string sessionId, CancellationToken ct = default)` to the interface and implement it.
- **Highlighted session in drawer**: The drawer ViewModel needs to know the `CurrentSessionId`. Verify how `ChatViewModel` exposes or publishes this. A `CurrentSessionChangedMessage` may be needed, or the drawer can read it from a shared state service.
- **Related files to inspect**: `AppShell.xaml`, `AppShell.xaml.cs`, any `FlyoutViewModel` or `AppShellViewModel`, `ContextSheet.xaml`, `ContextSheetViewModel.cs`, `ISessionService.cs`, `IAppPopupService.cs`, `MauiPopupService.cs`.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/drawer-sessions-delete-refactor |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Codebase Findings

**Drawer ViewModel:** `FlyoutViewModel` (`src/openMob.Core/ViewModels/FlyoutViewModel.cs`) — confirmed. It already holds `Sessions`, `ProjectSectionTitle`, `HasProject`, `IsLoading`. It already calls `_sessionService.GetSessionsByProjectAsync(currentProject.Id, ct)` — sessions ARE already filtered by the active project. The `ProjectSectionTitle` is set to the uppercase project name extracted from the worktree path.

**Drawer XAML:** Two custom controls:
- `FlyoutHeaderView.xaml` — currently shows hardcoded `"openMob"` text. Must be replaced with the active project name.
- `FlyoutContentView.xaml` — already shows filtered sessions. Contains `SwipeView` with a `DeleteSessionSwipeItem` that must be removed. Also shows a `UpdatedAt` timestamp column that must be removed (REQ-003: name only).

**`ISessionService.DeleteSessionAsync`:** Already exists — `Task<bool> DeleteSessionAsync(string id, CancellationToken ct = default)`. No interface change needed.

**`IAppPopupService.ShowConfirmDeleteAsync`:** Already exists — `Task<bool> ShowConfirmDeleteAsync(string title, string message, CancellationToken ct = default)`. No interface change needed.

**`ContextSheetViewModel`:** Already has `_currentProjectId` and receives `sessionId` via `InitializeAsync(string projectId, string sessionId, ...)`. The `sessionId` parameter is currently unused (reserved for future use). It must now be stored as `_currentSessionId` and used by `DeleteSessionCommand`.

**`IsBusy` on `ContextSheetViewModel`:** Already exists and is used by `InvokeSubagentCommand.CanExecute`. `DeleteSessionCommand` must also be gated on `!IsBusy`.

**`SessionDeletedMessage`:** Does not exist. Must be created in `src/openMob.Core/Messages/` following the `ProjectPreferenceChangedMessage` pattern.

**`CurrentSessionChangedMessage`:** Does not exist. Must be created so `FlyoutViewModel` can know the active session ID to highlight it. `ChatViewModel.SetSession(string sessionId)` is called from `ChatPage.ApplyQueryAttributes` — this is the right place to publish the message.

**`FlyoutViewModel` — `IDisposable`:** Currently does NOT implement `IDisposable`. Since it will subscribe to `WeakReferenceMessenger`, it must implement `IDisposable` with `UnregisterAll(this)` as the first line of `Dispose()`. However, since `FlyoutViewModel` is resolved as a Singleton (via `Application.Current?.Handler?.MauiContext?.Services.GetService<FlyoutViewModel>()`), it lives for the app lifetime — `Dispose` will be called by the DI container on app shutdown.

**Navigation after delete:** The route for a new chat is `//chat` with no `sessionId` parameter — `ChatPage.OnAppearing` already handles this case by calling `vm.NewChatCommand.ExecuteAsync(null)` when `vm.CurrentSessionId is null`. So post-delete navigation is: dismiss the Context Sheet (via `_popupService.PopPopupAsync()`), then navigate to `//chat` without a `sessionId`.

**`SelectSessionCommand` — close drawer:** Currently navigates to `//chat` but does NOT close the drawer. The drawer closes automatically on Shell navigation in MAUI when `FlyoutBehavior="Flyout"`. This is correct behavior — no change needed.

**`FlyoutViewModel` registration:** Resolved from DI as a Singleton via `Application.Current?.Handler?.MauiContext?.Services.GetService<FlyoutViewModel>()` in both `FlyoutHeaderView.xaml.cs` and `FlyoutContentView.xaml.cs`. It must be registered as `Singleton` in `MauiProgram.cs` (currently it is registered via `AddOpenMobCore()` — must verify).

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Messages | om-mobile-core | `src/openMob.Core/Messages/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/FlyoutViewModel.cs`, `ContextSheetViewModel.cs`, `ChatViewModel.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Controls/FlyoutHeaderView.xaml`, `FlyoutContentView.xaml`, `src/openMob/Views/Popups/ContextSheet.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/FlyoutViewModelTests.cs`, `ContextSheetViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Messages/SessionDeletedMessage.cs` — new sealed record published after session deletion
- `src/openMob.Core/Messages/CurrentSessionChangedMessage.cs` — new sealed record published by `ChatViewModel.SetSession` so `FlyoutViewModel` can highlight the active session

### Files to Modify

- `src/openMob.Core/ViewModels/FlyoutViewModel.cs` — add `CurrentSessionId` property, `IDisposable`, subscribe to `SessionDeletedMessage` and `CurrentSessionChangedMessage`, update `IsSelected` on session items, add `SelectSessionCommand` guard (skip nav if already active session)
- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — store `_currentSessionId` from `InitializeAsync`, add `DeleteSessionCommand` with `IsBusy` guard, confirmation dialog, delete, dismiss sheet, navigate to new chat
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — publish `CurrentSessionChangedMessage` from `SetSession()`
- `src/openMob/Views/Controls/FlyoutHeaderView.xaml` — replace hardcoded `"openMob"` with `{Binding ProjectSectionTitle}` (or a new `ActiveProjectName` property)
- `src/openMob/Views/Controls/FlyoutContentView.xaml` — remove `SwipeView` and `DeleteSessionSwipeItem`, remove timestamp column, keep session name only
- `src/openMob/Views/Popups/ContextSheet.xaml` — add "Delete Session" destructive row at the bottom

### Technical Dependencies

- `WeakReferenceMessenger` is already used in the project — no new NuGet packages required
- `ISessionService.DeleteSessionAsync` already exists — no interface change needed
- `IAppPopupService.ShowConfirmDeleteAsync` already exists — no interface change needed
- `IAppPopupService.PopPopupAsync` already exists — used for dismissing the Context Sheet before navigating

### Technical Risks

- **`FlyoutViewModel` singleton lifecycle:** If `FlyoutViewModel` is not currently registered as Singleton in `AddOpenMobCore()`, the two views (`FlyoutHeaderView`, `FlyoutContentView`) will resolve different instances and the binding will be inconsistent. Must verify registration and ensure Singleton.
- **`WeakReferenceMessenger` in Singleton:** Since `FlyoutViewModel` is a Singleton, its messenger subscriptions persist for the app lifetime. This is correct — no risk of stale handlers.
- **Post-delete navigation timing:** `PopPopupAsync` must complete before `GoToAsync("//chat")` to avoid modal stacking. Use `await` in sequence.
- **`SelectSessionCommand` — already-active guard:** The spec requires that tapping the active session closes the drawer without re-navigating. MAUI Shell closes the flyout on navigation automatically, but if we skip navigation, the flyout must be closed explicitly via `Shell.Current.FlyoutIsPresented = false`. This must be done via `INavigationService` or a dedicated `IFlyoutService` — not directly from the ViewModel. Since `INavigationService` is already injected, add a `CloseFlyoutAsync()` method or handle it in the XAML layer.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/drawer-sessions-delete-refactor`
2. [om-mobile-core] Create `SessionDeletedMessage`, `CurrentSessionChangedMessage`; modify `FlyoutViewModel` (IDisposable, messenger subscriptions, `CurrentSessionId`, `SelectSessionCommand` guard); modify `ContextSheetViewModel` (store `_currentSessionId`, add `DeleteSessionCommand`); modify `ChatViewModel` (publish `CurrentSessionChangedMessage` from `SetSession`)
3. ⟳ [om-mobile-ui] Modify `FlyoutHeaderView.xaml` (project name), `FlyoutContentView.xaml` (remove swipe/timestamp), `ContextSheet.xaml` (add Delete Session row) — can start once the ViewModel binding surface is defined
4. [om-tester] Update `FlyoutViewModelTests.cs` and `ContextSheetViewModelTests.cs` with new test cases for the new behaviors
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-012]` requirements implemented
- [ ] All `[AC-001]` through `[AC-010]` acceptance criteria satisfied
- [ ] Unit tests written/updated for `FlyoutViewModel` and `ContextSheetViewModel`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
