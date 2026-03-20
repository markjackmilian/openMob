# Session Context Sheet — Core Infrastructure

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-19                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

The Session Context Sheet is a bottom sheet modal opened from the chat page header's edit button. It exposes per-project session settings (agent, model, thinking level, auto-accept) that are currently placeholder-only. This spec covers the **core infrastructure layer**: extending the `ProjectPreference` entity and service to store all session-level settings per project, defining the global default values used when no project preference exists, and introducing the `ContextSheetViewModel` with its popup service integration. Subsequent vertical specs build on this foundation for each individual section.

---

## Scope

### In Scope
- Extension of `ProjectPreference` entity with new fields: `AgentName` (string?), `ThinkingLevel` (enum: Low/Medium/High), `AutoAccept` (bool)
- EF Core migration for the new `ProjectPreference` columns
- Extension of `IProjectPreferenceService` with new read/write methods for all preference fields
- Definition of global default values (Agent = "Default", ThinkingLevel = Medium, AutoAccept = false, Model = already handled)
- New `ContextSheetViewModel` in `openMob.Core`: loads preferences for the active project, exposes observable properties for all settings, auto-saves on every change
- New `IAppPopupService.ShowContextSheetAsync(string projectId, string sessionId)` method
- `ContextSheetViewModel` propagates changes to `ChatViewModel` via `WeakReferenceMessenger` (CommunityToolkit.Mvvm messaging)
- New message types: `ProjectPreferenceChangedMessage`
- `ChatViewModel` subscribes to `ProjectPreferenceChangedMessage` and updates its observable state accordingly
- Registration of `ContextSheetViewModel` in `AddOpenMobCore()` DI extension

### Out of Scope
- XAML / UI implementation of the Context Sheet (covered by `om-mobile-ui`)
- Agent selection logic (spec `session-context-sheet-2of3-agent-model`)
- Model selection logic (spec `session-context-sheet-2of3-agent-model`)
- Thinking level segmented control logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Auto-accept toggle logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Invoke Subagent logic (spec `session-context-sheet-3of3-thinking-autoaccept-subagent`)
- Thinking level support detection per model (future spec)
- Server-side persistence of any preference (all storage is local SQLite)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The `ProjectPreference` entity must be extended with three new nullable/defaulted fields:
   - `AgentName` (`string?`) — name of the selected primary agent; `null` means "Default"
   - `ThinkingLevel` (`ThinkingLevel` enum) — values: `Low`, `Medium`, `High`; default `Medium`
   - `AutoAccept` (`bool`) — default `false`

2. **[REQ-002]** A new EF Core migration must be created to add the three columns to the `ProjectPreferences` table. Existing rows must receive the default values (`AgentName = NULL`, `ThinkingLevel = 1` (Medium), `AutoAccept = 0`).

3. **[REQ-003]** `IProjectPreferenceService` must be extended with the following methods:
   - `Task<ProjectPreference> GetOrDefaultAsync(string projectId, CancellationToken ct)` — returns the stored preference for the project, or a new `ProjectPreference` populated with global defaults if none exists. Never returns `null`.
   - `Task<bool> SetAgentAsync(string projectId, string? agentName, CancellationToken ct)` — upsert `AgentName`
   - `Task<bool> SetThinkingLevelAsync(string projectId, ThinkingLevel level, CancellationToken ct)` — upsert `ThinkingLevel`
   - `Task<bool> SetAutoAcceptAsync(string projectId, bool autoAccept, CancellationToken ct)` — upsert `AutoAccept`
   - The existing `SetDefaultModelAsync` is preserved unchanged.

4. **[REQ-004]** Global default values (used when no `ProjectPreference` row exists for a project) are:
   - `AgentName`: `null` (displayed as "Default" in the UI)
   - `ThinkingLevel`: `ThinkingLevel.Medium`
   - `AutoAccept`: `false`
   - `DefaultModelId`: `null` (displayed as "No model" in the UI — already existing behaviour)

