# UI Overhaul — Design System Uniformity, Tabler Icons & Accent Consistency

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | Draft                        |
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
| 1 | Which specific Tabler Icons glyph to use for each existing icon in the app? | Open | To be resolved by `om-mobile-ui` during Technical Analysis by auditing each XAML file and mapping current icons to the closest Tabler equivalent |
| 2 | Should `SplashPage.xaml` (the in-app animated splash) also be updated to match the new splash background colour? | Open | Likely yes — `om-mobile-ui` to verify whether `SplashPage.xaml` exists as a separate animated screen and align its background with `ColorPrimaryLight` |
| 3 | Are there any pages that use `Shell.NavBarIsVisible="False"` or custom navigation chrome that would require special header treatment? | Open | `om-mobile-ui` to audit during Technical Analysis |
| 4 | Does the Tabler Icons webfont TTF file cover all required glyphs (send, settings, chevron, hamburger, plus, trash, check, etc.)? | Resolved | Yes — Tabler Icons 3.x includes 6 000+ icons covering all standard mobile UI needs |
| 5 | Which pages are "root" (no back button needed) vs "pushed" (back button required)? | Open | `om-mobile-ui` to audit `AppShell.xaml` routes during Technical Analysis. Root pages are Shell tab roots; all others are pushed and require a back button. |
| 6 | Do any existing Popups already have a close button? If so, does it use a consistent style? | Open | `om-mobile-ui` to audit all 6 Popup XAML files and standardise the close button pattern. |

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

### Key areas to investigate

1. **Tabler Icons TTF acquisition**: Download `tabler-icons.ttf` from the latest Tabler Icons release (`https://github.com/tabler/tabler-icons/releases`). The file is in `packages/icons-webfont/fonts/tabler-icons.ttf`. Verify the font alias name used in MAUI `ConfigureFonts` matches the `FontFamily` string used in XAML (`"TablerIcons"`).

2. **Glyph mapping audit**: Open each XAML file in `Views/Pages/`, `Views/Controls/`, `Views/Popups/`, and `AppShell.xaml`. For every icon currently rendered (whether via `MaterialSymbols`, a Unicode literal, or an image), identify the semantic intent and map it to the closest Tabler Icons glyph. Build the `IconKeys.cs` constants file from this audit. Reference: `https://tabler.io/icons` for browsing available icons and their Unicode codepoints.

3. **Tabler webfont codepoints**: Tabler Icons webfont uses Unicode private-use area codepoints (e.g. `\uea01`). The codepoint list is available in `packages/icons-webfont/tabler-icons.css` (each `.ti-xxx::before { content: "\eaXX"; }` entry). Extract the relevant codepoints for `IconKeys.cs`.

4. **Splash screen**: `src/openMob/Resources/Splash/splash.svg` is the source file. Redraw it with a `#10A37F` background rectangle filling the entire canvas and the existing logo/wordmark in `#FFFFFF`. In `openMob.csproj`, the `<MauiSplashScreen>` element should reference this SVG. If a `TintColor` attribute is used, set it to `#10A37F`. Also check `SplashPage.xaml` (in-app animated splash) — its `BackgroundColor` should be set to `{AppThemeBinding Light={StaticResource ColorPrimaryLight}, Dark={StaticResource ColorPrimaryDark}}`.

5. **Padding audit**: For each page, check the root `ContentPage` or its first child container for `Padding` values. The standard is `Padding="{StaticResource SpacingLg}"` (16pt all sides) or `Padding="{StaticResource SpacingLg},0"` for horizontal-only. Sheets (Popups) may use `SpacingXl` (24pt) for a more spacious feel.

6. **Header pattern audit**: Check each page for how the navigation title is set. Some may use `Title="..."` on `ContentPage` (rendered by Shell), others may use a custom `Shell.TitleView`. Standardise to `Shell.TitleView` with a `Label Style="{StaticResource Title2Label}"` for pages that need a custom header, or ensure `ContentPage.Title` is set and Shell renders it with the correct implicit style.

