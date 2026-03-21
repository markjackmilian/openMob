# UI Overhaul — Design System Uniformity, Tabler Icons & Accent Consistency

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | In Progress                  |
| Version | 1.1                          |

---

## Executive Summary

The openMob app currently suffers from visual inconsistency across its pages and controls: padding, margins, header styles, icon usage, and accent colour application vary from screen to screen. This spec defines a full UI overhaul focused exclusively on visual uniformity and modernity. It replaces the existing icon font with **Tabler Icons** (MIT, stroke-based, 6 000+ icons), enforces the existing design token system across every XAML file, and applies the green accent colour (`#10A37F` / `#1DB88E`) consistently from the splash screen through all interactive elements. The overhaul explicitly targets both **iOS and Android**, respecting each platform's navigation conventions — most critically, iOS has no hardware or system back button, so every page that can be navigated away from must provide an explicit in-UI back affordance. No navigation structure, ViewModel, service, or business logic is modified.

---

## Scope

### In Scope
- Replace `MaterialSymbols-Outlined.ttf` with **Tabler Icons webfont** (`TablerIcons.ttf`); register in `openMob.csproj` and `MauiProgram.cs`
- Create `src/openMob/Helpers/IconKeys.cs` — a centralised static class with `string` constants for every Tabler glyph used in the app
- Add an explicit `Label` style `IconLabel` in `Styles.xaml` with `FontFamily="TablerIcons"`, default size 24pt, inheriting accent or on-background colour
- Uniform page padding: all content pages use `SpacingLg` (16pt) as horizontal page padding; section separators use `SpacingXl` (24pt)
- Uniform page headers: every page uses the same header pattern — `Shell.TitleView` or `NavigationPage.TitleView` with `Title2Label` style (InterBold 22pt); no page deviates from this
- Uniform accent colour application: `ColorPrimaryLight/Dark` applied consistently on all interactive elements (buttons, switches, sliders, checkboxes, tab bar selected state, link labels, activity indicators, input focus rings) across all pages and controls
- Splash screen redesign: `splash.svg` redrawn with `ColorPrimaryLight` (`#10A37F`) as full-bleed background and the app logo/wordmark in white — consistent with the app accent from the first frame
- Update all XAML files (Pages, Controls, Popups) to replace any hardcoded icon references or `MaterialSymbols` glyph strings with Tabler glyph constants from `IconKeys`
- Update `AppShell.xaml` tab bar / flyout icons to use Tabler glyphs
- All spacing, radius, and colour values in every XAML file must reference existing tokens from `Styles.xaml` and `Colors.xaml`; no hardcoded values permitted
- **iOS back navigation**: every page that is pushed onto the navigation stack must expose an explicit back button in its `Shell.TitleView` (e.g. a Tabler `arrow-left` icon button on the leading side of the header) — iOS has no hardware back button and MAUI Shell's default back button rendering is inconsistent across themes
- **Android back navigation**: the Android system back button / gesture remains the primary back affordance; the in-header back button is still present for visual consistency but must not duplicate or conflict with system navigation

### Out of Scope
- Navigation structure changes (Shell routes, page hierarchy, flyout/tab layout)
- ViewModel, service, repository, or EF Core changes
- New UI components not already present in the codebase
- Typography font change (Inter family remains)
- New animations or page transitions
- New unit tests (no business logic changes)
- Accessibility audit beyond what token-based colour contrast already provides
- Platform-specific (iOS / Android) native styling beyond MAUI Shell properties and the back-button affordance described above

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** `TablerIcons.ttf` (Tabler Icons webfont, MIT licence) is added to `src/openMob/Resources/Fonts/` and registered as alias `TablerIcons` in both `openMob.csproj` (`<MauiFont>`) and `MauiProgram.cs` (`builder.ConfigureFonts`). `MaterialSymbols-Outlined.ttf` is removed from the project.

