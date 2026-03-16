# Server Management UI

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

This spec introduces a dedicated Server Management UI accessible from the Settings page. Users can view all saved server connections, add new ones, edit or delete existing ones, and set the active server. When the active server changes, the app reconnects automatically. A built-in mDNS scan lets users discover opencode servers on the local network without manual URL entry.

---

## Scope

### In Scope
- Navigation from the existing "Server Connection" row in `SettingsPage` to a new `ServerManagementPage`
- `ServerManagementPage`: list of all saved servers with a clear visual indicator for the active one
- `ServerManagementPage`: "+" button to add a new server (navigates to `ServerDetailPage` in Add mode)
- `ServerManagementPage`: tap on a row navigates to `ServerDetailPage` in Edit mode
- `ServerManagementPage`: "Scan for servers" button triggering mDNS discovery via `IOpencodeDiscoveryService`
- `ServerManagementPage`: "Discovered" section showing mDNS results; tap pre-populates `ServerDetailPage` in Add mode
- `ServerDetailPage`: simplified form with **Name**, **URL** (http/https, host, port extracted automatically), **Password** (optional, obscured)
- `ServerDetailPage`: **"Save"** button — validates inputs, persists via `IServerConnectionRepository`, stores password in `IServerCredentialStore`
- `ServerDetailPage`: **"Test Connection"** button — calls `IOpencodeApiClient.GetHealthAsync`, shows inline result
- `ServerDetailPage`: **"Set as Active"** button — calls `SetActiveAsync`, triggers `IOpencodeConnectionManager.IsServerReachableAsync` for immediate reconnection
- `ServerDetailPage`: **"Delete"** button (Edit mode only) — shows confirmation dialog, calls `DeleteAsync`
- Two new ViewModels in `openMob.Core`: `ServerManagementViewModel`, `ServerDetailViewModel`
- Two new Pages in `openMob`: `ServerManagementPage`, `ServerDetailPage`
- Shell route registration and DI registration for new pages and ViewModels

### Out of Scope
- Activating a server directly from the list row (without entering the detail page)
- Swipe-to-delete from the list
- Real-time online/offline status indicator in the server list
- Managing AI providers or API keys
- Editing the active server's connection status banner (handled by existing `StatusBannerView`)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The "Server Connection" row in `SettingsPage` must navigate to `ServerManagementPage` when tapped. Navigation uses `INavigationService.GoToAsync("server-management")`. A `NavigateToServerManagementCommand` must be added to `SettingsViewModel`.

2. **[REQ-002]** `ServerManagementPage` must load and display all server connections by calling `IServerConnectionRepository.GetAllAsync()` on page appearance. Each row must show: display name, host:port, and a visual active indicator (e.g. a filled checkmark icon or accent-coloured badge) for the server whose `IsActive` is `true`.

3. **[REQ-003]** `ServerManagementPage` must show an empty-state message (e.g. "No servers configured yet") when the list is empty.

4. **[REQ-004]** `ServerManagementPage` must include a "+" button in the navigation bar or as a floating action element. Tapping it navigates to `ServerDetailPage` with no pre-populated data (Add mode).

5. **[REQ-005]** Tapping a server row in `ServerManagementPage` navigates to `ServerDetailPage` passing the server's `Id` as a navigation parameter. `ServerDetailPage` loads the full record via `IServerConnectionRepository.GetByIdAsync(id)` and pre-populates the form (Edit mode).

6. **[REQ-006]** `ServerManagementPage` must include a **"Scan for servers"** button. When tapped, it calls `IOpencodeDiscoveryService.ScanAsync()` and displays results in a dedicated "Discovered on network" section below the saved list. An `ActivityIndicator` is shown while scanning is in progress. If no servers are found after the scan completes, the section shows "No servers found on the local network."

7. **[REQ-007]** Tapping a discovered server in the "Discovered" section navigates to `ServerDetailPage` in Add mode, pre-populating **Name** and **URL** from the `DiscoveredServerDto` (URL reconstructed as `http://{Host}:{Port}`). The **Password** field is left empty.

8. **[REQ-008]** `ServerDetailPage` must expose a form with the following fields:
   - **Name** — required, free text, max 100 characters
   - **URL** — required, text input with `Keyboard.Url`, placeholder `http://192.168.1.10:4096`
   - **Password** — optional, `IsPassword=True`, placeholder "Leave empty if not required"

