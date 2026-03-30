# Heartbeat Monitor Footer with Reconnection Flow

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-28                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

The server sends periodic `server.heartbeat` SSE events that are currently unhandled, producing visible warning cards in the chat UI. This feature silently consumes heartbeat events, uses them to drive a persistent traffic-light footer at the bottom of ChatPage showing the active server name and connection health, and triggers an automatic reconnection modal when heartbeats stop arriving. The existing `StatusBannerView` and its associated `NoProvider` logic are removed entirely, as provider configuration is now server-side only.

---

## Scope

### In Scope
- Silent consumption of `server.heartbeat` SSE events (no chat card produced)
- New `ConnectionFooterView` — always-visible footer at the bottom of ChatPage showing active server name and a traffic-light status indicator
- Traffic-light state machine: Green → Yellow → Red driven by heartbeat timeout thresholds
- Reconnecting modal: non-dismissible, shows spinner + status message + automatic reconnection attempts + "Gestisci server" button
- Navigation from reconnecting modal → ServerManagementPage with back navigation to ChatPage preserved
- Removal of `StatusBannerView` from ChatPage and all associated ViewModel logic (`StatusBannerInfo`, `StatusBannerType`, `UpdateStatusBanner()`)
- Removal of `NoProvider` banner logic (provider configuration is server-side; the concept no longer applies to the mobile app)

### Out of Scope
- Changes to the SSE protocol, `ChatEventParser`, or SSE transport layer
- Heartbeat monitoring outside ChatPage (other pages are not affected)
- Startup flow changes (`SplashViewModel` remains unchanged)
- Localisation system (strings remain hardcoded in Italian per project convention)
- Modifying the input bar (already implemented as a modal, not part of this feature)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** When a `server.heartbeat` SSE event is received, the system MUST update an internal `LastHeartbeatAt` timestamp and produce no visible output in the chat message list.

2. **[REQ-002]** `ChatViewModel` (or a dedicated `HeartbeatMonitorService`) MUST run a periodic check (every 5 seconds) against `LastHeartbeatAt` to determine the current connection health state.

3. **[REQ-003]** The connection health state MUST follow this traffic-light model:
   - **Green** (`Healthy`): last heartbeat received within the past 30 seconds.
   - **Yellow** (`Degraded`): no heartbeat received for 30–60 seconds.
   - **Red / Reconnecting** (`Lost`): no heartbeat received for more than 60 seconds.

4. **[REQ-004]** A new `ConnectionFooterView` control MUST be permanently visible at the bottom of ChatPage (below the message list, above the system navigation bar). It MUST display:
   - The name of the currently active server (from `IOpencodeConnectionManager` or `ServerConnection` entity).
   - A circular traffic-light indicator coloured according to the current health state: green (`ColorSuccess`), yellow (`ColorWarning`), red (`ColorError`).

5. **[REQ-005]** The `ConnectionFooterView` MUST replace the existing `StatusBannerView` in ChatPage. `StatusBannerView`, `StatusBannerInfo`, `StatusBannerType`, and `UpdateStatusBanner()` MUST be removed.

6. **[REQ-006]** When the health state transitions to `Lost` (Red), the system MUST automatically display a non-dismissible reconnection modal. The modal MUST show:
   - A spinner / activity indicator.
   - A status message, e.g. *"Connessione al server persa. Tentativo di riconnessione in corso…"*.
   - A secondary button labelled *"Gestisci server"*.
   - The current reconnection attempt number and total (e.g. *"Tentativo 2 di 3"*).

7. **[REQ-007]** While the reconnection modal is visible, the system MUST automatically attempt to re-establish the connection using `IOpencodeConnectionManager.IsServerReachableAsync()` with exponential backoff (attempts at 5 s, 10 s, 20 s intervals; maximum 3 attempts before pausing and retrying the cycle).

8. **[REQ-008]** If a reconnection attempt succeeds (server reachable AND heartbeat resumes), the modal MUST close automatically and the footer indicator MUST return to Green.

9. **[REQ-009]** If the user taps *"Gestisci server"* in the reconnection modal, the app MUST navigate to ServerManagementPage using the absolute push route `"///server-management"`, preserving back navigation to ChatPage.

10. **[REQ-010]** When the user navigates back from ServerManagementPage to ChatPage (via the system or Shell back button), the reconnection modal MUST reappear if the connection is still in `Lost` state, or remain closed if the connection has been restored.

11. **[REQ-011]** The health state machine MUST reset to `Healthy` (Green) immediately upon receiving any `server.heartbeat` event, regardless of the current state.

