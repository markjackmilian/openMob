# Spec — Session Context Sheet: Agent & Model Selection

**Feature slug:** session-context-sheet-agent-model
**Completed:** 2026-03-20
**Branch:** feature/session-context-sheet-agent-model (merged into develop)
**Complexity:** Medium
**Depends on:** session-context-sheet-1of3-core

---

## Executive Summary

Wires the existing `AgentPickerSheet` and `ModelPickerSheet` into `ContextSheetViewModel` via the callback pattern. Introduces `IAgentService.GetPrimaryAgentsAsync()` (filters `Mode == "primary" | "all"`, excludes `Hidden == true`). Agent and model selections are immediately persisted to `ProjectPreference` per project and propagated to `ChatViewModel` via `ProjectPreferenceChangedMessage`. The Project row in the Context Sheet is read-only (no command, no navigation).

---

## Requirements Implemented

| ID | Description | Status |
|----|-------------|--------|
| REQ-001 | `IAgentService.GetPrimaryAgentsAsync()` — filters primary/all, excludes hidden | ✅ |
| REQ-002 | `IAppPopupService.ShowAgentPickerAsync(Action<string?>, CancellationToken)` | ✅ |
| REQ-003 | `AgentPickerViewModel.OnAgentSelected` callback; primary mode uses `GetPrimaryAgentsAsync`; `IsSubagentMode` switches to `GetAgentsAsync` | ✅ |
| REQ-004 | `ContextSheetViewModel.SelectAgentCommand` and `SelectModelCommand` | ✅ |
| REQ-005 | Agent selection → `SetAgentAsync` → `ProjectPreferenceChangedMessage` (auto-save via `OnSelectedAgentNameChanged`) | ✅ |
| REQ-006 | Model selection → `SetDefaultModelAsync` → `ProjectPreferenceChangedMessage` (auto-save via `OnSelectedModelIdChanged`) | ✅ |
| REQ-007 | `ChatViewModel.SelectedAgentName` (`string?`) and `SelectedAgentDisplayName` (`string`, fallback `"build"`) | ✅ |
| REQ-008 | Project row read-only — no command, no `TapGestureRecognizer`, no chevron | ✅ |
| REQ-009 | DI registrations already existed — no new registrations needed | ✅ |

---

## Key Product Decisions (made during implementation)

### No synthetic "Default" entry in the agent picker
The spec originally called for a synthetic "Default" entry (Name=null) prepended to the list. After product review, this was removed in favour of showing only real agents from the server. The fallback display name for `null` preference is `"build"` (the opencode default primary agent), not `"Default"`.

### `AgentDto.Hidden` field added
The opencode `GET /agent` API returns a `hidden` boolean field. System agents (`compaction`, `title`, `summary`) have `Mode="primary"` but are hidden — they must not appear in the picker. `AgentDto` was extended with `Hidden bool` and `GetPrimaryAgentsAsync` filters `!a.Hidden`.

### Agent preference is per-project
`IProjectPreferenceService.SetAgentAsync(projectId, agentName)` stores the preference in `ProjectPreference` with a FK on `projectId`. Each project has an independent agent preference.

### opencode `GET /agent` is project-contextual
The opencode server is started in a project directory. `GET /agent` returns all agents available for that project — both global (`~/.config/opencode/agents/`) and project-local (`.opencode/agents/`). No `projectId` parameter is needed.

---

## Files Modified

| File | Change |
|------|--------|
| `src/openMob.Core/Infrastructure/Http/Dtos/Opencode/AgentDto.cs` | Added `Hidden bool` parameter |
| `src/openMob.Core/Services/IAgentService.cs` | Added `GetPrimaryAgentsAsync` signature |
| `src/openMob.Core/Services/AgentService.cs` | Implemented `GetPrimaryAgentsAsync` with LINQ filter |
| `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` | Added `OnAgentSelected` callback; primary/subagent mode switching in `LoadAgentsAsync` |
| `src/openMob.Core/Services/IAppPopupService.cs` | Added `ShowAgentPickerAsync` |
| `src/openMob/Services/MauiPopupService.cs` | Implemented `ShowAgentPickerAsync` |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Replaced stub `OpenAgentPickerAsync` with real impl; renamed to `SelectAgentAsync`/`SelectModelAsync`; removed `OpenProjectSwitcherCommand`; wrapped message handler in `_dispatcher.Dispatch` |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Added `SelectedAgentName`, `SelectedAgentDisplayName`; updated `LoadContextAsync` and message handler |
| `src/openMob/Views/Popups/AgentPickerSheet.xaml` | `{Binding Name, FallbackValue='build'}` |
| `src/openMob/Views/Popups/ContextSheet.xaml` | Removed Project row tap gesture + chevron; updated command bindings to `SelectAgentCommand`/`SelectModelCommand` |

---

## Tests Added

| File | New tests |
|------|-----------|
| `tests/openMob.Tests/Services/AgentServiceTests.cs` | `GetPrimaryAgentsAsync` — mixed modes, hidden filter, empty list |
| `tests/openMob.Tests/ViewModels/AgentPickerViewModelTests.cs` | Primary/subagent mode switching, callback invocation, null-safe callback |
| `tests/openMob.Tests/ViewModels/ContextSheetViewModelTests.cs` | `SelectAgentCommand` wiring, callback updates `SelectedAgentName` |
| `tests/openMob.Tests/ViewModels/ChatViewModelAgentTests.cs` | New file — `SelectedAgentDisplayName` computed, `LoadContextAsync` agent loading, message handler agent update |

**Total tests after merge:** 817 passing, 0 failing.

---

## Reviewer Findings (resolved)

| ID | Severity | Description | Resolution |
|----|----------|-------------|------------|
| M-001 | Major | Project row had `TapGestureRecognizer` bound to `OpenProjectSwitcherCommand` — REQ-008 violation | Removed command from ViewModel and gesture from XAML |
| M-002 | Major | `ProjectPreferenceChangedMessage` handler set observable properties without `_dispatcher.Dispatch` | Wrapped handler body in `_dispatcher.Dispatch(...)` |
| M-003 | Major | Commands named `OpenAgentPickerCommand`/`OpenModelPickerCommand` instead of `SelectAgentCommand`/`SelectModelCommand` | Renamed methods in ViewModel, updated XAML bindings and tests |
