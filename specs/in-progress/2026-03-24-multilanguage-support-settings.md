# Multilanguage Support with User Settings

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-24                   |
| Status  | Ready for Review             |
| Version | 1.0                          |
| Branch  | feature/multilanguage-support-settings |

---

## Executive Summary

The app must support multiple languages, with English as the default. Users can select their preferred language (English or Italian) from the existing Settings page. The preference is persisted locally on the device and applied at the next app restart. Only static UI strings are localised; dynamic content from APIs is out of scope.

---

## Scope

### In Scope
- Support for 2 languages: **English** (default) and **Italian**
- Language selection control on the existing Settings page
- Persistent local storage of the user's language preference
- Localisation of all static UI strings (labels, buttons, error messages, placeholders, titles)
- Silent fallback to English for any string not translated in Italian
- App restart required to apply a language change (no hot-swap)

### Out of Scope
- Localisation of dynamic content from server/API responses
- Automatic detection of the device OS language
- Support for additional languages beyond English and Italian in this version
- In-place language switching without app restart

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** On first install, the app must default to **English** as the active language.
2. **[REQ-002]** The Settings page must expose a language selector control (e.g. Picker/dropdown) listing the available options: **English** and **Italiano**.
3. **[REQ-003]** When the user selects a new language, the app must display an informational message stating that a restart is required for the change to take effect.
4. **[REQ-004]** The selected language preference must be saved persistently to local device storage immediately upon selection.
5. **[REQ-005]** On every app startup, the app must read the saved language preference and apply the corresponding culture to the UI before the first screen is rendered.
6. **[REQ-006]** All static UI strings (labels, buttons, titles, error messages, placeholders) across the entire app must be available in both English and Italian via resource files.
7. **[REQ-007]** If a string is missing in the selected language's resource file, the English version must be shown silently (no visible error or placeholder).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SettingsPage` / `SettingsViewModel` | Modified | Add language Picker and restart-required notification |
| `MauiProgram.cs` | Modified | Read saved language preference and set `CultureInfo` at bootstrap |
| `openMob.Core` — new `ILanguageService` | New | Interface + implementation for reading/writing language preference to local storage |
| `CoreServiceExtensions` | Modified | Register `ILanguageService` in the DI container |
| Localisation resource files (`.resx`) | New | `AppResources.resx` (EN) and `AppResources.it.resx` (IT) covering all static strings |
| All existing XAML views | Modified | Replace hardcoded strings with resource bindings |

### Dependencies
- Local storage mechanism (e.g. `Preferences` API from .NET MAUI) for persisting the language key
- .NET `ResourceManager` / `.resx` infrastructure for string resolution
- `System.Globalization.CultureInfo` for applying the selected culture at runtime

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Should the language selector show native language names (e.g. "Italiano") or localised names (e.g. "Italian" when in English)? | Open | To be decided during implementation |
| 2 | Should the restart prompt be a simple `DisplayAlert` or a custom in-app banner/snackbar? | Open | To be decided during UI implementation |
| 3 | Is `Preferences` (MAUI) the agreed storage mechanism, or is a custom abstraction required? | Open | `ILanguageService` abstraction allows the storage backend to be decided during technical analysis |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Given a fresh install with no saved preference, when the app launches, then the UI is displayed in English. *(REQ-001, REQ-005)*
- [ ] **[AC-002]** Given the Settings page, when the user selects "Italiano" and restarts the app, then all static UI strings are displayed in Italian. *(REQ-002, REQ-004, REQ-005, REQ-006)*
- [ ] **[AC-003]** Given the Settings page, when the user selects "English" and restarts the app, then all static UI strings are displayed in English. *(REQ-002, REQ-004, REQ-005, REQ-006)*
- [ ] **[AC-004]** Given a saved language preference, when the app is closed and reopened, then the previously selected language is applied without requiring the user to re-select it. *(REQ-004, REQ-005)*
- [ ] **[AC-005]** Given a string not translated in Italian, when the active language is Italian, then the English version of that string is shown with no visible error or placeholder. *(REQ-007)*
- [ ] **[AC-006]** Given the Settings page, when the user selects a different language, then an informational message is shown indicating that a restart is required to apply the change. *(REQ-003)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Localisation mechanism**: .NET MAUI supports `.resx`-based localisation natively. The recommended approach is to create `AppResources.resx` (neutral/English) and `AppResources.it.resx` (Italian) in `openMob.Core` or a shared resources assembly. `ResourceManager` resolves strings based on the current thread culture.
- **Culture application at startup**: `CultureInfo` must be set on both `Thread.CurrentThread.CurrentCulture` and `Thread.CurrentThread.CurrentUICulture` (and their `DefaultThread` equivalents) early in `MauiProgram.cs`, before any page is constructed, to ensure all resource lookups use the correct culture.
- **`ILanguageService`**: Should expose at minimum `GetSavedLanguage(): string` and `SaveLanguage(string languageCode): void`. The language code should follow BCP-47 format (e.g. `"en"`, `"it"`). The default value when no preference is saved must be `"en"`.
- **Storage backend**: MAUI `Preferences` API is the natural fit for a lightweight string key/value. Since `ILanguageService` is an abstraction, the implementation detail can be confirmed during technical analysis.
- **XAML bindings**: All hardcoded strings in XAML views must be replaced. The standard MAUI pattern uses `x:Static` with a static resource accessor class, or a markup extension wrapping `ResourceManager`. Confirm the preferred pattern to ensure consistency across all views.
- **`SettingsViewModel`**: Must expose an `ObservableProperty` for the selected language and a command to save it. The restart-required notification should be triggered from the command, not from the view code-behind.
- **Testing**: `ILanguageService` must be fully mockable. Unit tests should cover: default language on first run, save/load round-trip, and ViewModel command behaviour (save called, notification raised).
- **Constraints**: `openMob.Core` must remain free of MAUI dependencies. If `Preferences` (a MAUI API) is used as the storage backend, it must be wrapped behind `ILanguageService` and injected — never called directly from Core.

---

## Technical Analysis

### Change Type
- Feature

### Branch
- `feature/multilanguage-support-settings`

### Layers Involved
- `openMob.Core`: language service interface, localization helper, resources, settings ViewModel, language option model
- `openMob`: MAUI language service implementation, bootstrap culture application, localized Settings page UI
- `openMob.Tests`: ViewModel and localization helper coverage

### Execution Order
1. Add localization infrastructure and language persistence abstraction
2. Update startup and settings UI to consume the new language preference
3. Add/adjust unit tests for settings and localization behavior

### Risks / Notes
- Existing UI still contains some hardcoded strings outside Settings; the new localization infrastructure is in place, but additional view updates may be needed in follow-up work.
- App language changes apply on next restart, so the settings UX shows a restart reminder after saving.
