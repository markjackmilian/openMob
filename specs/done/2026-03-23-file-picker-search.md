# File Picker Server-Side Search

## Metadata
| Field       | Value                              |
|-------------|------------------------------------|
| Date        | 2026-03-23                         |
| Status      | **Completed**                      |
| Version     | 1.0                                |
| Completed   | 2026-03-23                         |
| Branch      | feature/file-picker-search (merged)|
| Merged into | develop                            |

---

## Executive Summary

The file picker modal (`FilePickerSheet`), accessible from the message composer, currently shows no files because `LoadFilesCommand` is never triggered on open. This spec fixes the broken modal by wiring the load trigger, replacing the recursive file fetch with a first-level tree call, adding server-side search with debounce, enabling directory navigation, and updating the item template with file/directory icons.

---

## Scope

### In Scope
- Fix missing `LoadFilesCommand` trigger: execute it via `OnNavigatedTo` in `FilePickerSheet.xaml.cs`, consistent with `AgentPickerSheet`, `ModelPickerSheet`, and `CommandPaletteSheet`
- Replace initial load from `GET /file?pattern=**` (full recursive) to `GET /file/tree?path=` (first-level nodes only)
- Directory navigation: tap on a directory node reloads the list with `GET /file/tree?path={relativePath}`; a back button/gesture allows ascending to the parent level up to root
- Replace client-side `SearchText` filter with server-side search: `SearchText` changes trigger `GET /file?pattern=*{text}*` after a 300 ms debounce
- Updated `CollectionView` item template: distinct icon for file vs. directory, `Name` as primary label, `RelativePath` as secondary label
- Updates to `FilePickerViewModel` to support tree navigation state (current path, back stack) and debounced server search
- New or updated method on `IFileService` / `FileService` to call `GetFileTreeAsync(path?)` for tree listing
- Unit test updates / additions for all changed ViewModel and Service logic

### Out of Scope
- File content preview
- Multi-file selection
- Filtering by file type or extension
- Search by symbol (`GET /file/symbol`) or full-text content (`GET /file/text`)
- Swipe-to-delete or any file management action (rename, delete)
- Showing hidden or git-ignored files differently

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `FilePickerSheet.xaml.cs` must override `OnNavigatedTo` and call `BindingContext.LoadFilesCommand.ExecuteAsync(null)`, matching the pattern used by `AgentPickerSheet`, `ModelPickerSheet`, and `CommandPaletteSheet`.

2. **[REQ-002]** On initial load (and whenever the current directory changes), `FilePickerViewModel` must call `IFileService.GetFileTreeAsync(currentPath)` where `currentPath` is `null` for the root. The result is a flat list of `FileNodeDto` items at that level only.

3. **[REQ-003]** `IFileService` must expose a new method `GetFileTreeAsync(string? path, CancellationToken ct)` that calls `IOpencodeApiClient.GetFileTreeAsync(path, ct)` and returns `IReadOnlyList<FileNodeDto>`. The existing `GetFilesAsync` method may be retained or removed — it must not be called by `FilePickerViewModel` anymore.

4. **[REQ-004]** Each item in the `CollectionView` must display:
   - An icon distinguishing file nodes (`Type == "file"`) from directory nodes (`Type == "directory"`). Icons must use existing MAUI font glyph or image resources consistent with the app's design system.
   - `Name` (last path segment) as the primary/title label.
   - `RelativePath` as the secondary/subtitle label, styled in a muted colour.

5. **[REQ-005]** Tapping a directory node must update `CurrentPath` in `FilePickerViewModel` to that node's `RelativePath`, push the previous path onto a back-navigation stack, and re-execute `LoadFilesCommand` to reload the list at the new level.

6. **[REQ-006]** A back button (or equivalent gesture) must be visible whenever the back-navigation stack is non-empty. Activating it pops the stack, restores `CurrentPath` to the previous value, and re-executes `LoadFilesCommand`.

7. **[REQ-007]** When `SearchText` is non-empty, `FilePickerViewModel` must start a 300 ms debounce timer (implemented via `CancellationTokenSource` + `Task.Delay` — no MAUI dependencies). After the delay, it calls `IFileService.FindFilesAsync(pattern: $"*{SearchText}*", ct)` which maps to `GET /file?pattern=*{text}*` and returns a flat `IReadOnlyList<FileDto>`.

