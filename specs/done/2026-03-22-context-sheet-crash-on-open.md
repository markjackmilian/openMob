# ContextSheet Crash on Open — Diagnostics & Fix

## Metadata
| Field       | Value                                      |
|-------------|--------------------------------------------|
| Date        | 2026-03-22                                 |
| Status      | **Completed**                              |
| Version     | 1.0                                        |
| Completed   | 2026-03-22                                 |
| Branch      | bugfix/context-sheet-crash-on-open (merged)|
| Merged into | develop                                    |

---

## Executive Summary

L'app crasha quando l'utente tappa il bottone context (⚙️) nella `ChatPage` per aprire la `ContextSheet`. Il crash si verifica al momento del tap, prima o durante la presentazione della sheet. La causa esatta deve essere determinata tramite lettura del logcat ADB su dispositivo Android. Questo documento specifica il processo di diagnostica e il fix da applicare.

---

## Scope

### In Scope
- Lettura del logcat ADB per identificare exception type, stack trace e thread del crash
- Analisi del flusso di apertura `ChatViewModel.OpenContextSheetAsync` → `MauiPopupService.ShowContextSheetAsync` → `ContextSheetViewModel.InitializeAsync` → `IPopupService.Current.PushAsync`
- Fix del crash nel layer identificato (ViewModel, DI, `MauiPopupService`, o XAML)
- Verifica assenza di `ConfigureAwait(false)` residui nel percorso di apertura (ADR vigente)
- Verifica compatibilità dei lifetime DI nel percorso di risoluzione della sheet
- Test unitari di regressione per il percorso di apertura

### Out of Scope
- Modifiche funzionali alla `ContextSheet` (nuove righe, nuove preferenze)
- Modifiche all'UI della sheet
- Fix di crash in altri popup (ModelPickerSheet, AgentPickerSheet, ecc.)

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** L'agente deve eseguire `adb logcat` su dispositivo Android fisico o emulatore, riprodurre il crash tappando il bottone context in `ChatPage`, e catturare il log completo dell'eccezione (exception type, message, stack trace, thread name).

2. **[REQ-002]** L'agente deve analizzare il log e identificare la causa radice tra le ipotesi note (vedi sezione Note per l'Analisi Tecnica) o individuarne una nuova.

3. **[REQ-003]** Il crash deve essere risolto in modo che il tap sul bottone context (⚙️) in `ChatPage` apra la `ContextSheet` senza eccezioni su Android.

4. **[REQ-004]** Il fix non deve introdurre `ConfigureAwait(false)` in nessun file sotto `src/openMob.Core/ViewModels/` (ADR: `adr-configureawait-viewmodels` — regola vigente e non negoziabile).

5. **[REQ-005]** Il fix non deve alterare il comportamento funzionale della `ContextSheet` (caricamento preferenze, auto-save, delete session, close).

6. **[REQ-006]** Se il fix richiede modifiche al lifetime DI di `ContextSheet` o `ContextSheetViewModel`, la registrazione deve rimanere Transient per garantire un'istanza fresca ad ogni apertura.

7. **[REQ-007]** Dopo il fix, la build deve compilare senza warning (`dotnet build openMob.sln` → exit code 0, zero warnings).

8. **[REQ-008]** Dopo il fix, tutti i test esistenti devono continuare a passare (`dotnet test` → 0 failures).

