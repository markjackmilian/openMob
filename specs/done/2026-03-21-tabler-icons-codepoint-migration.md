# Tabler Icons — Font File Integration & Codepoint Migration

## Metadata
| Field       | Value                                              |
|-------------|---------------------------------------------------|
| Date        | 2026-03-21                                        |
| Status      | **Completed**                                     |
| Version     | 1.0                                               |
| Completed   | 2026-03-28                                        |
| Branch      | feature/ui-overhaul-design-system (merged)        |
| Merged into | develop                                           |

---

## Executive Summary

La feature `ui-overhaul-design-system` ha completato tutta l'architettura necessaria per Tabler Icons: `IconKeys.cs` centralizzato, stili `IconLabel`/`IconLabelLg`, font alias `TablerIcons` registrato in `MauiProgram.cs`, e tutti i 68 riferimenti icone migrati in XAML. Tuttavia i codepoint Unicode in `IconKeys.cs` sono ancora quelli di **MaterialSymbols** (il vecchio font), non di Tabler Icons. Questa spec completa la migrazione: sostituisce il file font fisico e aggiorna tutti i 36 codepoint con i valori corretti di Tabler Icons.

---

## Contesto Tecnico

### Stato attuale (dopo merge di `ui-overhaul-design-system`)

- `src/openMob/Resources/Fonts/MaterialSymbols-Outlined.ttf` — font fisico ancora presente
- `src/openMob/MauiProgram.cs` — font registrato con alias `"MaterialSymbols"` punta a `MaterialSymbols-Outlined.ttf`
- `src/openMob/openMob.csproj` — `<MauiFont>` wildcard include tutti i font in `Resources/Fonts/`
- `src/openMob/Helpers/MaterialIcons.cs` — 36 costanti con codepoint MaterialSymbols (es. `"\ue5d2"`)
- Tutti i file XAML usano `{x:Static helpers:MaterialIcons.XxxName}` con `FontFamily="MaterialSymbols"`

### Obiettivo

- Aggiungere `TablerIcons.ttf` e rimuovere `MaterialSymbols-Outlined.ttf`
- Rinominare `MaterialIcons.cs` → `IconKeys.cs` (classe `MaterialIcons` → `IconKeys`)
- Aggiornare i 36 codepoint con i valori corretti di Tabler Icons
- Aggiornare `MauiProgram.cs` per registrare `TablerIcons.ttf` con alias `"TablerIcons"`
- Aggiornare tutti i riferimenti XAML: `MaterialIcons.XxxName` → `IconKeys.XxxName` e `FontFamily="MaterialSymbols"` → `FontFamily="TablerIcons"`
- Rinominare le costanti con nomi Tabler-style (es. `Close` → `X`, `ContentCopy` → `Copy`, etc.)

---

## Scope

### In Scope
- Aggiunta di `TablerIcons.ttf` in `src/openMob/Resources/Fonts/`
- Rimozione di `MaterialSymbols-Outlined.ttf`
- Aggiornamento registrazione font in `MauiProgram.cs`
- Creazione di `src/openMob/Helpers/IconKeys.cs` con codepoint Tabler corretti (sostituzione di `MaterialIcons.cs`)
- Rimozione di `src/openMob/Helpers/MaterialIcons.cs`
- Aggiornamento di tutti i file XAML per usare `IconKeys.XxxName` e `FontFamily="TablerIcons"`
- Verifica visiva che tutte le icone renderino correttamente

### Out of Scope
- Modifiche a ViewModel, servizi, test
- Aggiunta di nuove icone non già presenti in `MaterialIcons.cs`

---

## Functional Requirements

1. **[REQ-001]** Il file `TablerIcons.ttf` (Tabler Icons webfont, MIT licence) deve essere presente in `src/openMob/Resources/Fonts/` e registrato in `MauiProgram.cs` con alias `"TablerIcons"`.

2. **[REQ-002]** Il file `MaterialSymbols-Outlined.ttf` deve essere rimosso da `src/openMob/Resources/Fonts/`.

3. **[REQ-003]** In `MauiProgram.cs`, la registrazione del font deve puntare a `TablerIcons.ttf` con alias `"TablerIcons"`.

4. **[REQ-004]** Un nuovo file `src/openMob/Helpers/IconKeys.cs` deve contenere tutti i 36 codepoint aggiornati con i valori Unicode corretti di Tabler Icons webfont. Il vecchio `MaterialIcons.cs` deve essere eliminato.

5. **[REQ-005]** La documentazione XML della classe deve riflettere l'uso di Tabler Icons (non MaterialSymbols).

6. **[REQ-006]** Tutti i file XAML che referenziano `MaterialIcons.XxxName` devono essere aggiornati a `IconKeys.XxxName` e `FontFamily="MaterialSymbols"` → `FontFamily="TablerIcons"`.

7. **[REQ-007]** Il progetto deve buildare con zero errori e zero warning dopo la migrazione.

