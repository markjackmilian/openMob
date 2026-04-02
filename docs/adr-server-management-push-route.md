# ADR: Register server-management-push as a Separate Push Route for Back Navigation

## Date
2026-03-31

## Status
Accepted

## Context
`ServerManagementPage` is declared as a `ShellContent` root in `AppShell.xaml` with route `"server-management"`. Navigating to it with `"///server-management"` (triple-slash absolute route) resets the Shell navigation stack entirely. There is no back entry — pressing the Android back button exits the app, and iOS has no native back button.

The reconnection modal needs to send the user to `ServerManagementPage` and allow them to return to `ChatPage` after fixing the server. The existing `"///server-management"` route used by `SettingsViewModel` is intentional for that context (Settings → Server Management with no back to Settings), but is wrong for the reconnection flow.

## Decision
Register `ServerManagementPage` as an additional push route `"server-management-push"` in `AppShell.xaml.cs`:

```csharp
Routing.RegisterRoute("server-management-push", typeof(ServerManagementPage));
```

`ReconnectingModalViewModel.NavigateToServerManagementAsync` uses `"server-management-push"` instead of `"///server-management"`. `ServerManagementPage` already has a custom back button (`OnBackButtonTapped` → `Shell.Current.GoToAsync("..")`) that works correctly with push navigation on both iOS and Android.

## Rationale
The same page can be registered under multiple routes in MAUI Shell — one as a root (`ShellContent`) and one as a push route (`Routing.RegisterRoute`). This is a supported pattern. The page's existing custom back button (`".."`) works correctly with push navigation. No changes to `ServerManagementPage` itself are required. `SettingsViewModel` continues to use `"///server-management"` unchanged.

## Alternatives Considered
- **Use `"//server-management"` (double-slash)**: Still resets the navigation stack — back navigation does not work.
- **Add a dedicated `ServerManagementFromModalPage`**: A separate page wrapping the same content. Rejected — unnecessary duplication.
- **Navigate back programmatically from `ChatPage.OnAppearing`**: Fragile, depends on navigation stack state.

## Consequences
### Positive
- Back navigation works on both iOS and Android from the reconnection flow.
- No changes to `ServerManagementPage` or its ViewModel.
- `SettingsViewModel` behaviour unchanged.

### Negative / Trade-offs
- Two routes for the same page (`"server-management"` root + `"server-management-push"` push). Future developers must be aware of the distinction when adding navigation to `ServerManagementPage`.

## Related Features
heartbeat-monitor-footer

## Related Agents
om-mobile-ui (AppShell.xaml.cs), om-mobile-core (ReconnectingModalViewModel)
