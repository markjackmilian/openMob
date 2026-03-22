# Message Composer Popup — FAB Trigger with Inline Session Controls

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-22                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Replaces the inline textarea at the bottom of the chat page with a **Floating Action Button (FAB)** that opens a full-featured `MessageComposerSheet` — a UXDivers `PopupPage` that occupies the lower portion of the screen. The popup provides a large, comfortable writing area and inline controls for session-level settings (agent, think level, auto-accept) plus three picker buttons (subagent `@`, command `/`, file `#`) that insert tokens into the message text. The Send button is inside the popup and closes it on dispatch. This feature unifies message composition and session configuration into a single ergonomic surface, recovering screen real estate on the chat page.

---

## Scope

### In Scope
- Removal of the inline `Editor` / `InputBarView` from the chat page
- Addition of a FAB (always visible, even during streaming) as the sole message composition trigger
- New `MessageComposerSheet` — UXDivers `PopupPage` — with:
  - Large multi-line `Editor` (primary writing area, `AvoidKeyboard="True"`)
  - Session-level controls: **Agent** (chip/row), **Think Level** (segmented: Low / Medium / High), **Auto-Accept** (toggle)
  - Three picker buttons: **@ Subagent**, **/ Command**, **# File**
  - **Send** button (primary action, inside popup, closes popup on dispatch)
  - **Streaming guard**: Send disabled with label "Attendi risposta…" when streaming is in progress
- `MessageComposerViewModel` in `openMob.Core` managing all popup state
- `IAppPopupService.ShowMessageComposerAsync` + `MauiPopupService` implementation
- `FilePickerSheet` — new UXDivers `PopupPage` listing server-side project files
- `FilePickerViewModel` in `openMob.Core`
- `IFileService` / `FileService` in `openMob.Core` — fetches file list from opencode server API
- Uniform picker pattern: @ Subagent → `AgentPickerSheet` (subagent mode, already implemented), / Command → `CommandPaletteSheet` (already implemented), # File → `FilePickerSheet` (new); each inserts a token into the message text
- Session-level overrides: agent, think level, auto-accept changes in the popup apply to the **current session only** and are **not persisted** to `ProjectPreference` / SQLite
- `ChatViewModel` extended with `OpenMessageComposerCommand` and `IsStreaming` (already exists — verify binding)
- DI registration of `MessageComposerViewModel`, `FilePickerViewModel`, `MessageComposerSheet`, `FilePickerSheet`

### Out of Scope
- Removal of the existing `ContextSheet` (opened from the header — remains for persistent project-level preferences)
- Persistence of agent / think level / auto-accept changes made inside the composer popup
- Autocomplete inline (typing `/`, `@`, `#` in the text field does not trigger pickers)
- Upload of local device files (photos, documents)
- File content preview inside the picker
- Syntax highlighting in code blocks
- Voice input
- The `chat-page-redesign` spec (in-progress) defines the overall chat page layout; this spec only modifies the input area and adds the FAB

---

## Functional Requirements

> Requirements are numbered for traceability.

### FAB — Chat Page Trigger

1. **[REQ-001]** The inline `Editor` / `InputBarView` at the bottom of the chat page is removed. The chat page recovers the vertical space previously occupied by the input bar.

2. **[REQ-002]** A **Floating Action Button (FAB)** is added to the chat page, positioned at the bottom-right corner, above the safe area inset. The FAB is always visible regardless of streaming state, scroll position, or message list content.

3. **[REQ-003]** The FAB uses a **compose / edit icon** (e.g., `MaterialIcons.Edit` or `MaterialIcons.Create`). Size: 56×56pt. Background: `ColorPrimary`. Icon color: white. Elevation shadow on Android. The FAB must have a minimum touch target of 44×44pt (satisfied by its 56pt size).

4. **[REQ-004]** Tapping the FAB calls `ChatViewModel.OpenMessageComposerCommand`, which invokes `IAppPopupService.ShowMessageComposerAsync(projectId, sessionId, ct)`.

5. **[REQ-005]** The FAB does **not** change appearance or become disabled during streaming. The streaming guard is enforced inside the popup (REQ-016), not on the FAB itself.

