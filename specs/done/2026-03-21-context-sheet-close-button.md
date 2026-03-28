# Context Sheet — Close Button

## Metadata
| Field       | Value                                              |
|-------------|---------------------------------------------------|
| Date        | 2026-03-21                                        |
| Status      | **Completed**                                     |
| Version     | 1.0                                               |
| Completed   | 2026-03-28                                        |
| Branch      | feature/context-sheet-close-button (merged)       |
| Merged into | develop                                           |

---

## Executive Summary

La `ContextSheet` (modal delle impostazioni di sessione della chat) è attualmente priva di un pulsante di chiusura esplicito. Su Android la modal può essere dismessa tramite il back button nativo, ma su iOS questo gesto non esiste. Questa feature aggiunge un pulsante "Chiudi" fisso in fondo alla sheet, visibile su entrambe le piattaforme, che dismette la modal senza alterare le preferenze già auto-salvate.

---

## Scope

### In Scope
- Aggiunta di un pulsante "Chiudi" in fondo al layout XAML di `ContextSheet`
- Il pulsante è sempre visibile su iOS e Android
- Al tap il pulsante chiama `Navigation.PopModalAsync(animated: true)` per dismettere la modal
- Esposizione di un `CloseCommand` in `ContextSheetViewModel` (o gestione equivalente nel code-behind della `ContextSheet`)

### Out of Scope
- Logica di annulla/ripristina delle preferenze — le preferenze sono già auto-salvate al momento della modifica
- Modifiche alla logica di apertura della modal (`MauiPopupService.ShowContextSheetAsync`)
- Modifiche al comportamento del back button Android (già funzionante nativamente)
- Modifiche alle altre modal/picker presenti nell'app (AgentPicker, ModelPicker)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** La `ContextSheet` deve presentare un pulsante con etichetta "Chiudi" posizionato in fondo al layout, dopo tutti i controlli delle impostazioni di sessione.
2. **[REQ-002]** Il pulsante "Chiudi" deve essere visibile e interagibile su entrambe le piattaforme (iOS e Android).
3. **[REQ-003]** Al tap del pulsante, la modal viene dismessa tramite `Navigation.PopModalAsync(animated: true)`.
4. **[REQ-004]** La chiusura tramite pulsante non esegue alcuna operazione di salvataggio aggiuntiva né di rollback: le preferenze modificate durante la sessione sono già state persistite dall'auto-save.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob/Views/Popups/ContextSheet.xaml` | Modifica — aggiunta del pulsante "Chiudi" in fondo al layout | Unico file UI coinvolto |
| `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` | Modifica leggera — aggiunta di `CloseCommand` se la logica di dismiss è nel ViewModel | Alternativa: gestione diretta nel code-behind |
| `src/openMob/Views/Popups/ContextSheet.xaml.cs` | Possibile modifica — se `CloseCommand` è gestito nel code-behind | Da valutare in fase di analisi tecnica |

### Dependencies
- La modal è presentata tramite `Navigation.PushModalAsync` in `MauiPopupService.ShowContextSheetAsync` — la chiusura deve usare il corrispondente `Navigation.PopModalAsync`.
- Nessuna dipendenza da servizi esterni o da EF Core.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Il `CloseCommand` deve risiedere nel `ContextSheetViewModel` (Core) o nel code-behind della `ContextSheet` (MAUI)? | **Resolved** | **ViewModel.** `ContextSheetViewModel` ha già `IAppPopupService` iniettato e usa `_popupService.PopPopupAsync()` in `DeleteSessionAsync`. Il `CloseCommand` chiamerà lo stesso metodo — nessuna dipendenza aggiuntiva, piena testabilità, zero logica nel code-behind. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che la `ContextSheet` è aperta su **iOS**, quando l'utente tocca il pulsante "Chiudi", la modal viene dismessa e la chat torna visibile. *(REQ-001, REQ-002, REQ-003)*
- [ ] **[AC-002]** Dato che la `ContextSheet` è aperta su **Android**, il pulsante "Chiudi" è visibile e funzionante (in aggiunta al back button nativo). *(REQ-001, REQ-002, REQ-003)*
- [ ] **[AC-003]** Dopo la chiusura tramite pulsante, le preferenze modificate durante la sessione risultano invariate rispetto a quanto già auto-salvato. *(REQ-004)*
- [ ] **[AC-004]** Il pulsante "Chiudi" è posizionato in fondo alla sheet, dopo tutti i controlli delle impostazioni. *(REQ-001)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **Pattern di navigazione stabilito:** come documentato nella feature `session-context-sheet-1of3-core`, la modal è presentata con `Navigation.PushModalAsync` in `MauiPopupService.ShowContextSheetAsync`. La chiusura deve usare `Navigation.PopModalAsync(animated: true)` sulla stessa navigation stack.
- **Posizione del comando:** valutare se esporre `CloseCommand` nel `ContextSheetViewModel` (richiede iniezione di `INavigation` o di un wrapper di navigazione) oppure gestire il tap direttamente nel code-behind di `ContextSheet.xaml.cs` con `Navigation.PopModalAsync`. La seconda opzione è più semplice e non introduce dipendenze di navigazione nel Core — preferibile se non esiste già un'astrazione di navigazione nel progetto.
- **File XAML da modificare:** `src/openMob/Views/Popups/ContextSheet.xaml` — aggiungere un `Button` in fondo al layout root (presumibilmente un `VerticalStackLayout` o `Grid`).
- **Stile del pulsante:** seguire i token di stile esistenti in `App.xaml` / `ResourceDictionary` (colori `ColorPrimary`/`ColorBackground`, spaziatura `SpacingLg`, ecc.) per coerenza con il resto dell'UI della sheet.
- **Nessuna modifica al Core layer** se si sceglie l'approccio code-behind — questa feature è prevalentemente UI-only.
- **Test:** se `CloseCommand` è nel ViewModel, aggiungere un test unitario che verifica che il comando invochi il servizio/metodo di navigazione corretto. Se è nel code-behind, non è necessario alcun test unitario (nessuna logica di business).
- **Agenti coinvolti:** `om-mobile-ui` (XAML), eventualmente `om-mobile-core` se si aggiunge `CloseCommand` al ViewModel.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-21

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/context-sheet-close-button |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Open Question Resolution