8. **[REQ-008]** Nessun ViewModel, servizio o test deve essere modificato.

---

## Mapping dei Codepoint (VERIFICATO)

Codepoint estratti da `tabler-icons.css` v2.47.0 via CDN (`https://cdn.jsdelivr.net/npm/@tabler/icons-webfont@latest/tabler-icons.css`).

| Costante `IconKeys` | Vecchia costante `MaterialIcons` | Tabler Icon Name | Codepoint Tabler |
|---------------------|----------------------------------|------------------|-----------------|
| `Menu` | `Menu` | `menu-2` | `\uec42` |
| `Add` | `Add` | `plus` | `\ueb0b` |
| `Send` | `Send` | `send-2` | `\ufd5d` |
| `ArrowUp` | `ArrowUpward` | `arrow-up` | `\uea25` |
| `AutoAwesome` | `AutoAwesome` | `sparkles` | `\uf6d7` |
| `Mic` | `Mic` | `microphone` | `\ueaf0` |
| `Edit` | `Edit` | `pencil` | `\ueb04` |
| `Settings` | `Settings` | `settings` | `\ueb20` |
| `ChevronRight` | `ChevronRight` | `chevron-right` | `\uea61` |
| `Code` | `Code` | `code` | `\uea77` |
| `X` | `Close` | `x` | `\ueb55` |
| `Copy` | `ContentCopy` | `copy` | `\uea7a` |
| `Check` | `Check` | `check` | `\uea5e` |
| `Folder` | `Folder` | `folder` | `\ueaad` |
| `Chat` | `Chat` | `message` | `\ueaef` |
| `DotsVertical` | `MoreVert` | `dots-vertical` | `\uea94` |
| `Trash` | `Delete` | `trash` | `\ueb41` |
| `Bell` | `Notifications` | `bell` | `\uea35` |
| `Globe` | `Public` | `world` | `\ueb54` |
| `Brain` | `Psychology` | `brain` | `\uf59f` |
| `AlertTriangle` | `Warning` | `alert-triangle` | `\uea06` |
| `AlertCircle` | `Error` | `alert-circle` | `\uea05` |
| `InfoCircle` | `Info` | `info-circle` | `\ueac5` |
| `Search` | `Search` | `search` | `\ueb1c` |
| `ArrowLeft` | `ArrowBack` | `arrow-left` | `\uea19` |
| `ChevronDown` | `ExpandMore` | `chevron-down` | `\uea5f` |
| `Robot` | `SmartToy` | `robot` | `\uf00b` |
| `PlayerStop` | `Stop` | `player-stop` | `\ued4a` |
| `Key` | `Key` | `key` | `\ueac7` |
| `Link` | `Link` | `link` | `\ueade` |
| `CircleCheck` | `CheckCircle` | `circle-check` | `\uea67` |
| `Circle` | `RadioButtonUnchecked` | `circle` | `\uea6b` |
| `CircleDot` | `RadioButtonChecked` | `circle-dot` | `\uefb1` |
| `Clock` | `Schedule` | `clock` | `\uea70` |
| `Terminal` | `Terminal` | `terminal-2` | `\uebef` |
| `Adjustments` | `Tune` | `adjustments-horizontal` | `\uec38` |

---

## Acceptance Criteria