9. **[REQ-009]** The **"Save"** button in `ServerDetailPage` must:
   - Validate that **Name** is not empty and **URL** is a valid absolute URI with scheme `http` or `https`
   - Show an inline validation error message if validation fails (no navigation away)
   - On success in **Add mode**: call `IServerConnectionRepository.AddAsync()`, then `IServerCredentialStore.SavePasswordAsync()` if a password was entered
   - On success in **Edit mode**: call `IServerConnectionRepository.UpdateAsync()`, then update or delete the credential in `IServerCredentialStore` based on whether the password field is filled
   - After a successful save, pop back to `ServerManagementPage` and refresh the list

10. **[REQ-010]** The URL field value must be parsed to extract `Host`, `Port`, and `UseHttps` before persisting. The `Name` field is stored as-is. The `Username` field on the entity is set to `"opencode"` when a password is provided, and `null` when no password is provided, consistent with the onboarding flow.

11. **[REQ-011]** The **"Test Connection"** button in `ServerDetailPage` must:
    - Be enabled only when the URL field contains a non-empty value
    - Call `IOpencodeApiClient.GetHealthAsync()` using a 10-second timeout
    - Show an `ActivityIndicator` while the test is running
    - Display an inline status row with a coloured dot (green = success, red = failure) and a message (e.g. "Connected — server v1.2.3" or "Connection failed: \<reason\>")
    - Not save or modify any persisted data

12. **[REQ-012]** The **"Set as Active"** button in `ServerDetailPage` must:
    - Be visible in both Add and Edit mode, but enabled only after a successful **Save** (i.e. the record exists in the DB)
    - Call `IServerConnectionRepository.SetActiveAsync(id)`
    - Immediately call `IOpencodeConnectionManager.IsServerReachableAsync()` to trigger a reconnection attempt and update `ConnectionStatus`
    - Show a brief success/failure feedback inline (e.g. "Now active — server reachable" or "Set as active — server unreachable")
    - After activation, pop back to `ServerManagementPage` so the list reflects the new active server

13. **[REQ-013]** The **"Delete"** button must be visible only in Edit mode. When tapped:
    - Show a confirmation dialog via `IAppPopupService` (title: "Delete Server", message: "Are you sure you want to remove this server? This action cannot be undone.")
    - On confirmation: call `IServerConnectionRepository.DeleteAsync(id)` (which also removes credentials from `IServerCredentialStore` per the existing repository contract)
    - If the deleted server was the active one, no server is left active (the app shows a disconnected state)
    - Pop back to `ServerManagementPage` and refresh the list

14. **[REQ-014]** `ServerManagementPage` must refresh its list whenever it re-appears (i.e. on `OnAppearing` / `LoadCommand`) to reflect changes made in `ServerDetailPage`.

