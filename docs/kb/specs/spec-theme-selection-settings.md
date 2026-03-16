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
- New `SettingsViewModel` in `openMob.Core` with bindable `SelectedThemeLabel` property and `ApplyThemeCommand`
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

1. **[REQ-001]** `AppThemePreference` enum in `openMob.Core` with values `Light`, `Dark`, `System`.
2. **[REQ-002]** `IThemeService` interface in `openMob.Core` — `GetTheme()` and `SetThemeAsync(preference, ct)`.
3. **[REQ-003]** `MauiThemeService` persists via `Preferences.Default` with key `"app_theme_preference"`.
4. **[REQ-004]** `SetThemeAsync` applies theme immediately via `Application.Current.UserAppTheme`.
5. **[REQ-005]** `App.xaml.cs` applies persisted theme in `CreateWindow` before `new Window(shell)`.
6. **[REQ-006]** `SettingsViewModel` exposes `SelectedThemeLabel` (string) and `ApplyThemeCommand` (IAsyncRelayCommand<AppThemePreference>).
7. **[REQ-007]** `SettingsPage` bound via `x:DataType` compiled bindings.
8. **[REQ-008]** Appearance row right-hand label shows `SelectedThemeLabel`.
9. **[REQ-009]** Tapping Appearance row shows action sheet: "Light", "Dark", "Follow System", "Cancel".
10. **[REQ-010]** Selecting an option invokes `ApplyThemeCommand` with the corresponding `AppThemePreference`.
11. **[REQ-011]** Cancel leaves theme unchanged.

---

## Acceptance Criteria

- [x] **[AC-001]** Tap Appearance → action sheet with "Light", "Dark", "Follow System", "Cancel"
- [x] **[AC-002]** Select "Dark" → theme switches immediately
- [x] **[AC-003]** Preference persists across app restarts
- [x] **[AC-004]** "Follow System" follows OS theme
- [x] **[AC-005]** Right-hand label shows correct current value
- [x] **[AC-006]** Cancel → theme and label unchanged
- [x] **[AC-007]** Fresh install defaults to "System"
- [x] **[AC-008]** `SettingsViewModel` unit-testable with mocked `IThemeService` — 11/11 tests passed

---

## Files Delivered

| File | Action |
|------|--------|
| `src/openMob.Core/Infrastructure/Settings/AppThemePreference.cs` | Created |
| `src/openMob.Core/Infrastructure/Settings/IThemeService.cs` | Created |
| `src/openMob.Core/ViewModels/SettingsViewModel.cs` | Created |
| `src/openMob/Infrastructure/Settings/MauiThemeService.cs` | Created |
| `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` | Created |
| `src/openMob/App.xaml.cs` | Modified |
| `src/openMob/MauiProgram.cs` | Modified |
| `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Modified |
| `src/openMob/Views/Pages/SettingsPage.xaml` | Modified |
| `src/openMob/Views/Pages/SettingsPage.xaml.cs` | Modified |
