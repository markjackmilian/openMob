# Drawer — New Session Button

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

This feature adds a "New Session" button fixed at the top of the session list in the lateral drawer (hamburger menu). Tapping it creates a new session for the active project, navigates immediately to its `ChatPage`, and closes the drawer. The session title starts as "New Session" and is subsequently updated by opencode after the first message, following the existing title-generation behaviour.

---

## Scope

### In Scope
- "New Session" button fixed above the `CollectionView` of sessions in the drawer UI (`FlyoutContentView`)
- `NewSessionCommand` (`[AsyncRelayCommand]`) on `FlyoutViewModel`, disabled while `IsBusy = true`
- Session created via `ISessionService` with the active project ID and initial name `"New Session"`
- Automatic navigation to `ChatPage` for the newly created session via `INavigationService.GoToAsync`
- Drawer closed automatically after navigation via `INavigationService.CloseFlyoutAsync()` (already established in ADR)
- New `SessionCreatedMessage(string SessionId, string ProjectId)` sealed record in `openMob.Core/Messages/`
- `FlyoutViewModel` publishes `SessionCreatedMessage` after successful creation and subscribes to it to prepend the new session at the top of the `Sessions` collection
- Error alert shown when no active project is set; no session is created in that case

### Out of Scope
- "New Session" entry point from the Context Sheet (removed by user decision)
- Manual name input form or dialog before creation
- Session title update logic after the first message (already handled by existing opencode integration)
- Any server-side changes

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The drawer UI exposes a "New Session" button rendered as a fixed element above the sessions `CollectionView`. The button is always visible regardless of the number of sessions in the list.

2. **[REQ-002]** `FlyoutViewModel` exposes `NewSessionCommand` declared as `[AsyncRelayCommand(CanExecute = nameof(CanCreateNewSession))]`. `CanCreateNewSession` returns `false` while `IsBusy` is `true`. `IsBusy` must carry `[NotifyCanExecuteChangedFor(nameof(NewSessionCommand))]`.

3. **[REQ-003]** When `NewSessionCommand` is invoked and `ActiveProjectId` is `null` or empty, the command must display an error alert (title: `"No active project"`, body: `"Please select a project before creating a new session."`) and return without creating a session.

4. **[REQ-004]** When `ActiveProjectId` is set, the command calls `ISessionService.CreateSessionAsync(projectId, "New Session", cancellationToken)` to create the session. The initial session name is the literal string `"New Session"`.

5. **[REQ-005]** After successful creation, `FlyoutViewModel` publishes `SessionCreatedMessage(newSession.Id, projectId)` via `WeakReferenceMessenger.Default`.

6. **[REQ-006]** `FlyoutViewModel` subscribes to `SessionCreatedMessage` in its constructor (alongside existing `SessionDeletedMessage` and `CurrentSessionChangedMessage` subscriptions). On receipt, it prepends the new session item to the top of the `Sessions` `ObservableCollection`. The mutation must be dispatched to the UI thread via `IDispatcherService` (established pattern — see ADR `adr-idispatcherservice-viewmodel-injection`).

7. **[REQ-007]** After publishing the message, `FlyoutViewModel` navigates to `ChatPage` for the new session via `INavigationService.GoToAsync` passing the new session ID, then calls `INavigationService.CloseFlyoutAsync()` to close the drawer. Order: navigate first, then close flyout.

8. **[REQ-008]** The newly created session is highlighted in the drawer list as the active session (consistent with `CurrentSessionChangedMessage` behaviour already in place: `ChatViewModel.SetSession` publishes `CurrentSessionChangedMessage` on navigation, which `FlyoutViewModel` handles to update `IsSelected`).

9. **[REQ-009]** If `ISessionService.CreateSessionAsync` throws, the error is captured via `SentryHelper.CaptureException`, `IsBusy` is reset to `false`, and a user-facing error alert is shown (title: `"Error"`, body: `"Could not create session. Please try again."`). No navigation occurs.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `FlyoutViewModel` | Modified | New `NewSessionCommand`, subscription to `SessionCreatedMessage`, `IDispatcherService` dispatch for prepend |
| `FlyoutContentView.xaml` | Modified | Add "New Session" button above `CollectionView` |
| `ISessionService` | Verify | Check whether `CreateSessionAsync(projectId, name, ct)` already exists; add if missing |
| `openMob.Core/Messages/` | New file | `SessionCreatedMessage.cs` sealed record |
| `IAppPopupService` | Verify | Check whether a generic alert helper exists for error display; use it if available |
| `ChatViewModel` | No change expected | `SetSession` already publishes `CurrentSessionChangedMessage` on navigation — highlights new session automatically |