8. **[REQ-008]** While `SearchText` is non-empty, the list shows the flat search results. Directory navigation (REQ-005, REQ-006) is suspended: tapping a result row always invokes file selection regardless of node type. The back button is hidden during active search.

9. **[REQ-009]** Clearing `SearchText` (back to empty string) cancels any pending debounce, discards search results, and reloads the tree at `CurrentPath` via `LoadFilesCommand`.

10. **[REQ-010]** Tapping a file node (in tree mode or search mode) must invoke `OnFileSelected(node.RelativePath)` and close the popup, resulting in the token `@{relativePath}` being inserted into `MessageText` by `MessageComposerViewModel.InsertToken`.

11. **[REQ-011]** `FilePickerViewModel` must expose and correctly manage the following states, each driving a corresponding UI region:
    - `IsLoading` (`bool`) — shown during any API call
    - `IsEmpty` (`bool`) — shown when the list is empty and not loading and no error
    - `HasError` (`bool`) + `ErrorMessage` (`string?`) — shown on API failure
    - `IsSearchActive` (`bool`) — true when `SearchText` is non-empty; drives back-button visibility and item-tap behaviour

12. **[REQ-012]** All API calls in `FilePickerViewModel` must be cancellable. A new call must cancel any in-flight call of the same type (tree load or search) before starting.

13. **[REQ-013]** All new and modified logic in `FilePickerViewModel` and `FileService` must be covered by unit tests using xUnit + NSubstitute + FluentAssertions, following the project's `MethodUnderTest_WhenCondition_ExpectedBehavior` naming convention.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `FilePickerSheet.xaml.cs` | Modified | Add `OnNavigatedTo` override to trigger `LoadFilesCommand` |
| `FilePickerSheet.xaml` | Modified | Update `CollectionView` item template: icon, title, subtitle; add back button; bind `IsSearchActive` |
| `FilePickerViewModel` | Modified | Replace client-side filter with debounced server search; add tree navigation state (`CurrentPath`, back stack, `IsSearchActive`); add `BackCommand` |
| `IFileService` | Modified | Add `GetFileTreeAsync(string? path, CancellationToken ct)` method |
| `FileService` | Modified | Implement `GetFileTreeAsync` delegating to `IOpencodeApiClient.GetFileTreeAsync` |
| `FilePickerViewModelTests` | Modified/Extended | Update existing tests; add tests for tree navigation, debounce, search mode, back navigation |
| `FileServiceTests` | Modified/Extended | Add tests for `GetFileTreeAsync` |

### Dependencies
- `IOpencodeApiClient.GetFileTreeAsync(string? path, CancellationToken)` — already implemented, returns `IReadOnlyList<FileNodeDto>`
- `IOpencodeApiClient.FindFilesAsync(FindFilesRequest, CancellationToken)` — already implemented, returns `IReadOnlyList<string>`; `FileService.FindFilesAsync` wraps it (new method or reuse of existing `GetFilesAsync` with pattern parameter)
- `FileNodeDto` — already defined in `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/FileDtos.cs`; must expose at minimum `RelativePath`, `Name`, `Type` (`"file"` | `"directory"`)
- Existing design system icon/glyph resources in the MAUI project for file and folder icons

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should `GetFilesAsync` (the old `pattern=**` method) be removed from `IFileService`, or kept for potential future use? | Resolved | Keep — it uses a different endpoint and may be useful for future features. It will no longer be called by `FilePickerViewModel`. |
| 2 | What specific icon glyphs/images are available in the design system for file and folder? | Resolved | `IconKeys.File` (`\ueaa2`) and `IconKeys.Folder` (`\ueaad`) from Tabler Icons font. `IconKeys.ArrowLeft` (`\uea19`) for back button. |
| 3 | Should the back button be a dedicated UI element (e.g., a row at the top of the list showing the current path) or a button in the sheet header? | Resolved | Back button in the header row (left of title), replacing the title with current path when navigated into a subdirectory. Decided by om-mobile-ui. |
| 4 | Does `FileNodeDto.Type` use the string values `"file"` and `"directory"`, or an enum? | Resolved | String values. `FileNodeDto` record: `(string Name, string Path, string Absolute, string Type, bool Ignored)`. Note: property is `Path` not `RelativePath`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the message composer is open, when the user taps the file attachment button, then `FilePickerSheet` opens and immediately begins loading; the `ActivityIndicator` is visible. *(REQ-001)*

