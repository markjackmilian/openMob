# Chat Session Loading Indicator

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-25                   |
| Status  | In Progress                  |
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

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-31

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/chat-session-loading-indicator |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-ui, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| XAML Views | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml` |
| Code Review | om-reviewer | `src/openMob/Views/Pages/ChatPage.xaml` |

### Files to Create

_None — this is a pure XAML modification with no new files._

### Files to Modify

- `src/openMob/Views/Pages/ChatPage.xaml` — Add loading overlay (`ActivityIndicator` + label) inside the inner Grid Row 0 of the message area (outer Grid Row 2); update `EmptyStateView.IsVisible` binding to also gate on `NOT IsBusy`.

### Technical Dependencies

- `ChatViewModel.IsBusy` — `[ObservableProperty] bool _isBusy` — already implemented and correctly toggled in `LoadMessagesAsync` (set to `true` at line 616, `false` at line 728 of `ChatViewModel.cs`). **No changes to Core.**
- `ChatViewModel.IsEmpty` — `[ObservableProperty] bool _isEmpty` — already implemented. **No changes to Core.**
- `InvertedBoolConverter` — globally registered in `App.xaml` as `{StaticResource InvertedBoolConverter}`. Available in `ChatPage.xaml` without any local `ContentPage.Resources` declaration.
- Design tokens already present: `ColorPrimaryLight`, `ColorPrimaryDark`, `ColorOnBackgroundSecondaryLight`, `ColorOnBackgroundSecondaryDark`, `FontSizeBody`, `SpacingSm`.
- No new NuGet packages required.

### Structural Observations (from XAML audit)

1. **Outer Grid layout:** `RowDefinitions="Auto,Auto,*,Auto,Auto,Auto"` — Row 0=Header, Row 1=ContextStatusBar, Row 2=Message area (inner Grid), Row 3=SubagentIndicator, Row 4=Error/Permissions banners, Row 5=ConnectionFooter.
2. **Inner Grid (Row 2):** `RowDefinitions="*,Auto"` — Row 0 contains `EmptyStateView` + `CollectionView` (overlapping same cell), Row 1 contains TypingIndicator. FAB overlaps both rows via `Grid.RowSpan="2"`.
3. **Loading overlay placement:** Add a third child in the inner Grid's Row 0, overlapping `EmptyStateView` and `CollectionView`. Set `IsVisible="{Binding IsBusy}"`, `HorizontalOptions="Center"`, `VerticalOptions="Center"`.
4. **`EmptyStateView` visibility:** Currently `IsVisible="{Binding IsEmpty}"`. Must become `IsVisible="{Binding IsEmpty}"` **AND** `IsBusy = false`. Since MAUI `MultiBinding` has renderer limitations, the recommended approach is a **`DataTrigger`**: keep the base binding as `IsVisible="{Binding IsEmpty}"` and add a `DataTrigger` that sets `IsVisible=False` when `IsBusy=True`. This is pure XAML, no Core changes needed.
5. **`SuggestionChips` CollectionView:** **Does not currently exist in `ChatPage.xaml`.** The spec references it (REQ-004, AC-003) but the XAML audit confirms it is absent — the Row 4 `VerticalStackLayout` contains only the Pending Permissions banner and the Error banner. REQ-004 and AC-003 are therefore vacuously satisfied by the current state. The `SuggestionChips` property exists in `ChatViewModel` but has no corresponding XAML binding in `ChatPage`. **No action required for REQ-004.**

### Binding Strategy for `EmptyStateView`

Use a `DataTrigger` to hide `EmptyStateView` when `IsBusy = true`, layered on top of the existing `IsVisible="{Binding IsEmpty}"` binding:

```xml
<controls:EmptyStateView Grid.Row="0"
                         IsVisible="{Binding IsEmpty}"
                         Title="How can I help you?">
  <controls:EmptyStateView.Triggers>
    <DataTrigger TargetType="controls:EmptyStateView"
                 Binding="{Binding IsBusy}"
                 Value="True">
      <Setter Property="IsVisible" Value="False" />
    </DataTrigger>
  </controls:EmptyStateView.Triggers>
</controls:EmptyStateView>
```

This approach:
- Requires zero Core changes
- Uses only standard MAUI trigger mechanisms (no MultiBinding)
- Is fully compatible with compiled bindings (`x:DataType` already set on the page)

### Technical Risks

- **DataTrigger + IsVisible binding interaction:** When `IsBusy` goes back to `false`, the `DataTrigger` releases and the base `IsVisible="{Binding IsEmpty}"` binding resumes. This is standard MAUI trigger behaviour and is reliable.
- **No platform-specific concerns** — `ActivityIndicator` renders natively on both iOS and Android.
- **No secrets handling** — pure UI change.

### Execution Order

1. [Git Flow] Create branch `feature/chat-session-loading-indicator`
2. [om-mobile-ui] Implement XAML changes in `ChatPage.xaml`
3. [om-reviewer] Full review against spec
4. [Fix loop if needed] Address Critical and Major findings
5. [Git Flow] Finish branch and merge

### Definition of Done

- [x] No changes to `ChatViewModel` or any Core library code required
- [ ] [REQ-001] `ActivityIndicator` visible and spinning when `IsBusy = true`
- [ ] [REQ-002] "Loading session…" label visible below spinner when `IsBusy = true`
- [ ] [REQ-003] `EmptyStateView` hidden when `IsBusy = true`
- [ ] [REQ-004] `SuggestionChips` hidden when `IsBusy = true` *(vacuously satisfied — chips not present in current XAML)*
- [ ] [REQ-005] Overlay disappears and normal UI resumes when `IsBusy = false`
- [ ] [REQ-006] Input bar, FAB, header unaffected by overlay
- [ ] [REQ-007] Spinner colour follows `ColorPrimary` light/dark token
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
