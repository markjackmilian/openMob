# Technical Analysis — Server Management UI
**Feature slug:** server-management-ui
**Completed:** 2026-03-16
**Branch:** feature/server-management-ui
**Complexity:** Medium

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/server-management-ui |
| Branches from | develop |
| Complexity | Medium |
| Agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

---

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| Helper utilities | om-mobile-core | `src/openMob.Core/Infrastructure/Helpers/` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | All of the above |

---

## Files Created

- `src/openMob.Core/Infrastructure/Helpers/ServerUrlHelper.cs` — `TryParse` + `BuildUrl` static helpers
- `src/openMob.Core/ViewModels/ServerManagementViewModel.cs`
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs`
- `src/openMob/Views/Pages/ServerManagementPage.xaml` + `.xaml.cs`
- `src/openMob/Views/Pages/ServerDetailPage.xaml` + `.xaml.cs`
- `tests/openMob.Tests/Infrastructure/Helpers/ServerUrlHelperTests.cs`
- `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs`

## Files Modified

- `src/openMob.Core/ViewModels/SettingsViewModel.cs` — added `INavigationService` + `NavigateToServerManagementCommand`
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — Transient registration for new ViewModels
- `src/openMob/AppShell.xaml.cs` — `Routing.RegisterRoute` for push navigation
- `src/openMob/MauiProgram.cs` — Transient registration for new Pages
- `src/openMob/Views/Pages/SettingsPage.xaml` — `TapGestureRecognizer` on Server Connection row
- `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` — updated for new constructor

---

## Key Technical Decisions

### 1. Navigation: Routing.RegisterRoute, not ShellContent
Push-navigation pages must use `Routing.RegisterRoute()` in `AppShell.xaml.cs`.
`<ShellContent>` creates root Shell pages requiring `///` absolute navigation — using relative routes on them causes a runtime crash.
See: `adr-shell-push-navigation-routing.md`

### 2. Test Connection: Direct HTTP Probe via IHttpClientFactory
`TestConnectionCommand` uses `IHttpClientFactory.CreateClient("opencode")` to call `GET {formUrl}/global/health` directly.
Does NOT use `IOpencodeApiClient.GetHealthAsync()` which probes the active server, not the form URL.
See: `adr-test-connection-direct-http-probe.md`

### 3. ServerUrlHelper Extraction
URL parsing logic extracted from `OnboardingViewModel` into `ServerUrlHelper.TryParse` (static, `openMob.Core.Infrastructure.Helpers`).
`ServerUrlHelper.BuildUrl` added for the reverse (Edit mode URL reconstruction from Host/Port/UseHttps).

### 4. DeleteCommand CanExecute Guard
`[RelayCommand(CanExecute = nameof(IsEditMode))]` + `[NotifyCanExecuteChangedFor(nameof(DeleteCommand))]` on `_isEditMode`.
Provides ICommand-level guard in addition to XAML `IsVisible`, preventing null-dereference on `_savedServerId` in Add mode.

### 5. ServerDetailPage QueryProperty Pattern
Four `[QueryProperty]` attributes: `serverId`, `discoveredHost`, `discoveredPort`, `discoveredName`.
`OnAppearing` calls `InitialiseAsync` only if not already initialised via a query property (flag `_initialised`).

---

## Technical Dependencies

| Dependency | Usage |
|-----------|-------|
| `IServerConnectionRepository` | GetAllAsync, GetByIdAsync, AddAsync, UpdateAsync, DeleteAsync, SetActiveAsync |
| `IServerCredentialStore` | SavePasswordAsync, DeletePasswordAsync |
| `IOpencodeConnectionManager` | IsServerReachableAsync() — called after SetActive |
| `IOpencodeDiscoveryService` | ScanAsync() — ServerManagementViewModel only |
| `IHttpClientFactory` | Direct health probe in TestConnectionCommand — ServerDetailViewModel only |
| `INavigationService` | GoToAsync, PopAsync |
| `IAppPopupService` | ShowConfirmDeleteAsync |

No new NuGet packages. No EF Core migrations.

---

## Test Coverage

| File | Tests | Key paths |
|------|-------|-----------|
| `ServerUrlHelperTests` | 13 | TryParse (valid/invalid/null/ftp/relative), BuildUrl (default/non-default ports) |
| `ServerManagementViewModelTests` | 12 | Constructor guards, LoadCommand, ScanCommand (results/empty/dedup/diff-port), navigation commands |
| `ServerDetailViewModelTests` | 38+ | Constructor guards, InitialiseAsync (add/edit/discovered/password-placeholder), SaveCommand (validation/add/edit/credential-handling), TestConnectionCommand (reachable/unhealthy/HTTP-error/timeout/network-error/invalid-url), SetActiveCommand, DeleteCommand (cancel/confirm/CanExecute) |
| `SettingsViewModelTests` | +2 | NavigateToServerManagementCommand, null guard for INavigationService |

**Total: 348 tests, all passing.**
