# UI Design Guidelines — Chat-Oriented Layout & Navigation

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-14                   |
| Status  | In Progress                  |
| Version | 1.0                          |

---

## Executive Summary

Define the complete visual and interaction guidelines for openMob as a **minimalist, chat-oriented mobile app** with a hamburger-driven side menu. This spec establishes the screen structure, navigation model, component anatomy, spacing rules, and behavioral patterns that all future feature implementations must follow. It builds on the existing design system (Colors.xaml, Styles.xaml) and aligns with the Apple HIG / Fluent hybrid philosophy already adopted by the project.

---

## Scope

### In Scope
- App-level navigation structure using Shell Flyout (hamburger side menu)
- Chat page layout: header bar, message list, input bar, suggestion chips, empty state
- Message bubble anatomy and variants (sent / received)
- Input bar anatomy and interaction states
- Flyout menu structure, items, and visual treatment
- Responsive layout rules for iOS and Android
- Dark / light mode application to all new components
- Accessibility requirements for all described components
- New design tokens needed beyond the existing palette (chat-specific)

### Out of Scope
- Code-level implementation of components (separate feature specs)
- Backend / API integration
- ViewModel business logic
- Onboarding flow, settings page, user profile page
- Push notifications UI
- Voice input implementation (button placeholder only)
- Rich media messages (images, files, code blocks) — future spec

---

## Functional Requirements

> Requirements are numbered for traceability.

### Navigation

1. **[REQ-001]** The app must use a **Shell Flyout** as the primary navigation model. The flyout is triggered by a hamburger icon (three horizontal lines) positioned at the top-left of the header bar.

2. **[REQ-002]** The flyout menu must contain the following sections, in order from top to bottom:
   - **Header area**: App logo/name ("openMob") and a "New Chat" action button
   - **Session list**: Scrollable list of past conversation sessions, ordered by most recent first. Each item shows the session title (or "New conversation" if untitled) and a relative timestamp (e.g., "2m ago", "Yesterday")
   - **Footer area**: Settings icon/link, app version label

3. **[REQ-003]** The flyout must overlay the main content with a semi-transparent scrim (`ColorScrim`). It must be dismissible by tapping the scrim area or swiping left.

4. **[REQ-004]** On flyout open, the menu must slide in from the left edge with a **250ms CubicOut** easing animation. On close, it must slide out with a **200ms CubicIn** easing.

### Header Bar

5. **[REQ-005]** The chat page header bar must contain, from left to right:
   - Hamburger menu icon (tap opens flyout) — 44x44pt touch target
   - Conversation title (centered or left-aligned after icon, `FontSizeHeadline`, `Bold`, `ColorOnBackground`). If the title overflows, truncate with ellipsis
   - Right-side action area: "New Chat" icon button — 44x44pt touch target

6. **[REQ-006]** The header bar must have:
   - Background: `ColorSurface`
   - Height: 56pt (Android) / 44pt + safe area (iOS)
   - Bottom separator: 1px line using `ColorSeparator`
   - Horizontal padding: `SpacingLg` (16pt)

### Message List

7. **[REQ-007]** The message list must use a `CollectionView` with a `VerticalLinearItemsLayout`. Messages are displayed in chronological order (oldest at top, newest at bottom). The list must auto-scroll to the latest message when new messages arrive.

8. **[REQ-008]** The message area must occupy all available vertical space between the header bar and the input bar. Background: `ColorBackground`.

9. **[REQ-009]** Message list padding: `SpacingLg` (16pt) horizontal, `SpacingSm` (8pt) vertical spacing between consecutive messages.

10. **[REQ-010]** When messages from the same sender are consecutive, reduce vertical spacing to `SpacingXs` (4pt) and hide the avatar/author name on subsequent messages (grouped bubbles).

### Message Bubbles

11. **[REQ-011]** **Received messages** (from AI / other party):
    - Alignment: left-aligned
    - Background: `ColorSurface`
    - Text color: `ColorOnSurface`
    - Border radius: `RadiusLg` (16pt) on all corners, except bottom-left which uses `RadiusXs` (4pt) — "tail" effect
    - Max width: **80%** of screen width
    - Padding: `SpacingMd` (12pt) horizontal, `SpacingSm` (8pt) vertical
    - Optional avatar: `AvatarView` (Small, 28pt) to the left, aligned to bottom of bubble