### MessageComposerSheet — Layout and Presentation

6. **[REQ-006]** `MessageComposerSheet` is a UXDivers `PopupPage` with the following presentation properties:
   - `VerticalOptions="End"`, `HorizontalOptions="Fill"` — anchored to the bottom of the screen
   - `AvoidKeyboard="True"` — the popup content shifts up when the software keyboard appears, keeping the Editor and Send button visible
   - `SafeAreaAsPadding="Bottom"` — respects the home indicator / gesture bar on iOS
   - `CloseWhenBackgroundIsClicked="False"` — the user must explicitly dismiss (tap outside is disabled to prevent accidental loss of composed text)
   - `AppearingAnimation="{uxd:MoveInPopupAnimation MoveDirection=Bottom, Duration=300, Easing=CubicOut}"`
   - `DisappearingAnimation="{uxd:MoveOutPopupAnimation MoveDirection=Bottom, Duration=250, Easing=CubicIn}"`
   - `BackgroundColor="{DynamicResource PopupBackdropColor}"`
   - The popup panel itself: `RoundRectangle CornerRadius="20,20,0,0"`, `BackgroundColor="{DynamicResource BackgroundColor}"`, minimum height ~60% of screen height

7. **[REQ-007]** The popup panel layout (top to bottom):
   - **Handle bar**: 36×4pt, `RadiusFull`, `ColorOnBackgroundTertiary`, centered, `SpacingSm` top margin
   - **Session context row**: single horizontal row showing current agent name (chip) + think level (compact label) + auto-accept (icon indicator). Tapping individual elements opens their respective inline controls (see REQ-010 through REQ-012). `SpacingMd` horizontal padding, `SpacingSm` vertical padding.
   - **Editor area**: multi-line `Editor`, `AutoSize="TextChanges"`, minimum 4 lines, maximum 12 lines before scrolling. Placeholder: "Scrivi un messaggio…". `SpacingMd` horizontal padding. Fills available vertical space between the context row and the toolbar.
   - **Picker toolbar**: horizontal row of three equal-width buttons: `@ Subagent`, `/ Command`, `# File`. Each button: icon + short label, `FontSizeCaption1`, `ColorOnBackgroundSecondary`. `SpacingMd` horizontal padding, `SpacingSm` vertical padding.
   - **Action row**: `Send` button (right-aligned, `ColorPrimary`, `FontSizeBody`, `InterSemiBold`, `RadiusMd`, min width 80pt) + optional character count label (left-aligned, `FontSizeCaption2`, `ColorOnBackgroundTertiary`). `SpacingMd` horizontal padding, `SpacingMd` bottom padding.

8. **[REQ-008]** When `ShowMessageComposerAsync` is called, `MessageComposerViewModel.InitializeAsync(projectId, sessionId, ct)` is called **before** `IPopupService.Current.PushAsync(sheet)` (following the "initialize before push" pattern established in `adr-maui-popup-service-main-thread-guard`). The `PushAsync` call must be wrapped in `MainThread.InvokeOnMainThreadAsync(...)` because `InitializeAsync` contains `await` calls that may resume on a thread pool thread.

9. **[REQ-009]** `InitializeAsync` loads the current session context from `ChatViewModel` state (passed as initialization parameters or read from `IProjectPreferenceService.GetOrDefaultAsync`):
   - `SessionAgentName` (`string?`) — current agent for the session (initially from `ProjectPreference.AgentName`)
   - `SessionThinkingLevel` (`ThinkingLevel`) — current thinking level for the session (initially from `ProjectPreference.ThinkingLevel`)
   - `SessionAutoAccept` (`bool`) — current auto-accept for the session (initially from `ProjectPreference.AutoAccept`)
   - `MessageText` (`string`) — empty string on first open; preserved across opens within the same session if the popup is dismissed without sending (see REQ-017)

### Session Controls Inside the Popup

10. **[REQ-010]** **Agent control**: the session context row displays the current `SessionAgentName` (or "Default" if null) as a tappable chip. Tapping it calls `MessageComposerViewModel.SelectAgentCommand`, which invokes `IAppPopupService.ShowAgentPickerAsync(callback, ct)` (primary mode, already implemented). On selection, `SessionAgentName` is updated on the ViewModel. The change is **not** persisted to `ProjectPreference`.

