# iOS Keyboard Avoidance — Chat Input Modal

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-24                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Su iOS, quando l'utente tocca il campo di testo nella modale custom della pagina chat, la tastiera software si sovrappone completamente all'area di input, rendendola inaccessibile. Questo fix introduce il corretto comportamento di keyboard avoidance seguendo le best practice iOS per .NET MAUI: l'area di input trasla verso l'alto agganciandosi al bordo superiore della tastiera, e torna alla posizione originale alla chiusura della tastiera.

---

## Scope

### In Scope
- Fix del comportamento della tastiera iOS sulla modale custom di inserimento testo nella pagina chat
- L'area di input (campo testo + controlli associati) deve rimanere visibile e accessibile sopra la tastiera quando questa appare
- Gestione del ciclo di vita della tastiera: apertura, chiusura, variazione dinamica di altezza (es. passaggio a emoji keyboard)
- Applicazione delle best practice iOS per keyboard avoidance in .NET MAUI
- Il fix deve essere attivo **solo su iOS** (platform-specific), senza impatti su Android

### Out of Scope
- Android (non presenta il problema)
- Altre pagine o modali dell'app al di fuori della modale chat
- Redesign del layout generale della modale chat
- Gestione dello scroll della lista messaggi durante l'apertura della tastiera
- Qualsiasi altro elemento della modale non direttamente legato all'area di input

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Quando la tastiera iOS appare (evento `UIKeyboardWillShowNotification`), l'area di input della modale deve traslare verso l'alto in modo che il suo bordo inferiore rimanga visibile sopra il bordo superiore della tastiera.
2. **[REQ-002]** Quando la tastiera iOS si chiude (evento `UIKeyboardWillHideNotification`), la modale deve tornare alla posizione originale.
3. **[REQ-003]** Il comportamento deve gestire variazioni dinamiche dell'altezza della tastiera (es. passaggio da tastiera standard a emoji keyboard), reagendo all'evento `UIKeyboardWillChangeFrameNotification`.
4. **[REQ-004]** Prima dell'implementazione, l'agente tecnico deve verificare in codebase il tipo esatto di implementazione della modale (custom overlay XAML, `PushModalAsync`, `CommunityToolkit.Maui Popup`, ecc.) e adottare l'approccio tecnico più appropriato.
5. **[REQ-005]** L'animazione di traslazione deve essere fluida, con durata e curva di animazione sincronizzate con quelle native della tastiera iOS (ricavate dal `UIKeyboardAnimationDurationUserInfoKey` e `UIKeyboardAnimationCurveUserInfoKey`).
6. **[REQ-006]** La soluzione non deve introdurre regressioni visive in modalità chiara o scura, né alterare il layout della modale quando la tastiera è chiusa.
7. **[REQ-007]** La logica platform-specific iOS deve essere isolata (es. in un `IPlatformKeyboardService` o tramite `#if IOS` / handler MAUI) per non inquinare il codice condiviso.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| Chat page — modale input (XAML) | Modifica al layout: possibile aggiunta di `Padding` o `Margin` dinamici, o wrapping in `ScrollView` | Da verificare in base al tipo di modale |
| iOS platform code | Aggiunta di observer per notifiche tastiera (`NSNotificationCenter`) | Solo iOS |
| `MauiProgram.cs` / DI | Possibile registrazione di un servizio `IPlatformKeyboardService` | Se si adotta l'astrazione via interfaccia |
| ViewModel della modale chat | Nessun impatto previsto — il fix è puramente UI/platform | Da confermare in Technical Analysis |