5. **[REQ-005]** A new `ContextSheetViewModel` (`openMob.Core/ViewModels/ContextSheetViewModel.cs`) must be created with the following observable properties:
   - `ProjectName` (`string`) — read-only display name of the active project
   - `SelectedAgentName` (`string?`) — current agent name (`null` = Default)
   - `SelectedAgentDisplayName` (`string`) — computed: `SelectedAgentName ?? "Default"`
   - `SelectedModelId` (`string?`) — current model ID
   - `SelectedModelDisplayName` (`string`) — computed: extracted model name or "No model"
   - `ThinkingLevel` (`ThinkingLevel`) — current thinking level
   - `AutoAccept` (`bool`) — current auto-accept state
   - `IsBusy` (`bool`) — true while loading preferences

6. **[REQ-006]** `ContextSheetViewModel` must expose an `InitializeAsync(string projectId, string sessionId)` method that:
   - Sets `IsBusy = true`
   - Loads the project display name via `IProjectService`
   - Loads preferences via `IProjectPreferenceService.GetOrDefaultAsync(projectId)`
   - Populates all observable properties
   - Sets `IsBusy = false`
   - Must be called by the MAUI layer immediately after the sheet is pushed

7. **[REQ-007]** Each time a preference property changes in `ContextSheetViewModel` (agent, model, thinking level, auto-accept), the ViewModel must:
   - Immediately persist the change via the corresponding `IProjectPreferenceService.Set*Async` method
   - On successful save, publish a `ProjectPreferenceChangedMessage` via `WeakReferenceMessenger.Default`
   - On save failure, set an `ErrorMessage` observable property (non-blocking — the UI value is not rolled back)

8. **[REQ-008]** `ProjectPreferenceChangedMessage` is a new sealed record in `openMob.Core/Messages/`:
   ```
   ProjectPreferenceChangedMessage(string ProjectId, ProjectPreference UpdatedPreference)
   ```

9. **[REQ-009]** `ChatViewModel` must subscribe to `ProjectPreferenceChangedMessage` (via `WeakReferenceMessenger`) and, when the message's `ProjectId` matches the current session's project, update:
   - `SelectedModelId` and `SelectedModelName` from `UpdatedPreference.DefaultModelId`
   - Any future properties added by subsequent specs (agent display name, thinking level, auto-accept) follow the same pattern

10. **[REQ-010]** `IAppPopupService` must be extended with:
    ```
    Task ShowContextSheetAsync(string projectId, string sessionId, CancellationToken ct = default)
    ```
    The MAUI implementation resolves `ContextSheetViewModel` from DI, calls `InitializeAsync`, then pushes the `ContextSheet` page modally.

11. **[REQ-011]** `ContextSheetViewModel` must be registered in `AddOpenMobCore()` as `Transient`. `ContextSheet` (MAUI page) must be registered in `MauiProgram.cs` as `Transient`.