11. **[REQ-011]** **Think Level control**: the session context row displays the current `SessionThinkingLevel` as a compact label (e.g., "Think: Medium"). Tapping it expands an inline segmented control (Low / Medium / High) below the context row, or opens a small action sheet. Selecting a value updates `SessionThinkingLevel` on the ViewModel. The change is **not** persisted.

12. **[REQ-012]** **Auto-Accept control**: the session context row displays an icon indicating the current `SessionAutoAccept` state (e.g., a checkmark badge or a toggle icon). Tapping it toggles `SessionAutoAccept` on the ViewModel. The change is **not** persisted.

### Picker Buttons — Token Insertion

13. **[REQ-013]** **@ Subagent button**: calls `MessageComposerViewModel.InsertSubagentCommand`, which invokes `IAppPopupService.ShowSubagentPickerAsync(callback, ct)` (subagent mode, implemented in `session-context-sheet-3of3`). On agent selection, inserts `@agentName` at the current cursor position in `MessageText` (or appends with a leading space if cursor position is unavailable).

14. **[REQ-014]** **/ Command button**: calls `MessageComposerViewModel.InsertCommandCommand`, which invokes `IAppPopupService.ShowCommandPaletteAsync(callback, ct)`. The `CommandPaletteSheet` must be adapted to accept an `Action<string>` callback (currently it executes commands directly — see Open Questions). On command selection, inserts `/commandName` at the current cursor position in `MessageText`.

15. **[REQ-015]** **# File button**: calls `MessageComposerViewModel.InsertFileCommand`, which invokes `IAppPopupService.ShowFilePickerAsync(callback, ct)`. On file selection, inserts `@relativePath` at the current cursor position in `MessageText`.

### Send and Streaming Guard

16. **[REQ-016]** The **Send button** is enabled only when `MessageText` is non-empty AND `IsStreaming` is `false`. When `IsStreaming` is `true`, the Send button is disabled and its label changes to "Attendi risposta…" (or an activity indicator replaces the label). The `IsStreaming` state is passed to `MessageComposerViewModel` during `InitializeAsync` and updated via a `WeakReferenceMessenger` message or a callback if streaming starts while the popup is open.

17. **[REQ-017]** When the user taps **Send**:
    - `MessageComposerViewModel.SendCommand` is invoked
    - The message text and session overrides (agent, think level, auto-accept) are passed back to `ChatViewModel` via a `MessageComposedMessage` (new `WeakReferenceMessenger` message type)
    - `IPopupService.Current.PopAsync(this)` closes the popup
    - `ChatViewModel` receives the message and dispatches the send operation using the session overrides

18. **[REQ-018]** When the popup is **dismissed without sending** (Android back button, or a future close button):
    - `MessageText` is preserved in `MessageComposerViewModel` so that re-opening the popup restores the draft
    - Session overrides (agent, think level, auto-accept) are discarded — they revert to the project preference values on next open
    - No message is sent

### FilePickerSheet

19. **[REQ-019]** `FilePickerSheet` is a UXDivers `PopupPage` (same presentation style as `AgentPickerSheet` and `CommandPaletteSheet`):
    - Slide-up from bottom animation
    - Title: "Select File"
    - Search field at top (filters list in real-time)
    - `CollectionView` of file entries: icon (file type) + relative path label + file name label
    - Empty state: "No files found" if search yields no results or API returns empty
    - Loading state: activity indicator while `IFileService.GetFilesAsync()` is in progress
    - Error state: inline error label if the API call fails

20. **[REQ-020]** `IFileService` exposes:
    ```
    Task<OpencodeResult<IReadOnlyList<FileDto>>> GetFilesAsync(CancellationToken ct = default)
    ```
    `FileDto` is a sealed record with at minimum: `string RelativePath`, `string Name`, `string? Type`.