12. **[REQ-012]** **Sent messages** (from user):
    - Alignment: right-aligned
    - Background: `ColorPrimary`
    - Text color: `ColorOnPrimary`
    - Border radius: `RadiusLg` (16pt) on all corners, except bottom-right which uses `RadiusXs` (4pt) — "tail" effect
    - Max width: **80%** of screen width
    - Padding: `SpacingMd` (12pt) horizontal, `SpacingSm` (8pt) vertical
    - No avatar shown

13. **[REQ-013]** Each message bubble must display a **timestamp** below the bubble text:
    - Font: `FontSizeCaption2` (11pt), `ColorOnSurfaceSecondary` (received) or `ColorOnPrimary` with 70% opacity (sent)
    - Format: "HH:mm" for today, "Yesterday HH:mm", or "MMM dd, HH:mm" for older
    - Alignment: right-aligned within the bubble

14. **[REQ-014]** Sent messages must show a **delivery status indicator** next to the timestamp:
    - Sending: animated opacity pulse on a clock icon
    - Sent: single checkmark icon
    - Delivered: double checkmark icon
    - Read: double checkmark icon in `ColorPrimary` (or accent variant)

### Input Bar

15. **[REQ-015]** The input bar must be fixed at the bottom of the screen, above the safe area. It must contain, from left to right:
    - **Attach button** (+): circular, 36pt, `ColorOnBackgroundTertiary` icon on transparent background — 44x44pt touch target
    - **Text input field**: expandable `Editor` control, single line by default, grows up to 5 lines max
    - **Send button**: circular, 36pt, `ColorPrimary` background with white arrow-up icon — 44x44pt touch target. Visible only when text input is non-empty

16. **[REQ-016]** Input bar visual specifications:
    - Background: `ColorSurface`
    - Top separator: 1px line using `ColorSeparator`
    - Padding: `SpacingSm` (8pt) vertical, `SpacingLg` (16pt) horizontal
    - Text field: `ColorSurfaceSecondary` background, `RadiusFull` (pill shape), `SpacingMd` (12pt) horizontal padding, `FontSizeBody` (17pt)
    - Placeholder text: "Message..." in `ColorOnSurfaceTertiary`

17. **[REQ-017]** The send button must transition between states:
    - **Empty input**: send button hidden (or replaced by a microphone icon placeholder, non-functional)
    - **Text present**: send button fades in with 150ms animation
    - **Sending**: send button shows a brief pulse animation, then returns to normal

### Suggestion Chips

18. **[REQ-018]** When the conversation is empty (no messages), display a set of **suggestion chips** above the input bar. These are horizontally scrollable rounded rectangles.

19. **[REQ-019]** Suggestion chip visual specifications:
    - Background: `ColorSurface`
    - Border: 1px `ColorOutline`
    - Border radius: `RadiusMd` (12pt)
    - Padding: `SpacingSm` (8pt) vertical, `SpacingLg` (16pt) horizontal
    - Text: `FontSizeCallout` (16pt), `ColorOnSurface`, bold first line (title), regular second line (subtitle) in `ColorOnSurfaceSecondary`
    - Min width: 200pt, max width: 280pt
    - Spacing between chips: `SpacingSm` (8pt)
    - Container horizontal padding: `SpacingLg` (16pt)

### Empty State

20. **[REQ-020]** When a new conversation has no messages and no suggestion chips are loading, display a centered empty state:
    - App logo or icon: 64pt, `ColorOnBackgroundTertiary` tint
    - Title: "How can I help you?" — `FontSizeTitle2` (22pt), `ColorOnBackground`, centered
    - Subtitle: optional tagline — `FontSizeSubheadline` (15pt), `ColorOnBackgroundSecondary`, centered
    - Vertical position: centered in the message area, offset slightly above true center (40% from top)

### Flyout Menu Detail

21. **[REQ-021]** Flyout menu visual specifications:
    - Width: **80%** of screen width, max 320pt
    - Background: `ColorSurface`
    - Header area height: 64pt, contains app name (`FontSizeTitle3`, `Bold`) and "New Chat" button (`ColorPrimary`)
    - Session list items: height 56pt, `FontSizeBody` for title, `FontSizeCaption1` for timestamp, `ColorOnSurfaceSecondary` for timestamp
    - Active/selected session: `ColorPrimaryContainer` background
    - Swipe-to-delete on session items: red destructive action
    - Footer: `SpacingLg` padding, `FontSizeFootnote` for version, `ColorOnSurfaceTertiary`

