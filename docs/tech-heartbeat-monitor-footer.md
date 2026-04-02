# Technical Analysis — Heartbeat Monitor Footer with Reconnection Flow

**Feature slug:** heartbeat-monitor-footer
**Completed:** 2026-03-31
**Branch:** feature/heartbeat-monitor-footer
**Complexity:** High

---

## Architecture

### HeartbeatMonitorService (`IHeartbeatMonitorService`)
- **Lifetime:** Singleton (registered in `CoreServiceExtensions.AddOpenMobCore()`)
- **Timer:** `PeriodicTimer` with 5-second interval
- **Time abstraction:** `TimeProvider` injected (defaults to `TimeProvider.System`) — enables `FakeTimeProvider` in tests
- **Thread safety:** `_lastHeartbeatAtTicks` is `volatile long`; written via `Interlocked.Exchange` (SSE background thread), read via `Interlocked.Read` (timer thread)
- **State transitions:** Fires `event Action<ConnectionHealthState>? HealthStateChanged` only on actual state changes
- **`RecordHeartbeat()`:** Atomically resets timestamp; transitions to `Healthy` immediately if not already

### ReconnectingModalViewModel
- **Lifetime:** Transient — created per `Lost` transition in `ChatViewModel.OnHealthStateChanged`
- **Reconnection loop:** Exponential backoff `[5000, 10000, 20000]` ms, cycles indefinitely until success or cancellation
- **Events:** `ReconnectionSucceeded`, `ModalDismissedForNavigation`
- **Navigation:** Uses `"server-management-push"` push route (not `"///server-management"` root)

### ChatViewModel changes
- `_isReconnectingModalVisible` (`volatile bool`): guards against double-modal push
- `_reconnectionCts`: scopes the reconnection loop; cancelled on `Dispose()`
- `OnHealthStateChanged`: dispatches property update synchronously via `_dispatcher.Dispatch`; runs modal logic via `Task.Run` (not `async void`)
- `StartHeartbeatMonitorAsync`: checks `HealthState` immediately after start — if already `Lost`, triggers modal without waiting for next tick
- `OnDisappearing` does NOT stop the monitor (preserves state across child-page navigation)
- `ReconnectionSucceeded` handler: `RecordHeartbeat()` + `PopPopupAsync()` + `StartSseSubscriptionAsync()`

### MauiNavigationService
- All `GoToAsync`/`PopAsync` overloads wrapped in `MainThread.InvokeOnMainThreadAsync`
- Consistent with `MauiPopupService` pattern for `IPopupService.Current.PushAsync`

### ConnectionFooterView (XAML)
- `Grid` layout: `Auto` (dot) + `*` (server name) — name fills available space
- Top separator: 1px `BoxView` with `ColorOutline`
- Traffic-light via `DataTrigger` on `ConnectionHealthState` — no converter needed
- Dot: 10×10 `Ellipse`, left-aligned, `AppThemeBinding` fill

### AppShell.xaml.cs
- Added `Routing.RegisterRoute("server-management-push", typeof(ServerManagementPage))`
- Separate from `"server-management"` ShellContent root — allows push navigation with back stack

---

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| `server.heartbeat` handled in `ChatViewModel` switch (not `ChatEventParser`) | Minimal-risk; avoids touching parser and its test coverage |
| Monitor never stopped on `OnDisappearing` | Stops only on `ChatViewModel.Dispose()`; preserves state across child-page navigation |
| SSE restarted on reconnection | `IsServerReachableAsync` only confirms HTTP; SSE stream must be explicitly restarted |
| `"server-management-push"` route | Push navigation preserves back stack on both iOS and Android |
| Main thread marshalling in `MauiNavigationService` | Fixes crash when navigation called after `ConfigureAwait(false)` |
| `Task.Run` for modal logic (not `async void` dispatch) | Exceptions are observed; no unhandled exception crash |

---

## Test Coverage

| File | Tests | Coverage |
|------|-------|----------|
| `HeartbeatMonitorServiceTests.cs` | ~20 | State transitions, `RecordHeartbeat`, timer lifecycle, `FakeTimeProvider` |
| `ReconnectingModalViewModelTests.cs` | ~25 | Success path, failure/cancellation, navigation command, `AttemptSummary` |

All reflection-based state manipulation removed; `FakeTimeProvider` + polling used for time-dependent tests.

---

## Related ADRs
- `adr-shell-navigation-main-thread.md`
- `adr-server-management-push-route.md`
- `adr-sse-restart-on-reconnection.md`
