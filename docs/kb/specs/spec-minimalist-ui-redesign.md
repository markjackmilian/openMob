# Minimalist Design System & UI Redesign — ChatGPT-Inspired

## Metadata
| Field       | Value                                    |
|-------------|------------------------------------------|
| Date        | 2026-03-16                               |
| Status      | **Completed**                            |
| Version     | 1.0                                      |
| Completed   | 2026-03-16                               |
| Branch      | feature/minimalist-ui-redesign (merged)  |
| Merged into | develop                                  |

## Summary

openMob adopted a new ChatGPT-inspired minimalist design system: Inter font (4 weights), Material Symbols Outlined icons, green accent palette (#10A37F/#1DB88E) with full light/dark support. The ChatPage was redesigned with a custom topbar (model selector), message bubble templates, and a modern multiline input bar. All existing screens were updated to the new visual system without any business logic or ViewModel changes.

## Key Decisions
- **Font**: Inter (Regular, Medium, SemiBold, Bold) replaces OpenSans
- **Icons**: Material Symbols Outlined (static TTF, weight 400) replaces all Unicode emoji/glyphs
- **Accent color**: Green #10A37F (light) / #1DB88E (dark) replaces Apple HIG blue #007AFF
- **Icon constants**: Centralized in `src/openMob/Helpers/MaterialIcons.cs` — all XAML references use `{x:Static helpers:MaterialIcons.Xxx}`
- **No ViewModel changes**: Pure UI/XAML feature — zero business logic modifications
- **ModelPickerSheet**: Static 3 Claude models placeholder (Haiku, Sonnet, Opus) — API integration deferred

## Requirements Implemented
REQ-001 through REQ-037 (37 requirements), AC-001 through AC-014 (14 acceptance criteria)

## Files Changed (32 files)
- **New**: `MaterialIcons.cs`, 5 font TTF files
- **Deleted**: 2 OpenSans TTF files
- **Modified**: `MauiProgram.cs`, `Colors.xaml`, `Styles.xaml`, `ChatPage.xaml/.cs`, all Flyout views, all Pages, all Popups, `StatusBannerView.xaml`

## Lessons Learned
1. **MAUI DataTemplate single root**: A `DataTemplate` in MAUI must have exactly one root element. Adding a separator `BoxView` as a sibling to `SwipeView` inside a `DataTemplate` causes `XamlParseException` at runtime. Wrap in `VerticalStackLayout Spacing="0"`.
2. **MAUI Padding markup extension**: `Padding="{StaticResource SpacingSm},0"` is invalid XAML — you cannot mix a markup extension with literal values in a composite property. Use `Padding="8,0"` instead.
3. **MAUI incremental build caching**: After XAML changes, `dotnet build` may not update the APK correctly. Always `dotnet clean` + full rebuild + `adb uninstall` + fresh install when debugging runtime XAML crashes.
4. **MauiXamlInflator=SourceGen disabled**: The project has XAML source generation disabled due to a MAUI 10 bug with AppThemeBinding. XAML is parsed at runtime, so syntax errors only surface on device launch, not at compile time.