12. **[REQ-012]** The `ConnectionFooterView` MUST respect the app's light/dark theme using `AppThemeBinding` colour tokens consistent with the existing design system (`ColorSuccess`, `ColorWarning`, `ColorError`, `ColorSurface`, `ColorOnSurface`).

13. **[REQ-013]** The `ConnectionFooterView` MUST be positioned as the last fixed row in ChatPage's outer Grid, below the message list (`*` row) and below the `SubagentIndicatorView`, so that it sits flush at the bottom of the content area above the system navigation bar.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatPage.xaml` | Major | Remove `StatusBannerView` (Row 2); add `ConnectionFooterView` as new bottom row; adjust `RowDefinitions` |
| `ChatViewModel.cs` | Major | Remove `StatusBanner`, `UpdateStatusBanner()`, `NavigateToServerManagementCommand`; add heartbeat timestamp tracking, health state property, reconnection modal trigger |
| `StatusBannerView.xaml` / `.xaml.cs` | Removed | Entire control deleted |
| `StatusBannerInfo.cs` | Removed | Model no longer needed |
| `StatusBannerType.cs` | Removed | Enum no longer needed |
| `IOpencodeConnectionManager` | Minor | May need to expose `string? ActiveServerName` or the ViewModel reads it from the `ServerConnection` entity directly |
| `IAppPopupService` / `MauiPopupService` | Moderate | New method `ShowReconnectingModalAsync(...)` for the non-dismissible reconnection modal |
| `ChatEventParser` / SSE handlers | Minor | Add explicit `case ServerHeartbeatEvent` in `ChatViewModel` switch — calls `OnHeartbeatReceived()`, no chat card emitted |
| `sse-unhandled-message-fallback-card` (in-progress spec) | Dependency | `server.heartbeat` must be excluded from the fallback card logic introduced by that spec |

### Dependencies
- `sse-unhandled-message-fallback-card` spec (done): the fallback card spec is already merged; `server.heartbeat` must be explicitly excluded from the "unknown event → show card" path.
- `tabler-icons-codepoint-migration` spec (done): already merged; no conflict with `StatusBannerView` removal.
- `chat-session-loading-indicator` spec (todo): references the 6-row Grid layout of ChatPage (REQ-006 of that spec); the row index of the loading overlay must be re-verified after this feature changes the Grid structure.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the reconnection modal be implemented as a UXDivers popup sheet (via `IAppPopupService`) or as a native `DisplayAlert`-style overlay? | Resolved | Use `IAppPopupService` with a new dedicated popup sheet — consistent with the established rule that ViewModels never call `DisplayAlert` directly. |
| 2 | Where does the active server name come from — `IOpencodeConnectionManager`, `IServerConnectionService`, or direct DB read? | Resolved | Add `Task<string?> GetActiveServerNameAsync(CancellationToken ct)` to `IOpencodeConnectionManager` and implement it in `OpencodeConnectionManager` via `IServerConnectionRepository.GetActiveAsync()`. This keeps the pattern consistent with `GetBaseUrlAsync()`. |
| 3 | Should the periodic heartbeat check timer be owned by `ChatViewModel` or extracted into a dedicated `IHeartbeatMonitorService`? | Resolved | Dedicated `IHeartbeatMonitorService` with `StartAsync(CancellationToken)` / `StopAsync()` for testability. The service owns the `PeriodicTimer` and exposes an `event Action<ConnectionHealthState>? HealthStateChanged`. |
| 4 | What happens to the `NoProvider` banner path in `UpdateStatusBanner()`? | Resolved | Removed entirely — provider configuration is server-side; the concept no longer applies to the mobile app. |
| 5 | After the user navigates back from ServerManagementPage, should the reconnection modal reappear automatically? | Resolved | Yes — if the connection is still `Lost`, the modal reappears; if restored, it stays closed. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the SSE stream delivers a `server.heartbeat` event, when the event is received, then no card or UI element appears in the chat message list. *(REQ-001)*

- [ ] **[AC-002]** Given the app is on ChatPage, when the connection is healthy (heartbeat within 30 s), then `ConnectionFooterView` shows the active server name and a **green** indicator. *(REQ-003, REQ-004)*

- [ ] **[AC-003]** Given no heartbeat has been received for 30–60 seconds, when the periodic check fires, then the footer indicator turns **yellow**. *(REQ-003, REQ-004)*

- [ ] **[AC-004]** Given no heartbeat has been received for more than 60 seconds, when the periodic check fires, then the footer indicator turns **red** and the reconnection modal appears automatically. *(REQ-003, REQ-006)*

- [ ] **[AC-005]** Given the reconnection modal is visible, when the user attempts to dismiss it by tapping outside or pressing back, then the modal remains visible (non-dismissible). *(REQ-006)*