12. **[REQ-012]** `ChatViewModel` must expose an `OpenContextSheetCommand` (`[AsyncRelayCommand]`) that calls `_popupService.ShowContextSheetAsync(CurrentProjectId, CurrentSessionId)`. This command replaces the current stub `OpenProjectSwitcherAsync` as the primary action for the header edit button.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ProjectPreference.cs` | **Extended** | 3 new fields: `AgentName`, `ThinkingLevel`, `AutoAccept` |
| `AppDbContext.cs` | **Minor update** | Fluent config for new columns (defaults, max lengths) |
| `Migrations/` | **New migration** | `AddAgentThinkingAutoAcceptToProjectPreference` |
| `IProjectPreferenceService.cs` | **Extended** | 4 new method signatures |
| `ProjectPreferenceService.cs` | **Extended** | Implementations of 4 new methods following existing upsert pattern |
| `ContextSheetViewModel.cs` | **New file** | Core ViewModel for the sheet |
| `openMob.Core/Messages/` | **New folder + file** | `ProjectPreferenceChangedMessage.cs` |
| `ChatViewModel.cs` | **Extended** | Subscribe to message, new `OpenContextSheetCommand`, uses `WeakReferenceMessenger.Default` directly |
| `IAppPopupService.cs` | **Extended** | New `ShowContextSheetAsync` method |
| `MauiPopupService.cs` | **Extended** | Implementation of `ShowContextSheetAsync` |
| `CoreServiceExtensions.cs` | **Extended** | Register `ContextSheetViewModel` as Transient |
| `MauiProgram.cs` | **Extended** | Register `ContextSheet` page as Transient |
| `ThinkingLevel.cs` | **New file** | Enum in `openMob.Core/Models/Enums/` |

### Dependencies
- `IProjectService` — to resolve project display name in `ContextSheetViewModel`
- `IProjectPreferenceService` — extended by this spec
- `CommunityToolkit.Mvvm` — `WeakReferenceMessenger` (already a project dependency, messaging not yet used)
- EF Core 9.x — migration tooling
- Subsequent specs (`session-context-sheet-2of3-agent-model`, `session-context-sheet-3of3-thinking-autoaccept-subagent`) depend on this spec being implemented first

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should `ChatViewModel` inherit from `ObservableRecipient` (which auto-registers/unregisters on `IsActive`) or use `WeakReferenceMessenger.Default` directly? | Resolved | Use `WeakReferenceMessenger.Default` directly to avoid changing the base class of an already-complex ViewModel. Subscribe in the constructor, no lifecycle coupling needed. |
| 2 | Should `ThinkingLevel` enum be stored as int or string in SQLite? | Resolved | Store as int (EF Core default for enums). Provides compact storage and easy ordering (Low=0, Medium=1, High=2). |
| 3 | Is `sessionId` needed in `ShowContextSheetAsync` if preferences are per-project? | Resolved | Keep it in the signature for future use (e.g., session-level overrides in a future spec). `ContextSheetViewModel` ignores it for now. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the app is freshly installed, when `GetOrDefaultAsync` is called for any project ID, then a `ProjectPreference` with `AgentName=null`, `ThinkingLevel=Medium`, `AutoAccept=false`, `DefaultModelId=null` is returned without throwing. *(REQ-003, REQ-004)*
- [ ] **[AC-002]** Given a project with no stored preference, when the user opens the Context Sheet, then `SelectedAgentDisplayName="Default"`, `SelectedModelDisplayName="No model"`, `ThinkingLevel=Medium`, `AutoAccept=false` are shown. *(REQ-005, REQ-006)*
- [ ] **[AC-003]** Given the Context Sheet is open, when any preference is changed, then `IProjectPreferenceService.Set*Async` is called within the same user interaction and a `ProjectPreferenceChangedMessage` is published. *(REQ-007, REQ-008)*
- [ ] **[AC-004]** Given `ChatViewModel` is active and a `ProjectPreferenceChangedMessage` arrives for the current project, when the message contains a new `DefaultModelId`, then `SelectedModelId` and `SelectedModelName` are updated without requiring a page reload. *(REQ-009)*
- [ ] **[AC-005]** Given the chat page header edit button is tapped, when `OpenContextSheetCommand` executes, then the Context Sheet is pushed modally with preferences pre-loaded for the current project. *(REQ-010, REQ-012)*
- [ ] **[AC-006]** Given a save failure (e.g., DB error), when a preference change is attempted, then `ErrorMessage` is set on `ContextSheetViewModel` and the UI value is not rolled back. *(REQ-007)*
- [ ] **[AC-007]** Given the migration is applied, when inspecting the `ProjectPreferences` table, then columns `AgentName` (TEXT NULL), `ThinkingLevel` (INTEGER NOT NULL DEFAULT 1), `AutoAccept` (INTEGER NOT NULL DEFAULT 0) exist. *(REQ-001, REQ-002)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate

1. **`ChatViewModel` base class change**: Currently inherits from `ObservableObject`. To use `WeakReferenceMessenger.Default` directly, no base class change is needed — call `WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(this, (r, m) => ...)` in the constructor and `WeakReferenceMessenger.Default.UnregisterAll(this)` in a cleanup method. Verify if `ChatViewModel` has any `IDisposable` or lifecycle cleanup already in place.

2. **EF Core migration**: The new migration must add three columns to the existing `ProjectPreferences` table. Use `migrationBuilder.AddColumn<string>` for `AgentName` (nullable, no default), `migrationBuilder.AddColumn<int>` for `ThinkingLevel` (non-nullable, defaultValue: 1), and `migrationBuilder.AddColumn<bool>` for `AutoAccept` (non-nullable, defaultValue: false). The `Down` method must use `migrationBuilder.DropColumn`.

3. **`GetOrDefaultAsync` implementation**: Do not insert a row into the DB when returning defaults — return a transient `new ProjectPreference { ProjectId = projectId, ThinkingLevel = ThinkingLevel.Medium, AutoAccept = false }` without calling `SaveChangesAsync`. This avoids polluting the DB with rows for projects the user never customised.

4. **`ContextSheetViewModel` auto-save pattern**: Each `[ObservableProperty]`-decorated property that maps to a preference should trigger save via a `partial void On{Property}Changed(T value)` method (CommunityToolkit source generator pattern). This avoids explicit command wiring for each field.

5. **`MauiPopupService.ShowContextSheetAsync`**: Follow the existing `ShowModelPickerAsync` pattern — resolve `ContextSheet` from `Application.Current.MainPage.Handler.MauiContext.Services` (or the registered `IServiceProvider`), set the ViewModel, push modally via `Shell.Current.Navigation.PushModalAsync`.

### Constraints to Respect

- **Layer separation**: `ThinkingLevel` enum, `ProjectPreferenceChangedMessage`, and all ViewModel logic must live in `openMob.Core`. Zero MAUI dependencies.
- **Upsert pattern**: Follow the exact pattern in `ProjectPreferenceService.SetDefaultModelAsync` — `FindAsync` → mutate or `Add` → `SaveChangesAsync` → return bool. Wrap in try/catch, capture to Sentry.
- **Transient lifetime**: `ContextSheetViewModel` must be `Transient` (a new instance per sheet open). Do not register as Singleton.
- **No `async void`**: All save operations triggered by property changes must be fire-and-forget via `_ = SavePreferenceAsync(...)` pattern, not `async void`.
- **`ConfigureAwait(false)`**: Required on all `await` calls inside `openMob.Core` services and ViewModels.

### Related Files and Modules

| File | Relevance |
|------|-----------|
| `src/openMob.Core/Data/Entities/ProjectPreference.cs` | Extend with 3 new fields |
| `src/openMob.Core/Data/AppDbContext.cs` | Add fluent config for new columns |
| `src/openMob.Core/Data/Migrations/` | New migration file |
| `src/openMob.Core/Services/IProjectPreferenceService.cs` | Add 4 new method signatures |
| `src/openMob.Core/Services/ProjectPreferenceService.cs` | Implement 4 new methods |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Add `OpenContextSheetCommand`, subscribe to message |
| `src/openMob.Core/Services/IAppPopupService.cs` | Add `ShowContextSheetAsync` |
| `src/openMob/Services/MauiPopupService.cs` | Implement `ShowContextSheetAsync` |
| `src/openMob.Core/Extensions/CoreServiceExtensions.cs` | Register `ContextSheetViewModel` |
| `src/openMob/MauiProgram.cs` | Register `ContextSheet` page |

### References to Past Decisions

- As established in **app-navigation-structure** (2026-03-15): `IAppPopupService` abstraction is the standard for all popup/sheet interactions. The `Action<T>` callback pattern (used in `ShowModelPickerAsync`) is preferred over `Task<T>` for full-page sheets to keep Core decoupled from MAUI navigation lifecycle.
- As established in **chat-page-redesign** (2026-03-18): `ContextSheetViewModel` properties `ThinkingLevel`, `AutoAccept`, `IsSubagentActive` are listed as required additions to the chat layer. This spec provides the persistence and ViewModel foundation.
- `ProjectPreferenceService` uses `Transient` lifetime and the upsert pattern with Sentry error capture — replicate exactly.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-20

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/session-context-sheet-core |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Pre-existing State (Codebase Audit)

The following items were found to **already exist** in the codebase and must be treated as stubs to **replace or extend**, not create from scratch:

| Item | State | Action Required |
|------|-------|-----------------|
| `ThinkingLevel.cs` | ✅ Exists — correct location `openMob.Core/Models/` | No change needed |
| `ContextSheetViewModel.cs` | ⚠️ Exists as stub — no `InitializeAsync`, no auto-save, no messaging, wrong property names (`AgentName`/`ModelName` instead of `SelectedAgentName`/`SelectedModelId`) | **Full rewrite** per spec |
| `ContextSheet.xaml.cs` | ✅ Exists — calls `LoadContextCommand` on `OnAppearing` | Must be updated to call `InitializeAsync` instead |
| `ContextSheetViewModel` in DI | ✅ Already registered as Transient in `CoreServiceExtensions.cs` | No change needed |
| `ContextSheet` in DI | ✅ Already registered as Transient in `MauiProgram.cs` | No change needed |
| `IAppPopupService.ShowContextSheetAsync` | ⚠️ Exists with wrong signature: `Task ShowContextSheetAsync(CancellationToken ct = default)` — missing `projectId` and `sessionId` params | **Signature change** required |
| `MauiPopupService.ShowContextSheetAsync` | ⚠️ Exists with wrong signature — does not call `InitializeAsync` | **Rewrite** to match new signature |
| `ChatViewModel.OpenContextSheetAsync` | ⚠️ Exists but calls `_popupService.ShowContextSheetAsync(ct)` (old signature) | **Update** to pass `CurrentProjectId` and `CurrentSessionId` |
| `Messages/` folder | ❌ Does not exist | **Create** |
| `ProjectPreferenceChangedMessage.cs` | ❌ Does not exist | **Create** |
| `ProjectPreference` entity fields | ❌ Missing `AgentName`, `ThinkingLevel`, `AutoAccept` | **Extend** |
| EF Core migration | ❌ Missing | **Create** |
| `IProjectPreferenceService` new methods | ❌ Missing | **Add** |
| `ProjectPreferenceService` new methods | ❌ Missing | **Implement** |
| `ChatViewModel` WeakReferenceMessenger | ❌ Not subscribed | **Add** subscription in constructor + unregister in `Dispose()` |
| `AppDbContext` fluent config | ❌ Missing config for new columns | **Extend** `OnModelCreating` |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Data entity | om-mobile-core | `src/openMob.Core/Data/Entities/ProjectPreference.cs` |
| EF Core migration | om-mobile-core | `src/openMob.Core/Data/Migrations/` |
| DbContext config | om-mobile-core | `src/openMob.Core/Data/AppDbContext.cs` |
| Service interface | om-mobile-core | `src/openMob.Core/Services/IProjectPreferenceService.cs` |
| Service implementation | om-mobile-core | `src/openMob.Core/Services/ProjectPreferenceService.cs` |
| Message type | om-mobile-core | `src/openMob.Core/Messages/ProjectPreferenceChangedMessage.cs` |
| ContextSheetViewModel | om-mobile-core | `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` |
| ChatViewModel extension | om-mobile-core | `src/openMob.Core/ViewModels/ChatViewModel.cs` |
| Popup service interface | om-mobile-core | `src/openMob.Core/Services/IAppPopupService.cs` |
| Popup service MAUI impl | om-mobile-core | `src/openMob/Services/MauiPopupService.cs` |
| ContextSheet code-behind | om-mobile-core | `src/openMob/Views/Popups/ContextSheet.xaml.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