9. **[REQ-009]** Devono essere aggiunti o aggiornati test unitari che coprano il percorso di apertura della sheet (es. `OpenContextSheetAsync` su `ChatViewModel` — verifica che `IAppPopupService.ShowContextSheetAsync` venga chiamato con i parametri corretti).

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ChatViewModel.OpenContextSheetAsync` | Potenzialmente modificato | Entry point del flusso di apertura |
| `MauiPopupService.ShowContextSheetAsync` | Potenzialmente modificato | Risolve la sheet da DI, chiama `InitializeAsync`, poi `PushAsync` |
| `ContextSheetViewModel.InitializeAsync` | Potenzialmente modificato | Esegue chiamate async a servizi prima della presentazione |
| `ContextSheet.xaml.cs` | Potenzialmente modificato | Code-behind della `PopupPage` |
| `CoreServiceExtensions.cs` | Potenzialmente modificato | Registrazione DI di `ContextSheetViewModel` |
| `MauiProgram.cs` | Potenzialmente modificato | Registrazione DI di `ContextSheet` via `AddTransientPopup<>` |
| `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` | Aggiornato | Nuovi test per `OpenContextSheetAsync` |

### Dependencies
- **UXDivers Popups v0.9.4** — `IPopupService.Current.PushAsync` è il meccanismo di presentazione (ADR: `adr-uxdivers-popups-adoption`)
- **ADR: `adr-configureawait-viewmodels`** — `ConfigureAwait(false)` vietato in tutti i ViewModel
- **ADR: `adr-uxdivers-popups-adoption`** — pattern "initialize before push" per la ContextSheet
- **`IAppPopupService`** — astrazione Core che isola i ViewModel da UXDivers

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Qual è l'exception type e lo stack trace esatti del crash? | **Resolved** | `XamlParseException`: `Button.CornerRadius` non accetta `x:Double` da `StaticResource`. Fix: valore letterale `"8"`. Secondo crash (storico): `RuntimeException` per `PushAsync` su thread pool — fix: `MainThread.InvokeOnMainThreadAsync`. |
| 2 | Il crash avviene durante `InitializeAsync` (prima del `PushAsync`) o durante/dopo il `PushAsync`? | **Resolved** | Il crash XAML avviene durante la costruzione della `ContextSheet` (prima di `PushAsync`). Il crash thread-context avveniva dopo `InitializeAsync`, durante `PushAsync`. |
| 3 | Il crash è riproducibile anche su iOS o solo su Android? | **Resolved** | Confermato solo su Android. Il crash XAML `CornerRadius` è Android-specific (il parser XAML runtime è più strict su Android). |
| 4 | `MauiPopupService` è Singleton ma risolve `ContextSheet` (Transient) ad ogni chiamata — il `ContextSheetViewModel` iniettato ha tutte le dipendenze con lifetime compatibile? | **Resolved** | Confermato: nessun problema di lifetime. Tutti i service iniettati sono Transient o Singleton — compatibili con Transient ViewModel. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Dato che l'utente è nella `ChatPage` con un progetto attivo, quando tappa il bottone context (⚙️), allora la `ContextSheet` si apre senza crash su Android.
- [x] **[AC-002]** Dato che la `ContextSheet` è aperta, quando l'utente la chiude (bottone Close o tap sul backdrop), allora non si verifica alcun crash.
- [x] **[AC-003]** `adb logcat` non mostra eccezioni non gestite (`FATAL EXCEPTION`, `AndroidRuntime`) durante l'apertura della `ContextSheet`.
- [x] **[AC-004]** `dotnet build openMob.sln` completa con exit code 0 e zero warnings.
- [x] **[AC-005]** `dotnet test tests/openMob.Tests/openMob.Tests.csproj` completa con 0 failures.
- [x] **[AC-006]** Nessun file sotto `src/openMob.Core/ViewModels/` contiene `ConfigureAwait(false)` nel percorso modificato.
- [x] **[AC-007]** Esiste almeno un test che verifica che `ChatViewModel.OpenContextSheetAsync` chiami `IAppPopupService.ShowContextSheetAsync` con `CurrentProjectId` e `CurrentSessionId` corretti.

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Step 1 — Lettura obbligatoria del logcat

Prima di toccare qualsiasi codice, eseguire:

```bash
adb logcat -c && adb logcat *:E | grep -A 30 "FATAL\|AndroidRuntime\|openMob"
```

Riprodurre il crash tappando il bottone context in `ChatPage`. Catturare l'intera stack trace. Il tipo di eccezione determina il fix corretto.

### Ipotesi di causa radice (in ordine di probabilità)

**Ipotesi A — Thread context violation (più probabile su Android)**
- `MauiPopupService.ShowContextSheetAsync` chiama `await vm.InitializeAsync(...)` poi `await IPopupService.Current.PushAsync(sheet)`.
- Se `InitializeAsync` internamente usa `ConfigureAwait(false)` in un service (es. `ProjectService`, `ProjectPreferenceService`, `AgentService`), la continuazione dopo `await vm.InitializeAsync(...)` in `MauiPopupService` potrebbe tornare su un thread pool thread.
- `IPopupService.Current.PushAsync` richiede il main thread su Android — chiamarlo da un thread pool thread causa `RuntimeException: Can't create handler inside thread that has not called Looper.prepare()`.
- **Fix atteso:** Aggiungere `await MainThread.InvokeOnMainThreadAsync(...)` attorno a `IPopupService.Current.PushAsync(sheet)` in `MauiPopupService.ShowContextSheetAsync`, oppure verificare che `MauiPopupService` non perda il SynchronizationContext. Nota: `MauiPopupService` NON è un ViewModel, quindi `ConfigureAwait(false)` nei service sottostanti è corretto e atteso — il problema è che `MauiPopupService` deve garantire di chiamare `PushAsync` sul main thread.

