# Server Delete Navigation & List Blank Screen Bugfix

## Metadata
| Field       | Value                                  |
|-------------|----------------------------------------|
| Date        | 2026-03-17                             |
| Status      | **Completed**                          |
| Version     | 1.0                                    |
| Completed   | 2026-03-19                             |
| Branch      | bugfix/server-delete-navigation (merged) |
| Merged into | develop                                |

---

## Executive Summary

Two related bugs affect the server management flow. First, after confirming the deletion of a server in `ServerDetailPage`, the app remains on the detail page instead of navigating back to `ServerManagementPage`. Second, when the user manually navigates back to `ServerManagementPage` after a delete, the page renders blank (white screen) because `IsLoading` is stuck at `true` when `LoadAsync` fails silently. Both bugs are caused by incorrect `try/finally` structure in `ServerDetailViewModel.DeleteAsync` and missing error-state handling in `ServerManagementViewModel.LoadAsync` / `ServerManagementPage`.

---

## Scope

### In Scope
- Fix `ServerDetailViewModel.DeleteAsync`: navigate back **before** resetting `IsDeleting` in `finally`, and surface a visible error message if delete fails
- Fix `ServerManagementViewModel.LoadAsync`: expose a `LoadError` property when the repository call throws, so the UI can show an error message instead of a blank screen
- Fix `ServerManagementPage.xaml`: add an error state view bound to `LoadError`; ensure `IsLoading = false` always results in visible content (either list or error)

### Out of Scope
- Changes to `SaveAsync` or `SetActiveAsync` navigation (those work correctly)
- Changes to the delete confirmation dialog
- Changes to mDNS scan logic
- Any new features or UI redesign

---

## Functional Requirements

1. **[REQ-001]** After the user confirms deletion in `ServerDetailPage`, the app must call `DeleteAsync` on the repository, then immediately call `_navigationService.PopAsync` to return to `ServerManagementPage`. The `IsDeleting` flag must be reset to `false` only after navigation completes (or after an error is shown — see REQ-002).

2. **[REQ-002]** If `DeleteAsync` throws an exception (repository error, network error, etc.), the app must **not** navigate away. Instead, it must display an inline error message on `ServerDetailPage` (reusing the existing `ValidationError` observable property) and reset `IsDeleting = false`. The error message must read: `"Delete failed: <exception message>"`.

3. **[REQ-003]** The `finally` block in `ServerDetailViewModel.DeleteAsync` must **not** call `PopAsync`. Navigation must happen inside the `try` block, after a successful delete, before the `finally` runs. The `finally` block is responsible only for resetting `IsDeleting = false`.

4. **[REQ-004]** `ServerManagementViewModel` must expose a new observable property `LoadError` (type `string?`, default `null`). When `LoadAsync` catches an exception, it must set `LoadError` to a human-readable message (e.g. `"Could not load servers. Please try again."`) instead of only logging to Sentry. `LoadError` must be reset to `null` at the start of each `LoadAsync` call.

5. **[REQ-005]** `ServerManagementPage.xaml` must display an error state when `LoadError` is non-null. The error state must be visible in place of the server list (i.e. the `ScrollView` must remain visible — only its content changes). The error view must show the `LoadError` message and a "Retry" button bound to `LoadCommand`.

6. **[REQ-006]** The `ScrollView` visibility in `ServerManagementPage.xaml` must **not** be bound to `IsLoading`. The `ScrollView` must always be visible; only the `ActivityIndicator` overlay is shown/hidden based on `IsLoading`. This ensures that when `IsLoading` returns to `false` after an error, the page is never blank.

---

## Acceptance Criteria

- [x] **[AC-001]** Given the user taps "Delete Server" and confirms, when the repository delete succeeds, then the app navigates back to `ServerManagementPage` and the deleted server is no longer in the list. *(REQ-001, REQ-003)*

- [x] **[AC-002]** Given the user taps "Delete Server" and confirms, when the repository delete throws an exception, then the app stays on `ServerDetailPage`, `IsDeleting` is reset to `false`, and an inline error message is shown (e.g. "Delete failed: …"). *(REQ-002, REQ-003)*

- [x] **[AC-003]** Given `ServerManagementPage` appears and `LoadAsync` throws an exception, then `IsLoading` returns to `false`, `LoadError` is set to a non-null message, and the page shows the error message and a "Retry" button — never a blank white screen. *(REQ-004, REQ-005, REQ-006)*

- [x] **[AC-004]** Given the user taps "Retry" on the error state in `ServerManagementPage`, then `LoadCommand` is re-executed, `LoadError` is cleared, and the server list is reloaded. *(REQ-004, REQ-005)*

- [x] **[AC-005]** Given `LoadAsync` succeeds, then `LoadError` is `null` and the server list is visible as before. *(REQ-004)*

- [x] **[AC-006]** Given `LoadAsync` is in progress, then the `ActivityIndicator` overlay is visible and the `ScrollView` is also visible (not hidden). *(REQ-006)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-19

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/server-delete-navigation |
| Branches from | develop |
| Estimated complexity | Low |
| Agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Files Modified

| File | Change |
|------|--------|
| `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` | `DeleteAsync` catch block now sets `ValidationError = $"Delete failed: {ex.Message}"` |
| `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` | Added `[ObservableProperty] string? _loadError`; `LoadAsync` clears it at start and sets it on exception |
| `src/openMob/Views/Pages/ServerManagementPage.xaml` | Removed `IsVisible` from `ScrollView`; added error state panel; wrapped existing content with inverted visibility |
| `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs` | Added `DeleteCommand_WhenRepositoryThrows_SetsValidationErrorAndDoesNotNavigate`; strengthened success-path assertions |
| `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs` | Added `LoadCommand_WhenRepositoryThrows_SetsLoadError` and `LoadCommand_WhenCalledAfterError_ClearsLoadError` |

### Root Causes

**Bug 1 — `ServerDetailViewModel.DeleteAsync`:** The `catch` block silently swallowed exceptions via `SentryHelper` only, with no user-visible feedback. `PopAsync` was already correctly inside the `try` block; the only missing piece was setting `ValidationError` in the catch.

**Bug 2 — `ServerManagementPage` blank screen:** `ScrollView` had `IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}"`. If `IsLoading` ever got stuck at `true` (or if the user expected an error message instead of an empty list), the page would be blank. Fix: `ScrollView` is always visible; error state is shown via `LoadError` property.

### Key Technical Decisions

- **Reuse `ValidationError`** (not a new `DeleteError` property) — already wired to the error label in `ServerDetailPage.xaml`.
- **`NullToVisibilityConverter` with `ConverterParameter="Invert"`** — already registered in `App.xaml`; no new converter needed.
- **Wrap-and-toggle XAML pattern** — existing content wrapped in a `VerticalStackLayout` with inverted `LoadError` visibility, rather than adding `IsVisible` to every individual child element.
