# Chat Session Loading Indicator

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-25                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

When the user selects a session from the flyout drawer, the chat page currently shows no visual feedback while messages are being fetched from the remote API. The fetch can be slow on large sessions or weak connections, leaving the user staring at the empty-state view ("How can I help you?") with no indication that a load is in progress. This feature adds a centred `ActivityIndicator` spinner with a *"Loading session…"* label to `ChatPage`, bound to the existing `IsBusy` property on `ChatViewModel`.

---

## Scope

### In Scope
- Add a loading overlay (spinner + label) to `ChatPage.xaml`, visible when `ChatViewModel.IsBusy = true`.
- Hide `EmptyStateView` while `IsBusy = true` to avoid the misleading "How can I help you?" state during load.
- Hide the `SuggestionChips` `CollectionView` (Row 5) while `IsBusy = true`, since chips are only meaningful on a genuinely empty session.
- No changes to `ChatViewModel` or any Core library code — `IsBusy` is already correctly toggled.

### Out of Scope
- Disabling the input bar, FAB, or "New Chat" button during loading.
- Skeleton / placeholder message bubbles.
- Caching message history locally (SQLite) to reduce load time.
- Changes to the navigation flow or `SetSession` logic.
- Any changes to `FlyoutViewModel` or `FlyoutContentView`.

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** While `ChatViewModel.IsBusy = true`, `ChatPage` MUST display a centred `ActivityIndicator` (spinning, `IsRunning = true`) in the message content area (Row 3).
2. **[REQ-002]** Directly below the spinner, a `Label` with the static text *"Loading session…"* MUST be visible whenever `IsBusy = true`.
3. **[REQ-003]** While `IsBusy = true`, the `EmptyStateView` MUST be hidden, regardless of the value of `IsEmpty`.
4. **[REQ-004]** While `IsBusy = true`, the `SuggestionChips` `CollectionView` in Row 5 MUST be hidden.
5. **[REQ-005]** When `IsBusy` returns to `false`, the loading overlay MUST disappear and the normal chat UI MUST resume: the `CollectionView` of messages is shown if `IsEmpty = false`, or `EmptyStateView` + chips are shown if `IsEmpty = true`.
6. **[REQ-006]** The loading overlay MUST NOT interfere with the input bar (Row 5 bottom area), the FAB, the header (Row 0), the context status bar (Row 1), or the status banner (Row 2).
7. **[REQ-007]** The spinner colour MUST follow the app's primary colour token (`ColorPrimary` light/dark) to remain consistent with the design system.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob/Views/Pages/ChatPage.xaml` | Modified | Add loading overlay; update `IsVisible` bindings on `EmptyStateView` and `SuggestionChips` |
| `src/openMob.Core/ViewModels/ChatViewModel.cs` | None | `IsBusy` already exists and is correctly toggled in `LoadMessagesAsync` |
| `src/openMob/Views/Pages/ChatPage.xaml.cs` | None | No code-behind changes required |

### Dependencies
- `ChatViewModel.IsBusy` (`[ObservableProperty] bool _isBusy`) — already implemented; no changes needed.
- MAUI `ActivityIndicator` control — standard MAUI control, no new NuGet packages required.
- Design tokens: `ColorPrimaryLight` / `ColorPrimaryDark`, `ColorOnBackgroundSecondaryLight` / `Dark`, `FontSizeBody`, `SpacingSm` — all already defined in the app's `ResourceDictionary`.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the input bar be disabled during loading? | Resolved | No — input bar remains fully interactive during load. |
| 2 | Should a label accompany the spinner? | Resolved | Yes — static text *"Loading session…"* displayed below the spinner. |
| 3 | Spinner style: classic `ActivityIndicator` or custom skeleton? | Resolved | Classic `ActivityIndicator` spinner. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the user taps a session in the flyout, when the chat page receives the `sessionId` and `LoadMessagesAsync` begins, then a spinning `ActivityIndicator` and the label *"Loading session…"* are visible at the centre of the message area. *(REQ-001, REQ-002)*
- [ ] **[AC-002]** Given `IsBusy = true`, when the `EmptyStateView` would normally be visible (because `Messages` is empty), then it is hidden. *(REQ-003)*
- [ ] **[AC-003]** Given `IsBusy = true`, when the `SuggestionChips` collection would normally be visible, then it is hidden. *(REQ-004)*
- [ ] **[AC-004]** Given `LoadMessagesAsync` completes successfully, when `IsBusy` becomes `false` and messages exist, then the spinner disappears and the `CollectionView` of messages is visible. *(REQ-005)*
- [ ] **[AC-005]** Given `LoadMessagesAsync` completes (success or error), when `IsBusy` becomes `false` and `Messages` is still empty, then the spinner disappears and `EmptyStateView` + chips are visible. *(REQ-005)*
- [ ] **[AC-006]** Given the spinner is visible, when the user taps the input bar or the FAB, then those controls respond normally. *(REQ-006)*
- [ ] **[AC-007]** Given the app is in dark mode, when the spinner is visible, then its colour matches `ColorPrimaryDark`. *(REQ-007)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Only file to modify:** `src/openMob/Views/Pages/ChatPage.xaml`. No C# changes are expected.
- **Binding target:** `ChatViewModel.IsBusy` (already an `[ObservableProperty]`). Bind `ActivityIndicator.IsRunning` and the overlay's `IsVisible` directly to `IsBusy`.
- **`EmptyStateView` visibility fix:** The current binding is `IsVisible="{Binding IsEmpty}"`. It must become a multi-condition: `IsEmpty AND NOT IsBusy`. Use a `MultiBinding` with a `BooleanAndConverter`, or introduce a computed property `IsEmptyAndNotBusy` in `ChatViewModel`. The XAML-only approach (MultiBinding) is preferred to avoid touching Core.
- **`SuggestionChips` visibility fix:** Same pattern — currently `IsVisible="{Binding IsEmpty}"`, must also gate on `NOT IsBusy`.
- **Loading overlay placement:** Insert a new `VerticalStackLayout` (or `Grid`) inside Row 3 of the outer `Grid`, overlapping the same cell as `EmptyStateView` and `CollectionView`. Set `IsVisible="{Binding IsBusy}"`, `HorizontalOptions="Center"`, `VerticalOptions="Center"`, `ZIndex` above the other Row 3 children if needed.
- **`ActivityIndicator` colour:** Use `Color="{AppThemeBinding Light={StaticResource ColorPrimaryLight}, Dark={StaticResource ColorPrimaryDark}}"`.
- **Label style:** Use `FontFamily="InterRegular"`, `FontSize="{StaticResource FontSizeBody}"`, `TextColor` bound to `ColorOnBackgroundSecondaryLight/Dark`, `HorizontalTextAlignment="Center"`.
- **No converter needed for `IsBusy` → spinner visibility** — bind directly. An `InvertedBoolConverter` (already present in the page) may be needed for hiding `EmptyStateView` when `IsBusy = true`.
- **Existing `InvertedBoolConverter`:** Already referenced in `ChatPage.xaml` (used for `CollectionView` visibility). Can be reused.
- **MAUI `MultiBinding` caveat:** MAUI's `MultiBinding` support is limited on some renderers. If `MultiBinding` proves unreliable, the fallback is to add a computed `bool IsEmptyAndIdle => IsEmpty && !IsBusy` property to `ChatViewModel` with `[NotifyPropertyChangedFor]` on both `IsEmpty` and `IsBusy`.
- **No new tests required** for this change — it is a pure XAML UI binding with no new business logic. Existing `ChatViewModel` tests remain unaffected.