### Dependencies
- `INavigationService.CloseFlyoutAsync()` — already implemented (ADR `adr-closeflyoutasync-navigation-service`)
- `IDispatcherService` — already injected into `FlyoutViewModel` (ADR `adr-idispatcherservice-viewmodel-injection`)
- `WeakReferenceMessenger` pattern — already established (ADR `adr-weakreferencemessenger-viewmodel-communication`)
- `SessionDeletedMessage` / `CurrentSessionChangedMessage` — already exist in `openMob.Core/Messages/`

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Does `ISessionService` already expose `CreateSessionAsync(string projectId, string name, CancellationToken ct)`? | Open | To be verified during technical analysis |
| 2 | Does `IAppPopupService` expose a generic `ShowAlertAsync(string title, string message)` method? | Open | To be verified during technical analysis; if not, add it following the existing `ShowConfirmDeleteAsync` pattern |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the drawer is open, the "New Session" button is visible at the top of the session list regardless of how many sessions exist. *(REQ-001)*
- [ ] **[AC-002]** Given `IsBusy = true`, when the user taps "New Session", `NewSessionCommand` does not execute. *(REQ-002)*
- [ ] **[AC-003]** Given no active project is set, when the user taps "New Session", an alert is shown with title "No active project" and no session is created. *(REQ-003)*
- [ ] **[AC-004]** Given an active project is set, when the user taps "New Session", a session named "New Session" is created for that project. *(REQ-004)*
- [ ] **[AC-005]** After creation, the new session appears at the top of the drawer session list. *(REQ-006)*
- [ ] **[AC-006]** After creation, the app navigates to `ChatPage` for the new session and the drawer is closed. *(REQ-007)*
- [ ] **[AC-007]** The new session is highlighted as the active session in the drawer list. *(REQ-008)*
- [ ] **[AC-008]** Given `ISessionService.CreateSessionAsync` throws, an error alert is shown, no navigation occurs, and `IsBusy` is reset to `false`. *(REQ-009)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Key areas to investigate:**
  - Verify whether `ISessionService` already exposes `CreateSessionAsync(string projectId, string name, CancellationToken ct)`. If not, it must be added to both the interface and its implementation.
  - Verify whether `IAppPopupService` has a generic `ShowAlertAsync(string title, string message)` method. If not, add it (following the existing `ShowConfirmDeleteAsync` pattern).
  - Confirm the exact route/query string format used by `INavigationService.GoToAsync` to navigate to `ChatPage` with a session ID (check how `SelectSessionCommand` in `FlyoutViewModel` currently does it).

- **Suggested implementation approach:**
  - `SessionCreatedMessage` should follow the same sealed record pattern as `SessionDeletedMessage`: `sealed record SessionCreatedMessage(string SessionId, string ProjectId)`.
  - `FlyoutViewModel.NewSessionCommand` handler: set `IsBusy = true` → guard `ActiveProjectId` → call `CreateSessionAsync` → publish `SessionCreatedMessage` → navigate → close flyout → set `IsBusy = false` (in `finally`).
  - The `SessionCreatedMessage` handler in `FlyoutViewModel` prepends a new `SessionItem` to `Sessions` inside `_dispatcher.Dispatch(...)`. Do **not** trigger a full `LoadSessionsCommand` reload — prepend only, to avoid flickering.
  - As established in the `drawer-sessions-delete-refactor` spec, `ConfigureAwait(false)` must **not** be used in ViewModel methods that call popup/toast/dialog/navigation APIs (Android ANR risk).

- **Constraints to respect:**
  - `openMob.Core` must have zero MAUI dependencies — use `IDispatcherService`, `INavigationService`, `IAppPopupService` abstractions only.
  - `FlyoutViewModel` is a Singleton — the `_disposeCts` cancellation token must be passed to `CreateSessionAsync` and checked in the `SessionCreatedMessage` handler before mutating `Sessions`.
  - All `Sessions` `ObservableCollection` mutations must go through `_dispatcher.Dispatch(...)`.

- **Related files or modules (if known):**
  - `src/openMob.Core/ViewModels/FlyoutViewModel.cs`
  - `src/openMob.Core/Messages/SessionDeletedMessage.cs` (reference for new message)
  - `src/openMob.Core/Messages/CurrentSessionChangedMessage.cs` (reference)
  - `src/openMob.Core/Services/Interfaces/ISessionService.cs`
  - `src/openMob.Core/Services/Interfaces/IAppPopupService.cs`
  - `src/openMob/Views/Flyout/FlyoutContentView.xaml`
  - `tests/openMob.Tests/ViewModels/FlyoutViewModelTests.cs` (add new test cases)
