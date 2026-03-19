# Server Delete Navigation & List Blank Screen Bugfix

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-17                   |
| Status  | In Progress                  |
| Version | 1.0                          |

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

> Requirements are numbered for traceability.

1. **[REQ-001]** After the user confirms deletion in `ServerDetailPage`, the app must call `DeleteAsync` on the repository, then immediately call `_navigationService.PopAsync` to return to `ServerManagementPage`. The `IsDeleting` flag must be reset to `false` only after navigation completes (or after an error is shown — see REQ-002).

2. **[REQ-002]** If `DeleteAsync` throws an exception (repository error, network error, etc.), the app must **not** navigate away. Instead, it must display an inline error message on `ServerDetailPage` (reusing the existing `ValidationError` observable property) and reset `IsDeleting = false`. The error message must read: `"Delete failed: <exception message>"`.

3. **[REQ-003]** The `finally` block in `ServerDetailViewModel.DeleteAsync` must **not** call `PopAsync`. Navigation must happen inside the `try` block, after a successful delete, before the `finally` runs. The `finally` block is responsible only for resetting `IsDeleting = false`.

4. **[REQ-004]** `ServerManagementViewModel` must expose a new observable property `LoadError` (type `string?`, default `null`). When `LoadAsync` catches an exception, it must set `LoadError` to a human-readable message (e.g. `"Could not load servers. Please try again."`) instead of only logging to Sentry. `LoadError` must be reset to `null` at the start of each `LoadAsync` call.

5. **[REQ-005]** `ServerManagementPage.xaml` must display an error state when `LoadError` is non-null. The error state must be visible in place of the server list (i.e. the `ScrollView` must remain visible — only its content changes). The error view must show the `LoadError` message and a "Retry" button bound to `LoadCommand`.

