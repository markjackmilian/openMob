# ADR: Adopt Inter Font + Material Symbols Outlined as Design System Foundation

## Date
2026-03-16

## Status
Accepted

## Context
openMob needed a cohesive, modern design system to replace the default OpenSans font and scattered Unicode emoji/glyph icons. The app's visual identity was inconsistent — using Apple HIG blue accent, system-default fonts, and a mix of Unicode characters for icons. A ChatGPT-inspired minimalist aesthetic was chosen as the design direction.

## Decision
1. **Inter** (4 weights: Regular, Medium, SemiBold, Bold) adopted as the sole app font, registered in `MauiProgram.cs` with aliases `InterRegular`, `InterMedium`, `InterSemiBold`, `InterBold`.
2. **Material Symbols Outlined** (static TTF, weight 400) adopted as the sole icon system, registered as `MaterialSymbols`.
3. All icon glyphs centralized in `src/openMob/Helpers/MaterialIcons.cs` as `public const string` fields with Unicode codepoints.
4. All XAML icon references must use `{x:Static helpers:MaterialIcons.Xxx}` — raw Unicode escapes (`&#xNNNN;`) are prohibited.
5. **Green accent** (#10A37F light / #1DB88E dark) replaces Apple HIG blue (#007AFF) as the primary color.
6. All colors in XAML must reference semantic tokens via `AppThemeBinding` from `Colors.xaml` — zero hardcoded hex values.

## Rationale
- **Inter** is a highly legible, open-source font designed for screens, with excellent weight coverage and Google Fonts availability.
- **Material Symbols Outlined** provides 3000+ consistent icons in a single TTF file, eliminating the need for individual SVG/PNG assets.
- Centralizing icon constants in a single C# class enables IDE autocomplete, compile-time checking, and single-point-of-change for icon updates.
- The green accent differentiates openMob from generic iOS apps while aligning with the ChatGPT-inspired design language.

## Alternatives Considered
- **SF Symbols (iOS only)**: Rejected — not cross-platform, would require separate Android icon solution.
- **SVG/PNG icon assets**: Rejected — larger bundle size, no font-level scaling, harder to maintain.
- **Material Design Icons (MDI)**: Considered — Material Symbols is the newer, more complete successor.
- **Keeping OpenSans**: Rejected — Inter has better weight coverage (4 vs 2) and is more aligned with modern minimalist design.

## Consequences
### Positive
- Consistent visual identity across all 20+ screens
- Single source of truth for icons (`MaterialIcons.cs`) and colors (`Colors.xaml`)
- Full light/dark mode support via `AppThemeBinding` on every color token
- Font files are OFL-licensed — no licensing concerns

### Negative / Trade-offs
- Font TTF files add ~2.6MB to the APK (Inter 4 weights + Material Symbols)
- `MaterialIcons.cs` must be manually updated when new icons are needed
- Material Symbols static TTF may not contain every possible icon variant (weight/fill/grade)
- `FontFamily` must be explicitly set in all styles — MAUI does not cascade custom fonts automatically

## Related Features
minimalist-ui-redesign

## Related Agents
om-mobile-core (font registration, MaterialIcons.cs), om-mobile-ui (all XAML), om-reviewer