- [ ] **[AC-006]** Given the reconnection modal is visible, when a reconnection attempt succeeds and a heartbeat is subsequently received, then the modal closes automatically and the footer returns to green. *(REQ-008, REQ-011)*

- [ ] **[AC-007]** Given the reconnection modal is visible, when the user taps "Gestisci server", then the app navigates to ServerManagementPage with the back button returning to ChatPage. *(REQ-009)*

- [ ] **[AC-008]** Given the user is on ServerManagementPage (reached via the reconnection modal), when the user presses back, then ChatPage is shown; if the connection is still lost, the modal reappears; if restored, it does not. *(REQ-010)*

- [ ] **[AC-009]** Given `StatusBannerView` previously occupied Row 2 of ChatPage, when this feature is implemented, then `StatusBannerView`, `StatusBannerInfo`, and `StatusBannerType` are fully removed from the codebase with no compilation errors or warnings. *(REQ-005)*

- [ ] **[AC-010]** Given the app switches between light and dark theme, when `ConnectionFooterView` is visible, then its colours update correctly using `AppThemeBinding` tokens. *(REQ-012)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

- **Heartbeat event routing:** `server.heartbeat` is currently parsed by `ChatEventParser` as a `ServerHeartbeatEvent` (or falls to `UnknownEvent`). Verify the exact parsed type and add an explicit `case` in `ChatViewModel`'s SSE switch to call `OnHeartbeatReceived()` without emitting any chat item. Ensure this case is excluded from the `sse-unhandled-message-fallback-card` logic.

- **Health state timer:** Evaluate `System.Threading.PeriodicTimer` (available in .NET 6+) vs. a `TimeProvider`-based approach for the periodic 5-second check. The project already uses `TimeProvider` (injected as optional constructor parameter, defaulting to `TimeProvider.System`) in `SplashViewModel` — prefer consistency. A dedicated `IHeartbeatMonitorService` with `StartAsync(CancellationToken)` / `StopAsync()` is recommended so the timer lifecycle is decoupled from `ChatViewModel` and testable in isolation.

- **Active server name:** Determine the cleanest source for the server display name — `IOpencodeConnectionManager` currently exposes `GetBaseUrlAsync()` but not a friendly name. The `ServerConnection` entity (in `AppDatabase`) likely has a `Name` property. Consider exposing `string? ActiveServerName` on `IOpencodeConnectionManager` or reading it via `IServerConnectionService`.

- **Reconnection modal:** Implement as a new UXDivers popup sheet (`PopupResultPage<ReconnectResult>` or similar) registered in DI and exposed via a new `IAppPopupService.ShowReconnectingModalAsync(...)` method. The modal ViewModel must accept a `CancellationToken` and expose an `IAsyncRelayCommand` for the "Gestisci server" action. The reconnection retry loop (exponential backoff: 5 s, 10 s, 20 s, max 3 attempts per cycle) should run inside the modal ViewModel or the `IHeartbeatMonitorService`.

- **Grid layout change:** Current `ChatPage.xaml` outer Grid has `RowDefinitions="Auto,Auto,Auto,*,Auto,Auto"` (Row 0=Header, Row 1=ContextStatusBar, Row 2=StatusBanner, Row 3=Messages, Row 4=SubagentIndicator, Row 5=InputArea — but input is now a modal). Removing Row 2 (`StatusBannerView`) and adding `ConnectionFooterView` at the bottom changes row indices. Re-verify `chat-session-loading-indicator` spec (todo) which references specific row numbers for its overlay.

- **`StatusBannerView` removal cascade:** Check `tabler-icons-codepoint-migration` (done) which listed `StatusBannerView.xaml` as a migration target — already merged, no conflict.

- **Navigation pattern:** `"///server-management"` (triple-slash absolute push) is the established pattern for ChatPage → ServerManagementPage with back navigation, as decided in `server-offline-startup-navigation`. No changes needed to `AppShell.xaml` routing.

- **`OnConnectionStatusChanged` cleanup:** The existing `_connectionManager.StatusChanged` subscription in `ChatViewModel` currently calls `UpdateStatusBanner()`. After this feature, the subscription should instead notify `IHeartbeatMonitorService` or update a `ConnectionStatus` property bound to `ConnectionFooterView`. The `IsServerOffline` property and `UpdateStatusBanner()` method are deleted.