- [ ] **[AC-002]** Given the sheet has loaded, when the root tree is returned by the server, then the `CollectionView` shows all first-level nodes, each with a file or folder icon, a name label, and a path subtitle. *(REQ-002, REQ-003, REQ-004)*

- [ ] **[AC-003]** Given the list shows a directory node, when the user taps it, then the list reloads showing the contents of that directory, and the back button becomes visible. *(REQ-005, REQ-006)*

- [ ] **[AC-004]** Given the user has navigated into a subdirectory, when the user taps the back button, then the list reloads showing the previous directory's contents; at root level the back button is hidden. *(REQ-006)*

- [ ] **[AC-005]** Given the search field is empty, when the user types at least one character and waits 300 ms, then the list is replaced with server search results matching `*{text}*`. *(REQ-007, REQ-008)*

- [ ] **[AC-006]** Given the user is typing rapidly, when keystrokes arrive within 300 ms of each other, then only one API call is made (the last one); intermediate calls are cancelled. *(REQ-007, REQ-012)*

- [ ] **[AC-007]** Given search results are shown, when the user clears the search field, then the tree reloads at the current directory and the back button state is restored. *(REQ-009)*

- [ ] **[AC-008]** Given any list is shown (tree or search), when the user taps a file node, then the popup closes and `@{relativePath}` is inserted into the message composer text. *(REQ-010)*

- [ ] **[AC-009]** Given the API call fails, when the error state is shown, then `HasError` is `true`, `ErrorMessage` is non-null, and the list is empty. *(REQ-011)*

- [ ] **[AC-010]** All unit tests in `FilePickerViewModelTests` and `FileServiceTests` pass with no warnings. *(REQ-013)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- `src/openMob/Views/Popups/FilePickerSheet.xaml.cs` — add `OnNavigatedTo` override; compare with `AgentPickerSheet.xaml.cs` and `ModelPickerSheet.xaml.cs` for the exact pattern used
- `src/openMob/Views/Popups/FilePickerSheet.xaml` — update `CollectionView` `DataTemplate`: add icon (`Image` or `Label` with font glyph), two `Label` elements for name/path, bind tap behaviour to `SelectFileCommand` (file) vs. a new `NavigateToDirectoryCommand` (directory); add back button bound to `BackCommand` with visibility tied to `IsSearchActive == false && BackStack.Count > 0`
- `src/openMob.Core/ViewModels/FilePickerViewModel.cs` — replace `OnSearchTextChanged` + `ApplyFilter()` with a debounced server call using `CancellationTokenSource`; add `CurrentPath` (`string?`), `BackStack` (`Stack<string?>`), `IsSearchActive` (`bool`), `BackCommand` (`[RelayCommand]`), `NavigateToDirectoryCommand` (`[RelayCommand(string)]`)
- `src/openMob.Core/Services/IFileService.cs` / `FileService.cs` — add `GetFileTreeAsync(string? path, CancellationToken ct)` returning `IReadOnlyList<FileNodeDto>`; verify `FileNodeDto` field names in `FileDtos.cs`
- `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/FileDtos.cs` — confirm `FileNodeDto` shape (`RelativePath`, `Name`, `Type`)

### Suggested implementation approach
- **Debounce pattern**: in `OnSearchTextChanged`, cancel the previous `CancellationTokenSource`, create a new one, then `await Task.Delay(300, cts.Token)` before calling the API. If the token is cancelled (user typed again), the `OperationCanceledException` is swallowed silently.
- **Tree navigation**: `CurrentPath` (`string?`, null = root) + `Stack<string?>` for back navigation. `LoadFilesCommand` always uses `CurrentPath`. `NavigateToDirectoryCommand` pushes current path, sets new path, executes `LoadFilesCommand`. `BackCommand` pops stack, restores path, executes `LoadFilesCommand`.
- **Search vs. tree mode**: `IsSearchActive = !string.IsNullOrEmpty(SearchText)`. In search mode, `SelectFileCommand` handles all taps; `NavigateToDirectoryCommand` is not bound. In tree mode, the `DataTemplate` uses a `DataTrigger` or converter on `Type` to switch between the two commands.
- **Icon resources**: check `src/openMob/Resources/` for existing font glyphs (e.g., Material Icons or similar) already used in the app. Use the same glyph font for consistency.