2. **[REQ-002]** A static class `IconKeys` is created at `src/openMob/Helpers/IconKeys.cs`. It contains one `public const string` per Tabler glyph used anywhere in the app. The constant name is PascalCase and descriptive (e.g. `IconKeys.Send`, `IconKeys.Settings`, `IconKeys.ChevronRight`). No XAML file may contain a raw Unicode glyph string for an icon — all references go through `IconKeys`.

3. **[REQ-003]** `Styles.xaml` gains an explicit style `x:Key="IconLabel"` targeting `Label` with `FontFamily="TablerIcons"`, `FontSize="{StaticResource FontSizeHeadline}"` (17pt default), `LineBreakMode="NoWrap"`. A second variant `x:Key="IconLabelLg"` uses `FontSizeTitle3` (20pt) for larger touch-target icons. Both inherit text colour from the implicit `Label` style.

4. **[REQ-004]** Every content page (`ChatPage`, `MainPage`, `OnboardingPage`, `ProjectDetailPage`, `ProjectsPage`, `ServerDetailPage`, `ServerManagementPage`, `SettingsPage`, `SplashPage`) applies uniform horizontal padding of `SpacingLg` (16pt) to its root content container. Vertical spacing between logical sections uses `SpacingXl` (24pt). No page uses hardcoded numeric padding or margin values.

5. **[REQ-005]** Every page that displays a navigation title uses the same header pattern: a `Shell.TitleView` (or equivalent) containing a single `Label` with `Style="{StaticResource Title2Label}"` (InterBold, 22pt). Pages that currently use a different font weight, size, or family for their title are corrected to match this standard.

6. **[REQ-006]** The accent colour `ColorPrimaryLight` / `ColorPrimaryDark` is applied via `AppThemeBinding` to every interactive element across all pages and controls:
   - `Button` (primary fill) — already in implicit style, verify no page overrides it locally with a different colour
   - `Switch.OnColor`
   - `Slider` minimum track and thumb
   - `CheckBox.Color`
   - `ActivityIndicator.Color`
   - Tab bar selected icon and label (`Shell.TabBarForegroundColor`, `Shell.TabBarTitleColor`)
   - `LinkLabel` text colour
   - Any tappable row highlight or selected-state indicator in `CollectionView` / `ListView` items
   Any element that currently uses a hardcoded colour or a non-primary accent token for interactive state is corrected.

7. **[REQ-007]** The splash screen is redesigned:
   - `splash.svg` is redrawn with a solid background fill of `#10A37F` (the light-mode primary accent)
   - The app logo or wordmark is rendered in white (`#FFFFFF`) centred on the canvas
   - The MAUI splash configuration in `openMob.csproj` (`<MauiSplashScreen>`) sets `TintColor` to `#10A37F` if using a single-colour SVG approach, or references the redrawn SVG directly
   - The result is a full-bleed green splash consistent with the app's accent identity

8. **[REQ-008]** All icon usages in `Views/Controls/*.xaml`, `Views/Pages/*.xaml`, `Views/Popups/*.xaml`, and `AppShell.xaml` are updated to use Tabler glyph constants via `{x:Static helpers:IconKeys.XxxName}` bindings. The `xmlns:helpers` namespace alias pointing to `openMob.Helpers` is declared at the top of each XAML file that uses icons.

9. **[REQ-009]** All spacing, border radius, and colour values in every modified XAML file reference tokens from `Styles.xaml` (`SpacingXxs`…`SpacingXxxl`, `RadiusXs`…`RadiusFull`) and `Colors.xaml` (semantic tokens only, not raw palette entries). Inline hardcoded values (e.g. `Padding="16"`, `CornerRadius="8"`, `TextColor="#10A37F"`) are replaced with `{StaticResource ...}` references.

10. **[REQ-010]** No ViewModel, service, repository, EF Core entity, migration, or test file is modified. The 884-test suite must pass without changes after the UI overhaul.