7. **Accent colour audit**: Search all XAML files for `TextColor`, `BackgroundColor`, `BorderColor`, `Stroke`, `Color` attributes. Any that reference raw hex values or non-primary tokens on interactive elements must be corrected to use `{AppThemeBinding Light={StaticResource ColorPrimaryLight}, Dark={StaticResource ColorPrimaryDark}}`.

8. **`MaterialSymbols` cleanup**: After replacing all usages, remove `MaterialSymbols-Outlined.ttf` from `Resources/Fonts/`, remove its `<MauiFont>` entry from `openMob.csproj`, and remove its `ConfigureFonts` registration from `MauiProgram.cs`. Search the entire solution for any remaining string `"MaterialSymbols"` to ensure no orphaned references.

9. **iOS back button — platform audit**: Audit `AppShell.xaml` to identify which pages are Shell tab roots (no back button needed) and which are pushed pages (back button required). For each pushed page, add a leading `ImageButton` or `Label` in `Shell.TitleView` using `IconKeys.ArrowLeft` that calls `INavigationService.PopAsync()`. Use `Shell.BackButtonBehavior` if appropriate, but prefer an explicit XAML button for full visual control. Verify on iOS simulator that the MAUI default back button is suppressed (`Shell.BackButtonBehavior IsVisible="False"` or equivalent) so only the custom button is shown.

10. **Android back button — no conflict**: On Android, the system back gesture must still work. Do not intercept or override `OnBackPressed` / `BackButtonPressed`. The custom in-header back button is additive, not a replacement. Test on Android emulator that both the header button and the system gesture navigate back correctly.

11. **Modal close button**: For each Popup in `Views/Popups/`, add a trailing `Label` (or `ImageButton`) in the sheet header using `IconKeys.X` styled with `IconLabel`. The close action calls the existing dismiss/pop mechanism already used in each sheet. Verify on iOS that the sheet can be dismissed without any swipe gesture (some sheets may have `IsSwipeEnabled="False"`).

### Suggested implementation approach

Execute in this order to minimise regressions:
1. Add `TablerIcons.ttf` and register it — verify font loads with a test `Label` on a scratch page
2. Create `IconKeys.cs` with all constants (can be done in parallel with step 1)
3. Add `IconLabel` / `IconLabelLg` styles to `Styles.xaml`
4. Update `AppShell.xaml` (tab bar icons) — high visibility, validates font is working
5. Update `Views/Controls/` — shared controls affect all pages
6. Update `Views/Pages/` one page at a time — padding, header, icons, accent
7. Update `Views/Popups/` — sheets
8. Redesign `splash.svg` and update `SplashPage.xaml`
9. Remove `MaterialSymbols-Outlined.ttf` and clean up registrations
10. Full build + test run

### Constraints to respect
- All colour references on themed elements must use `AppThemeBinding` — never a single static colour for light/dark-aware elements
- All spacing/radius values must use `StaticResource` tokens — never hardcoded numbers
- All icon glyph strings must go through `IconKeys` constants — never inline Unicode in XAML
- No ViewModel, service, or test file may be modified
- `ConfigureAwait(false)` must NOT be used in ViewModels (established rule from `drawer-sessions-delete-refactor`)
- The 884-test suite must pass unchanged
- **iOS**: every non-root page must have an explicit in-header back button — never rely on the MAUI Shell default back button rendering, which is visually inconsistent
- **iOS**: every dismissible modal must have an explicit close button — iOS has no hardware back button
- **Android**: do not intercept or override system back navigation; the custom back button is additive only

### Related files or modules
- `src/openMob/Resources/Styles/Colors.xaml` — full semantic token palette (do not add new tokens; use existing ones)
- `src/openMob/Resources/Styles/Styles.xaml` — typography, spacing, radius, implicit and explicit styles
- `src/openMob/Resources/Fonts/` — font assets directory
- `src/openMob/Resources/Splash/splash.svg` — splash source
- `src/openMob/AppShell.xaml` — Shell chrome, tab bar, flyout
- `src/openMob/MauiProgram.cs` — font registration
- `src/openMob/openMob.csproj` — `<MauiFont>` and `<MauiSplashScreen>` entries
- Past feature `drawer-sessions-delete-refactor`: established the icon usage pattern in `FlyoutContentView.xaml` and `FlyoutHeaderView.xaml` — these files must be included in the icon migration