### Constraints to respect
- `FilePickerViewModel` must have **zero MAUI dependencies** — `Task.Delay` for debounce is acceptable; `Device.StartTimer`, `MainThread`, or any `Microsoft.Maui.*` namespace is not.
- `FilePickerSheet.xaml.cs` must remain minimal — only the `OnNavigatedTo` trigger and any necessary popup lifecycle wiring.
- All new public/internal members must have `/// <summary>` XML documentation.
- As established in the server-management-ui feature, popup sheets use `AddTransientPopup<TSheet, TViewModel>()` for DI registration — no changes needed here as `FilePickerSheet` is already registered.

### Related files or modules
- `src/openMob/Views/Popups/AgentPickerSheet.xaml.cs` — reference for `OnNavigatedTo` pattern
- `src/openMob/Views/Popups/ModelPickerSheet.xaml.cs` — reference for `OnNavigatedTo` pattern
- `src/openMob/Services/MauiPopupService.cs` — `ShowFilePickerAsync` sets `OnFileSelected` callback before push; no changes needed
- `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` — `GetFileTreeAsync` and `FindFilesAsync` already declared
- `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` — verify actual HTTP call implementation for both endpoints
- `tests/openMob.Tests/ViewModels/FilePickerViewModelTests.cs` — existing 22 tests; most will need updating
- `tests/openMob.Tests/Services/FileServiceTests.cs` — existing 8 tests; extend with `GetFileTreeAsync` coverage

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-23

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature (bug fix + enhancement) |
| Git Flow branch | feature/file-picker-search |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Services | om-mobile-core | `src/openMob.Core/Services/IFileService.cs`, `FileService.cs` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/FilePickerViewModel.cs` |
| DTOs (read-only) | om-mobile-core | `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/FileDtos.cs` (verify, no changes) |
| XAML Views | om-mobile-ui | `src/openMob/Views/Popups/FilePickerSheet.xaml` |
| Code-behind | om-mobile-ui | `src/openMob/Views/Popups/FilePickerSheet.xaml.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/FilePickerViewModelTests.cs`, `tests/openMob.Tests/Services/FileServiceTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — all changes are modifications to existing files.

### Files to Modify

| File | Agent | Reason |
|------|-------|--------|
| `src/openMob.Core/Services/IFileService.cs` | om-mobile-core | Add `GetFileTreeAsync` and `FindFilesAsync` methods |
| `src/openMob.Core/Services/FileService.cs` | om-mobile-core | Implement `GetFileTreeAsync` (delegates to `IOpencodeApiClient.GetFileTreeAsync`) and `FindFilesAsync` (delegates to `IOpencodeApiClient.FindFilesAsync` with pattern, maps to `FileDto`) |
| `src/openMob.Core/ViewModels/FilePickerViewModel.cs` | om-mobile-core | Major rewrite: replace `Files`/`FilteredFiles`/`ApplyFilter` with `Items` (`ObservableCollection<FileDto>`), add `CurrentPath`, `_backStack`, `IsSearchActive`, `CanGoBack`, `BackCommand`, `NavigateToDirectoryCommand`, debounced `OnSearchTextChanged`, cancellation management |
| `src/openMob/Views/Popups/FilePickerSheet.xaml.cs` | om-mobile-ui | Add `OnNavigatedTo` override (REQ-001) |
| `src/openMob/Views/Popups/FilePickerSheet.xaml` | om-mobile-ui | Update DataTemplate (file/folder icon via converter or DataTrigger on `Type`), swap label order (Name primary, Path secondary), add back button in header, bind to new ViewModel surface |
| `tests/openMob.Tests/ViewModels/FilePickerViewModelTests.cs` | om-tester | Rewrite most tests for new API surface; add tree navigation, debounce, search mode, back navigation, cancellation tests |
| `tests/openMob.Tests/Services/FileServiceTests.cs` | om-tester | Add `GetFileTreeAsync` and `FindFilesAsync` tests |