> **Note:** No XAML changes are required for this spec. `om-mobile-ui` is not involved.

### Files to Create

- `src/openMob.Core/Messages/ProjectPreferenceChangedMessage.cs` — new sealed record for messaging
- `src/openMob.Core/Data/Migrations/20260320000000_AddAgentThinkingAutoAcceptToProjectPreference.cs` — EF Core migration (hand-written, not generated, following existing migration style)
- `src/openMob.Core/Data/Migrations/20260320000000_AddAgentThinkingAutoAcceptToProjectPreference.Designer.cs` — EF Core migration designer snapshot

### Files to Modify

- `src/openMob.Core/Data/Entities/ProjectPreference.cs` — add `AgentName`, `ThinkingLevel`, `AutoAccept` fields
- `src/openMob.Core/Data/AppDbContext.cs` — add fluent config for 3 new columns in `OnModelCreating`
- `src/openMob.Core/Data/Migrations/AppDbContextModelSnapshot.cs` — update snapshot to include new columns
- `src/openMob.Core/Services/IProjectPreferenceService.cs` — add `GetOrDefaultAsync`, `SetAgentAsync`, `SetThinkingLevelAsync`, `SetAutoAcceptAsync`
- `src/openMob.Core/Services/ProjectPreferenceService.cs` — implement 4 new methods
- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — full rewrite: `InitializeAsync`, auto-save via `On*Changed`, messaging, new property names
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — add WeakReferenceMessenger subscription in constructor, unregister in `Dispose()`, update `OpenContextSheetAsync` to pass `projectId`/`sessionId`
- `src/openMob.Core/Services/IAppPopupService.cs` — update `ShowContextSheetAsync` signature to `(string projectId, string sessionId, CancellationToken ct = default)`
- `src/openMob/Services/MauiPopupService.cs` — update `ShowContextSheetAsync` to match new signature and call `InitializeAsync`
- `src/openMob/Views/Popups/ContextSheet.xaml.cs` — update `OnAppearing` to not call `LoadContextCommand` (initialization is now done by `MauiPopupService` before push)