22. **[REQ-022]** The flyout header must include a **"New Chat" button**:
    - Style: `SecondaryButton` or icon-only (compose icon)
    - Tap action: creates a new empty conversation and closes the flyout
    - Touch target: 44x44pt minimum

### General Layout Rules

23. **[REQ-023]** All screens must respect **safe areas** on both iOS (notch, home indicator) and Android (status bar, navigation bar). Content must never render behind system UI unless explicitly designed to (e.g., scrim overlay).

24. **[REQ-024]** The keyboard must push the input bar up when active. The message list must scroll to keep the latest visible message in view when the keyboard appears.

25. **[REQ-025]** All interactive elements must meet the **44x44pt minimum touch target** (Apple HIG). Padding or hit-test areas must be expanded if the visual element is smaller.

26. **[REQ-026]** All new components and screens must support **dark and light mode** using the existing `AppThemeBinding` tokens in Colors.xaml. No hardcoded color values are permitted.

### New Design Tokens

27. **[REQ-027]** The following new design tokens must be added to the existing resource dictionaries to support chat-specific UI:

    | Token Name | Type | Light Value | Dark Value | Usage |
    |------------|------|-------------|------------|-------|
    | `ColorBubbleSent` | Color | `ColorPrimary` | `ColorPrimary` | Sent message bubble background |
    | `ColorBubbleReceived` | Color | `ColorSurface` | `ColorSurface` | Received message bubble background |
    | `ColorOnBubbleSent` | Color | `ColorOnPrimary` | `ColorOnPrimary` | Text on sent bubble |
    | `ColorOnBubbleReceived` | Color | `ColorOnSurface` | `ColorOnSurface` | Text on received bubble |
    | `ColorInputBarBackground` | Color | `ColorSurface` | `ColorSurface` | Input bar background |
    | `ColorInputFieldBackground` | Color | `ColorSurfaceSecondary` | `ColorSurfaceSecondary` | Text field background |
    | `SizeBubbleMaxWidth` | Double | 0.80 | 0.80 | Max bubble width as fraction of screen |
    | `SizeAvatarSmall` | Double | 28 | 28 | Small avatar diameter |
    | `SizeFlyoutWidth` | Double | 0.80 | 0.80 | Flyout width as fraction of screen |
    | `SizeFlyoutMaxWidth` | Double | 320 | 320 | Flyout max width in pt |

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `AppShell.xaml` | Major | Must be reconfigured from simple ShellContent to Shell Flyout with FlyoutBehavior, FlyoutContent template |
| `MainPage.xaml` | Major | Current placeholder must be replaced with the chat page layout |
| `Colors.xaml` | Minor | New chat-specific semantic tokens to be added |
| `Styles.xaml` | Minor | New explicit styles for chat components (bubble, input bar, chip) |
| `Views/Controls/` | Major | New ContentView components: MessageBubbleView, InputBarView, SuggestionChipView, EmptyStateView, SessionListItemView |
| `Views/Pages/` | Major | New pages: ChatPage, potentially FlyoutContentPage |
| `Converters/` | Minor | New converters: DateTimeToRelativeStringConverter, MessageStatusToIconConverter |

