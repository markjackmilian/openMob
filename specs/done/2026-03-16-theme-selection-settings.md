# Theme Selection — Settings Page

## Metadata
| Field       | Value                                    |
|-------------|------------------------------------------|
| Date        | 2026-03-16                               |
| Status      | **Completed**                            |
| Version     | 1.0                                      |
| Completed   | 2026-03-16                               |
| Branch      | feature/theme-selection-settings (merged) |
| Merged into | develop                                  |

---

## Executive Summary

The Settings page currently displays an "Appearance" row but tapping it does nothing — there is no ViewModel, no command, and no theme-switching logic wired up. This feature makes theme selection fully functional: the user can choose between Light, Dark, and System (follow OS) themes via a native action sheet, the change is applied immediately to the running app, and the preference is persisted across sessions using `Preferences`.

---

## Scope

### In Scope
- New `AppThemePreference` enum (`Light`, `Dark`, `System`) in `openMob.Core`
- New `IThemeService` interface in `openMob.Core` with `GetTheme()` and `SetThemeAsync(AppThemePreference)` methods
- New `MauiThemeService` implementation in `openMob` (MAUI project) using `Preferences` for persistence and `Application.Current.UserAppTheme` for immediate application
- New `SettingsViewModel` in `openMob.Core` with bindable `SelectedThemeLabel` property and `SelectThemeCommand`
- Update `SettingsPage.xaml` to bind to `SettingsViewModel`: make the "Appearance" row tappable and show the current theme label on the right
- Update `SettingsPage.xaml.cs` to wire up the action sheet display (platform UI concern, cannot live in Core ViewModel)
- Apply the persisted theme at app startup in `App.xaml.cs` before the UI is shown
- Register `IThemeService`, `MauiThemeService`, and `SettingsViewModel` in `MauiProgram.cs`

### Out of Scope
- Animated transitions during theme switching
- Custom/user-defined colour themes
- Settings rows: Server Connection, Notifications, API Keys (existing stubs, not touched)
- Per-page or per-component theme overrides

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The system must define an `AppThemePreference` enum in `openMob.Core` with three values: `Light`, `Dark`, `System`.
2. **[REQ-002]** The system must expose an `IThemeService` interface in `openMob.Core` with the following members:
   - `AppThemePreference GetTheme()` — returns the currently persisted preference
   - `Task SetThemeAsync(AppThemePreference preference, CancellationToken ct = default)` — persists the preference and applies it immediately
3. **[REQ-003]** `MauiThemeService` must persist the theme preference using `Preferences.Default` with a stable key (e.g. `"app_theme_preference"`).
4. **[REQ-004]** `MauiThemeService.SetThemeAsync` must apply the theme immediately by setting `Application.Current.UserAppTheme` to the corresponding `Microsoft.Maui.ApplicationModel.AppTheme` value (`Light`, `Dark`, or `Unspecified` for System).
5. **[REQ-005]** At app startup, `App.xaml.cs` must read the persisted theme via `IThemeService` and apply it before the main window is created, so the correct theme is active from the first frame.
6. **[REQ-006]** `SettingsViewModel` must expose:
   - A read-only `string SelectedThemeLabel` property (values: `"Light"`, `"Dark"`, `"System"`) reflecting the current preference
   - An `IAsyncRelayCommand<AppThemePreference> ApplyThemeCommand` that calls `IThemeService.SetThemeAsync` and updates `SelectedThemeLabel`
7. **[REQ-007]** `SettingsPage` must be bound to `SettingsViewModel` via `x:DataType` compiled bindings.
8. **[REQ-008]** The "Appearance" row in `SettingsPage` must display `SelectedThemeLabel` in the right-hand label (replacing the hardcoded `"System"` text).
9. **[REQ-009]** Tapping the "Appearance" row must present a native action sheet with three options: "Light", "Dark", "Follow System", plus a "Cancel" option.
10. **[REQ-010]** Selecting an option from the action sheet must invoke `ApplyThemeCommand` with the corresponding `AppThemePreference` value, changing the theme immediately and persisting the choice.
11. **[REQ-011]** If the user dismisses the action sheet without selecting (Cancel), the current theme must remain unchanged.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `openMob.Core/Models` or `openMob.Core/Infrastructure` | **Add** `AppThemePreference` enum | New file |
| `openMob.Core/Infrastructure/Settings/IThemeService.cs` | **Add** new interface | New file |
| `openMob.Core/ViewModels/SettingsViewModel.cs` | **Add** new ViewModel | New file |
| `openMob/Infrastructure/Settings/MauiThemeService.cs` | **Add** MAUI implementation | New file |
| `openMob/App.xaml.cs` | **Modify** `CreateWindow` to apply persisted theme at startup | Requires `IThemeService` injected into `App` |
| `openMob/MauiProgram.cs` | **Modify** to register `IThemeService`, `MauiThemeService`, `SettingsViewModel` | Singleton for service, Transient for ViewModel |
| `openMob/Views/Pages/SettingsPage.xaml` | **Modify** Appearance row: add `TapGestureRecognizer`, bind right-hand label | Compiled binding via `x:DataType` |
| `openMob/Views/Pages/SettingsPage.xaml.cs` | **Modify** to inject `SettingsViewModel`, display action sheet, call command | Action sheet is platform UI — lives in code-behind |