6. **[REQ-006]** The `ScrollView` visibility in `ServerManagementPage.xaml` must **not** be bound to `IsLoading`. The `ScrollView` must always be visible; only the `ActivityIndicator` overlay is shown/hidden based on `IsLoading`. This ensures that when `IsLoading` returns to `false` after an error, the page is never blank.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ServerDetailViewModel.cs` | Modified | Fix `DeleteAsync` try/finally structure; add error message on failure |
| `ServerManagementViewModel.cs` | Modified | Add `LoadError` observable property; set it on exception in `LoadAsync` |
| `ServerManagementPage.xaml` | Modified | Remove `IsVisible` binding from `ScrollView`; add error state view with Retry button |

### Dependencies
- `IServerConnectionRepository.DeleteAsync` — already implemented; no changes needed
- `INavigationService.PopAsync` — already implemented; no changes needed
- `IAppPopupService` — not involved in this fix
- `ValidationError` property on `ServerDetailViewModel` — already exists; reused for delete error message

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the delete error use `ValidationError` (existing property) or a new `DeleteError` property? | Resolved | Reuse `ValidationError` — it is already wired to the error label in the XAML and avoids adding a new property. |
| 2 | Should `IsDeleting` be reset before or after showing the error? | Resolved | Reset in `finally` after the `try` block — the `try` either navigates away (success) or sets `ValidationError` (failure). The `finally` always resets `IsDeleting = false`. |
| 3 | Should the `ScrollView` in `ServerManagementPage` be always visible or conditionally visible? | Resolved | Always visible. The `ActivityIndicator` overlay handles the loading state. The `ScrollView` shows either the list, the empty state, or the error state. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the user taps "Delete Server" and confirms, when the repository delete succeeds, then the app navigates back to `ServerManagementPage` and the deleted server is no longer in the list. *(REQ-001, REQ-003)*

- [ ] **[AC-002]** Given the user taps "Delete Server" and confirms, when the repository delete throws an exception, then the app stays on `ServerDetailPage`, `IsDeleting` is reset to `false`, and an inline error message is shown (e.g. "Delete failed: …"). *(REQ-002, REQ-003)*

- [ ] **[AC-003]** Given `ServerManagementPage` appears and `LoadAsync` throws an exception, then `IsLoading` returns to `false`, `LoadError` is set to a non-null message, and the page shows the error message and a "Retry" button — never a blank white screen. *(REQ-004, REQ-005, REQ-006)*

- [ ] **[AC-004]** Given the user taps "Retry" on the error state in `ServerManagementPage`, then `LoadCommand` is re-executed, `LoadError` is cleared, and the server list is reloaded. *(REQ-004, REQ-005)*

- [ ] **[AC-005]** Given `LoadAsync` succeeds, then `LoadError` is `null` and the server list is visible as before. *(REQ-004)*

- [ ] **[AC-006]** Given `LoadAsync` is in progress, then the `ActivityIndicator` overlay is visible and the `ScrollView` is also visible (not hidden). *(REQ-006)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Bug 1 — `ServerDetailViewModel.DeleteAsync` wrong try/finally structure

**Current code (lines 479–507):**
```csharp
[RelayCommand(CanExecute = nameof(IsEditMode))]
private async Task DeleteAsync(CancellationToken ct)
{
    IsDeleting = true;
    try
    {
        var confirmed = await _popupService.ShowConfirmDeleteAsync(...);
        if (!confirmed)
            return;                                          // ← finally runs here, IsDeleting = false ✓
        await _serverConnectionRepository.DeleteAsync(...);
        await _navigationService.PopAsync(ct);              // ← if this throws, finally runs on wrong context
    }
    catch (Exception ex)
    {
        SentryHelper.CaptureException(ex, ...);             // ← swallows exception silently, no user feedback
    }
    finally
    {
        IsDeleting = false;                                  // ← runs AFTER PopAsync, may cause threading issue
    }
}
```

**Root cause:** `PopAsync` calls `Shell.Current.GoToAsync("..", true)` which is a MAUI UI operation. When `finally` runs after `PopAsync` completes, the page is already off the stack. On some MAUI versions/platforms, the continuation after `await PopAsync` may not execute reliably because the page's dispatcher context is gone. Additionally, exceptions from `DeleteAsync` are silently swallowed with no user-visible feedback.

**Required fix:**
```csharp
[RelayCommand(CanExecute = nameof(IsEditMode))]
private async Task DeleteAsync(CancellationToken ct)
{
    IsDeleting = true;
    try
    {
        var confirmed = await _popupService.ShowConfirmDeleteAsync(...);
        if (!confirmed)
            return;

        await _serverConnectionRepository.DeleteAsync(_savedServerId!, ct).ConfigureAwait(false);
        // Navigate BEFORE finally — page is still alive at this point
        await _navigationService.PopAsync(ct).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        // Stay on page, show error to user
        ValidationError = $"Delete failed: {ex.Message}";
        SentryHelper.CaptureException(ex, new Dictionary<string, object>
        {
            ["context"] = "ServerDetailViewModel.DeleteCommand",
        });
    }
    finally
    {
        // Always reset the busy flag — page may or may not still be visible
        IsDeleting = false;
    }
}
```

**Note:** `PopAsync` must remain inside the `try` block (not in `finally`) so that if it throws, the error is caught and shown. The `finally` only resets `IsDeleting`.

---

### Bug 2 — `ServerManagementPage` blank screen

**Current code — ViewModel (`LoadAsync`, lines 85–107):**
```csharp
catch (Exception ex)
{
    SentryHelper.CaptureException(ex, ...);   // ← exception swallowed, IsLoading reset in finally ✓
}
finally
{
    IsLoading = false;                         // ← IsLoading correctly reset
}
```

**Current code — XAML (`ServerManagementPage.xaml`, line 29):**
```xml
<ScrollView IsVisible="{Binding IsLoading, Converter={StaticResource InvertedBoolConverter}}">
```

**Root cause:** When `LoadAsync` throws, `IsLoading` is correctly reset to `false` in `finally`. However, `Servers` remains an empty `ObservableCollection` (never populated). The `CollectionView.EmptyView` shows "No servers configured yet" — which is misleading when the real cause is a load error. More critically, if `IsLoading` gets stuck at `true` for any reason (e.g. a re-entrant call or a platform-level cancellation before `finally` runs), the `ScrollView` is hidden and the page is blank.

**Required fix — ViewModel:** Add `LoadError` property; set it on exception; clear it at start of `LoadAsync`:
```csharp
[ObservableProperty]
private string? _loadError;

[RelayCommand]
private async Task LoadAsync(CancellationToken ct)
{
    IsLoading = true;
    LoadError = null;   // ← clear previous error
    try
    {
        var servers = await _serverConnectionRepository.GetAllAsync(ct).ConfigureAwait(false);
        Servers = new ObservableCollection<ServerConnectionDto>(servers);
        DiscoveredServers = [];
        ScanCompleted = false;
    }
    catch (Exception ex)
    {
        LoadError = "Could not load servers. Please try again.";
        SentryHelper.CaptureException(ex, new Dictionary<string, object>
        {
            ["context"] = "ServerManagementViewModel.LoadAsync",
        });
    }
    finally
    {
        IsLoading = false;
    }
}
```

**Required fix — XAML:** Remove `IsVisible` from `ScrollView`; add error state inside the `VerticalStackLayout`:
```xml
<!-- Remove IsVisible binding from ScrollView — always visible -->
<ScrollView>
  <VerticalStackLayout ...>

    <!-- Error state — shown when LoadError is non-null -->
    <VerticalStackLayout
        IsVisible="{Binding LoadError, Converter={StaticResource NullToVisibilityConverter}}"
        Spacing="{StaticResource SpacingMd}"
        Margin="0,32,0,0"
        HorizontalOptions="Center">
      <Label
          Text="{Binding LoadError, FallbackValue='', TargetNullValue=''}"
          Style="{StaticResource CalloutLabel}"
          HorizontalTextAlignment="Center" />
      <Button
          Text="Retry"
          Command="{Binding LoadCommand}"
          Style="{StaticResource SecondaryButton}"
          HorizontalOptions="Center" />
    </VerticalStackLayout>

    <!-- Existing saved servers section — hide when error is shown -->
    <Label
        Text="SAVED SERVERS"
        ...
        IsVisible="{Binding LoadError, Converter={StaticResource NullToVisibilityConverter}, ConverterParameter=Invert}" />
    ...
  </VerticalStackLayout>