### Dependencies
- **Project scaffolding** (completed) — this spec builds on the architecture established in `specs/done/2026-03-13-project-scaffolding.md`
- **Design system** (completed) — Colors.xaml and Styles.xaml are already in place with full semantic token palette
- **om-mobile-ui agent spec** — component patterns (BindableProperty, ContentView) are already defined
- **CommunityToolkit.Maui** — already referenced, provides additional controls and behaviors
- **CommunityToolkit.Mvvm** — already referenced, provides ObservableProperty and RelayCommand for ViewModels

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the flyout support swipe-from-edge gesture to open (in addition to hamburger tap)? | Decided | Yes — MAUI Shell Flyout supports swipe-from-edge natively on both platforms. Default behavior is enabled. |
| 2 | Should message bubbles support long-press context menu (copy, delete, retry)? | Deferred | Future spec — out of scope for this guidelines implementation |
| 3 | Should the app show a typing indicator animation when AI is generating a response? | Deferred | Future spec — out of scope for this guidelines implementation |
| 4 | Should suggestion chips be hardcoded or dynamic from the API? | Decided | Hardcoded fallback for this implementation. Dynamic API integration in a future spec. ViewModel exposes `ObservableCollection<SuggestionChip>` populated with defaults. |
| 5 | Maximum number of visible suggestion chips? | Decided | 4-6 chips, horizontally scrollable via `CollectionView` with `HorizontalLinearItemsLayout` |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the app is launched, when the user taps the hamburger icon, then a flyout menu slides in from the left with a 250ms CubicOut animation and a scrim overlay appears. *(REQ-001, REQ-003, REQ-004)*
- [ ] **[AC-002]** Given the flyout is open, when the user taps the scrim or swipes left, then the flyout closes with a 200ms CubicIn animation. *(REQ-003, REQ-004)*
- [ ] **[AC-003]** Given the flyout is open, when the user views the menu, then they see a header with app name and "New Chat" button, a scrollable session list, and a footer with settings and version. *(REQ-002, REQ-021, REQ-022)*
- [ ] **[AC-004]** Given a conversation with messages, when the user views the chat page, then sent messages appear as right-aligned bubbles with `ColorPrimary` background and received messages appear as left-aligned bubbles with `ColorSurface` background. *(REQ-011, REQ-012)*
- [ ] **[AC-005]** Given a message bubble, when the user inspects it, then it displays a timestamp in the correct format and sent messages show a delivery status icon. *(REQ-013, REQ-014)*
- [ ] **[AC-006]** Given the input bar, when the text field is empty, then the send button is hidden; when text is entered, then the send button fades in within 150ms. *(REQ-015, REQ-017)*
- [ ] **[AC-007]** Given the input bar text field, when the user types multiple lines, then the field expands up to 5 lines maximum and the message list adjusts accordingly. *(REQ-015)*
- [ ] **[AC-008]** Given a new empty conversation, when the user views the chat page, then suggestion chips are displayed above the input bar in a horizontally scrollable row, and a centered empty state is shown in the message area. *(REQ-018, REQ-019, REQ-020)*
- [ ] **[AC-009]** Given any screen in the app, when the device is in dark mode, then all components render using dark theme tokens with no hardcoded colors visible. *(REQ-026)*
- [ ] **[AC-010]** Given any interactive element, when measured, then its touch target is at least 44x44pt. *(REQ-025)*
- [ ] **[AC-011]** Given the keyboard is shown, when the user is typing in the input bar, then the input bar is pushed above the keyboard and the message list scrolls to keep the latest message visible. *(REQ-024)*
- [ ] **[AC-012]** Given the chat-specific design tokens defined in REQ-027, when inspecting Colors.xaml, then all new tokens are present with correct AppThemeBinding values for light and dark modes. *(REQ-027)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key Areas to Investigate
- **Shell Flyout configuration**: MAUI Shell supports `FlyoutBehavior="Flyout"` natively. Investigate whether the built-in Shell Flyout provides enough customization (custom FlyoutContent, FlyoutHeader, FlyoutFooter) or if a custom drawer implementation is needed for the design requirements (animation timing, scrim color, width control).
- **Keyboard handling**: MAUI's `KeyboardAutoManagerScroll` behavior on iOS and Android. Verify that the input bar correctly repositions when the soft keyboard appears without manual adjustment. Test with `SoftInputMode.AdjustResize` on Android.
- **CollectionView auto-scroll**: Investigate `CollectionView.ScrollTo` for auto-scrolling to the latest message. Consider `ItemsUpdatingScrollMode="KeepLastItemInView"` property.
- **Expandable Editor in input bar**: MAUI `Editor` control auto-sizing behavior. May need `AutoSize="TextChanges"` with a max height constraint to limit to 5 lines.
- **Message grouping logic**: Consecutive messages from the same sender should be visually grouped (reduced spacing, hidden avatar). This is a ViewModel concern — the `MessageBubbleView` needs `IsFirstInGroup` / `IsLastInGroup` bindable properties.

