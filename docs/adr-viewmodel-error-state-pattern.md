# ADR: ViewModel Error State Pattern for Async Load Operations

## Date
2026-03-19

## Status
Accepted

## Context
When a ViewModel's load command fails (repository throws, network error, etc.), the app needs to communicate the failure to the user. Before this decision, exceptions were silently swallowed via `SentryHelper.CaptureException` only, with no user-visible feedback. This caused two symptoms:

1. Pages bound to `IsLoading` via `InvertedBoolConverter` could become permanently blank if `IsLoading` got stuck, or show a misleading empty state instead of an error.
2. Destructive operations (delete) that failed silently left the user confused — the spinner disappeared but nothing happened.

## Decision

**For async load operations** (`LoadAsync`, `RefreshAsync`, etc.) in ViewModels:

1. Expose a `string? LoadError` observable property (default `null`).
2. At the start of the command, set `LoadError = null` to clear any previous error.
3. In the `catch` block, set `LoadError` to a human-readable message (e.g. `"Could not load X. Please try again."`). Also call `SentryHelper.CaptureException`.
4. In the XAML page, **never bind `ScrollView.IsVisible` to `IsLoading`**. The `ScrollView` is always visible. The `ActivityIndicator` overlay handles the loading indicator.
5. Inside the `ScrollView`, use two sibling `VerticalStackLayout` elements:
   - Error state: `IsVisible="{Binding LoadError, Converter={StaticResource NullToVisibilityConverter}}"` — contains error `Label` + Retry `Button` bound to `LoadCommand`.
   - Normal content: `IsVisible="{Binding LoadError, Converter={StaticResource NullToVisibilityConverter}, ConverterParameter=Invert}"` — contains the existing page content.

**For async destructive operations** (`DeleteAsync`, `RemoveAsync`, etc.) in ViewModels:

1. Navigation (`PopAsync`, `GoToAsync`) must be inside the `try` block, **after** the repository call succeeds.
2. The `catch` block must set an existing error property (e.g. `ValidationError`) to a user-readable message: `$"Delete failed: {ex.Message}"`. Also call `SentryHelper.CaptureException`.
3. The `finally` block is responsible **only** for resetting the busy flag (e.g. `IsDeleting = false`). Never put navigation in `finally`.

## Rationale

- **Blank screen prevention:** Decoupling `ScrollView` visibility from `IsLoading` eliminates the entire class of blank-screen bugs caused by stuck loading state.
- **User feedback:** Setting an error property ensures the user always sees a meaningful message instead of an empty list or a spinner that never stops.
- **Retry affordance:** Binding a Retry button to `LoadCommand` and clearing `LoadError` at the start of each call gives users a clear recovery path without restarting the app.
- **Navigation safety:** Keeping `PopAsync` inside `try` (not `finally`) ensures that if navigation throws, the error is caught and shown. Running code after `await PopAsync` in `finally` is unreliable on some MAUI versions because the page's dispatcher context may be gone.

## Alternatives Considered

- **`IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}"` on `ScrollView`:** The existing pattern before this fix. Rejected because it creates a blank-screen risk when `IsLoading` is stuck and shows a misleading empty state on load error.
- **Separate `IsError` boolean property instead of `string? LoadError`:** Rejected because a string property carries the message directly, eliminating the need for a separate `ErrorMessage` property. The `NullToVisibilityConverter` handles the visibility logic cleanly.
- **`Toast` or `Snackbar` for error display:** Rejected for load errors because they are transient and disappear, leaving the user with a blank page and no retry affordance. Inline error state is persistent and actionable.

## Consequences

### Positive
- Pages can never be blank due to stuck `IsLoading` state.
- Users always see a meaningful error message and a Retry button when load fails.
- Destructive operation failures are surfaced inline, not silently swallowed.
- Pattern is consistent and reusable across all pages with async load commands.

### Negative / Trade-offs
- Each page with a load command requires a `LoadError` property in the ViewModel and an error state panel in the XAML. This is a small amount of boilerplate per page.
- The XAML structure becomes slightly more nested (extra wrapping `VerticalStackLayout`).

## Implementation Reference

First applied in: `bugfix/server-delete-navigation` (2026-03-19)

**Affected files:**
- `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` — `LoadError` property + `LoadAsync` pattern
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — `DeleteAsync` navigation-in-try pattern
- `src/openMob/Views/Pages/ServerManagementPage.xaml` — error state panel + content wrapper

**Converter used:** `NullToVisibilityConverter` (already in `App.xaml`) with `ConverterParameter="Invert"` for the inverted case.

## Related Features
server-delete-navigation-bugfix

## Related Agents
om-mobile-core (ViewModel implementation), om-mobile-ui (XAML error state panel)