</ScrollView>
```

**Alternative simpler approach:** Keep the `ScrollView` always visible and wrap the existing content in a `VerticalStackLayout` that is hidden when `LoadError` is non-null, with the error state shown instead. This avoids touching every child element's `IsVisible`.

---

### Related files to modify
- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — `DeleteAsync` method (lines 479–507)
- `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` — `LoadAsync` method (lines 85–107); add `LoadError` property
- `src/openMob/Views/Pages/ServerManagementPage.xaml` — remove `IsVisible` from `ScrollView`; add error state view

### Test coverage targets
- `ServerDetailViewModelTests` — add: `DeleteCommand_WhenRepositoryThrows_SetsValidationErrorAndDoesNotNavigate`
- `ServerDetailViewModelTests` — update: `DeleteCommand_WhenConfirmed_NavigatesBack` (verify `PopAsync` called, `IsDeleting` reset)
- `ServerManagementViewModelTests` — add: `LoadCommand_WhenRepositoryThrows_SetsLoadError`
- `ServerManagementViewModelTests` — add: `LoadCommand_WhenCalledAfterError_ClearsLoadError`

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
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/ServerDetailViewModel.cs`, `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` |
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ServerManagementPage.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ServerDetailViewModelTests.cs`, `tests/openMob.Tests/ViewModels/ServerManagementViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

_None — this is a pure bug fix; no new files required._

### Files to Modify

- `src/openMob.Core/ViewModels/ServerDetailViewModel.cs` — fix `DeleteAsync` catch block to set `ValidationError` and remove silent swallow; `PopAsync` stays in `try`, `finally` only resets `IsDeleting`
- `src/openMob.Core/ViewModels/ServerManagementViewModel.cs` — add `[ObservableProperty] private string? _loadError;`; set `LoadError = null` at start of `LoadAsync`; set `LoadError = "Could not load servers. Please try again."` in catch block
- `src/openMob/Views/Pages/ServerManagementPage.xaml` — remove `IsVisible` binding from `<ScrollView>`; add error state `<VerticalStackLayout>` (with `Label` + Retry `Button`) bound to `LoadError`; wrap existing content in a `VerticalStackLayout` with `IsVisible` bound to `LoadError` inverted

### Technical Dependencies

- `NullToVisibilityConverter` — already registered in `App.xaml` as `{StaticResource NullToVisibilityConverter}`; supports `ConverterParameter="Invert"` for the inverted case — **no new converter needed**
- `ValidationError` observable property — already exists on `ServerDetailViewModel`; already wired to the error label in `ServerDetailPage.xaml`
- `INavigationService.PopAsync` — already implemented; no changes needed
- `IServerConnectionRepository.DeleteAsync` — already implemented; no changes needed

### Technical Risks

- **MAUI dispatcher context after PopAsync:** The root cause of Bug 1 is that code running after `await PopAsync` may not execute reliably on some MAUI versions because the page is off the stack. The fix (moving `PopAsync` inside `try`, keeping `finally` only for `IsDeleting = false`) is the correct mitigation. No platform-specific conditionals are needed.
- **No new migrations or interface changes** — this fix touches only method bodies and XAML; no breaking changes to public interfaces.
- **`NullToVisibilityConverter` with `ConverterParameter="Invert"`** — confirmed working pattern already used in the project (e.g. `ServerDetailPage.xaml` uses `NullToVisibilityConverter` without invert; the converter supports the `"Invert"` parameter per its implementation).

### Execution Order

> Steps that can run in parallel are marked with ⟳.

1. [Git Flow] Create branch `bugfix/server-delete-navigation`
2. [om-mobile-core] Fix `ServerDetailViewModel.DeleteAsync` and `ServerManagementViewModel.LoadAsync` / add `LoadError` property
3. ⟳ [om-mobile-ui] Fix `ServerManagementPage.xaml` (can start immediately — no new ViewModel binding surface needed; `LoadError` property name is known from spec)
4. [om-tester] Add/update unit tests for `ServerDetailViewModel` and `ServerManagementViewModel`
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge into develop

### Definition of Done

- [x] All `[REQ-001]` through `[REQ-006]` requirements implemented
- [x] All `[AC-001]` through `[AC-006]` acceptance criteria satisfied
- [x] Unit tests written: `DeleteCommand_WhenRepositoryThrows_SetsValidationErrorAndDoesNotNavigate`, `LoadCommand_WhenRepositoryThrows_SetsLoadError`, `LoadCommand_WhenCalledAfterError_ClearsLoadError`
- [x] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [x] Git Flow branch finished and deleted
- [x] Spec moved to `specs/done/` with Completed status