### Dependencies
- .NET MAUI iOS platform handlers
- `UIKit.UIKeyboard` notification system (iOS SDK)
- Eventuale uso di `CommunityToolkit.Maui` se la modale è un `Popup` (da verificare)

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Tipo esatto di implementazione della modale chat (custom overlay, `PushModalAsync`, `CommunityToolkit.Maui Popup`, altro) | Open | Da risolvere in Technical Analysis tramite ispezione del codebase |
| 2 | La modale è presentata tramite Shell navigation o navigazione standalone? | Open | Da risolvere in Technical Analysis |
| 3 | È già presente nel progetto un meccanismo di keyboard avoidance per altri contesti? | Open | Da verificare in codebase — se esiste, riutilizzarlo |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che l'utente è sulla modale chat e tocca il campo di testo, quando la tastiera iOS appare, allora il campo di input è completamente visibile sopra la tastiera senza essere coperto. *(REQ-001)*
- [ ] **[AC-002]** Dato che la tastiera è aperta, quando l'utente la chiude (swipe down o tap fuori dal campo), allora la modale torna alla posizione originale senza artefatti visivi o layout residui. *(REQ-002)*
- [ ] **[AC-003]** Dato che la tastiera è aperta con layout standard, quando l'utente passa alla emoji keyboard (altezza diversa), allora il layout si adatta correttamente alla nuova altezza senza sovrapposizioni. *(REQ-003)*
- [ ] **[AC-004]** L'animazione di traslazione dell'area di input è fluida e visivamente coerente con il comportamento nativo iOS. *(REQ-005)*
- [ ] **[AC-005]** Su Android, il comportamento della modale chat rimane invariato rispetto alla versione precedente al fix — nessuna regressione. *(REQ-007)*
- [ ] **[AC-006]** Il layout della modale in modalità chiara e scura è visivamente corretto sia con tastiera aperta che chiusa. *(REQ-006)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate
- **Identificare il tipo di modale:** Ispezionare il codice della chat page per determinare se la modale è un `ContentPage` presentato con `Navigation.PushModalAsync`, un overlay XAML custom (es. `Grid` con `IsVisible`), o un `Popup` di `CommunityToolkit.Maui`. Questo è il prerequisito per scegliere l'approccio corretto.
- **Verificare se esiste già un keyboard avoidance mechanism** nel progetto (cercare `UIKeyboard`, `KeyboardHelper`, `IKeyboardService`, `KeyboardWillShow` nel codebase).
- **Verificare la presenza di `KeyboardAutoManagerScroll`** di CommunityToolkit.Maui, che potrebbe già gestire alcuni scenari automaticamente se abilitato.

### Suggested implementation approach (if known)
Le best practice per .NET MAUI iOS keyboard avoidance prevedono tipicamente uno dei seguenti approcci, in ordine di preferenza:

1. **Se la modale è una `ContentPage` con `PushModalAsync`:** Usare `KeyboardAutoManagerScroll` di CommunityToolkit.Maui (se già dipendenza del progetto) oppure aggiungere un `NSNotificationCenter` observer in un iOS platform handler che aggiusta il `Padding.Bottom` della pagina in modo animato.

2. **Se la modale è un overlay XAML custom (es. `Grid` sovrapposto):** Iniettare un servizio `IPlatformKeyboardService` con implementazione iOS che espone un `Observable` o evento con l'altezza corrente della tastiera. Il ViewModel (o il code-behind della view) aggiorna un `BottomPadding` binding sull'area di input.

3. **Se la modale è un `CommunityToolkit.Maui Popup`:** Verificare se il Popup gestisce nativamente il keyboard avoidance; in caso contrario, applicare l'approccio del punto 2.

### Constraints to respect
- Il fix deve essere **platform-specific iOS only** — usare `#if IOS` o handler condizionali per non impattare Android.
- Rispettare la separazione dei layer: la logica di rilevamento tastiera deve stare in `openMob` (MAUI project, platform glue), non in `openMob.Core`.
- Se si introduce un'interfaccia `IPlatformKeyboardService`, registrarla nel DI in `MauiProgram.cs` con implementazione iOS e un no-op per Android.
- Nessuna dipendenza da librerie di terze parti non già presenti nel progetto, salvo `CommunityToolkit.Maui` se già referenziata.
- Animazione sincronizzata con la tastiera: usare `UIKeyboardAnimationDurationUserInfoKey` e `UIKeyboardAnimationCurveUserInfoKey` dalle notifiche iOS per ottenere durata e curva native.

### Related files or modules (if known)
- `src/openMob/` — cercare file relativi alla chat page e alla modale di input
- `src/openMob/Platforms/iOS/` — posizione naturale per il codice platform-specific iOS
- `src/openMob.Core/ViewModels/` — ViewModel della chat page (per eventuale binding del padding)
- `MauiProgram.cs` — per registrazione DI di eventuali nuovi servizi