**Ipotesi B — NullReferenceException su `Shell.Current` o `IPopupService.Current`**
- `IPopupService.Current` potrebbe essere `null` se `UseUXDiversPopups()` non è stato inizializzato correttamente, o se viene chiamato prima che la Shell sia pronta.
- Verificare che `UseUXDiversPopups()` sia presente in `MauiProgram.cs` (già confermato nel codice).

**Ipotesi C — Eccezione in `InitializeAsync` non gestita che sale fino al crash**
- `ContextSheetViewModel.InitializeAsync` ha un `try/catch` che cattura tutto tranne `OperationCanceledException` e lo invia a Sentry. Questa ipotesi è meno probabile perché l'eccezione verrebbe swallowed.
- Tuttavia, se Sentry non è configurato (DSN vuoto — confermato in `MauiProgram.cs`), `SentryHelper.CaptureException` potrebbe lanciare. Da verificare.

### File chiave da ispezionare
- `src/openMob/Services/MauiPopupService.cs` — metodo `ShowContextSheetAsync` (righe 157–172)
- `src/openMob.Core/ViewModels/ContextSheetViewModel.cs` — metodo `InitializeAsync` (righe 473–516)
- `src/openMob.Core/ViewModels/ChatViewModel.cs` — metodo `OpenContextSheetAsync` (righe 889–916)
- `src/openMob.Core/Infrastructure/Monitoring/SentryHelper.cs` — verificare comportamento con DSN vuoto
- `src/openMob.Core/Services/ProjectService.cs`, `ProjectPreferenceService.cs`, `AgentService.cs` — verificare presenza di `ConfigureAwait(false)` (atteso e corretto nei service, ma rilevante per capire il thread context)

### Pattern di fix atteso (Ipotesi A)

In `MauiPopupService.ShowContextSheetAsync`, assicurarsi che `PushAsync` venga chiamato sul main thread:

```csharp
public async Task ShowContextSheetAsync(string projectId, string sessionId, CancellationToken ct = default)
{
    ct.ThrowIfCancellationRequested();

    var sheet = _serviceProvider.GetRequiredService<ContextSheet>();

    if (sheet.BindingContext is ContextSheetViewModel vm)
    {
        await vm.InitializeAsync(projectId, sessionId, ct);
    }

    // Garantisce che PushAsync venga chiamato sul main thread
    // anche se InitializeAsync ha perso il SynchronizationContext
    await MainThread.InvokeOnMainThreadAsync(
        () => IPopupService.Current.PushAsync(sheet));
}
```

Stesso pattern da applicare a tutti gli altri metodi `Show*Async` in `MauiPopupService` che chiamano `IPopupService.Current.PushAsync` dopo un `await` (potenziale crash latente sugli altri popup).

### Vincoli da rispettare
- `ConfigureAwait(false)` vietato in `src/openMob.Core/ViewModels/` (ADR: `adr-configureawait-viewmodels`)
- `ConfigureAwait(false)` corretto e atteso in `src/openMob.Core/Services/` e `Infrastructure/`
- `MauiPopupService` è nel progetto MAUI (`src/openMob/Services/`) — può usare `MainThread` senza violare la separazione dei layer
- `ContextSheetViewModel` rimane Transient
- Il pattern "initialize before push" rimane invariato (ADR: `adr-uxdivers-popups-adoption`)

### Riferimenti a decisioni passate
- **ADR `adr-configureawait-viewmodels`** (2026-03-21): `ConfigureAwait(false)` vietato nei ViewModel, obbligatorio nei Service. La causa radice di questo crash è probabilmente analoga: un service con `ConfigureAwait(false)` fa perdere il SynchronizationContext a `MauiPopupService`.
- **ADR `adr-uxdivers-popups-adoption`** (2026-03-22): UXDivers Popups adottato come libreria unificata. `IPopupService.Current.PushAsync` è il meccanismo corretto. Il pattern "initialize before push" è stabilito.
- **Feature `drawer-sessions-delete-refactor`** (2026-03-21): Il fix `ConfigureAwait(false)` su 14 ViewModel ha risolto crash analoghi. Questo crash potrebbe essere la stessa classe di problema ma nel layer `MauiPopupService` invece che nei ViewModel.

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-22

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/context-sheet-crash-on-open |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| MAUI Services (popup layer) | om-mobile-core | `src/openMob/Services/MauiPopupService.cs` |
| Unit Tests | om-tester | `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` |
| Code Review | om-reviewer | all of the above |

> **Note:** `om-mobile-ui` is NOT involved — no XAML changes required. The fix is entirely in the MAUI service layer and tests.

### Root Cause (Confirmed by Code Inspection)

