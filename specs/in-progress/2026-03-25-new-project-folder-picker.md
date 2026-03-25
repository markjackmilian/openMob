# New Project — Server Folder Picker

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-25                   |
| Status  | In Progress                  |
| Version | 1.0                          |
| Branch  | feature/new-project-folder-picker |

---

## Executive Summary

This feature replaces the current project creation flow with a server-side folder picker. The user will choose a directory from the opencode server filesystem, register that directory as a project, and then be taken directly to the chat view with the new project active. The picker must browse folders only and start from the server's current directory.

---

## Scope

### In Scope
- Open the project creation flow from the existing `+` button on `ProjectsPage`.
- Replace the current text-based add-project popup with a folder picker modal.
- Browse the opencode server filesystem starting from the server current directory.
- Show folders only; hide files and ignored entries.
- Allow navigation into subfolders and back to parent folders.
- Allow selecting the currently displayed folder as the project target.
- Create/register the project on the server using the selected folder path.
- Activate the newly created or existing project after selection.
- Navigate to `//chat` after the project is activated.
- Show inline loading and error states inside the picker.

### Out of Scope
- Creating projects from the device/local filesystem.
- Renaming or deleting projects.
- Adding project creation entry points outside the `ProjectsPage` `+` button.
- Full-text search in the folder picker.
- Modifying project switching UI in the chat flyout.

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** The existing `+` button in `ProjectsPage` must open a server-side folder picker modal for project creation.

2. **[REQ-002]** The folder picker must initialize from the server's current directory, not from the device filesystem. The starting path must be read from the opencode server app info (`path.cwd`).

3. **[REQ-003]** The folder picker must display folders only. File entries and ignored entries must not be selectable or visible.

4. **[REQ-004]** Tapping a folder must navigate into that folder. The picker must maintain back navigation to the previous folder hierarchy level.

5. **[REQ-005]** The picker must provide a confirmation action that selects the currently displayed folder as the project target.

6. **[REQ-006]** When the user confirms a folder, the app must create/register the project using that server path, then activate the resulting project, then navigate to `//chat`.

7. **[REQ-007]** If the selected folder already corresponds to an existing project, the app must activate that project instead of creating a duplicate.

8. **[REQ-008]** The picker must show a loading indicator while the server folder list is being fetched and must preserve the sheet open on recoverable errors.

9. **[REQ-009]** Any failure during folder loading or project registration must be surfaced inline in the picker and captured for monitoring.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ProjectsPage.xaml` | Modified | `+` button opens the new folder picker flow |
| `ProjectsViewModel.cs` | Modified | Opens folder picker instead of current add-project popup |
| `FolderPickerViewModel.cs` | New | Server-side folder navigation and selection logic |
| `FolderPickerSheet.xaml` | New | Modal UI for browsing folders only |
| `IAppPopupService.cs` | Modified | Add a method to present the folder picker |
| `MauiPopupService.cs` | Modified | Resolve and show the new folder picker |
| `IFileService` / `FileService` | Reused / possibly adjusted | Directory listing is the basis for the picker tree |
| `IOpencodeApiClient` | Modified | Expose app info needed to obtain `path.cwd` |
| `IProjectService` | Modified | Add create/register-or-activate flow for selected folder |
| `IActiveProjectService` | Modified | May need temporary worktree handling before activation |

### Dependencies
- opencode server app info endpoint for current directory (`GET /app`).
- opencode project listing endpoint (`GET /project`).
- opencode session creation flow, used to register a project context for the selected folder.
- Existing folder navigation patterns from `FilePickerViewModel`.
- Existing global `x-opencode-directory` header injection in `OpencodeApiClient`.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | If the selected folder already exists as a project, should the app show a confirmation or silently activate it? | Resolved | Silently activate it and continue to chat. |
| 2 | Can the server current directory itself be selected as the new project root? | Resolved | Yes. The current directory is selectable. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the user is on `ProjectsPage`, when they tap `+`, then the server-side folder picker opens. *(REQ-001)*
- [ ] **[AC-002]** Given the picker opens, when it loads, then it starts at the server current directory and shows only folders. *(REQ-002, REQ-003)*
- [ ] **[AC-003]** Given the user navigates folders, when they tap a folder or go back, then the picker updates the current path correctly. *(REQ-004)*
- [ ] **[AC-004]** Given the user confirms a folder, when the operation succeeds, then the project is activated and the app navigates to `//chat`. *(REQ-005, REQ-006)*
- [ ] **[AC-005]** Given the selected folder already exists as a project, when the user confirms, then the existing project is activated without duplication. *(REQ-007)*
- [ ] **[AC-006]** Given folder loading or project creation fails, when the error occurs, then the sheet remains open and the error is shown inline. *(REQ-008, REQ-009)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- Key areas to investigate: reusing the existing file-tree navigation pattern, adding server app info retrieval, and wiring a create-or-activate project flow.
- Suggested implementation approach (if known): adapt the current add-project popup into a folder picker, or replace it with a dedicated folder-picker sheet reusing the same popup registration pattern.
- Constraints to respect: folders only, no local filesystem access, no duplicate project creation, and navigation to chat after activation.
- Related files or modules (if known):
  - `src/openMob.Core/ViewModels/ProjectsViewModel.cs`
  - `src/openMob.Core/ViewModels/FilePickerViewModel.cs`
  - `src/openMob.Core/Services/IFileService.cs`
  - `src/openMob.Core/Services/ProjectService.cs`
  - `src/openMob.Core/Services/ActiveProjectService.cs`
  - `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs`
- `src/openMob/Views/Pages/ProjectsPage.xaml`
  - `src/openMob/Views/Popups/AddProjectSheet.xaml`

---

## Technical Analysis

### Change Type
Feature

### Branch
`feature/new-project-folder-picker`

### Layers Involved
- `openMob.Core`: folder picker ViewModel, project registration flow, popup abstraction, API client contract
- `openMob`: new folder picker popup UI and popup registration
- `openMob.Tests`: ViewModel and service coverage for the new flow

### Implementation Order
1. Add/extend Core contracts for folder selection and project registration.
2. Implement the new folder picker ViewModel and popup UI.
3. Wire the MAUI popup service and DI registrations.
4. Update the Projects page flow to activate the selected project and navigate to chat.
5. Add/update tests for the project service, projects ViewModel, and folder picker ViewModel.

### Key Risks
- The server-side current directory lookup may fail or return an unexpected shape, so the picker must handle fallback/error states gracefully.
- Project registration is currently modeled through the existing session/project APIs, so the implementation must avoid duplicate project creation.
- The popup must stay responsive while loading folder listings and must not close on recoverable errors.