### Technical Dependencies

- `IOpencodeApiClient.GetFileTreeAsync(string? path, CancellationToken)` — **already implemented** in `OpencodeApiClient.cs` (line 650-659), calls `GET /file/tree?path={path}`, returns `IReadOnlyList<FileNodeDto>`
- `IOpencodeApiClient.FindFilesAsync(FindFilesRequest, CancellationToken)` — **already implemented** in `OpencodeApiClient.cs` (line 619-641), calls `GET /file?pattern={pattern}&path={path}`, returns `IReadOnlyList<string>`
- `FileNodeDto(Name, Path, Absolute, Type, Ignored)` — **already defined** in `FileDtos.cs`. **CRITICAL NOTE:** The property is `Path` (not `RelativePath`). The service layer must map `FileNodeDto.Path` → `FileDto.RelativePath` for consistency with the existing `FileDto` record.
- `FileDto(RelativePath, Name, Type?)` — **already defined** in `FileDto.cs`. The `Type` parameter (currently optional, defaulting to `null`) will now be populated with `"file"` or `"directory"` when mapping from `FileNodeDto`.
- `IconKeys.File`, `IconKeys.Folder`, `IconKeys.ArrowLeft` — **already available** in `src/openMob/Helpers/IconKeys.cs`
- No new NuGet packages required.
- No EF Core migrations required.

### Technical Risks

- **Breaking existing tests:** The `FilePickerViewModel` rewrite changes the public API surface significantly. All 17 existing ViewModel tests will need updating. The `FileService` tests for `GetFilesAsync` remain valid (method is kept).
- **Debounce testability:** The 300ms `Task.Delay` debounce in `OnSearchTextChanged` is an `async void`-like fire-and-forget from the property setter's partial method. Testing requires careful use of `Task.Delay` or exposing a `Task` for test synchronization. Recommend the ViewModel expose an internal `Task? SearchTask` property that tests can await.
- **`FileNodeDto.Path` vs `FileDto.RelativePath` mapping:** The HTTP-layer DTO uses `Path` while the service-layer DTO uses `RelativePath`. `FileService.GetFileTreeAsync` must map `node.Path` → `FileDto.RelativePath` and `node.Type` → `FileDto.Type`.
- **Collection type change:** The ViewModel currently uses two `ObservableCollection<FileDto>` (`Files` + `FilteredFiles`). The new design should use a single `ObservableCollection<FileDto>` (`Items`) that is repopulated on each tree load or search result. This simplifies the binding surface.
- **No platform-specific concerns** — all changes are in shared code and XAML.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/file-picker-search` from `develop`
2. **[om-mobile-core]** Implement `IFileService.GetFileTreeAsync` + `IFileService.FindFilesAsync` in service layer; rewrite `FilePickerViewModel` with tree navigation, debounced search, cancellation management (REQ-002 through REQ-012)
3. ⟳ **[om-mobile-ui]** Implement `OnNavigatedTo` in `FilePickerSheet.xaml.cs` (REQ-001); update `FilePickerSheet.xaml` with new DataTemplate, back button, icon switching (REQ-004, REQ-006). Can start layout/structure immediately; data bindings after om-mobile-core publishes ViewModel surface.
4. **[om-tester]** Rewrite `FilePickerViewModelTests` and extend `FileServiceTests` (REQ-013)
5. **[om-reviewer]** Full review against spec — all REQ and AC items
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-013]` requirements implemented
- [ ] All `[AC-001]` through `[AC-010]` acceptance criteria satisfied
- [ ] Unit tests written for all new/modified Services and ViewModel logic
- [ ] `om-reviewer` verdict: Approved or Approved with remarks
- [ ] `dotnet build openMob.sln` — zero errors, zero warnings
- [ ] `dotnet test tests/openMob.Tests/openMob.Tests.csproj` — all tests pass
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
