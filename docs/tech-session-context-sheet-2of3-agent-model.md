# Technical Analysis — Session Context Sheet: Agent & Model Selection

**Feature slug:** session-context-sheet-agent-model
**Completed:** 2026-03-20
**Branch:** feature/session-context-sheet-agent-model (merged into develop)
**Complexity:** Medium

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/session-context-sheet-agent-model |
| Branches from | develop |
| Agents involved | om-mobile-core, om-mobile-ui (XAML fixes), om-tester, om-reviewer |

---

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Services/` |
| ViewModels | om-mobile-core | `src/openMob.Core/ViewModels/` |
| MAUI service implementation | om-mobile-core | `src/openMob/Services/MauiPopupService.cs` |
| XAML (minimal) | om-mobile-ui | `src/openMob/Views/Popups/ContextSheet.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/` |
| Code Review | om-reviewer | all of the above |

---

## Key Technical Decisions

### 1. `AgentDto.Hidden` — system agents must be filtered

The opencode `GET /agent` endpoint returns all agents including system agents (`compaction`, `title`, `summary`) which have `Mode="primary"` but `Hidden=true`. Without filtering on `Hidden`, these would appear in the user-facing picker.

**Decision:** Add `Hidden bool` to `AgentDto` and filter `!a.Hidden` in `GetPrimaryAgentsAsync`.

```csharp
// AgentService.cs
public async Task<IReadOnlyList<AgentDto>> GetPrimaryAgentsAsync(CancellationToken ct = default)
{
    var all = await GetAgentsAsync(ct).ConfigureAwait(false);
    return all.Where(a => (a.Mode is "primary" or "all") && !a.Hidden).ToList();
}
```

### 2. No synthetic "Default" entry — show only real agents

The spec originally called for a synthetic "Default" entry (Name=null) prepended to the picker list. After product review this was removed. The picker shows only real agents from the server. The fallback display name for `null` preference is `"build"` (the opencode default primary agent).

**Impact on `AgentItem`:** `Name` remains `string` (non-nullable). No `DisplayName` computed property needed.

**Impact on `SelectedAgentDisplayName`:**
```csharp
// ContextSheetViewModel.cs and ChatViewModel.cs
public string SelectedAgentDisplayName => SelectedAgentName ?? "build";
```

**Impact on XAML:**
```xml
<!-- AgentPickerSheet.xaml -->
<Label Text="{Binding Name, FallbackValue='build'}" />
```

### 3. Auto-save pattern — no explicit save logic in commands

`ContextSheetViewModel` uses CommunityToolkit.Mvvm partial method hooks:
- `OnSelectedAgentNameChanged` → calls `SaveAgentAsync` → `SetAgentAsync` + `PublishChangedMessageAsync`
- `OnSelectedModelIdChanged` → calls `SaveModelAsync` → `SetDefaultModelAsync` + `PublishChangedMessageAsync`

The `SelectAgentCommand` only needs to call `ShowAgentPickerAsync` with a callback that sets `SelectedAgentName`. The auto-save fires automatically via the generated partial method. No explicit persistence call in the command body.

### 4. `ProjectPreferenceChangedMessage` handler must dispatch to UI thread

The message handler in `ChatViewModel` sets observable properties (`SelectedModelId`, `SelectedModelName`, `SelectedAgentName`). These must be set on the UI thread for MAUI bindings to work correctly.

```csharp
WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(this, (_, message) =>
{
    if (message.ProjectId != CurrentProjectId)
        return;

    var pref = message.UpdatedPreference;

    _dispatcher.Dispatch(() =>   // ← required
    {
        // ... property mutations
        SelectedAgentName = pref.AgentName;
    });
});
```

The early-exit guard (`if (message.ProjectId != CurrentProjectId) return;`) is correctly placed **outside** the `Dispatch` call to avoid unnecessary dispatches.

### 5. `AgentPickerViewModel` dual-mode: primary vs subagent

`AgentPickerViewModel` serves two use cases:
- **Primary mode** (`IsSubagentMode = false`): called from `ContextSheetViewModel.SelectAgentCommand` via `ShowAgentPickerAsync`. Uses `GetPrimaryAgentsAsync()`. Invokes `OnAgentSelected` callback on selection.
- **Subagent mode** (`IsSubagentMode = true`): called from the subagent invocation flow. Uses `GetAgentsAsync()` (unfiltered). Sets `SelectedSubagentName` on selection. Does NOT invoke `OnAgentSelected`.

`MauiPopupService.ShowAgentPickerAsync` always sets `vm.IsSubagentMode = false` before presenting.

### 6. Agent preference is per-project, server list is project-contextual

- `ProjectPreference.AgentName` is stored per `projectId` in SQLite.
- `GET /agent` returns agents available for the current project (global + project-local `.opencode/agents/`). No `projectId` parameter — the server is started in the project directory.

---

## Files Modified

| File | Change summary |
|------|----------------|
| `AgentDto.cs` | +`Hidden bool` |
| `IAgentService.cs` | +`GetPrimaryAgentsAsync` |
| `AgentService.cs` | +`GetPrimaryAgentsAsync` impl |
| `AgentPickerViewModel.cs` | +`OnAgentSelected` callback; dual-mode `LoadAgentsAsync`; `SelectAgentAsync` invokes callback |
| `IAppPopupService.cs` | +`ShowAgentPickerAsync(Action<string?>, CancellationToken)` |
| `MauiPopupService.cs` | +`ShowAgentPickerAsync` impl |
| `ContextSheetViewModel.cs` | Replaced stub with real `SelectAgentAsync`; renamed `SelectModelAsync`; removed `OpenProjectSwitcherCommand`; `_dispatcher.Dispatch` in message handler |
| `ChatViewModel.cs` | +`SelectedAgentName`, `SelectedAgentDisplayName`; `LoadContextAsync` loads `AgentName`; message handler dispatched |
| `AgentPickerSheet.xaml` | `{Binding Name, FallbackValue='build'}` |
| `ContextSheet.xaml` | Removed Project row tap + chevron; `SelectAgentCommand`/`SelectModelCommand` bindings |

---

## Reviewer Findings Summary

| ID | Severity | Root cause | Fix |
|----|----------|------------|-----|
| M-001 | Major | `OpenProjectSwitcherCommand` exposed on ViewModel + bound in XAML — REQ-008 violation | Removed command and XAML gesture |
| M-002 | Major | `ProjectPreferenceChangedMessage` handler mutated observable properties off UI thread | Wrapped in `_dispatcher.Dispatch(...)` |
| M-003 | Major | Commands named `OpenAgentPickerCommand`/`OpenModelPickerCommand` instead of spec-required names | Renamed methods → `SelectAgentAsync`/`SelectModelAsync` |