11. **[REQ-011]** Every page that is pushed onto the navigation stack (i.e. is not a root Shell tab) exposes an explicit **back button** in the leading position of its `Shell.TitleView`. The back button uses the Tabler `arrow-left` glyph (`IconKeys.ArrowLeft`), styled with `IconLabel`, and invokes `Shell.Current.GoToAsync("..")` or the existing `INavigationService.PopAsync()`. This requirement applies equally to iOS (no hardware back button) and Android (provides visual consistency alongside the system gesture).

12. **[REQ-012]** Modal pages and bottom sheets (Popups) that can be dismissed expose an explicit **close button** in the trailing position of their header, using the Tabler `x` glyph (`IconKeys.X`). This ensures iOS users can always dismiss a modal without a hardware button.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob/Resources/Fonts/` | `MaterialSymbols-Outlined.ttf` removed; `TablerIcons.ttf` added | Font file swap |
| `src/openMob/openMob.csproj` | `<MauiFont>` entry updated | Font registration |
| `src/openMob/MauiProgram.cs` | `ConfigureFonts` updated | Font alias registration |
| `src/openMob/Helpers/IconKeys.cs` | New file | Centralised glyph constants |
| `src/openMob/Resources/Styles/Styles.xaml` | `IconLabel` and `IconLabelLg` styles added | Icon label styles |
| `src/openMob/Resources/Splash/splash.svg` | Redrawn with accent background + white logo | Splash redesign |
| `src/openMob/AppShell.xaml` | Tab bar / flyout icons updated to Tabler | Icon swap |
| `src/openMob/Views/Pages/*.xaml` (8 files) | Padding, header style, icon, accent uniformity | All pages |
| `src/openMob/Views/Controls/*.xaml` (15 files) | Padding, icon, accent uniformity | All controls |
| `src/openMob/Views/Popups/*.xaml` (6 files) | Padding, icon, accent uniformity, close button | All sheets |
| Back button affordance | Added to all non-root pages via `Shell.TitleView` | iOS critical; Android consistency |

### Dependencies
- **Tabler Icons webfont**: download `tabler-icons.ttf` from the official Tabler Icons release (MIT licence). Latest stable release available at `https://github.com/tabler/tabler-icons/releases`. The webfont variant (`.ttf`) is included in each release under `packages/icons-webfont/fonts/`.
- **Existing token system**: `Colors.xaml` and `Styles.xaml` are already well-structured with semantic tokens. This spec does not require new tokens — only consistent application of existing ones.
- **`AppThemeBinding`**: all colour references must use `AppThemeBinding` with `Light=` and `Dark=` variants. No single-value colour references for themed elements.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Which specific Tabler Icons glyph to use for each existing icon in the app? | Resolved | Mapped from existing `MaterialIcons.cs` (36 constants). See Technical Analysis for full mapping. |
| 2 | Should `SplashPage.xaml` (the in-app animated splash) also be updated to match the new splash background colour? | Resolved | Yes — `SplashPage.xaml` background should use `{AppThemeBinding Light={StaticResource ColorPrimaryLight}, Dark={StaticResource ColorPrimaryDark}}` |
| 3 | Are there any pages that use `Shell.NavBarIsVisible="False"` or custom navigation chrome that would require special header treatment? | Resolved | Yes — `SplashPage` and `OnboardingPage` have `Shell.NavBarIsVisible="False"`. These are root pages and do not need back buttons. |
| 4 | Does the Tabler Icons webfont TTF file cover all required glyphs (send, settings, chevron, hamburger, plus, trash, check, etc.)? | Resolved | Yes — Tabler Icons 3.x includes 6 000+ icons covering all standard mobile UI needs |
| 5 | Which pages are "root" (no back button needed) vs "pushed" (back button required)? | Resolved | Root: SplashPage, OnboardingPage, ChatPage (Shell tab root). Pushed: ProjectsPage, ProjectDetailPage, SettingsPage, ServerManagementPage, ServerDetailPage. |
| 6 | Do any existing Popups already have a close button? If so, does it use a consistent style? | Resolved | To be standardised — all 6 popups will get a consistent close button using `IconKeys.X`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given the project is built (`dotnet build openMob.sln`), when the build completes, then it exits with code 0 and zero warnings. *(REQ-001, REQ-002)*
- [ ] **[AC-002]** Given the app runs on iOS or Android, when any screen containing icons is displayed, then every icon renders as the correct Tabler glyph — no empty squares, no missing glyphs, no fallback characters. *(REQ-001, REQ-002, REQ-003, REQ-008)*
- [ ] **[AC-003]** Given any content page is displayed, when the page layout is inspected, then the horizontal content padding is uniformly 16pt (`SpacingLg`) and no hardcoded numeric padding values exist in the XAML. *(REQ-004, REQ-009)*
- [ ] **[AC-004]** Given any page with a navigation title is displayed, when the header is inspected, then the title uses `Title2Label` style (InterBold, 22pt) consistently across all pages. *(REQ-005)*
- [ ] **[AC-005]** Given any interactive element (button, switch, slider, checkbox, tab bar item) is displayed, when the element is in its active/selected state, then it uses the primary accent colour (`#10A37F` light / `#1DB88E` dark) with no hardcoded colour overrides. *(REQ-006)*
- [ ] **[AC-006]** Given the app is launched on a device, when the splash screen appears, then it shows a full-bleed green background (`#10A37F`) with the app logo in white. *(REQ-007)*
- [ ] **[AC-007]** Given the test suite is run (`dotnet test`), when all tests complete, then all 884 existing tests pass with zero failures. *(REQ-010)*
- [ ] **[AC-008]** Given any XAML file in the project is inspected, when searching for raw Unicode glyph strings (e.g. `&#xe3c9;`) or hardcoded colour hex values on interactive elements, then none are found — all references use `IconKeys` constants or `StaticResource` tokens. *(REQ-002, REQ-009)*
- [ ] **[AC-009]** Given the app runs on an **iOS** device or simulator, when the user navigates to any non-root page, then a visible back button (Tabler `arrow-left`) is present in the top-left of the header and tapping it navigates back correctly. *(REQ-011)*
- [ ] **[AC-010]** Given the app runs on an **iOS** device or simulator, when any modal or bottom sheet is open, then a visible close button (Tabler `x`) is present in the top-right of the sheet header and tapping it dismisses the sheet. *(REQ-012)*
- [ ] **[AC-011]** Given the app runs on an **Android** device or emulator, when the user navigates to any non-root page, then both the in-header back button and the system back gesture/button work correctly without conflict. *(REQ-011)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

(See original spec for full notes — preserved in Technical Analysis below.)

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/ui-overhaul-design-system |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-ui, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Fonts / Assets | om-mobile-ui | src/openMob/Resources/Fonts/, src/openMob/Resources/Splash/ |
| Project Config | om-mobile-ui | src/openMob/openMob.csproj, src/openMob/MauiProgram.cs |
| Icon Constants | om-mobile-ui | src/openMob/Helpers/IconKeys.cs (replaces MaterialIcons.cs) |
| Styles / Theme | om-mobile-ui | src/openMob/Resources/Styles/Styles.xaml |
| XAML Views | om-mobile-ui | src/openMob/Views/Pages/*.xaml, src/openMob/Views/Controls/*.xaml, src/openMob/Views/Popups/*.xaml |
| Shell | om-mobile-ui | src/openMob/AppShell.xaml |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob/Resources/Fonts/TablerIcons.ttf` — Tabler Icons webfont (downloaded from GitHub releases)
- `src/openMob/Helpers/IconKeys.cs` — centralised static class with Tabler glyph constants (replaces `MaterialIcons.cs`)

### Files to Modify

- `src/openMob/openMob.csproj` — replace `MaterialSymbols-Outlined.ttf` MauiFont entry with `TablerIcons.ttf`, update splash screen config
- `src/openMob/MauiProgram.cs` — replace `MaterialSymbols` font registration with `TablerIcons`
- `src/openMob/Helpers/MaterialIcons.cs` — DELETE (replaced by `IconKeys.cs`)
- `src/openMob/Resources/Styles/Styles.xaml` — add `IconLabel` and `IconLabelLg` explicit styles
- `src/openMob/Resources/Splash/splash.svg` — redraw with `#10A37F` background and white logo
- `src/openMob/AppShell.xaml` — update all icon references to Tabler glyphs
- `src/openMob/Views/Pages/ChatPage.xaml` — padding, icons, accent uniformity
- `src/openMob/Views/Pages/OnboardingPage.xaml` — padding, icons
- `src/openMob/Views/Pages/ProjectDetailPage.xaml` — padding, icons, back button
- `src/openMob/Views/Pages/ProjectsPage.xaml` — padding, icons, back button
- `src/openMob/Views/Pages/ServerDetailPage.xaml` — padding, icons, back button
- `src/openMob/Views/Pages/ServerManagementPage.xaml` — padding, icons, back button
- `src/openMob/Views/Pages/SettingsPage.xaml` — padding, icons, back button
- `src/openMob/Views/Pages/SplashPage.xaml` — background colour update
- `src/openMob/Views/Controls/*.xaml` (all 17 controls) — icon migration, padding tokens
- `src/openMob/Views/Popups/*.xaml` (all 6 popups) — icon migration, close button, padding tokens

### Files to Delete

- `src/openMob/Resources/Fonts/MaterialSymbols-Outlined.ttf` — replaced by TablerIcons.ttf
- `src/openMob/Helpers/MaterialIcons.cs` — replaced by IconKeys.cs

### Technical Dependencies

- **Tabler Icons TTF**: Must be downloaded from `https://github.com/tabler/tabler-icons/releases` before implementation starts
- **Existing design tokens**: `Colors.xaml` and `Styles.xaml` already define all needed tokens — no new tokens required
- No new NuGet packages required
- No ViewModel, service, or business logic changes
- No unit test changes (REQ-010)

### Technical Risks

- **Icon glyph mapping accuracy**: Each MaterialSymbols glyph must be mapped to the correct Tabler equivalent. Wrong codepoints will show empty squares or wrong icons.
- **Font registration**: The `FontFamily` alias in MAUI must match exactly between `MauiProgram.cs` registration and XAML usage. A mismatch causes silent fallback to system font.
- **Splash SVG format**: MAUI's splash screen processing has specific SVG requirements. The redrawn SVG must be simple (no complex gradients or filters).
- **Back button on Shell pages**: `Shell.TitleView` replaces the entire title bar. Must ensure the back button + title layout works correctly on both iOS and Android without conflicting with Shell's built-in back button.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Create branch `feature/ui-overhaul-design-system`
2. [om-mobile-ui] Download and add `TablerIcons.ttf`, create `IconKeys.cs`, register font
3. [om-mobile-ui] Add `IconLabel`/`IconLabelLg` styles to `Styles.xaml`
4. [om-mobile-ui] Update `AppShell.xaml` — validates font is working
5. [om-mobile-ui] Update all `Views/Controls/*.xaml` — shared controls affect all pages
6. [om-mobile-ui] Update all `Views/Pages/*.xaml` — padding, header, icons, accent, back buttons
7. [om-mobile-ui] Update all `Views/Popups/*.xaml` — close buttons, icons
8. [om-mobile-ui] Redesign `splash.svg` and update `SplashPage.xaml`
9. [om-mobile-ui] Remove `MaterialSymbols-Outlined.ttf` and `MaterialIcons.cs`, clean up registrations
10. [om-reviewer] Full review against spec
11. [Fix loop if needed] Address Critical and Major findings
12. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-012]` requirements implemented
- [ ] All `[AC-001]` through `[AC-011]` acceptance criteria satisfied
- [ ] No unit tests modified; all 884 tests pass unchanged
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
