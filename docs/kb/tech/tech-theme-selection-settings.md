# Technical Analysis — Theme Selection — Settings Page

**Feature slug:** theme-selection-settings
**Completed:** 2026-03-16
**Branch:** feature/theme-selection-settings (merged into develop)
**Complexity:** Low

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/theme-selection-settings |
| Branches from | develop |
| Estimated complexity | Low |
| Agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

---

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Enum + Interface | om-mobile-core | `src/openMob.Core/Infrastructure/Settings/` |
| ViewModel | om-mobile-core | `src/openMob.Core/ViewModels/SettingsViewModel.cs` |
| DI registration (Core) | om-mobile-core | `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` |
| MAUI service implementation | om-mobile-core | `src/openMob/Infrastructure/Settings/MauiThemeService.cs` |
| App startup wiring | om-mobile-core | `src/openMob/App.xaml.cs` |
| DI registration (MAUI) | om-mobile-core | `src/openMob/MauiProgram.cs` |
| XAML View | om-mobile-ui | `src/openMob/Views/Pages/SettingsPage.xaml` |
| Code-behind (action sheet) | om-mobile-ui | `src/openMob/Views/Pages/SettingsPage.xaml.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

---

## Key Technical Decisions

### 1. `AppThemePreference.System = 0` as default
The `System` value is assigned integer value `0` so that `Preferences.Default.Get(key, 0)` returns `System` on a fresh install with no stored preference. This satisfies AC-007 without any special-case logic.

### 2. Main-thread dispatch in `MauiThemeService`
`Application.Current.UserAppTheme` must be set on the main thread. `MauiThemeService.SetThemeAsync` uses `MainThread.InvokeOnMainThreadAsync` with `ConfigureAwait(false)` to guarantee safe dispatch from any calling context (including background threads from `IAsyncRelayCommand`).

### 3. Theme applied in `CreateWindow`, not constructor
`App.xaml.cs` applies the persisted theme in `CreateWindow` (not the constructor) because `Application.Current` may not be fully initialised at constructor time. Inside `App` (which IS `Application`), `UserAppTheme` is accessed as a direct property (`this.UserAppTheme`), not via `Application.Current`.

### 4. Action sheet in code-behind, not ViewModel
`DisplayActionSheet` is a `Page` method — it cannot be called from `openMob.Core` ViewModels (which have zero MAUI dependencies). The code-behind `OnAppearanceTapped` handler calls `DisplayActionSheet`, maps the string result to `AppThemePreference`, then calls `ViewModel.ApplyThemeCommand.ExecuteAsync(preference)`. This is the correct MVVM pattern for platform-native UI interactions.

### 5. `async void` event handler is acceptable
`TapGestureRecognizer.Tapped` fires a standard .NET event. The handler must be `async void` — this is the one documented exception to the "no async void" rule in AGENTS.md. All exceptions are caught internally and reported via `SentryHelper.CaptureException`.

### 6. `IThemeService` registered before `AddOpenMobCore()`
`IThemeService` is registered as Singleton in `MauiProgram.cs` before `AddOpenMobCore()` is called. This ensures the service is available when `SettingsViewModel` (registered inside `AddOpenMobCore`) is resolved, and when `App` is resolved by the MAUI host.

---

## Files Created

| File | Purpose |
|------|---------|
| `src/openMob.Core/Infrastructure/Settings/AppThemePreference.cs` | Enum: `Light=1`, `Dark=2`, `System=0` |
| `src/openMob.Core/Infrastructure/Settings/IThemeService.cs` | Core interface — pure BCL, zero MAUI deps |
| `src/openMob.Core/ViewModels/SettingsViewModel.cs` | ViewModel with `[ObservableProperty]` + `[RelayCommand]` |
| `src/openMob/Infrastructure/Settings/MauiThemeService.cs` | MAUI impl: `Preferences` + `MainThread.InvokeOnMainThreadAsync` |
| `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` | 11 unit tests — NSubstitute + FluentAssertions |

## Files Modified

| File | Change |
|------|--------|
| `src/openMob/App.xaml.cs` | Added `IThemeService` ctor param; apply theme in `CreateWindow` |
| `src/openMob/MauiProgram.cs` | Register `IThemeService` as Singleton |
| `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Add `SettingsViewModel` as Transient |
| `src/openMob/Views/Pages/SettingsPage.xaml` | `x:DataType`, `TapGestureRecognizer`, `SelectedThemeLabel` binding |
| `src/openMob/Views/Pages/SettingsPage.xaml.cs` | DI-injected ViewModel, action sheet handler |

---

## Technical Risks Encountered

- None materialised. All risks identified in the Technical Analysis were mitigated by the implementation choices above.

---

## Review Findings Summary

| ID | Severity | Description | Resolution |
|----|----------|-------------|------------|
| m-001 | 🟡 Minor | Pre-existing `BoxView` separator pattern (not introduced by this feature) | No action — pre-existing |
| m-002 | 🟡 Minor | `ConfigureAwait(false)` convention inconsistency in ViewModels (pre-existing) | No action — pre-existing |
| m-003 | 🟡 Minor | `Debug.WriteLine` in catch block → replaced with `SentryHelper.CaptureException` | Fixed before merge |

**Final verdict:** ⚠️ Approved with remarks — zero Critical, zero Major.
