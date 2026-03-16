# Technical Analysis — Minimalist Design System & UI Redesign
**Feature slug:** minimalist-ui-redesign
**Completed:** 2026-03-16
**Branch:** feature/minimalist-ui-redesign
**Complexity:** High

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/minimalist-ui-redesign |
| Branches from | develop |
| Estimated complexity | High |
| Agents involved | om-mobile-core (minimal), om-mobile-ui (primary), om-reviewer |

## Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Font registration (MauiProgram.cs) | om-mobile-core | `src/openMob/MauiProgram.cs` |
| Icon constants (MaterialIcons.cs) | om-mobile-core | `src/openMob/Helpers/MaterialIcons.cs` |
| Design tokens (Colors.xaml) | om-mobile-ui | `src/openMob/Resources/Styles/Colors.xaml` |
| Styles (Styles.xaml) | om-mobile-ui | `src/openMob/Resources/Styles/Styles.xaml` |
| ChatPage redesign | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml`, `ChatPage.xaml.cs` |
| Flyout views | om-mobile-ui | `src/openMob/Views/Controls/Flyout*.xaml` |
| All other pages | om-mobile-ui | `src/openMob/Views/Pages/*.xaml` |
| Popup sheets | om-mobile-ui | `src/openMob/Views/Popups/*.xaml` |
| StatusBannerView | om-mobile-ui | `src/openMob/Views/Controls/StatusBannerView.xaml` |

## Color Palette Reference (post-redesign)

### Primary Accent (Green)
| Token | Light | Dark |
|-------|-------|------|
| ColorPrimary | #10A37F | #1DB88E |
| ColorPrimaryContainer | #E6F4EF | #0D2E23 |
| ColorOnPrimary | #FFFFFF | #FFFFFF |
| ColorOnPrimaryContainer | #0D8F6F | #1DB88E |

### Backgrounds
| Token | Light | Dark |
|-------|-------|------|
| ColorBackground | #FFFFFF | #0D0D0D |
| ColorBackgroundSecondary | #F7F7F8 | #1A1A1A |

### Surfaces
| Token | Light | Dark |
|-------|-------|------|
| ColorSurface | #FFFFFF | #1A1A1A |
| ColorSurfaceSecondary | #F0F0F0 | #2A2A2A |

### Text
| Token | Light | Dark |
|-------|-------|------|
| ColorOnBackground | #0D0D0D | #FFFFFF |
| ColorOnBackgroundSecondary | #6E6E80 | #8E8EA0 |
| ColorOnBackgroundTertiary | #ACACBE | #565869 |

### Borders
| Token | Light | Dark |
|-------|-------|------|
| ColorOutline | #E5E5E5 | #2A2A2A |
| ColorSeparator | #E5E5E5 | #2A2A2A |

## Font Registration (MauiProgram.cs)

```csharp
fonts.AddFont("Inter-Regular.ttf", "InterRegular");
fonts.AddFont("Inter-Medium.ttf", "InterMedium");
fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
fonts.AddFont("Inter-Bold.ttf", "InterBold");
fonts.AddFont("MaterialSymbols-Outlined.ttf", "MaterialSymbols");
```

## Font Usage Convention

| Context | FontFamily | Example |
|---------|-----------|---------|
| Body text, inputs | InterRegular | Labels, Entry, Editor |
| Titles (LargeTitle, Title1-3) | InterBold | Page titles, section headers |
| Headlines, emphasis | InterSemiBold | Card headers, topbar title |
| Callout, links | InterMedium | Navigation items, secondary actions |
| Icons | MaterialSymbols | All icons via `{x:Static helpers:MaterialIcons.Xxx}` |

## MaterialIcons.cs Glyph Reference

| Constant | Unicode | Usage |
|----------|---------|-------|
| Menu | \ue5d2 | Hamburger menu |
| Add | \ue145 | Add/plus button |
| ArrowUpward | \ue5d8 | Send button |
| Mic | \ue029 | Microphone placeholder |
| Edit | \ue3c9 | New chat / edit |
| Settings | \ue8b8 | Settings icon |
| ChevronRight | \ue5cc | Disclosure indicator |
| Close | \ue5cd | Close/dismiss |
| Check | \ue5ca | Selected/checkmark |
| Folder | \ue2c7 | Projects |
| Chat | \ue0b7 | Chat/message |
| MoreVert | \ue5d4 | More options |
| Delete | \ue872 | Delete action |
| Notifications | \ue7f4 | Bell icon |
| Public | \ue80b | Globe/network |
| SmartToy | \ue99a | AI/agent icon |
| Key | \ue73c | API key |
| CheckCircle | \ue86c | Success state |
| RadioButtonUnchecked | \ue836 | Unselected option |

## Runtime Bugs Encountered & Fixed

### Bug 1: DataTemplate Multiple Children
- **Error**: `XamlParseException: Multiple child elements in DataTemplate`
- **Cause**: `FlyoutContentView.xaml` DataTemplate had `SwipeView` + `BoxView` as siblings
- **Fix**: Wrap both in `<VerticalStackLayout Spacing="0">`
- **Rule**: MAUI DataTemplate must have exactly ONE root element

### Bug 2: Invalid Padding Markup Extension
- **Error**: `XamlParseException: Expression must end with '}'`
- **Cause**: `Padding="{StaticResource SpacingSm},0"` — cannot mix markup extension with literal
- **Fix**: Use `Padding="8,0"` (hardcoded value matching SpacingSm=8)
- **Rule**: Composite XAML properties (Padding, Margin, Thickness) cannot use StaticResource for individual components

### Bug 3: Incremental Build Stale XAML
- **Symptom**: Build succeeds but device runs old XAML
- **Cause**: MAUI Android incremental build doesn't always re-package modified XAML files
- **Fix**: `dotnet clean` + full rebuild + `adb uninstall` + fresh install
- **Rule**: Always clean build when debugging runtime XAML crashes