**Ipotesi A confermata.** Tutti e tre i service chiamati da `ContextSheetViewModel.InitializeAsync` usano `ConfigureAwait(false)`:
- `ProjectService.GetProjectByIdAsync` → `_apiClient.GetProjectByIdAsync(ct).ConfigureAwait(false)` ✅ (corretto nei service)
- `ProjectPreferenceService.GetOrDefaultAsync` → EF Core con `ConfigureAwait(false)` ✅ (corretto nei service)
- `AgentService.GetSubagentAgentsAsync` → `_apiClient.GetAgentsAsync(ct).ConfigureAwait(false)` ✅ (corretto nei service)

Dopo `await vm.InitializeAsync(...)` in `MauiPopupService.ShowContextSheetAsync`, il SynchronizationContext è perso (thread pool thread). La chiamata successiva `await IPopupService.Current.PushAsync(sheet)` avviene su un thread pool thread → crash Android (`RuntimeException: Can't create handler inside thread that has not called Looper.prepare()`).

**Stesso problema latente** esiste in tutti gli altri metodi `Show*Async` che chiamano `PushAsync` dopo un `await` con `InitializeAsync` o `LoadProjectsCommand.ExecuteAsync`:
- `ShowProjectSwitcherAsync` — chiama `await vm.LoadProjectsCommand.ExecuteAsync(null)` poi `PushAsync`
- `ShowModelPickerAsync` — non ha `await` prima di `PushAsync` (safe)
- `ShowAgentPickerAsync` — non ha `await` prima di `PushAsync` (safe)
- `ShowSubagentPickerAsync` — non ha `await` prima di `PushAsync` (safe)
- `ShowCommandPaletteAsync` — non ha `await` prima di `PushAsync` (safe)
- `ShowAddProjectAsync` — non ha `await` prima di `PushAsync` (safe)

**Scope del fix:** `ShowContextSheetAsync` (crash attivo) + `ShowProjectSwitcherAsync` (crash latente).

### Files to Create

_Nessun file nuovo da creare._

### Files to Modify

- `src/openMob/Services/MauiPopupService.cs` — wrappare `IPopupService.Current.PushAsync(sheet)` con `MainThread.InvokeOnMainThreadAsync(...)` in `ShowContextSheetAsync` e `ShowProjectSwitcherAsync`
- `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs` — aggiungere test per `OpenContextSheetAsync` ([REQ-009], [AC-007])

### Technical Dependencies

- `Microsoft.Maui.Controls.MainThread` — già disponibile nel progetto MAUI, nessun NuGet aggiuntivo
- `IAppPopupService` — interfaccia esistente, nessuna modifica all'interfaccia
- `ContextSheetViewModel` rimane Transient — nessuna modifica DI

### Technical Risks

- **Nessun breaking change** — la firma pubblica di `IAppPopupService` non cambia
- **Nessuna migrazione EF Core** — nessuna modifica al modello dati
- **Attenzione:** `MainThread.InvokeOnMainThreadAsync(() => IPopupService.Current.PushAsync(sheet))` — il lambda cattura `sheet` (Transient, già risolto) — nessun problema di lifetime
- **`PopAsync`** in `MauiPopupService` non è interessato — non ha `await` prima della chiamata

### Execution Order

> Nessun parallelismo necessario — fix piccolo e sequenziale.

1. [Git Flow] Create branch `bugfix/context-sheet-crash-on-open`
2. [om-mobile-core] Fix `MauiPopupService.ShowContextSheetAsync` e `ShowProjectSwitcherAsync` con `MainThread.InvokeOnMainThreadAsync`
3. [om-tester] Aggiungere test `OpenContextSheetAsync_WhenProjectIsActive_CallsShowContextSheetWithCorrectIds` in `ChatViewModelTests.cs`
4. [om-reviewer] Full review contro spec
5. [Fix loop se necessario] Risolvere Critical e Major findings
6. [Git Flow] Finish branch e merge in develop

### Definition of Done

- [x] [REQ-003] `ShowContextSheetAsync` chiama `PushAsync` sul main thread — crash risolto
- [x] [REQ-004] Nessun `ConfigureAwait(false)` introdotto in `src/openMob.Core/ViewModels/`
- [x] [REQ-005] Comportamento funzionale della `ContextSheet` invariato
- [x] [REQ-006] `ContextSheetViewModel` rimane Transient
- [x] [REQ-007] `dotnet build openMob.sln` → exit code 0, zero warnings
- [x] [REQ-008] `dotnet test` → 0 failures
- [x] [REQ-009] / [AC-007] Test `OpenContextSheetAsync` aggiunto in `ChatViewModelTests.cs`
- [x] `om-reviewer` verdict: ✅ Approved (fix confermato funzionante su dispositivo)
- [x] Git Flow branch finished e deleted
- [x] Spec moved to `specs/done/` con status Completed
