# Spec: Heartbeat Monitor Footer with Reconnection Flow

**Slug:** heartbeat-monitor-footer
**Completed:** 2026-03-31
**Branch:** feature/heartbeat-monitor-footer → develop

---

## Executive Summary

The server sends periodic `server.heartbeat` SSE events that were previously unhandled, producing visible warning cards in the chat UI. This feature silently consumes heartbeat events, drives a persistent traffic-light footer at the bottom of ChatPage showing the active server name and connection health, and triggers an automatic reconnection modal when heartbeats stop arriving. The existing `StatusBannerView` and its associated `NoProvider` logic are removed entirely.

---

## Requirements (summary)

| ID | Requirement |
|----|-------------|
| REQ-001 | `server.heartbeat` SSE events produce no chat card |
| REQ-002 | Periodic 5-second health check via `IHeartbeatMonitorService` |
| REQ-003 | Traffic-light: Healthy (0–30s), Degraded (30–60s), Lost (>60s) |
| REQ-004 | `ConnectionFooterView` always visible at bottom of ChatPage |
| REQ-005 | `StatusBannerView`, `StatusBannerInfo`, `StatusBannerType` fully removed |
| REQ-006 | Non-dismissible reconnection modal on Lost state |
| REQ-007 | Exponential backoff: 5s, 10s, 20s, max 3 per cycle |
| REQ-008 | Modal closes automatically on reconnection success |
| REQ-009 | "Gestisci server" navigates to ServerManagementPage with back navigation |
| REQ-010 | Modal reappears on return from ServerManagementPage if still Lost |
| REQ-011 | Any heartbeat immediately resets state to Healthy |
| REQ-012 | AppThemeBinding colour tokens throughout |
| REQ-013 | ConnectionFooterView is the last row in ChatPage Grid |

---

## Key Implementation Decisions

### Navigation route for "Gestisci server"
Use `"server-management-push"` (registered via `Routing.RegisterRoute` in `AppShell.xaml.cs`) instead of `"///server-management"` (ShellContent root). Triple-slash resets the Shell stack — no back navigation possible. See `adr-server-management-push-route.md`.

### Shell.Current.GoToAsync must run on main thread
`MauiNavigationService` wraps all `GoToAsync`/`PopAsync` calls in `MainThread.InvokeOnMainThreadAsync`. Core ViewModels use `ConfigureAwait(false)` — continuations may resume on thread pool threads. See `adr-shell-navigation-main-thread.md`.

### Heartbeat monitor lifecycle
The monitor runs continuously for the lifetime of `ChatViewModel`. `OnDisappearing` does NOT stop it — stopping on child-page navigation loses health state. On `OnAppearing`, if state is already `Lost`, the modal is shown immediately without waiting for the next 5s tick.

### SSE restart on reconnection
`ReconnectionSucceeded` handler calls `StartSseSubscriptionAsync()` if a session is active. `IsServerReachableAsync` only confirms HTTP reachability — the SSE stream must be explicitly restarted. See `adr-sse-restart-on-reconnection.md`.

### Double-modal guard
`_isReconnectingModalVisible` (volatile bool) prevents pushing a second modal if one is already visible. `ModalDismissedForNavigation` event on `ReconnectingModalViewModel` resets the flag when the user taps "Gestisci server".

---

## Files Created
- `src/openMob.Core/Models/ConnectionHealthState.cs`
- `src/openMob.Core/Services/IHeartbeatMonitorService.cs`
- `src/openMob.Core/Services/HeartbeatMonitorService.cs`
- `src/openMob.Core/ViewModels/ReconnectingModalViewModel.cs`
- `src/openMob/Views/Controls/ConnectionFooterView.xaml` + `.xaml.cs`
- `src/openMob/Views/Popups/ReconnectingModalSheet.xaml` + `.xaml.cs`
- `tests/openMob.Tests/Services/HeartbeatMonitorServiceTests.cs`
- `tests/openMob.Tests/ViewModels/ReconnectingModalViewModelTests.cs`

## Files Modified
- `src/openMob.Core/Infrastructure/Http/IOpencodeConnectionManager.cs` — added `GetActiveServerNameAsync`
- `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs`
- `src/openMob.Core/Services/IAppPopupService.cs` — added `ShowReconnectingModalAsync`
- `src/openMob.Core/ViewModels/ChatViewModel.cs`
- `src/openMob/AppShell.xaml.cs` — registered `server-management-push` route
- `src/openMob/Services/MauiNavigationService.cs` — main thread marshalling
- `src/openMob/Services/MauiPopupService.cs`
- `src/openMob/Views/Pages/ChatPage.xaml` + `.xaml.cs`
- `src/openMob/MauiProgram.cs`

## Files Deleted
- `src/openMob/Views/Controls/StatusBannerView.xaml` + `.xaml.cs`
- `src/openMob.Core/Models/StatusBannerInfo.cs`
- `src/openMob.Core/Models/StatusBannerType.cs`