- [ ] **[AC-001]** `dotnet build openMob.sln` — zero errori, zero warning.
- [ ] **[AC-002]** `MaterialSymbols-Outlined.ttf` non è presente in `Resources/Fonts/` né referenziato in alcun file.
- [ ] **[AC-003]** `TablerIcons.ttf` è presente in `Resources/Fonts/` e registrato in `MauiProgram.cs`.
- [ ] **[AC-004]** Tutti i 36 codepoint in `IconKeys.cs` sono valori Unicode Tabler Icons (range `\uea01`–`\ufdff` circa).
- [ ] **[AC-005]** L'app lanciata su simulatore iOS o emulatore Android mostra tutte le icone correttamente — nessun quadratino vuoto, nessun glifo errato.
- [ ] **[AC-006]** Nessun ViewModel, servizio o test è stato modificato.
- [ ] **[AC-007]** `dotnet test` — tutti i test passano invariati.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature (sub-task of ui-overhaul-design-system) |
| Git Flow branch | feature/ui-overhaul-design-system (existing) |
| Branches from | develop |
| Estimated complexity | Medium |
| Estimated agents involved | om-mobile-ui, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Fonts / Assets | om-mobile-ui | src/openMob/Resources/Fonts/ |
| Project Config | om-mobile-ui | src/openMob/MauiProgram.cs |
| Icon Constants | om-mobile-ui | src/openMob/Helpers/IconKeys.cs (replaces MaterialIcons.cs) |
| XAML Views | om-mobile-ui | All XAML files referencing MaterialIcons → IconKeys |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob/Helpers/IconKeys.cs` — centralised static class with 36 Tabler Icons glyph constants (replaces `MaterialIcons.cs`)
- `src/openMob/Resources/Fonts/TablerIcons.ttf` — Tabler Icons webfont v2.47.0 (downloaded from npm/GitHub)

### Files to Modify

- `src/openMob/MauiProgram.cs` — replace `MaterialSymbols-Outlined.ttf` / `MaterialSymbols` font registration with `TablerIcons.ttf` / `TablerIcons`
- All XAML files (Pages, Controls, Popups) — replace `MaterialIcons.XxxName` → `IconKeys.XxxName` and `FontFamily="MaterialSymbols"` → `FontFamily="TablerIcons"`

### Files to Delete

- `src/openMob/Resources/Fonts/MaterialSymbols-Outlined.ttf` — replaced by TablerIcons.ttf
- `src/openMob/Helpers/MaterialIcons.cs` — replaced by IconKeys.cs

### Constant Renaming Map

The following constants are renamed from MaterialSymbols naming to Tabler-style naming. XAML references must be updated accordingly.

| Old (`MaterialIcons.X`) | New (`IconKeys.X`) | XAML files affected |
|--------------------------|---------------------|---------------------|
| `ArrowUpward` | `ArrowUp` | InputBarView.xaml |
| `Close` | `X` | ChatPage.xaml |
| `ContentCopy` | `Copy` | (code block copy — verify usage) |
| `MoreVert` | `DotsVertical` | (verify usage) |
| `Delete` | `Trash` | ProjectsPage.xaml, ContextSheet.xaml |
| `Notifications` | `Bell` | SettingsPage.xaml |
| `Public` | `Globe` | (verify usage) |
| `Psychology` | `Brain` | ContextSheet.xaml |
| `Warning` | `AlertTriangle` | (verify usage) |
| `Error` | `AlertCircle` | OnboardingModelSelectionView.xaml |
| `Info` | `InfoCircle` | SettingsPage.xaml, StatusBannerView.xaml |
| `ArrowBack` | `ArrowLeft` | (verify usage) |
| `ExpandMore` | `ChevronDown` | (verify usage) |
| `SmartToy` | `Robot` | Multiple files (8+ references) |
| `Stop` | `PlayerStop` | (verify usage) |
| `CheckCircle` | `CircleCheck` | OnboardingCompletionView.xaml, ServerManagementPage.xaml |
| `RadioButtonUnchecked` | `Circle` | OnboardingModelSelectionView.xaml |
| `RadioButtonChecked` | `CircleDot` | OnboardingModelSelectionView.xaml |
| `Schedule` | `Clock` | (verify usage) |
| `Tune` | `Adjustments` | ChatPage.xaml |

### Technical Dependencies

- **Tabler Icons TTF v2.47.0**: Must be downloaded from npm (`@tabler/icons-webfont`) or GitHub releases before implementation starts. The TTF file is at `packages/icons-webfont/fonts/tabler-icons.ttf` in the release.
- **Codepoints verified**: All 36 codepoints have been extracted from `tabler-icons.css` v2.47.0 and are listed in the mapping table above.
- No new NuGet packages required
- No ViewModel, service, or business logic changes
- No unit test changes

### Technical Risks

- **Font file download**: The TTF must be the webfont variant (not the SVG sprite or React package). Wrong file = no glyphs render.
- **Codepoint accuracy**: All 36 codepoints verified against `tabler-icons.css` v2.47.0. If a different version of the font is used, codepoints may differ.
- **XAML reference count**: There are 57+ XAML references to `MaterialIcons.XxxName` across ~20 files. All must be updated. A missed reference will cause a build error (good — fails fast).
- **FontFamily string match**: The alias `"TablerIcons"` in `MauiProgram.cs` must exactly match `FontFamily="TablerIcons"` in XAML. Case-sensitive.
- **`send-2` codepoint `\ufd5d`**: This is outside the typical `\uea01`–`\uefff` range noted in the spec. This is correct — Tabler Icons v2.47.0 uses extended PUA range up to `\ufdxx` for newer icons.

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. [Git Flow] Switch to branch `feature/ui-overhaul-design-system`
2. [om-mobile-ui] Download `TablerIcons.ttf` and add to `Resources/Fonts/`
3. [om-mobile-ui] Create `IconKeys.cs` with all 36 Tabler codepoints
4. [om-mobile-ui] Update `MauiProgram.cs` font registration
5. [om-mobile-ui] Update all XAML files: `MaterialIcons` → `IconKeys`, `MaterialSymbols` → `TablerIcons`
6. [om-mobile-ui] Delete `MaterialSymbols-Outlined.ttf` and `MaterialIcons.cs`
7. [om-mobile-ui] Build verification (`dotnet build`)
8. [om-reviewer] Full review against spec
9. [Fix loop if needed] Address Critical and Major findings

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-008]` requirements implemented
- [ ] All `[AC-001]` through `[AC-007]` acceptance criteria satisfied
- [ ] No unit tests modified; all tests pass unchanged
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Spec moved to `specs/done/` with Completed status