15. **[REQ-015]** All new ViewModels must follow the existing MVVM conventions: `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` / `[AsyncRelayCommand]`, constructor injection only, zero MAUI dependencies.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SettingsPage.xaml` | Modified | Add `TapGestureRecognizer` on "Server Connection" row, bind to new command |
| `SettingsViewModel.cs` | Modified | Add `NavigateToServerManagementCommand`, inject `INavigationService` |
| `AppShell.xaml` | Modified | Register routes `server-management` and `server-detail` |
| `MauiProgram.cs` | Modified | Register `ServerManagementPage`, `ServerDetailPage`, `ServerManagementViewModel`, `ServerDetailViewModel` |
| `CoreServiceExtensions.cs` | Modified | Register `ServerManagementViewModel` and `ServerDetailViewModel` as Transient |
| `IOpencodeConnectionManager` | Used (read/write) | `IsServerReachableAsync()` called on activation to trigger reconnection |
| `IServerConnectionRepository` | Used (read/write) | All CRUD operations and `SetActiveAsync` |
| `IServerCredentialStore` | Used (read/write) | Save/update/delete password on Save and Delete |
| `IOpencodeDiscoveryService` | Used (read) | `ScanAsync()` called from `ServerManagementViewModel` |
| `IOpencodeApiClient` | Used (read) | `GetHealthAsync()` called from `ServerDetailViewModel` |

### New Files
| File | Layer | Notes |
|------|-------|-------|
| `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` | Core | List, scan, navigation |
| `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` | Core | Form, save, test, activate, delete |
| `src/openMob.Core/Infrastructure/Helpers/ServerUrlHelper.cs` | Core | Static URL parse helper extracted from OnboardingViewModel |
| `src/openMob/Views/Pages/ServerManagementPage.xaml` + `.cs` | MAUI | List page |
| `src/openMob/Views/Pages/ServerDetailPage.xaml` + `.cs` | MAUI | Detail/form page |

### Dependencies
- `IServerConnectionRepository` — already implemented (spec `2026-03-15-01`)
- `IServerCredentialStore` — already implemented (spec `2026-03-15-01`)
- `IOpencodeConnectionManager` — already implemented (spec `2026-03-15-02`)
- `IOpencodeDiscoveryService` — already implemented (spec `2026-03-15-03`)
- `IOpencodeApiClient` — already implemented (spec `2026-03-15-02`)
- `INavigationService` — already implemented
- `IAppPopupService` — already implemented

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should "Set as Active" be available before saving in Add mode? | Resolved | No — the button is enabled only after a successful Save (record must exist in DB). |
| 2 | What happens to the active connection state if the user deletes the active server? | Resolved | No server is left active; the app enters a disconnected state. No automatic fallback to another server. |
| 3 | Should the password field in Edit mode show a placeholder indicating a password is already saved? | Resolved | Yes — if `ServerConnectionDto.HasPassword` is `true`, the placeholder reads "Password saved — leave empty to keep unchanged". If the user types a new value, it replaces the stored one. If the field is cleared and saved, the credential is deleted. |
| 4 | Should mDNS-discovered servers already in the saved list be filtered out of the "Discovered" section? | Resolved | Yes — deduplicate by `Host:Port` against the saved list to avoid confusion. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the user is on the Settings page, when they tap "Server Connection", then `ServerManagementPage` is pushed onto the navigation stack. *(REQ-001)*

- [ ] **[AC-002]** Given at least one server is saved, when `ServerManagementPage` appears, then the list is populated and the active server row displays a distinct visual indicator. *(REQ-002)*

- [ ] **[AC-003]** Given no servers are saved, when `ServerManagementPage` appears, then an empty-state message is shown. *(REQ-003)*

- [ ] **[AC-004]** Given the user taps "+", when `ServerDetailPage` opens, then all form fields are empty and the "Delete" button is not visible. *(REQ-004)*

- [ ] **[AC-005]** Given the user taps an existing server row, when `ServerDetailPage` opens, then Name and URL are pre-populated, the "Delete" button is visible, and if `HasPassword` is true the password placeholder reads "Password saved — leave empty to keep unchanged". *(REQ-005, REQ-008)*

- [ ] **[AC-006]** Given the user enters a valid Name and URL and taps "Save" in Add mode, then a new `ServerConnection` is persisted, the password (if provided) is stored in `IServerCredentialStore`, and the user is returned to the updated list. *(REQ-009, REQ-010)*

- [ ] **[AC-007]** Given the user enters an invalid URL and taps "Save", then an inline validation error is shown and no navigation occurs. *(REQ-009)*

- [ ] **[AC-008]** Given the user taps "Test Connection" with a valid URL, then an `ActivityIndicator` appears, and within 10 seconds an inline status message with a coloured dot is shown. *(REQ-011)*

- [ ] **[AC-009]** Given the user has saved a server and taps "Set as Active", then `SetActiveAsync` is called, `IsServerReachableAsync` is triggered, and on returning to `ServerManagementPage` the activated server shows the active indicator. *(REQ-012)*

- [ ] **[AC-010]** Given the user taps "Delete" and confirms, then the server is removed from the list, its credential is deleted from `IServerCredentialStore`, and if it was the active server no other server becomes active automatically. *(REQ-013)*

- [ ] **[AC-011]** Given the user taps "Scan for servers", then an `ActivityIndicator` is shown, discovered servers appear in the "Discovered" section, and servers already in the saved list are not shown in the discovered section. *(REQ-006, REQ-007)*

- [ ] **[AC-012]** Given the user taps a discovered server, when `ServerDetailPage` opens in Add mode, then Name and URL are pre-populated from the discovery result. *(REQ-007)*

- [ ] **[AC-013]** Given the user returns to `ServerManagementPage` after any add/edit/delete/activate action, then the list reflects the latest state from the repository. *(REQ-014)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Foundational layer is complete:** `IServerConnectionRepository`, `IServerCredentialStore`, `IOpencodeConnectionManager`, `IOpencodeDiscoveryService`, and `IOpencodeApiClient` are all implemented and registered. This spec is purely UI + ViewModel work — no new Core infrastructure is needed.

- **Navigation pattern:** Follow the existing push navigation pattern used by `ProjectDetailPage`. Register routes in `AppShell.xaml` as `ShellContent` with `FlyoutItemIsVisible="False"`. Use `INavigationService.GoToAsync("server-detail", parameters)` to pass the server ID. Use `[QueryProperty]` on `ServerDetailPage.xaml.cs` to receive the parameter, then call `ViewModel.InitialiseAsync(id)`.

- **SettingsViewModel change:** `SettingsViewModel` currently only injects `IThemeService`. Adding `INavigationService` is required for `NavigateToServerManagementCommand`. Follow the same constructor injection pattern.

- **URL parsing:** Reuse the exact URL parsing logic already present in `OnboardingViewModel.TestConnectionAsync` (lines 233–247): `Uri.TryCreate`, extract `Host`, `Port`, `UseHttps`. Do not duplicate — consider extracting to a static helper method in `openMob.Core.Helpers` (e.g. `ServerUrlHelper.TryParse`).

- **Password field edit mode behaviour:** In Edit mode, if `HasPassword` is `true`, the password `Entry` must be empty (never pre-filled with the actual password — it is never read back from `IServerCredentialStore` for display). On Save: if the field is non-empty → `SavePasswordAsync` (overwrite); if the field is empty and `HasPassword` was `true` → `DeletePasswordAsync`; if the field is empty and `HasPassword` was `false` → no-op.

- **mDNS scan in ViewModel:** `IOpencodeDiscoveryService.ScanAsync()` returns `IAsyncEnumerable<DiscoveredServerDto>`. The ViewModel must iterate it with `await foreach` and add items to an `ObservableCollection<DiscoveredServerDto>` progressively. Deduplication against the saved list must be done by `(Host, Port)` key. The scan runs for up to 10 seconds (internal timeout in `OpencodeDiscoveryService`).

- **Reconnection on activation:** After `SetActiveAsync`, call `IOpencodeConnectionManager.IsServerReachableAsync()`. This internally calls `GET /global/health` on the new active server and updates `ConnectionStatus` via `StatusChanged` event. The existing `StatusBannerView` (already subscribed to this event) will automatically reflect the new state — no additional wiring needed in the new ViewModels.

- **DI lifetimes:** Register `ServerManagementViewModel` and `ServerDetailViewModel` as `Transient` in `CoreServiceExtensions.AddOpenMobCore()`, consistent with all other ViewModels. Register `ServerManagementPage` and `ServerDetailPage` as `Transient` in `MauiProgram.cs`.

- **Test coverage targets:** `ServerManagementViewModel` — `LoadServersCommand` (empty list, populated list), `ScanCommand` (results found, no results, deduplication). `ServerDetailViewModel` — `SaveCommand` (add success, edit success, validation failure — empty name, invalid URL), `TestConnectionCommand` (success, failure, timeout), `SetActiveCommand` (success, server unreachable), `DeleteCommand` (confirmed, cancelled).

- **Related existing files:**
  - `src/openMob.Core/ViewModels/OnboardingViewModel.cs` — reference for URL parsing and connection test pattern
  - `src/openMob.Core/ViewModels/SettingsViewModel.cs` — file to modify
  - `src/openMob/Views/Pages/SettingsPage.xaml` — file to modify
  - `src/openMob/AppShell.xaml` — file to modify
  - `src/openMob/MauiProgram.cs` — file to modify
  - `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — file to modify
  - `src/openMob.Core/Infrastructure/Discovery/DiscoveredServerDto.cs` — DTO used in scan results
  - `src/openMob.Core/Infrastructure/Http/IOpencodeConnectionManager.cs` — `IsServerReachableAsync` and `StatusChanged`

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-16

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/server-management-ui |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | No new services — all infrastructure already exists |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| Helper utilities | om-mobile-core | `src/openMob.Core/Infrastructure/Helpers/` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/` |
| Styles / Theme | om-mobile-ui | Reuse existing ResourceDictionary tokens only |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/` |
| Code Review | om-reviewer | All of the above |

