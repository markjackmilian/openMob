# Chat Page Redesign — Context-Aware Cockpit with Markdown Rendering

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-18                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Redesign the chat page from a bilateral messenger-style layout to a **full-width, notebook-style cockpit** optimized for mobile. The current UI lacks visibility into the active project, session name, agent, and model, and provides no controls for thinking level, auto-accept, commands, or subagent invocation. This spec replaces the bubble-based message layout with full-width message blocks featuring Markdown rendering, introduces a compact contextual header with a unified Context Sheet for all session controls, and adds a Command Palette for invoking commands and subagents — all without leaving the conversation.

**Supersedes visual aspects of:** spec `2026-03-14-chat-ui-design-guidelines` (message bubbles, header layout). Navigation model (Shell Flyout for sessions) and input bar fundamentals are preserved.

---

## Scope

### In Scope
- New message layout: full-width message blocks replacing bilateral bubbles
- Visual sender distinction via colored left border bar + sender label
- Markdown rendering in agent messages (headers, bold, italic, lists, code blocks, tables, links, blockquotes)
- Simplified single-row header with editable session name
- Collapsible context status bar showing project, model, and thinking level
- Unified Context Sheet (bottom sheet) for all session-level controls: project, agent, model, thinking level, auto-accept, subagent invocation
- Command Palette bottom sheet with searchable command list
- Explicit subagent invocation UI
- Subagent activity indicator in the message flow
- New design tokens for message blocks, Markdown elements, context bar, and sheets
- Dark/light mode support for all new components
- Accessibility: 44x44pt touch targets, semantic labels

### Out of Scope
- Server connection management (covered by existing specs)
- Complete Settings page (separate spec)
- File browser / symbol search UI
- Onboarding / first-run flow
- Rich media messages (images, file attachments)
- Syntax highlighting in code blocks (future enhancement)
- SSE networking fixes (separate spec)
- Push notifications UI
- Voice input

---

## Functional Requirements

> Requirements are numbered for traceability.

### Message Layout

1. **[REQ-001]** Messages occupy 100% of the available width between horizontal page padding (SpacingMd / 12pt each side). The bilateral bubble layout (80% max width, left/right alignment) is removed.

2. **[REQ-002]** Each message block has a **colored vertical bar** (3pt wide, full height of the message) on the left edge as sender indicator:
   - User messages: `ColorPrimary` bar
   - Agent messages: `ColorAgentAccent` bar (new token — a secondary accent distinct from primary)
   - Subagent messages: `ColorSubagentAccent` bar (new token — tertiary accent)

3. **[REQ-003]** Each message block displays a **sender label** above the content:
   - User messages: "You" — FontSizeCaption1, InterSemiBold, ColorPrimary
   - Agent messages: agent display name (e.g., "Claude Sonnet") — FontSizeCaption1, InterSemiBold, ColorAgentAccent
   - Subagent messages: subagent name with badge — FontSizeCaption1, InterSemiBold, ColorSubagentAccent

4. **[REQ-004]** **User message blocks**: compact presentation. ColorSurfaceSecondary background, SpacingSm (8pt) vertical padding, SpacingMd (12pt) horizontal padding (after the bar). Body text in FontSizeBody, InterRegular, ColorOnBackground. Single-line or few-line display of the prompt sent.

5. **[REQ-005]** **Agent message blocks**: full-width content area. ColorBackground (transparent/page background), SpacingSm (8pt) vertical padding, SpacingMd (12pt) horizontal padding (after the bar). Content rendered as Markdown (see REQ-008 through REQ-016).

6. **[REQ-006]** Vertical spacing between message blocks: SpacingMd (12pt). Consecutive messages from the same sender: SpacingSm (8pt), sender label hidden on subsequent messages.

7. **[REQ-007]** Timestamp displayed below each message block: FontSizeCaption2 (11pt), InterRegular, ColorOnBackgroundTertiary. Format: "HH:mm" (today) / "Yesterday HH:mm" / "MMM dd, HH:mm". Aligned to the right edge of the message block.

### Markdown Rendering