21. **[REQ-021]** The opencode server endpoint for listing project files must be determined during Technical Analysis (see Open Questions). The `FileService` implementation calls `IOpencodeApiClient` following the `OpencodeResult<T>` pattern. The `x-opencode-directory` header is injected globally by `OpencodeApiClient.ExecuteAsync` — no special handling needed.

22. **[REQ-022]** `FilePickerViewModel` exposes:
    - `Files` (`ObservableCollection<FileDto>`) — full list loaded on init
    - `FilteredFiles` (`ObservableCollection<FileDto>`) — filtered by `SearchText`
    - `SearchText` (`string`) — bound to the search field; `OnSearchTextChanged` filters `FilteredFiles`
    - `IsLoading` (`bool`)
    - `ErrorMessage` (`string?`)
    - `SelectFileCommand` (`[RelayCommand]`) — accepts `FileDto`, invokes `OnFileSelected` callback, pops the sheet

### MessageComposedMessage

23. **[REQ-023]** A new `MessageComposedMessage` sealed record is added to `openMob.Core/Messages/`:
    ```csharp
    sealed record MessageComposedMessage(
        string ProjectId,
        string SessionId,
        string Text,
        string? AgentOverride,
        ThinkingLevel ThinkingLevelOverride,
        bool AutoAcceptOverride
    );
    ```
    `ChatViewModel` subscribes to this message in its constructor and unregisters in `Dispose()` (following the `WeakReferenceMessenger` pattern established in `adr-weakreferencemessenger-viewmodel-communication`).

24. **[REQ-024]** When `ChatViewModel` receives a `MessageComposedMessage`, it dispatches the send operation on the main thread using the override values. The session overrides are used for this single message only — they do not mutate `ChatViewModel`'s persistent state properties (`SelectedAgentName`, `ThinkingLevel`, `AutoAccept`).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatPage.xaml` | **Modified** — remove `InputBarView`, add FAB | FAB positioned with `AbsoluteLayout` or `Grid` overlay at bottom-right |
| `ChatViewModel.cs` | **Extended** — add `OpenMessageComposerCommand`; subscribe to `MessageComposedMessage` | `IsStreaming` already exists — verify it is observable |
| `InputBarView.xaml` | **Removed** from chat page (file may be retained for other uses or deleted) | Coordinate with `chat-page-redesign` spec |
| `MessageComposerSheet.xaml` + `.xaml.cs` | **New** — UXDivers `PopupPage` | `src/openMob/Views/Popups/` |
| `MessageComposerViewModel.cs` | **New** — `openMob.Core` | `src/openMob.Core/ViewModels/` |
| `FilePickerSheet.xaml` + `.xaml.cs` | **New** — UXDivers `PopupPage` | `src/openMob/Views/Popups/` |
| `FilePickerViewModel.cs` | **New** — `openMob.Core` | `src/openMob.Core/ViewModels/` |
| `IFileService.cs` + `FileService.cs` | **New** — `openMob.Core` | `src/openMob.Core/Services/` |
| `IAppPopupService.cs` | **Extended** — add `ShowMessageComposerAsync`, `ShowFilePickerAsync` | `src/openMob.Core/Services/` |
| `MauiPopupService.cs` | **Extended** — implement new Show* methods with `MainThread` guard | `src/openMob/Services/` |
| `IOpencodeApiClient.cs` | **Possibly extended** — add file listing method (TBD) | `src/openMob.Core/Infrastructure/Http/` |
| `OpencodeApiClient.cs` | **Possibly extended** — implement file listing | `src/openMob.Core/Infrastructure/Http/` |
| `MessageComposedMessage.cs` | **New** — `openMob.Core/Messages/` | New message type |
| `CoreServiceExtensions.cs` | **Extended** — register `MessageComposerViewModel`, `FilePickerViewModel`, `IFileService` | `src/openMob.Core/Infrastructure/DI/` |
| `MauiProgram.cs` | **Extended** — `AddTransientPopup<MessageComposerSheet, MessageComposerViewModel>()`, `AddTransientPopup<FilePickerSheet, FilePickerViewModel>()` | `src/openMob/` |
| `CommandPaletteSheet` | **Possibly modified** — add `Action<string>` callback mode (see Open Questions) | `src/openMob/Views/Popups/` |