### Files to Create

- `src/openMob.Core/Infrastructure/Helpers/ServerUrlHelper.cs` — static helper with `TryParse(string url, out string host, out int port, out bool useHttps)` extracted from `OnboardingViewModel`
- `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` — list, scan, navigation commands
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — form state, save, test, activate, delete commands
- `src/openMob/Views/Pages/ServerManagementPage.xaml` — list page XAML
- `src/openMob/Views/Pages/ServerManagementPage.xaml.cs` — code-behind with `OnAppearing` → `LoadCommand`
- `src/openMob/Views/Pages/ServerDetailPage.xaml` — detail/form page XAML
- `src/openMob/Views/Pages/ServerDetailPage.xaml.cs` — code-behind with `[QueryProperty]` for `serverId`
- `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs`

### Files to Modify

- `src/openMob.Core/ViewModels/SettingsViewModel.cs` — add `INavigationService` injection + `NavigateToServerManagementCommand`
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — register `ServerManagementViewModel`, `ServerDetailViewModel` as Transient
- `src/openMob/AppShell.xaml` — add `ShellContent` for `server-management` and `server-detail` routes with `FlyoutItemIsVisible="False"`
- `src/openMob/MauiProgram.cs` — register `ServerManagementPage` and `ServerDetailPage` as Transient
- `src/openMob/Views/Pages/SettingsPage.xaml` — add `TapGestureRecognizer` on "Server Connection" row bound to `NavigateToServerManagementCommand`
- `tests/openMob.Tests/ViewModels/SettingsViewModelTests.cs` — add tests for new `NavigateToServerManagementCommand`