8. **[REQ-008]** Agent messages must render Markdown content natively (not in a WebView). The rendering engine must convert Markdown text into native MAUI views.

9. **[REQ-009]** **Headers**: h1 through h6 rendered with decreasing font sizes. h1: FontSizeTitle1 InterBold. h2: FontSizeTitle2 InterBold. h3: FontSizeTitle3 InterSemiBold. h4: FontSizeHeadline InterSemiBold. h5: FontSizeSubheadline InterMedium. h6: FontSizeFootnote InterMedium. All in ColorOnBackground. SpacingMd top margin, SpacingSm bottom margin.

10. **[REQ-010]** **Inline formatting**: bold (**text**) → InterBold. Italic (*text*) → italic style. Bold+italic (***text***) → InterBold + italic. Inline code (`code`) → InterRegular monospace, ColorCodeInline background (new token), RadiusXs, 2pt horizontal padding.

11. **[REQ-011]** **Lists**: unordered (bullet) and ordered (numbered) lists. SpacingSm (8pt) left indent per nesting level. Bullet: "•" character in ColorOnBackgroundSecondary. Numbers: "1.", "2.", etc. Line spacing: SpacingXs (4pt) between items.

12. **[REQ-012]** **Code blocks**: fenced code blocks (```) rendered in a container with ColorCodeBlockBackground (new token) background, RadiusMd corners, SpacingMd padding. Monospace font (platform default monospace). FontSizeCaption1. Horizontal scroll if content overflows. Optional language label top-right in FontSizeCaption2, ColorOnBackgroundTertiary.

13. **[REQ-013]** **Tables**: rendered as a horizontally scrollable grid. Header row: InterSemiBold, ColorSurfaceSecondary background. Body rows: InterRegular, alternating ColorBackground / ColorSurfaceSecondary. Cell padding: SpacingSm. Border: 1px ColorSeparator. FontSizeCaption1.

14. **[REQ-014]** **Links**: rendered as tappable text in ColorPrimary, underlined. Tap opens the URL in the system browser via `Launcher.OpenAsync()`.

15. **[REQ-015]** **Blockquotes**: left border 3pt ColorOutline, SpacingMd left padding, italic text in ColorOnBackgroundSecondary. Background: ColorSurfaceSecondary with RadiusSm.

16. **[REQ-016]** **Horizontal rules** (---): 1px ColorSeparator line, SpacingMd vertical margin.

### Header

17. **[REQ-017]** The header is a **single row**, height 56pt Android / 44pt+safe area iOS:
    - Left: hamburger icon (MaterialIcons.Menu, 44x44pt touch target) → opens Shell Flyout
    - Center: session name (FontSizeHeadline, InterSemiBold, ColorOnBackground, ellipsis if truncated, tappable → triggers rename)
    - Right: context icon (MaterialIcons.MoreVert or MaterialIcons.Settings, 44x44pt touch target) → opens Context Sheet

18. **[REQ-018]** Header background: ColorSurface. Bottom border: 1px ColorSeparator. Horizontal padding: SpacingLg (16pt).

### Context Status Bar

19. **[REQ-019]** Immediately below the header, a **context status bar** (single row, ~28pt height) displays a condensed read-only summary of the active session context:
    ```
    ProjectName · ModelName · thinking: level
    ```
    FontSizeCaption1, InterRegular, ColorOnBackgroundSecondary. Centered horizontally. Background: ColorBackground (seamless with page).

20. **[REQ-020]** Status bar content priority (when horizontal space is insufficient, items are hidden right-to-left):
    1. Project name (always visible, ellipsis if needed)
    2. Model name (hidden if space < threshold)
    3. Thinking level (hidden first)
    Separator: " · " (middle dot with spaces) in ColorOnBackgroundTertiary.

21. **[REQ-021]** The entire status bar is tappable — tap opens the Context Sheet (same as the header context icon).

22. **[REQ-022]** The status bar **collapses** (hides with 150ms animation) when the user scrolls down in the message list, and **reappears** when the user scrolls up or reaches the top. This maximizes vertical space for message content.

### Session Name Editing

23. **[REQ-023]** Tapping the session name in the header opens an **edit modal** (alert dialog or bottom sheet with a single text field) pre-filled with the current session name. Confirm saves the new name via the session service API. Cancel discards changes.

24. **[REQ-024]** The session name is updated in the header and in the flyout session list immediately upon confirmation.

### Context Sheet

25. **[REQ-025]** The Context Sheet is a **bottom sheet** (half-screen height, draggable to expand/dismiss) that serves as the single control panel for all session-level settings. It is opened by:
    - Tapping the context icon in the header (REQ-017)
    - Tapping the context status bar (REQ-021)

26. **[REQ-026]** Context Sheet layout (top to bottom):
    - **Handle bar** (centered, 36pt wide, 4pt height, RadiusFull, ColorOnBackgroundTertiary)
    - **Section: Project** — displays active project name with folder icon. Tappable row → opens ProjectSwitcherSheet (existing).
    - **Section: Agent** — displays active agent name with SmartToy icon. Tappable row → opens AgentPickerSheet (existing).
    - **Section: Model** — displays active model name with provider icon. Tappable row → opens ModelPickerSheet (existing).
    - **Divider** (1px ColorSeparator, SpacingMd vertical margin)
    - **Section: Thinking Level** — label + segmented control with options: "Low", "Medium", "High". Current value highlighted with ColorPrimary.
    - **Section: Auto-Accept** — label + toggle switch. Description text: "Automatically accept agent suggestions" in FontSizeCaption1, ColorOnBackgroundSecondary.
    - **Divider**
    - **Section: Invoke Subagent** — tappable row with SmartToy icon + "Invoke Subagent..." label → opens AgentPickerSheet in subagent mode (see REQ-031).

27. **[REQ-027]** Each tappable row in the Context Sheet: 56pt height, SpacingLg horizontal padding, icon (24pt, ColorOnBackgroundSecondary) + label (FontSizeBody, InterRegular, ColorOnBackground) + current value (FontSizeBody, InterRegular, ColorOnBackgroundSecondary, right-aligned) + chevron right icon. 44x44pt touch target.

28. **[REQ-028]** Thinking level and auto-accept changes are applied immediately (no save button) and persisted via the config API (`UpdateConfigAsync`). The context status bar updates in real-time.

### Command Palette

29. **[REQ-029]** A **command button** is added to the input bar area, to the left of the text input field (replacing or repositioning the existing attach "+" button):
    - Icon: MaterialIcons.Terminal or MaterialIcons.Code (slash-like icon), 36pt, 44x44pt touch target
    - Tap opens the Command Palette bottom sheet

30. **[REQ-030]** The Command Palette is a **bottom sheet** (expandable to ~70% screen height) containing:
    - **Search field** at the top: pill-shaped, placeholder "Search commands...", filters the list in real-time as the user types
    - **Command list**: populated from `GetCommandsAsync()`. Each row shows command name (FontSizeBody, InterSemiBold) + description if available (FontSizeCaption1, ColorOnBackgroundSecondary). Tappable → executes the command via the appropriate API call.
    - **Empty state**: "No commands found" if search yields no results
    - Commands are loaded once when the sheet opens and cached for the session lifetime. Pull-to-refresh to reload.

### Subagent Invocation and Indicators

31. **[REQ-031]** When "Invoke Subagent" is tapped in the Context Sheet, the AgentPickerSheet opens in **subagent mode**: the sheet title changes to "Invoke Subagent", and selecting an agent sends the subagent invocation request to the server rather than changing the primary agent.

32. **[REQ-032]** When a subagent is active (detected via SSE events or message metadata), a **subagent activity indicator** is displayed inline in the message flow:
    - Card-style container: ColorSurfaceSecondary background, RadiusMd, SpacingSm padding, full width
    - Content: SmartToy icon (animated pulse) + subagent name (InterSemiBold) + status text ("Working...", "Completed") in ColorOnBackgroundSecondary
    - The indicator transitions from "Working..." (with animation) to "Completed" (static) when the subagent finishes

33. **[REQ-033]** Subagent messages in the conversation flow use the subagent color bar (REQ-002) and display the subagent name as sender label (REQ-003), visually distinguishing them from the primary agent's messages.

### Input Bar Updates

34. **[REQ-034]** The input bar layout is updated to:
    - Left: command button (REQ-029)
    - Center: expandable Editor (unchanged behavior — 1 line default, 5 lines max, AutoSize)
    - Right: send button (unchanged behavior — visible only when text is non-empty, ColorPrimary, fade-in animation)

35. **[REQ-035]** The input bar retains all existing behaviors: keyboard pushes it up, send button pulse animation, placeholder text "Message...".

### Empty State

36. **[REQ-036]** The empty state view is updated to reflect the new layout but retains the core pattern: centered icon + title "How can I help you?" + subtitle. Suggestion chips remain horizontally scrollable above the input bar.

37. **[REQ-037]** When the session has no messages, the context status bar remains visible (does not collapse) to orient the user on the active context.

### Design Tokens

38. **[REQ-038]** New color tokens to add (Light/Dark pairs, following established AppThemeBinding pattern):

    | Token | Purpose |
    |-------|---------|
    | ColorAgentAccent / Light+Dark | Agent message left bar and sender label |
    | ColorSubagentAccent / Light+Dark | Subagent message left bar and sender label |
    | ColorCodeInline / Light+Dark | Inline code background |
    | ColorCodeBlockBackground / Light+Dark | Fenced code block background |
    | ColorMessageUserBackground / Light+Dark | User message block background |
    | ColorContextBar / Light+Dark | Context status bar background (if distinct from page) |

39. **[REQ-039]** New sizing/spacing tokens:

    | Token | Value | Purpose |
    |-------|-------|---------|
    | SizeMessageBarWidth | 3 | Left color bar width (pt) |
    | SizeContextBarHeight | 28 | Context status bar height (pt) |
    | SizeSheetHandle | 36x4 | Bottom sheet handle dimensions |

### General

40. **[REQ-040]** All new components must support dark/light mode via AppThemeBinding inline (following the established pattern — no standalone AppThemeBinding in ResourceDictionary).

41. **[REQ-041]** All interactive elements must have a minimum touch target of 44x44pt.

42. **[REQ-042]** The Shell Flyout for session list navigation remains unchanged. The flyout session list must reflect session name changes made via the header edit (REQ-024).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| ChatPage.xaml | **Major redesign** | New Grid layout: header (Auto) + status bar (Auto) + messages (*) + input bar (Auto) |
| MessageBubbleView.xaml | **Replaced** | Removed. New MessageBlockView.xaml with full-width layout |
| InputBarView.xaml | **Modified** | Attach button replaced by command button; layout adjusted |
| ChatViewModel.cs | **Extended** | New properties: SessionName, ThinkingLevel, AutoAccept, Commands, IsSubagentActive, SubagentName. New commands: RenameSessionCommand, OpenContextSheetCommand, OpenCommandPaletteCommand, InvokeSubagentCommand, ChangeThinkingLevelCommand, ToggleAutoAcceptCommand |
| ChatPage header area | **Redesigned** | Single row + collapsible status bar replaces current 2-element header |
| AgentPickerSheet | **Extended** | Subagent mode (different title, different action on selection) |
| EmptyStateView.xaml | **Minor update** | Layout adjustment for new message area |
| Colors.xaml | **Extended** | 6 new color token pairs |
| Styles.xaml | **Extended** | New styles for MessageBlock, ContextBar, CommandPalette, SubagentIndicator |
| AppShell.xaml | **No change** | Flyout structure preserved |

### Dependencies
- `IOpencodeApiClient.GetCommandsAsync()` — for Command Palette content (implemented in opencode-api-client spec)
- `IOpencodeApiClient.GetAgentsAsync()` — for agent/subagent lists (implemented)
- `IOpencodeApiClient.UpdateConfigAsync()` — for persisting thinking level and auto-accept
- `ISessionService` — for session rename
- `AgentPickerSheet` / `ProjectSwitcherSheet` / `ModelPickerSheet` — existing popup sheets (from app-navigation-structure spec)
- SSE events — for detecting subagent activity (partially implemented, known SSE issues)
- Markdown parsing library or custom implementation — to be determined in Technical Analysis

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Which approach for Markdown rendering in MAUI? Options: (a) custom parser building native Label/Layout views, (b) WebView with local HTML, (c) third-party library (e.g., Markdig + custom renderer). | Open | To be evaluated in Technical Analysis. Native rendering preferred for performance and theme consistency. |
| 2 | Do commands from `GetCommandsAsync()` accept structured arguments, or are they name-only triggers? | Open | Depends on opencode API schema. If arguments are needed, the Command Palette must support an argument input step. |
| 3 | How are thinking level and auto-accept communicated to the server? Via `UpdateConfigAsync` on the config API group? | Open | To be verified against opencode API. If not supported, these become local-only preferences. |
| 4 | What SSE event types indicate subagent start/completion? | Open | Depends on opencode event schema. Fallback: detect from message metadata (sender field). |
| 5 | Should the context status bar show the agent name or the model name? Current proposal shows model. | Resolved | Model name — the agent is implicit from the model. If the user needs agent info, they open the Context Sheet. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a conversation with messages, when viewing the chat page, then all messages occupy full width with a colored left border bar (user=ColorPrimary, agent=ColorAgentAccent) and sender label above content. *(REQ-001, REQ-002, REQ-003)*
- [ ] **[AC-002]** Given an agent message containing Markdown (headers, bold, italic, lists, code blocks, tables, links, blockquotes), when rendered, then all Markdown elements display correctly with appropriate typography and styling. *(REQ-008 through REQ-016)*
- [ ] **[AC-003]** Given the chat page is visible, when looking at the header area, then a single-row header shows session name (center) and a context status bar below shows "Project · Model · thinking: level". *(REQ-017, REQ-018, REQ-019)*
- [ ] **[AC-004]** Given the chat page, when tapping the session name in the header, then an edit modal appears pre-filled with the current name, and confirming updates the name in header and flyout. *(REQ-023, REQ-024)*
- [ ] **[AC-005]** Given the chat page, when tapping the context icon or the status bar, then the Context Sheet opens showing project, agent, model, thinking level, auto-accept, and invoke subagent options. *(REQ-025, REQ-026)*
- [ ] **[AC-006]** Given the Context Sheet is open, when tapping project/agent/model rows, then the corresponding existing picker sheet opens; when changing thinking level or auto-accept, then values are applied immediately and the status bar updates. *(REQ-026, REQ-027, REQ-028)*
- [ ] **[AC-007]** Given the input bar, when tapping the command button, then the Command Palette bottom sheet opens with a searchable list of commands from the server. *(REQ-029, REQ-030)*
- [ ] **[AC-008]** Given the Context Sheet, when tapping "Invoke Subagent", then the AgentPickerSheet opens in subagent mode and selecting an agent sends the invocation request. *(REQ-031)*
- [ ] **[AC-009]** Given a subagent is active, when viewing the message flow, then a subagent activity indicator card is visible with the subagent name and animated status; subagent messages have a distinct color bar. *(REQ-032, REQ-033)*
- [ ] **[AC-010]** Given any new component, when switching between light and dark mode, then all elements render correctly with appropriate theme colors. *(REQ-040)*
- [ ] **[AC-011]** Given any interactive element in the redesigned chat page, when measuring its touch target, then it is at least 44x44pt. *(REQ-041)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate

1. **Markdown rendering approach**: Evaluate three options:
   - **(a) Custom parser + native views**: Use Markdig (NuGet) to parse MD into an AST, then walk the tree to build MAUI `Label`, `StackLayout`, `Border`, `Grid` views. Pros: full theme integration, no WebView overhead. Cons: significant implementation effort, edge cases.
   - **(b) WebView with local HTML**: Render MD to HTML (via Markdig), display in a `WebView` with CSS matching the app theme. Pros: complete MD support. Cons: WebView performance overhead, theme sync complexity, touch event passthrough issues.
   - **(c) Third-party MAUI Markdown library**: Check if a mature library exists for .NET MAUI Markdown rendering. Evaluate quality and maintenance status.
   - **Recommendation**: Option (a) is preferred for consistency with the native UI approach. Markdig is a well-maintained .NET library.

2. **Collapsible status bar**: Investigate `CollectionView.Scrolled` event to detect scroll direction. Use `TranslationY` animation or `IsVisible` + `HeightRequest` animation for smooth collapse/expand. Consider `CommunityToolkit.Maui` behaviors if available.

3. **Bottom sheet implementation**: The project already uses `IAppPopupService` abstraction (from app-navigation-structure spec). Context Sheet and Command Palette should follow the same pattern. Verify if `UXDivers.Popups.Maui` (deferred in past spec) has been adopted, or if a custom bottom sheet implementation is needed.

4. **Subagent detection**: Investigate the SSE event stream (`SubscribeToEventsAsync`) for event types that indicate subagent lifecycle. If not available, check if `MessageWithPartsDto` contains sender/agent metadata that can distinguish primary agent from subagent messages.

### Constraints to Respect

- **AppThemeBinding pattern**: All new color tokens must be Light/Dark Color pairs. AppThemeBinding used inline only — never as standalone ResourceDictionary element. (Established in chat-ui-design-guidelines tech analysis)
- **Converter adapter pattern**: Any new converters must have pure logic in `openMob.Core` and thin MAUI wrappers. (Established in chat-ui-completion tech analysis)
- **MauiXamlInflator=SourceGen disabled**: XAML is parsed at runtime. Syntax errors only surface on device launch. (Known constraint)
- **Layer separation**: All ViewModels, services, converters, models in `openMob.Core`. Zero MAUI dependencies in Core.
- **DI pattern**: Constructor injection only. Every service behind an interface. Registration in `AddOpenMobCore()` (Core) and `MauiProgram.cs` (MAUI).

### Related Files and Modules

| File | Relevance |
|------|-----------|
| `src/openMob/Views/Pages/ChatPage.xaml` | Primary file to redesign |
| `src/openMob/Views/Controls/MessageBubbleView.xaml` | To be replaced by MessageBlockView |
| `src/openMob/Views/Controls/InputBarView.xaml` | To be modified (command button) |
| `src/openMob/Views/Controls/EmptyStateView.xaml` | Minor update |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | Major extension |
| `src/openMob.Core/Models/ChatMessage.cs` | May need new properties (sender type, subagent flag) |
| `src/openMob/Views/Popups/AgentPickerSheet.xaml` | Extend with subagent mode |
| `src/openMob/Resources/Styles/Colors.xaml` | New tokens |
| `src/openMob/Resources/Styles/Styles.xaml` | New styles |
| `src/openMob.Core/Infrastructure/Http/IOpencodeApiClient.cs` | GetCommandsAsync, GetAgentsAsync, UpdateConfigAsync |

### References to Past Decisions

- As established in **chat-ui-design-guidelines** (2026-03-14): Shell Flyout is the navigation model, CollectionView with KeepLastItemInView for messages, Grid(Auto,*,Auto) layout pattern.
- As established in **app-navigation-structure** (2026-03-15): AgentPickerSheet, ModelPickerSheet, ProjectSwitcherSheet already exist as popup sheets. INavigationService and IAppPopupService abstractions are in place.
- As established in **chat-ui-completion** (2026-03-16): InputBarView uses TextChanged event (not binding) for reliable ViewModel propagation. BubbleMaxWidth is a BindableProperty on ChatPage.
- As established in **minimalist-ui-redesign** (2026-03-16): Inter font family (4 weights), Material Symbols Outlined icons, green accent #10A37F/#1DB88E, MaterialIcons.cs centralized constants.
- As established in **opencode-api-client** (2026-03-15): Full API client with GetAgentsAsync, GetCommandsAsync, UpdateConfigAsync, SSE streaming. OpencodeResult<T> pattern for all API responses.