### Dependencies
- `UXDivers.Popups.Maui` v0.9.4 — already installed (`adr-uxdivers-popups-adoption`)
- `IAppPopupService.ShowAgentPickerAsync` — already implemented (`session-context-sheet-2of3-agent-model`)
- `IAppPopupService.ShowSubagentPickerAsync` — implemented in `session-context-sheet-3of3` (in-progress)
- `IAppPopupService.ShowCommandPaletteAsync` — already implemented (`chat-page-redesign`)
- `AgentPickerSheet` (primary mode) — already implemented
- `CommandPaletteSheet` — already implemented (may need callback adaptation)
- `WeakReferenceMessenger` pattern — established in `adr-weakreferencemessenger-viewmodel-communication`
- `MainThread.InvokeOnMainThreadAsync` guard — established in `adr-maui-popup-service-main-thread-guard`
- opencode file listing API endpoint — **to be determined** (Open Question #1)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | What is the opencode server endpoint for listing project files? Candidates: `GET /file`, `GET /ls`, `GET /project/files`, or a directory listing endpoint. Must be verified against the running opencode API or its OpenAPI spec. | **Open** | To be determined during Technical Analysis |
| 2 | Is the file path inserted as relative (e.g., `@src/foo.ts`) or absolute? | **Open** | Likely relative to the project root (consistent with opencode `@` mention syntax). To be confirmed against opencode behavior. |
| 3 | `CommandPaletteSheet` currently executes commands directly when selected. For the composer popup, we need it to return the command name as a token to insert into the text. Should we: (a) add a callback mode to `CommandPaletteViewModel` (similar to `AgentPickerViewModel`'s `OnAgentSelected`), or (b) create a separate `CommandPickerSheet` for token insertion? | **Open** | Recommended: option (a) — add `Action<string>? OnCommandSelected` callback to `CommandPaletteViewModel`; when set, selection inserts the token instead of executing. Consistent with the agent picker pattern. |
| 4 | How is `IsStreaming` communicated to `MessageComposerViewModel` if streaming starts while the popup is already open? Options: (a) `WeakReferenceMessenger` `StreamingStateChangedMessage`, (b) pass a `Func<bool>` delegate, (c) poll `ChatViewModel` state. | **Open** | Recommended: option (a) — new `StreamingStateChangedMessage` sent by `ChatViewModel` when streaming starts/stops. `MessageComposerViewModel` subscribes and updates `IsStreaming`. |
| 5 | Should the `MessageComposerSheet` have an explicit **close/dismiss button** (X) in addition to the Android back button? | **Open** | Recommended: yes — a small `X` icon button in the top-right of the handle bar area, calling `IPopupService.Current.PopAsync(this)` without sending. Improves discoverability on iOS where there is no back button. |
| 6 | Draft persistence scope: should `MessageText` be preserved only for the current app session (in-memory in `MessageComposerViewModel`), or persisted to SQLite across app restarts? | **Open** | Recommended: in-memory only (simpler, no migration needed). The ViewModel is Transient — the draft lives as long as the ViewModel instance. If the app is killed, the draft is lost. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the chat page is visible, when looking at the bottom of the screen, then no inline textarea is present; a FAB with a compose icon is visible at the bottom-right corner. *(REQ-001, REQ-002, REQ-003)*
- [ ] **[AC-002]** Given the chat page, when tapping the FAB, then `MessageComposerSheet` slides up from the bottom with a smooth animation; the Editor is focused and the keyboard appears. *(REQ-004, REQ-006, REQ-008)*
- [ ] **[AC-003]** Given the composer popup is open and the keyboard is visible, when the keyboard appears, then the popup content (Editor + Send button) shifts up and remains fully visible above the keyboard. *(REQ-006 — `AvoidKeyboard="True"`)*
- [ ] **[AC-004]** Given the composer popup is open, when looking at the session context row, then the current agent name, think level, and auto-accept state are displayed. *(REQ-007, REQ-009)*
- [ ] **[AC-005]** Given the composer popup is open, when tapping the agent chip, then `AgentPickerSheet` opens in primary mode; selecting an agent updates the agent chip in the popup without persisting to `ProjectPreference`. *(REQ-010)*
- [ ] **[AC-006]** Given the composer popup is open, when tapping the think level label, then a segmented control (Low / Medium / High) appears; selecting a value updates the label without persisting. *(REQ-011)*
- [ ] **[AC-007]** Given the composer popup is open, when tapping the auto-accept icon, then `SessionAutoAccept` toggles without persisting. *(REQ-012)*
- [ ] **[AC-008]** Given the composer popup is open, when tapping "@ Subagent", then `AgentPickerSheet` opens in subagent mode; selecting an agent inserts `@agentName` into the message text at the cursor position. *(REQ-013)*
- [ ] **[AC-009]** Given the composer popup is open, when tapping "/ Command", then `CommandPaletteSheet` opens in callback mode; selecting a command inserts `/commandName` into the message text. *(REQ-014)*
- [ ] **[AC-010]** Given the composer popup is open, when tapping "# File", then `FilePickerSheet` opens with a list of project files from the server; selecting a file inserts `@relativePath` into the message text. *(REQ-015, REQ-019, REQ-020)*
- [ ] **[AC-011]** Given streaming is in progress and the composer popup is open, when looking at the Send button, then it is disabled and shows "Attendi risposta…". *(REQ-016)*
- [ ] **[AC-012]** Given the composer popup is open with non-empty text and no streaming, when tapping Send, then the popup closes, `ChatViewModel` receives a `MessageComposedMessage` with the text and session overrides, and the message is dispatched. *(REQ-017, REQ-023, REQ-024)*
- [ ] **[AC-013]** Given the composer popup is dismissed without sending (back button or close button), when re-opening the popup in the same session, then the previously typed text is restored in the Editor. *(REQ-018)*
- [ ] **[AC-014]** Given the FAB is visible and streaming is in progress, when looking at the FAB, then it is still visible and tappable (streaming guard is inside the popup, not on the FAB). *(REQ-005)*
- [ ] **[AC-015]** Given `ChatViewModel` receives a `MessageComposedMessage` with `AgentOverride = "myagent"`, when the message is dispatched, then `ChatViewModel.SelectedAgentName` is **not** mutated — the override is used only for that single send operation. *(REQ-024)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate

1. **opencode file listing endpoint (critical — REQ-021)**: Before implementing `IFileService`, the implementer must:
   - Inspect the running opencode server's OpenAPI spec (`GET /api` or equivalent)
   - Search `IOpencodeApiClient` for any existing file-related methods
   - Identify the correct endpoint (candidates: `GET /file`, `GET /ls`, `GET /project/files`)
   - If the endpoint returns a tree structure, flatten it to a list of `FileDto` with `RelativePath`
   - If no endpoint exists, `FilePickerSheet` must show an error state: "File listing not supported by this server version"
   - Document the chosen endpoint in the Technical Analysis section

2. **`AvoidKeyboard` + bottom-anchored popup**: UXDivers `PopupPage` with `AvoidKeyboard="True"` shifts the popup content via `TranslationY`. Since the popup is anchored to the bottom (`VerticalOptions="End"`), the content will shift upward when the keyboard appears. Verify this behaves correctly on both iOS and Android. The `SafeAreaAsPadding="Bottom"` must be set to avoid double-offsetting with the keyboard inset on iOS.

3. **`MessageComposerViewModel` as Transient**: The ViewModel is Transient (new instance per popup open). Draft text (`MessageText`) is preserved in the ViewModel instance as long as the popup is not garbage-collected. Since `MauiPopupService` resolves the sheet from DI (`_serviceProvider.GetRequiredService<MessageComposerSheet>()`), a new instance is created on each `ShowMessageComposerAsync` call. To preserve the draft across opens, either: (a) register `MessageComposerViewModel` as Singleton (not recommended — leaks state), or (b) store the draft in a dedicated `IDraftService` (simple in-memory dictionary keyed by `sessionId`). **Recommended: option (b)** — a lightweight `IDraftService` with `GetDraft(sessionId)` / `SaveDraft(sessionId, text)` / `ClearDraft(sessionId)` methods.

4. **`CommandPaletteViewModel` callback adaptation**: The current `CommandPaletteViewModel` executes commands directly. Adding `Action<string>? OnCommandSelected` (same pattern as `AgentPickerViewModel.OnAgentSelected`) allows dual-mode: when the callback is set, selection invokes the callback instead of executing. `MauiPopupService.ShowCommandPaletteAsync` already exists — a new overload `ShowCommandPaletteAsync(Action<string> onCommandSelected, ct)` should be added to `IAppPopupService`.

5. **`IsStreaming` propagation to open popup**: `ChatViewModel` already tracks streaming state. A new `StreamingStateChangedMessage(string ProjectId, string SessionId, bool IsStreaming)` should be sent via `WeakReferenceMessenger` when streaming starts and stops. `MessageComposerViewModel` subscribes in its constructor and unregisters in `Dispose()`. The handler must dispatch to the main thread via `_dispatcher.Dispatch(...)` before mutating `IsStreaming`.

6. **FAB positioning on chat page**: The FAB should be placed using an `AbsoluteLayout` overlay or a `Grid` with `RowSpan` so it floats above the message list without pushing content up. Coordinate with the `chat-page-redesign` spec layout (`Grid(Auto,*,Auto)`) — the FAB replaces the `Auto` bottom row previously occupied by `InputBarView`.

7. **`PopupResultPage<string>` vs `WeakReferenceMessenger`**: Two patterns are available for returning the composed message to `ChatViewModel`:
   - `PopupResultPage<MessageComposedMessage>` — `PushAsync` returns the result directly to the caller (`MauiPopupService`), which then needs to forward it to `ChatViewModel`. This creates coupling between `MauiPopupService` and `ChatViewModel`.
   - `WeakReferenceMessenger` with `MessageComposedMessage` — decoupled, consistent with the existing pattern for `ProjectPreferenceChangedMessage`. **Recommended: `WeakReferenceMessenger`** — aligns with `adr-weakreferencemessenger-viewmodel-communication`.

### Suggested Implementation Approach

**Phase A — Core (om-mobile-core):**
1. Create `MessageComposedMessage.cs` in `openMob.Core/Messages/`
2. Create `StreamingStateChangedMessage.cs` in `openMob.Core/Messages/`
3. Create `IDraftService.cs` + `DraftService.cs` (in-memory, keyed by sessionId)
4. Create `IFileService.cs` + `FileService.cs` (after API endpoint is confirmed)
5. Create `FileDto.cs` sealed record
6. Create `FilePickerViewModel.cs`
7. Create `MessageComposerViewModel.cs` (depends on `IProjectPreferenceService`, `IAppPopupService`, `IDraftService`, `IDispatcher`)
8. Extend `IAppPopupService` with `ShowMessageComposerAsync`, `ShowFilePickerAsync`, `ShowCommandPaletteAsync(Action<string>, ct)` overload
9. Extend `ChatViewModel`: add `OpenMessageComposerCommand`, subscribe to `MessageComposedMessage`, send `StreamingStateChangedMessage` on streaming state changes
10. Register new services and ViewModels in `CoreServiceExtensions.cs`

**Phase B — UI (om-mobile-ui):**
1. Create `FilePickerSheet.xaml` + `.xaml.cs` (UXDivers `PopupPage`)
2. Create `MessageComposerSheet.xaml` + `.xaml.cs` (UXDivers `PopupPage`, `AvoidKeyboard="True"`)
3. Modify `ChatPage.xaml`: remove `InputBarView`, add FAB
4. Extend `MauiPopupService`: implement `ShowMessageComposerAsync` (with `MainThread` guard), `ShowFilePickerAsync`, `ShowCommandPaletteAsync` callback overload
5. Register new popups in `MauiProgram.cs` via `AddTransientPopup<>()`
6. Adapt `CommandPaletteSheet` for callback mode (if needed)

### Constraints to Respect

- **`AvoidKeyboard="True"` must be set before `PushAsync`** (UXDivers constraint — changing it after push has no effect)
- **`MainThread.InvokeOnMainThreadAsync` guard** required in `ShowMessageComposerAsync` because `InitializeAsync` contains `await` calls (established in `adr-maui-popup-service-main-thread-guard`)
- **No persistence of session overrides**: agent, think level, auto-accept changes in the popup must NOT call `IProjectPreferenceService.Set*Async`. They are in-memory only on `MessageComposerViewModel`.
- **`WeakReferenceMessenger.UnregisterAll(this)` must be the first line of `MessageComposerViewModel.Dispose()`** (established in `adr-weakreferencemessenger-viewmodel-communication`)
- **`ConfigureAwait(false)`** on all `await` calls in `openMob.Core` services and ViewModels (except ViewModels — see `adr-configureawait-viewmodels`)
- **Layer separation**: `MessageComposerViewModel` and `FilePickerViewModel` live in `openMob.Core`. Zero MAUI dependencies. `IDispatcher` injected for main-thread dispatch.
- **`AddTransientPopup<TPopup, TViewModel>()`** is the correct DI registration for UXDivers popups (established in `adr-uxdivers-popups-adoption`)

### Related Files or Modules

| File | Relevance |
|------|-----------|
| `src/openMob/Views/Pages/ChatPage.xaml` | Remove InputBarView, add FAB |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Add `OpenMessageComposerCommand`, `MessageComposedMessage` subscription, `StreamingStateChangedMessage` publishing |
| `src/openMob/Views/Controls/InputBarView.xaml` | To be removed from chat page (coordinate with `chat-page-redesign`) |
| `src/openMob.Core/Services/IAppPopupService.cs` | Add `ShowMessageComposerAsync`, `ShowFilePickerAsync`, `ShowCommandPaletteAsync` overload |
| `src/openMob/Services/MauiPopupService.cs` | Implement new Show* methods |
| `src/openMob/MauiProgram.cs` | Register new popups |
| `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` | Register new services/ViewModels |
| `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` | Possibly add file listing method |
| `src/openMob.Core/Infrastructure/Http/OpencodeApiClient.cs` | Possibly implement file listing |
| `src/openMob/Views/Popups/CommandPaletteSheet.xaml` | Possibly adapt for callback mode |
| `src/openMob.Core/ViewModels/CommandPaletteViewModel.cs` | Possibly add `OnCommandSelected` callback |
| `src/openMob.Core/Messages/` | New message types |

### References to Past Decisions

- As established in **`adr-uxdivers-popups-adoption`** (2026-03-22): all custom popups extend `PopupPage`; DI registration via `AddTransientPopup<TPopup, TViewModel>()`; `IPopupService.Current.PushAsync/PopAsync` for navigation.
- As established in **`adr-maui-popup-service-main-thread-guard`** (2026-03-22): any `Show*Async` method that `await`s before `PushAsync` must wrap `PushAsync` in `MainThread.InvokeOnMainThreadAsync(...)`.
- As established in **`adr-weakreferencemessenger-viewmodel-communication`** (2026-03-22): `WeakReferenceMessenger` is the decoupled communication pattern between ViewModels; `UnregisterAll(this)` must be first line of `Dispose()`.
- As established in **`session-context-sheet-1of3-core`** (2026-03-19): `ThinkingLevel` enum (`Low=0`, `Medium=1`, `High=2`), `ProjectPreference` fields, `IProjectPreferenceService.GetOrDefaultAsync`.
- As established in **`session-context-sheet-2of3-agent-model`** (2026-03-19): `AgentPickerViewModel.OnAgentSelected` callback pattern; `IAppPopupService.ShowAgentPickerAsync(Action<string?>, ct)`.
- As established in **`session-context-sheet-3of3-thinking-autoaccept-subagent`** (in-progress): `IAppPopupService.ShowSubagentPickerAsync(Action<string>, ct)`.
- As established in **`chat-page-redesign`** (in-progress): `CommandPaletteSheet` and `IAppPopupService.ShowCommandPaletteAsync` are already implemented; `InputBarView` is the current input component to be replaced.
- As established in **`adr-safe-fire-and-forget-callback-pattern`** (2026-03-21): `Action<T>` callbacks that need async work must use the safe fire-and-forget pattern (`_ = SafeMethodAsync(arg)` with try/catch + Sentry).