### Suggested Implementation Approach
- **Phase 1**: Configure `AppShell.xaml` as a Flyout shell with custom `FlyoutContent` template. Create `ChatPage.xaml` as the main content page.
- **Phase 2**: Implement `MessageBubbleView` ContentView with all BindableProperties per REQ-011/012/013/014.
- **Phase 3**: Implement `InputBarView` ContentView with expandable editor and animated send button per REQ-015/016/017.
- **Phase 4**: Implement `SuggestionChipView` and `EmptyStateView` per REQ-018/019/020.
- **Phase 5**: Add new design tokens to Colors.xaml per REQ-027.

### Constraints to Respect
- All new components must live in `Views/Controls/` as `ContentView` with typed `BindableProperty` — zero ViewModel knowledge inside components.
- All testable logic (message grouping, timestamp formatting, status mapping) must reside in `openMob.Core` (converters, helpers, or ViewModel logic).
- No hardcoded colors — all values must reference semantic tokens from `Colors.xaml`.
- No deprecated controls: use `CollectionView` (not `ListView`), `VerticalStackLayout` (not `StackLayout`), `Border` (not `Frame` for new components).
- As established in the project scaffolding spec, `openMob.Core` has zero MAUI dependencies. All converters must be pure .NET implementations.

### Related Files or Modules
- `src/openMob/AppShell.xaml` — must be reconfigured for Flyout navigation
- `src/openMob/Resources/Styles/Colors.xaml` — add new chat-specific tokens
- `src/openMob/Resources/Styles/Styles.xaml` — add new explicit styles for chat components
- `src/openMob/Views/Pages/MainPage.xaml` — replace with ChatPage or repurpose
- `src/openMob/Views/Controls/` — all new ContentView components
- `src/openMob.Core/Infrastructure/Http/Dtos/MessageDto.cs` — existing DTO for message data
- `src/openMob.Core/Infrastructure/Http/Dtos/SessionDto.cs` — existing DTO for session data
- `.opencode/agents/om-mobile-ui.md` — component patterns and design philosophy reference

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-14

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/chat-ui-design-guidelines |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Converters | om-mobile-core | src/openMob.Core/Converters/, src/openMob.Core/Models/ |
| ViewModels | om-mobile-core | src/openMob.Core/ViewModels/ |
| Data / DTOs | om-mobile-core | src/openMob.Core/Infrastructure/Http/Dtos/ |
| XAML Views | om-mobile-ui | src/openMob/Views/Pages/ |
| UI Components | om-mobile-ui | src/openMob/Views/Controls/ |
| Styles / Theme | om-mobile-ui | src/openMob/Resources/Styles/ |
| Shell Navigation | om-mobile-ui | src/openMob/AppShell.xaml |
| DI Registration | om-mobile-core | src/openMob/MauiProgram.cs, src/openMob.Core/Infrastructure/DI/ |
| Unit Tests | om-tester | tests/openMob.Tests/ |
| Code Review | om-reviewer | all of the above |

### Files to Create

**In `src/openMob.Core/` (om-mobile-core):**
- `src/openMob.Core/Models/ChatMessage.cs` — Domain model wrapping MessageDto with computed properties (IsFromUser, IsFirstInGroup, IsLastInGroup, FormattedTimestamp, DeliveryStatus)
- `src/openMob.Core/Models/SuggestionChip.cs` — Simple model for suggestion chip data (Title, Subtitle)
- `src/openMob.Core/Models/MessageDeliveryStatus.cs` — Enum: Sending, Sent, Delivered, Read
- `src/openMob.Core/Converters/DateTimeToRelativeStringConverter.cs` — Pure .NET IValueConverter: DateTimeOffset → "HH:mm" / "Yesterday HH:mm" / "MMM dd, HH:mm"
- `src/openMob.Core/Converters/MessageStatusToIconConverter.cs` — Pure .NET IValueConverter: MessageDeliveryStatus → string icon glyph/resource name
- `src/openMob.Core/Converters/BoolToVisibilityConverter.cs` — Pure .NET IValueConverter: bool → bool (with inversion support via parameter)
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — Main chat page ViewModel with message collection, input text, send command, suggestion chips, empty state logic
- `src/openMob.Core/ViewModels/SessionListViewModel.cs` — Flyout session list ViewModel with session collection, selection, new chat command, delete command
- `src/openMob.Core/Services/IChatService.cs` — Interface for chat operations (placeholder, methods throw NotImplementedException for now)
- `src/openMob.Core/Services/ISessionService.cs` — Interface for session operations (placeholder)
- `src/openMob.Core/Services/ChatService.cs` — Stub implementation
- `src/openMob.Core/Services/SessionService.cs` — Stub implementation