### Technical Dependencies

- `IServerConnectionRepository` — `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `SetActiveAsync` — all already implemented
- `IServerCredentialStore` — `SavePasswordAsync`, `DeletePasswordAsync` — already implemented
- `IOpencodeConnectionManager` — `IsServerReachableAsync()` — already implemented
- `IOpencodeDiscoveryService` — `ScanAsync()` returning `IAsyncEnumerable<DiscoveredServerDto>` — already implemented
- `IOpencodeApiClient` — `GetHealthAsync()` — already implemented
- `INavigationService` — `GoToAsync(route)`, `GoToAsync(route, parameters)`, `PopAsync()` — already implemented
- `IAppPopupService` — `ShowConfirmDeleteAsync()` — already implemented
- No new NuGet packages required
- No EF Core migrations required (no new entities)

### Technical Risks

- **`IAppPopupService` delete confirmation:** The spec calls for a generic confirmation dialog (not `ShowConfirmDeleteAsync` which has a fixed "Delete" button label). Reviewing `IAppPopupService` — `ShowConfirmDeleteAsync(title, message)` is the correct method to use; its signature matches the spec's requirement exactly.
- **`IOpencodeApiClient` is Transient:** `ServerDetailViewModel` will receive its own `IOpencodeApiClient` instance. The `GetHealthAsync` call in `TestConnectionCommand` must use a locally-created `CancellationTokenSource` with a 10-second timeout linked to the command's `CancellationToken`, matching the pattern in `OnboardingViewModel`.
- **`IServerConnectionRepository` is Scoped:** ViewModels are Transient; they receive a Scoped repository. This is consistent with all other ViewModels in the project (e.g. `ProjectsViewModel`, `OnboardingViewModel`). No lifetime mismatch issue.
- **mDNS scan deduplication:** The `ScanAsync` `await foreach` loop must check `(Host, Port)` against the already-loaded `Servers` collection before adding to `DiscoveredServers`. The `Servers` collection must be loaded before `ScanCommand` is invoked (guaranteed by `LoadCommand` running on `OnAppearing`).
- **`ServerDetailViewModel.InitialiseAsync`:** Must handle the case where `id` is `null` or empty (Add mode) vs. a valid ULID (Edit mode). The `IsEditMode` flag drives button visibility. The `SavedServerId` property (set after a successful Add) enables the `SetActiveCommand`.
- **URL reconstruction for Edit mode:** In Edit mode, the URL field must be reconstructed from `Host`, `Port`, and `UseHttps` stored in the DTO (e.g. `http://192.168.1.10:4096`). This is the reverse of `ServerUrlHelper.TryParse`.
- **`SettingsViewModelTests` update:** Adding `INavigationService` to `SettingsViewModel`'s constructor will break the existing `SettingsViewModelTests` (which constructs `SettingsViewModel` with only `IThemeService`). The test file must be updated to inject a mocked `INavigationService`.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/server-management-ui`
2. **[om-mobile-core]** Implement `ServerUrlHelper`, `ServerManagementViewModel`, `ServerDetailViewModel`; modify `SettingsViewModel`, `CoreServiceExtensions`
3. ⟳ **[om-mobile-ui]** Implement `ServerManagementPage`, `ServerDetailPage` XAML; modify `AppShell.xaml`, `MauiProgram.cs`, `SettingsPage.xaml` — can start on layout/structure immediately; data bindings require ViewModel binding surface from step 2
4. **[om-tester]** Write unit tests for `ServerManagementViewModel`, `ServerDetailViewModel`; update `SettingsViewModelTests` — requires step 2 complete
5. **[om-reviewer]** Full review against spec — requires steps 2, 3, 4 complete
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-015]` requirements implemented
- [ ] All `[AC-001]` through `[AC-013]` acceptance criteria satisfied
- [ ] `ServerUrlHelper.TryParse` extracted and used by both `ServerManagementViewModel` and `ServerDetailViewModel`
- [ ] Unit tests written for `ServerManagementViewModel` and `ServerDetailViewModel`
- [ ] `SettingsViewModelTests` updated for new constructor signature
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
