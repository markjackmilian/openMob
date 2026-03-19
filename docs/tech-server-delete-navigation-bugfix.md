# Technical Analysis — Server Delete Navigation & List Blank Screen Bugfix

**Feature slug:** server-delete-navigation-bugfix
**Completed:** 2026-03-19
**Branch:** bugfix/server-delete-navigation (merged into develop)
**Complexity:** Low

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/server-delete-navigation |
| Branches from | develop |
| Estimated complexity | Low |
| Agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ServerDetailViewModel.cs`, `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ServerManagementPage.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs`, `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

## Files Created

_None — pure bug fix; no new files._

## Files Modified

| File | Change summary |
|------|---------------|
| `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` | `DeleteAsync` catch block: added `ValidationError = $"Delete failed: {ex.Message}";` before `SentryHelper` call |
| `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` | Added `[ObservableProperty] private string? _loadError;`; `LoadAsync`: added `LoadError = null` before try, `LoadError = "Could not load servers. Please try again."` in catch |
| `src/openMob/Views/Pages/ServerManagementPage.xaml` | Removed `IsVisible` from `<ScrollView>`; added error state `<VerticalStackLayout>` (Label + Retry Button) bound to `LoadError`; wrapped existing content in `<VerticalStackLayout IsVisible="{Binding LoadError, ..., ConverterParameter=Invert}">` |
| `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs` | Added `DeleteCommand_WhenRepositoryThrows_SetsValidationErrorAndDoesNotNavigate`; added `ValidationError.Should().BeNull()` to success-path test |
| `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs` | Added `LoadCommand_WhenRepositoryThrows_SetsLoadError` and `LoadCommand_WhenCalledAfterError_ClearsLoadError` |

## Root Cause Analysis

### Bug 1 — `ServerDetailViewModel.DeleteAsync` silent exception swallow

The `catch` block called only `SentryHelper.CaptureException` with no user-visible feedback. `PopAsync` was already correctly inside the `try` block (not in `finally`), so the navigation structure was sound. The only missing piece was setting `ValidationError` in the catch block so the user sees the error instead of nothing happening.

**Fix:** One line added to the catch block: `ValidationError = $"Delete failed: {ex.Message}";`

### Bug 2 — `ServerManagementPage` blank screen on load error

`ScrollView` had `IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}"`. This meant:
- While loading: `ScrollView` hidden, `ActivityIndicator` shown ✓
- After successful load: `ScrollView` shown ✓
- After failed load: `ScrollView` shown, but `Servers` is empty → `CollectionView.EmptyView` shows "No servers configured yet" (misleading)
- If `IsLoading` ever stuck at `true`: `ScrollView` permanently hidden → blank page ✗

**Fix:** `ScrollView` is always visible. A `LoadError` property drives an error state panel. The existing content is wrapped in a `VerticalStackLayout` that hides when `LoadError` is non-null.

## Technical Dependencies

- `NullToVisibilityConverter` — already registered in `App.xaml`; supports `ConverterParameter="Invert"` — **no new converter needed**
- `ValidationError` — already existed on `ServerDetailViewModel`; already wired to error label in `ServerDetailPage.xaml`
- `INavigationService.PopAsync` — no changes
- `IServerConnectionRepository.DeleteAsync` — no changes

## Technical Risks Encountered

None. No migrations, no interface changes, no breaking changes to public APIs.

## Execution Order (actual)

1. ✅ Git Flow: created `bugfix/server-delete-navigation` from `develop`
2. ✅ om-mobile-core: fixed both ViewModels — build clean
3. ✅ om-mobile-ui: fixed XAML — build clean (parallel with core)
4. ✅ om-tester: added 3 new tests + 2 assertion improvements — 56/56 pass
5. ✅ om-reviewer: ⚠️ Approved with remarks (0 Critical, 0 Major, 2 Minor)
6. ✅ Minor [m-001] addressed: added `ValidationError.Should().BeNull()` to success-path test
7. ✅ Git Flow: merged into `develop`, branch deleted

## Review Findings Summary

| ID | Severity | Description | Resolution |
|----|----------|-------------|------------|
| m-001 | 🟡 Minor | Success-path test missing `ValidationError.Should().BeNull()` assertion | Fixed — assertion added |
| m-002 | 🟡 Minor | `_ = LoadCommand.ExecuteAsync(null)` fire-and-forget in `OnAppearing` | Advisory — consistent with other pages, no change needed |
