# ADR: Marshal All Shell Navigation to Main Thread in MauiNavigationService

## Date
2026-03-31

## Status
Accepted

## Context
ViewModels in `openMob.Core` use `ConfigureAwait(false)` on all `await` calls, as required by the project's coding standards. This means that after any awaited service call, the continuation may resume on a thread pool thread rather than the UI thread.

`Shell.Current.GoToAsync` is a MAUI API that must be called on the main thread. When called from a thread pool thread on Android, it causes an immediate crash. The issue was discovered when `ReconnectingModalViewModel.NavigateToServerManagementAsync` called `_navigationService.GoToAsync` after `await _popupService.PopPopupAsync(ct).ConfigureAwait(false)` — the continuation resumed on a thread pool thread and the navigation call crashed.

Previously, navigation calls happened to work because they were triggered directly from UI event handlers (tap commands), which run on the main thread. The reconnection modal was the first case where navigation was triggered from a background task chain.

## Decision
Wrap all `Shell.Current.GoToAsync` and `Shell.Current.GoToAsync("..")` calls in `MauiNavigationService` with `MainThread.InvokeOnMainThreadAsync(...)`.

This applies to all three overloads:
- `GoToAsync(string route, CancellationToken ct)`
- `GoToAsync(string route, IDictionary<string, object> parameters, CancellationToken ct)`
- `PopAsync(CancellationToken ct)`

## Rationale
Centralising the main-thread guarantee in `MauiNavigationService` protects all callers automatically, regardless of which thread they call from. This is consistent with the existing pattern in `MauiPopupService`, where `IPopupService.Current.PushAsync` is already wrapped in `MainThread.InvokeOnMainThreadAsync` for the same reason. The alternative — requiring each ViewModel to marshal before calling `INavigationService` — would be error-prone and violates the principle that Core ViewModels should not need to know about MAUI threading constraints.

## Alternatives Considered
- **Require callers to marshal**: Each ViewModel calls `MainThread.InvokeOnMainThreadAsync` before `GoToAsync`. Rejected — Core has no dependency on MAUI; adding `MainThread` calls would violate layer separation.
- **Use `IDispatcherService` in ViewModels**: Inject and dispatch navigation calls. Rejected — adds boilerplate to every ViewModel that navigates.

## Consequences
### Positive
- All navigation is safe to call from any thread, including background tasks and `Task.Run` continuations.
- No changes required in ViewModels — the fix is transparent to all existing callers.

### Negative / Trade-offs
- Slight overhead from `InvokeOnMainThreadAsync` when called from the main thread (no-op in practice, but adds a task allocation).

## Related Features
heartbeat-monitor-footer

## Related Agents
om-mobile-ui (MauiNavigationService), om-mobile-core (all ViewModels that navigate)