**In `src/openMob/` (om-mobile-ui):**
- `src/openMob/Views/Pages/ChatPage.xaml` + `ChatPage.xaml.cs` — Main chat page with header, message list, input bar, empty state, suggestion chips
- `src/openMob/Views/Controls/MessageBubbleView.xaml` + `MessageBubbleView.xaml.cs` — Reusable message bubble with BindableProperties for all variants
- `src/openMob/Views/Controls/InputBarView.xaml` + `InputBarView.xaml.cs` — Input bar with expandable editor, attach button, animated send button
- `src/openMob/Views/Controls/SuggestionChipView.xaml` + `SuggestionChipView.xaml.cs` — Single suggestion chip ContentView
- `src/openMob/Views/Controls/EmptyStateView.xaml` + `EmptyStateView.xaml.cs` — Centered empty state with icon, title, subtitle
- `src/openMob/Views/Controls/SessionListItemView.xaml` + `SessionListItemView.xaml.cs` — Flyout session list item with title, timestamp, selected state

**In `tests/openMob.Tests/` (om-tester):**
- `tests/openMob.Tests/Converters/DateTimeToRelativeStringConverterTests.cs`
- `tests/openMob.Tests/Converters/MessageStatusToIconConverterTests.cs`
- `tests/openMob.Tests/Converters/BoolToVisibilityConverterTests.cs`
- `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/SessionListViewModelTests.cs`

### Files to Modify

- `src/openMob/AppShell.xaml` + `AppShell.xaml.cs` — Reconfigure from simple ShellContent to Shell Flyout with `FlyoutBehavior="Flyout"`, custom `FlyoutContent`, `FlyoutHeader`, `FlyoutFooter`, `FlyoutBackdrop` set to `ColorScrim`
- `src/openMob/Resources/Styles/Colors.xaml` — Add 10 new chat-specific design tokens per REQ-027
- `src/openMob/Resources/Styles/Styles.xaml` — Add new explicit styles: `MessageBubbleSentBorder`, `MessageBubbleReceivedBorder`, `InputBarEditor`, `SuggestionChipBorder`, `ChatHeaderBar`
- `src/openMob/MauiProgram.cs` — Register new ViewModels and Pages in DI container, register converters
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — Register IChatService, ISessionService, ChatViewModel, SessionListViewModel
- `src/openMob/Views/Pages/MainPage.xaml` — Will be replaced by ChatPage (MainPage.xaml deleted or kept as redirect)

### Technical Dependencies

- **Project scaffolding** (completed) — 3-project architecture, DI composition root, design system all in place
- **Existing DTOs** — `MessageDto` (Id, SessionId, Content, Role, CreatedAt) and `SessionDto` (Id, Title, CreatedAt) provide the data contract. No API changes needed.
- **No new NuGet packages required** — CommunityToolkit.Maui and CommunityToolkit.Mvvm already referenced
- **No EF Core migrations needed** — This feature is UI-only with stub services; no database schema changes
- **No Claude server API endpoints involved** — Services are stubbed with NotImplementedException; API integration is a future spec

### Technical Decisions

1. **Shell Flyout vs Custom Drawer**: Use MAUI's built-in Shell Flyout. Research confirms Shell supports `FlyoutContent` (fully custom content template), `FlyoutHeader`, `FlyoutFooter`, `FlyoutBackdrop` (scrim), `FlyoutWidth`, and `FlyoutBackgroundColor`. The built-in flyout handles swipe-from-edge, scrim overlay, and slide animation natively. Custom animation timing (250ms/200ms CubicOut/CubicIn per REQ-004) is NOT directly configurable on Shell Flyout — the platform controls the animation. **Decision**: Use Shell Flyout for structure and accept platform-native animation timing. Document this as a known deviation from spec. The visual result is equivalent; only the exact timing differs.