### Constraints to respect
- No Rx / `IObservable<T>` — use `event Action<T>` and `TimeProvider` as established.
- All popup interactions go through `IAppPopupService` — never `DisplayAlert` or `Shell.Current` directly from a ViewModel.
- `ConfigureAwait(false)` on all `await` calls in `openMob.Core`.
- Strings in Italian (hardcoded).
- `ConnectionFooterView` must live in `src/openMob/Views/Controls/`; its backing state properties live in `ChatViewModel` (or a sub-ViewModel) in `src/openMob.Core/`.

### Related files or modules
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — main ViewModel to modify
- `src/openMob/Views/Pages/ChatPage.xaml` — Grid layout change + control swap
- `src/openMob/Views/Controls/StatusBannerView.xaml` — to be deleted
- `src/openMob.Core/Models/StatusBannerInfo.cs` — to be deleted
- `src/openMob.Core/Models/StatusBannerType.cs` — to be deleted
- `src/openMob.Core/Services/IAppPopupService.cs` — new method to add
- `src/openMob/Services/MauiPopupService.cs` — new method implementation
- `src/openMob.Core/Services/IOpencodeConnectionManager.cs` — possible `ActiveServerName` addition
- `src/openMob.Core/Services/OpencodeConnectionManager.cs` — possible implementation
- `specs/done/2026-03-28-sse-unhandled-message-fallback-card.md` — coordinate exclusion of `server.heartbeat`
- `specs/done/2026-03-21-tabler-icons-codepoint-migration.md` — already merged, no conflict
- `specs/todo/2026-03-25-chat-session-loading-indicator.md` — re-verify row indices after Grid change

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-30

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | `feature/heartbeat-monitor-footer` |
| Branches from | `develop` |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Services/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| Infrastructure / HTTP | om-mobile-core | `src/openMob.Core/Infrastructure/Http/` |
| Models | om-mobile-core | `src/openMob.Core/Models/` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/` |
| UI Controls | om-mobile-ui | `src/openMob/Views/Controls/` |
| Popup Sheets | om-mobile-ui | `src/openMob/Views/Popups/` |
| MAUI Services | om-mobile-ui | `src/openMob/Services/` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob.Core/Services/IHeartbeatMonitorService.cs` — interface for the heartbeat monitor service
- `src/openMob.Core/Services/HeartbeatMonitorService.cs` — implementation with `PeriodicTimer` + `TimeProvider`, exposes `event Action<ConnectionHealthState>? HealthStateChanged`
- `src/openMob.Core/Models/ConnectionHealthState.cs` — enum: `Healthy`, `Degraded`, `Lost`
- `src/openMob.Core/ViewModels/ReconnectingModalViewModel.cs` — ViewModel for the reconnection modal popup; owns the exponential backoff retry loop
- `src/openMob/Views/Controls/ConnectionFooterView.xaml` + `.xaml.cs` — always-visible footer with server name + traffic-light indicator
- `src/openMob/Views/Popups/ReconnectingModalSheet.xaml` + `.xaml.cs` — non-dismissible UXDivers popup sheet

### Files to Modify

- `src/openMob.Core/Infrastructure/Http/IOpencodeConnectionManager.cs` — add `Task<string?> GetActiveServerNameAsync(CancellationToken ct)` method
- `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` — implement `GetActiveServerNameAsync` via `IServerConnectionRepository.GetActiveAsync()`
- `src/openMob.Core/Services/IAppPopupService.cs` — add `Task ShowReconnectingModalAsync(ReconnectingModalViewModel vm, CancellationToken ct)` method
- `src/openMob/Services/MauiPopupService.cs` — implement `ShowReconnectingModalAsync` using UXDivers `IPopupService.Current.PushAsync`
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — remove `StatusBanner`, `HasNoProvider`, `IsServerOffline`, `UpdateStatusBanner()`, `NavigateToServerManagementCommand`; add `IHeartbeatMonitorService` dependency, `ConnectionHealthState` property, `ActiveServerName` property, `OnHeartbeatReceived()`, heartbeat SSE case in switch, `OnAppearing`/`OnDisappearing` lifecycle hooks for monitor start/stop
- `src/openMob/Views/Pages/ChatPage.xaml` — remove Row 2 (`StatusBannerView`), update `RowDefinitions` from `Auto,Auto,Auto,*,Auto,Auto` to `Auto,Auto,*,Auto,Auto`, add `ConnectionFooterView` as Row 4 (new bottom row), shift `SubagentIndicatorView` to Row 3
- `src/openMob/Views/Pages/ChatPage.xaml.cs` — add `OnAppearing`/`OnDisappearing` overrides to call `ChatViewModel.StartHeartbeatMonitorCommand` / `StopHeartbeatMonitorCommand`
- `src/openMob/MauiProgram.cs` — register `IHeartbeatMonitorService`, `HeartbeatMonitorService`, `ReconnectingModalSheet`, `ReconnectingModalViewModel` in DI
- `src/openMob.Core/GlobalUsings.cs` — no change expected
- **DELETE** `src/openMob/Views/Controls/StatusBannerView.xaml` + `.xaml.cs`
- **DELETE** `src/openMob.Core/Models/StatusBannerInfo.cs`
- **DELETE** `src/openMob.Core/Models/StatusBannerType.cs`