### Dependencies
- `Microsoft.Maui.Storage.Preferences` — already used by `MauiOpencodeSettingsService`, no new NuGet required
- `Application.Current.UserAppTheme` — MAUI built-in, no new NuGet required
- `CommunityToolkit.Mvvm` — already used by all ViewModels, `[ObservableProperty]` and `[AsyncRelayCommand]` available

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should theme change be immediate or require restart? | Resolved | Immediate via `Application.Current.UserAppTheme` |
| 2 | UI for theme selection: native action sheet or custom popup? | Resolved | Native action sheet (simpler, platform-native on iOS/Android) |
| 3 | Should `IThemeService` be injected into `App` for startup application, or should startup read `Preferences` directly? | Resolved | Inject `IThemeService` into `App` for consistency and testability |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Given the user is on the Settings page, when they tap the "Appearance" row, then a native action sheet appears with options "Light", "Dark", "Follow System", and "Cancel". *(REQ-009)*
- [x] **[AC-002]** Given the action sheet is shown, when the user selects "Dark", then the app theme switches to dark immediately without requiring a restart. *(REQ-004, REQ-010)*
- [x] **[AC-003]** Given the user has selected "Light", when they close and reopen the app, then the light theme is still active. *(REQ-003, REQ-005)*
- [x] **[AC-004]** Given the user selects "Follow System", when the OS is in dark mode, then the app displays the dark theme; when the OS switches to light, the app follows. *(REQ-004, REQ-010)*
- [x] **[AC-005]** Given any theme is selected, the right-hand label on the "Appearance" row shows the correct current value ("Light", "Dark", or "System"). *(REQ-008)*
- [x] **[AC-006]** Given the action sheet is shown, when the user taps "Cancel", then the theme and label remain unchanged. *(REQ-011)*
- [x] **[AC-007]** Given a fresh app install with no saved preference, when the app starts, then the theme defaults to "System" (follows OS). *(REQ-003, REQ-005)*
- [x] **[AC-008]** `SettingsViewModel` can be unit-tested with a mocked `IThemeService` — `ApplyThemeCommand` calls `SetThemeAsync` and updates `SelectedThemeLabel`. *(REQ-006)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-16

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/theme-selection-settings |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

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
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

### Files Created

- `src/openMob.Core/Infrastructure/Settings/AppThemePreference.cs` — `AppThemePreference` enum (`Light`, `Dark`, `System`)
- `src/openMob.Core/Infrastructure/Settings/IThemeService.cs` — Core interface with `GetTheme()` and `SetThemeAsync()`
- `src/openMob.Core/ViewModels/SettingsViewModel.cs` — ViewModel with `SelectedThemeLabel` and `ApplyThemeCommand`
- `src/openMob/Infrastructure/Settings/MauiThemeService.cs` — MAUI implementation using `Preferences` + `Application.Current.UserAppTheme`
- `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` — 11 unit tests for `SettingsViewModel`

### Files Modified

- `src/openMob/App.xaml.cs` — `IThemeService` injected; theme applied in `CreateWindow` before `new Window(shell)`
- `src/openMob/MauiProgram.cs` — `IThemeService` registered as Singleton (`MauiThemeService`)
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — `SettingsViewModel` added as Transient
- `src/openMob/Views/Pages/SettingsPage.xaml` — `x:DataType`, `TapGestureRecognizer`, `SelectedThemeLabel` binding
- `src/openMob/Views/Pages/SettingsPage.xaml.cs` — DI-injected ViewModel, action sheet handler

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-011]` requirements implemented
- [x] All `[AC-001]` through `[AC-008]` acceptance criteria satisfied
- [x] Unit tests written for `SettingsViewModel` (happy path, error path, label update) — 11/11 passed
- [x] `om-reviewer` verdict: ⚠️ Approved with remarks (zero Critical, zero Major)
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