2. **CollectionView auto-scroll**: Use `ItemsUpdatingScrollMode="KeepLastItemInView"` on the message CollectionView. This is a built-in MAUI property that automatically scrolls to the bottom when new items are added to the bound collection. No manual `ScrollTo` calls needed for the basic case.

3. **Expandable Editor**: Use `Editor` with `AutoSize="TextChanges"` and a `MaximumHeightRequest` calculated as approximately 5 lines × line height (~17pt × 5 = 85pt + padding). The Editor is wrapped in a `Border` with `RadiusFull` for the pill shape.

4. **Keyboard handling**: On Android, `SoftInputMode.AdjustResize` is the default for MAUI Shell apps. On iOS, MAUI's `KeyboardAutoManagerScroll` handles repositioning. The `Grid` layout of ChatPage (header row, message area star row, input bar auto row) naturally accommodates keyboard resize.

5. **Message grouping**: Computed in `ChatViewModel` when the message collection changes. Each `ChatMessage` model gets `IsFirstInGroup` and `IsLastInGroup` boolean properties set by the ViewModel. The `MessageBubbleView` uses these to adjust spacing and avatar visibility via BindableProperties.

6. **Converters in openMob.Core**: All converters implement `IValueConverter` from `System.Globalization` (pure .NET, no MAUI dependency). They are registered in XAML as `StaticResource` in `App.xaml` or page-level resources. The converter classes live in `src/openMob.Core/Converters/` per the layer separation rule.

7. **Color tokens for bubbles**: REQ-027 specifies that `ColorBubbleSent` maps to `ColorPrimary` and `ColorBubbleReceived` maps to `ColorSurface`. Since these are semantic aliases, they must be defined as new `AppThemeBinding` entries in Colors.xaml referencing the raw palette values (not other semantic tokens, as MAUI doesn't support `DynamicResource` inside `AppThemeBinding`). The `x:Double` tokens (`SizeBubbleMaxWidth`, etc.) are theme-independent and defined once.

### Technical Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Shell Flyout animation timing not configurable | Low | Accept platform-native timing. Document deviation. Visual result is equivalent. |
| `Editor` `AutoSize` may not respect `MaximumHeightRequest` consistently across platforms | Medium | Test on both iOS and Android simulators. Fallback: wrap in a `ScrollView` with fixed max height. |
| `AppThemeBinding` cannot reference other `DynamicResource` tokens | Medium | Define bubble color tokens using raw palette values (e.g., `Blue500` not `ColorPrimary`). This duplicates the mapping but is the only reliable approach. |
| `CollectionView` `ItemsUpdatingScrollMode="KeepLastItemInView"` may have edge cases with keyboard resize | Low | Test thoroughly. Fallback: manual `ScrollTo` in ViewModel via `IDispatcher`. |
| Flyout width (`FlyoutWidth`) is an absolute value, not a percentage | Medium | Calculate width programmatically in `AppShell.xaml.cs` using `DeviceDisplay.MainDisplayInfo` and set `FlyoutWidth` in code-behind. Cap at 320pt per REQ-021. |

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/chat-ui-design-guidelines` from `develop`
2. **[om-mobile-core]** Create models (ChatMessage, SuggestionChip, MessageDeliveryStatus), converters (DateTimeToRelativeString, MessageStatusToIcon, BoolToVisibility), service interfaces (IChatService, ISessionService) with stub implementations, ViewModels (ChatViewModel, SessionListViewModel), and DI registration
3. ⟳ **[om-mobile-ui]** Once om-mobile-core defines the ViewModel binding surface: implement all XAML — AppShell flyout configuration, ChatPage, MessageBubbleView, InputBarView, SuggestionChipView, EmptyStateView, SessionListItemView, new design tokens in Colors.xaml, new styles in Styles.xaml
4. ⟳ **[om-tester]** Once om-mobile-core completes: write unit tests for all converters and ViewModels
5. **[om-reviewer]** Full review against spec — all agents must complete before review starts
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-027]` requirements implemented
- [ ] All `[AC-001]` through `[AC-012]` acceptance criteria satisfied
- [ ] Unit tests written for all new Converters and ViewModels
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] `dotnet build openMob.sln` — zero errors, zero warnings
- [ ] `dotnet test tests/openMob.Tests/openMob.Tests.csproj` — all tests pass
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
- [ ] Knowledge base indexed