### Technical Dependencies

- `CommunityToolkit.Mvvm` — `WeakReferenceMessenger` already available (no new NuGet packages required)
- EF Core 9.x — already configured; migration is hand-written following existing pattern
- No new NuGet packages required

### Technical Risks

- **Breaking change on `IAppPopupService`**: Changing `ShowContextSheetAsync` signature from `(CancellationToken)` to `(string, string, CancellationToken)` will break the existing call in `ChatViewModel.OpenContextSheetAsync`. Both files must be updated atomically.
- **`ContextSheetViewModel` property rename**: The existing stub uses `AgentName` (string) and `ModelName` (string). The spec requires `SelectedAgentName` (string?), `SelectedAgentDisplayName` (string), `SelectedModelId` (string?), `SelectedModelDisplayName` (string). Existing tests in `ContextSheetViewModelTests.cs` reference the old property names and **must be rewritten** by `om-tester`.
- **EF Core migration**: Hand-written migration must match the snapshot exactly. The `AppDbContextModelSnapshot.cs` must also be updated to reflect the new columns.
- **`ContextSheet.xaml.cs` `OnAppearing`**: Currently calls `LoadContextCommand`. After the rewrite, `LoadContextCommand` will no longer exist (replaced by `InitializeAsync`). The code-behind must be updated to remove this call — initialization is now done by `MauiPopupService.ShowContextSheetAsync` before the page is pushed.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/session-context-sheet-core`
2. [om-mobile-core] Implement all Core changes (entity, migration, service, messages, ViewModels, popup service)
3. [om-tester] Write/rewrite unit tests for `ProjectPreferenceService`, `ContextSheetViewModel`, `ChatViewModel` (messaging)
4. [om-reviewer] Full review against spec
5. [Fix loop if needed] Address Critical and Major findings
6. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-012]` requirements implemented
- [ ] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [ ] Unit tests written for all new/modified Services and ViewModels
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