**Q1 — CloseCommand placement:** Resolved as **ViewModel** approach.

Analysis of the existing codebase confirms that `ContextSheetViewModel` already has `IAppPopupService` injected and uses `_popupService.PopPopupAsync(ct)` inside `DeleteSessionAsync`. Adding `CloseCommand` to the ViewModel:
- Reuses the existing `IAppPopupService` abstraction — zero new dependencies
- Is fully testable via NSubstitute mocking of `IAppPopupService`
- Keeps the code-behind clean (no business logic)
- Is consistent with the existing pattern in the same ViewModel

The code-behind approach is explicitly rejected because it would be inconsistent with the established pattern where all navigation/popup dismissal goes through `IAppPopupService`.

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| ViewModel | om-mobile-core | `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` |
| XAML View | om-mobile-ui | `src/openMob/Views/Popups/ContextSheet.xaml` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ContextSheetViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- None — all changes are modifications to existing files.

### Files to Modify

- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — add `CloseCommand` that calls `_popupService.PopPopupAsync(ct)`
- `src/openMob/Views/Popups/ContextSheet.xaml` — add `Button` with `Text="Chiudi"` at the bottom of the `VerticalStackLayout`, bound to `CloseCommand`, styled with `SecondaryButton` style and `SpacingLg` margin
- `tests/openMob.Tests/ViewModels/ContextSheetViewModelTests.cs` — add test for `CloseCommand` (file may already exist; add test method)

### Technical Dependencies

- `IAppPopupService.PopPopupAsync(CancellationToken)` — already exists and is already injected into `ContextSheetViewModel`
- No new NuGet packages required
- No EF Core migrations required
- No new interfaces required

### Technical Risks

- **None significant.** This is a low-risk, additive change. The `PopPopupAsync` method is already used in the same ViewModel and is known to work correctly.
- The `[NotifyCanExecuteChangedFor]` pattern on `IsBusy` should **not** be applied to `CloseCommand` — the close button must always be enabled regardless of busy state, so the user can always exit the sheet.

### Execution Order

> Steps that can run in parallel are marked with ⟳.

1. [Git Flow] Create branch `feature/context-sheet-close-button`
2. [om-mobile-core] Add `CloseCommand` to `ContextSheetViewModel`
3. ⟳ [om-mobile-ui] Add "Chiudi" button to `ContextSheet.xaml` (can start immediately — binding surface is trivial: just `CloseCommand`)
4. [om-tester] Write unit test for `CloseCommand`
5. [om-reviewer] Full review against spec
6. [Fix loop if needed] Address Critical and Major findings
7. [Git Flow] Finish branch and merge

### Definition of Done

- [ ] [REQ-001] Button "Chiudi" present at the bottom of `ContextSheet.xaml`
- [ ] [REQ-002] Button visible on both iOS and Android (no platform conditional needed)
- [ ] [REQ-003] `CloseCommand` calls `_popupService.PopPopupAsync(ct)` — modal dismissed
- [ ] [REQ-004] No save/rollback logic in `CloseCommand` — preferences already auto-saved
- [ ] [AC-001] iOS: tap "Chiudi" → modal dismissed
- [ ] [AC-002] Android: button visible and functional
- [ ] [AC-003] Preferences unchanged after close
- [ ] [AC-004] Button positioned after all settings controls
- [ ] Unit test for `CloseCommand` written and passing
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
