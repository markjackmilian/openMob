# ADR: Theme Service — Core Interface + MAUI Implementation Pattern

## Date
2026-03-16

## Status
Accepted

## Context
The app needed a theme selection feature (Light / Dark / System) that:
1. Persists the user's preference across sessions
2. Applies the theme immediately at runtime via `Application.Current.UserAppTheme`
3. Applies the persisted theme at startup before the first frame is rendered
4. Is unit-testable without MAUI platform dependencies

The challenge: `Application.Current.UserAppTheme` and `Preferences` are MAUI APIs that cannot be referenced from `openMob.Core` (a pure .NET class library with zero MAUI dependencies). The `SettingsViewModel` must live in `openMob.Core` for testability, but it needs to trigger theme changes.

## Decision
Introduce `IThemeService` in `openMob.Core` with pure BCL types only, and implement it as `MauiThemeService` in the `openMob` MAUI project. This follows the same pattern already established by `IOpencodeSettingsService` / `MauiOpencodeSettingsService`.

- `IThemeService` (Core): `GetTheme()` + `SetThemeAsync(AppThemePreference, CancellationToken)`
- `MauiThemeService` (MAUI): `Preferences.Default` for persistence + `MainThread.InvokeOnMainThreadAsync` for safe `UserAppTheme` assignment
- `App.xaml.cs` receives `IThemeService` as a constructor parameter and applies the persisted theme in `CreateWindow` (not the constructor) before `new Window(shell)`
- `IThemeService` registered as Singleton in `MauiProgram.cs` before `AddOpenMobCore()`

## Rationale
- Keeps `openMob.Core` free of MAUI dependencies — testable with NSubstitute
- Consistent with the existing `IOpencodeSettingsService` pattern — no new architectural concept introduced
- Singleton lifetime is correct: theme preference is global app state, not per-request
- Applying theme in `CreateWindow` (not constructor) avoids the risk of `Application.Current` not being fully initialised at construction time

## Alternatives Considered
- **Read `Preferences` directly in `App.xaml.cs` at startup**: Would work for startup but bypasses the service abstraction, making the startup logic untestable and duplicating the persistence key.
- **Store theme in `SecureStorage`**: Overkill — theme preference is not sensitive data. `Preferences` is the correct storage for non-sensitive user settings.
- **Apply theme in `App` constructor**: Risky — `Application.Current` may not be fully initialised. `CreateWindow` is the safe point.

## Consequences
### Positive
- `SettingsViewModel` is fully unit-testable with a mocked `IThemeService`
- Theme logic is centralised in one service — easy to extend (e.g. add per-session theme in future)
- Consistent with existing settings service pattern — low cognitive overhead for future developers

### Negative / Trade-offs
- `App.xaml.cs` constructor now has two parameters (`IServiceProvider`, `IThemeService`) — slightly more complex than a single-parameter constructor
- `MauiThemeService` must dispatch to the main thread explicitly — adds a small amount of boilerplate

## Related Features
theme-selection-settings

## Related Agents
om-mobile-core (implements the pattern), om-tester (tests via interface mock), om-mobile-ui (consumes ViewModel binding surface)