### Technical Dependencies

- `IOpencodeConnectionManager.IsServerReachableAsync()` — already exists; used by `ReconnectingModalViewModel` for health probes
- `IOpencodeConnectionManager.GetActiveServerNameAsync()` — new method to add; reads `ServerConnection.Name` via `IServerConnectionRepository.GetActiveAsync()`
- `IServerConnectionRepository.GetActiveAsync()` — already exists; returns `ServerConnectionDto` which has `Name` field
- `INavigationService.GoToAsync("///server-management")` — already exists; used by `ReconnectingModalViewModel`
- `IAppPopupService.ShowReconnectingModalAsync(...)` — new method; MAUI implementation uses `IPopupService.Current.PushAsync`
- `TimeProvider` — already used in `SplashViewModel`; inject into `HeartbeatMonitorService` for testability
- `ServerHeartbeatEvent` — currently falls to `UnknownEvent` in `ChatEventParser`; must add explicit `"server.heartbeat"` case returning a new `ServerHeartbeatEvent` type, OR handle it in `ChatViewModel`'s `UnknownEvent` case by checking `e.RawType == "server.heartbeat"`. **Decision: handle in `ChatViewModel` switch via `UnknownEvent` check to avoid touching `ChatEventParser` and its test coverage.** This is the minimal-risk approach.

### Technical Risks

- **Grid row index shift**: Removing Row 2 (`StatusBannerView`) shifts `SubagentIndicatorView` from Row 4 → Row 3 and the error/suggestion area from Row 5 → Row 4. The new `ConnectionFooterView` occupies a new Row 4 (appended at bottom). The `chat-session-loading-indicator` spec (todo) references specific row numbers — its implementation must be coordinated.
- **Non-dismissible modal**: UXDivers `PopupPage` supports `CloseWhenBackgroundIsClicked = false` and Android back-button override. Must verify both platforms.
- **Reconnection modal lifecycle**: When user navigates to `ServerManagementPage` from the modal, the modal must be dismissed first (or the navigation must happen from within the modal's ViewModel). The `ReconnectingModalViewModel` calls `_popupService.PopPopupAsync()` then `_navigationService.GoToAsync("///server-management")`. On return to ChatPage, `OnAppearing` re-evaluates the health state and re-shows the modal if still `Lost`.
- **`server.heartbeat` exclusion from fallback card**: The `sse-unhandled-message-fallback-card` spec is already done. The current `HandleUnknownEvent` creates a fallback card for ALL unknown events. The heartbeat case must be intercepted **before** `HandleUnknownEvent` is called. Solution: in the `UnknownEvent` case in `ChatViewModel`'s switch, check `e.RawType == "server.heartbeat"` first and call `OnHeartbeatReceived()` instead of `HandleUnknownEvent(e)`.
- **Thread safety**: `LastHeartbeatAt` is written from the SSE background thread and read from the `PeriodicTimer` callback. Use `volatile DateTime` or `Interlocked`-based pattern.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/heartbeat-monitor-footer`
2. **[om-mobile-core]** Implement `ConnectionHealthState` enum, `IHeartbeatMonitorService` + `HeartbeatMonitorService`, `ReconnectingModalViewModel`, extend `IOpencodeConnectionManager` + `OpencodeConnectionManager`, extend `IAppPopupService`, update `ChatViewModel`
3. ⟳ **[om-mobile-ui]** Implement `ConnectionFooterView`, `ReconnectingModalSheet`, update `ChatPage.xaml`, update `MauiPopupService`, delete `StatusBannerView` — can start layout/styles immediately; wire bindings after step 2 publishes ViewModel surface
4. **[om-tester]** Write unit tests for `HeartbeatMonitorService` and `ReconnectingModalViewModel` — after step 2 completes
5. **[om-reviewer]** Full review against spec — after steps 2, 3, 4 complete
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-013]` requirements implemented
- [ ] All `[AC-001]` through `[AC-010]` acceptance criteria satisfied
- [ ] `StatusBannerView`, `StatusBannerInfo`, `StatusBannerType` fully deleted — zero compilation errors
- [ ] Unit tests written for `HeartbeatMonitorService` and `ReconnectingModalViewModel`
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
