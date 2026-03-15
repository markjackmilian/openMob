# App Navigation Structure & Page Flow

## Metadata
| Field       | Value                                          |
|-------------|------------------------------------------------|
| Date        | 2026-03-15                                     |
| Status      | **Completed**                                  |
| Version     | 1.1                                            |
| Completed   | 2026-03-16                                     |
| Branch      | feature/app-navigation-structure (merged)      |
| Merged into | develop                                        |

---

## Executive Summary

Define the complete navigation structure and page hierarchy for openMob's main flow:
**Splash → Onboarding → Chat (schermata principale)**. La Home viene eliminata in favore di un approccio chat-first: la Chat è la schermata radice dell'app. Il contesto (progetto, agente, modello) è sempre visibile nell'header della Chat. La navigazione verso sessioni, progetti e impostazioni avviene tramite Shell Flyout (hamburger) e bottom sheet contestuali.

Questa spec definisce **struttura e componenti** di ogni pagina, le **transizioni di navigazione**, e i **flussi utente** principali. Lo stile visivo (colori, tipografia, spacing) è già definito in `spec-chat-ui-design-guidelines` e non viene ridefinito qui.

---

## Scope

### In Scope
- Struttura di navigazione globale (Shell Flyout + route stack)
- Splash / Bootstrap: logica di routing iniziale
- Onboarding: 5 step, flusso lineare
- Chat: schermata principale, header contestuale, flyout aggiornato
- Projects: pagina di gestione progetti (lista + dettaglio)
- Sessions: gestione sessioni nel flyout (no pagina separata nel flusso principale)
- Bottom sheet: Project Switcher, Agent/Model Picker
- Flussi di navigazione: tutti i percorsi utente dal bootstrap alla chat

### Out of Scope
- Stile visivo dettagliato (già definito in `spec-chat-ui-design-guidelines`)
- Implementazione API / backend
- Settings (spec separata)
- Commands / Shortcuts (spec separata)
- Share (spec separata)
- MCP Plugins (spec separata)
- Notifiche push

---

## Decisioni di Design

### D-001: Nessuna Home — Chat come schermata radice
La Home viene eliminata. La Chat è la schermata principale dell'app. Motivazione: il flusso più frequente è "apri app → chatta"; una Home aggiunge un tap inutile. Il contesto (progetto, agente, modello) è esposto nell'header della Chat stessa.

### D-002: Sessions nel Flyout, non in una pagina separata
Le sessioni del progetto corrente sono accessibili direttamente dal flyout. Non esiste una pagina "Sessions List" nel flusso principale. Il flyout mostra le sessioni del progetto selezionato, ordinate per data decrescente.

### D-003: Project Switcher come Bottom Sheet
Il cambio progetto avviene tramite un bottom sheet modale, accessibile dall'header della Chat (tap sul nome del progetto). Non è una pagina separata nel navigation stack.

### D-004: Agent/Model Picker come Bottom Sheet
La selezione di agente e modello avviene tramite bottom sheet modale, accessibile dall'header della Chat.

### D-005: Onboarding Provider Setup è opzionale
L'utente può saltare la configurazione del provider AI durante l'onboarding e configurarla in seguito dalle Settings.

### D-006: UXDivers Popups come libreria unica per alert e dialog
Tutti gli alert di conferma, dialog informativi e action sheet dell'app usano la libreria **UXDivers.Popups.Maui** (`dotnet add package UXDivers.Popups.Maui`). Non si usa `DisplayAlert`, `DisplayActionSheet` o `DisplayPromptAsync` nativi di MAUI. Motivazione: UI coerente, animazioni, supporto dark theme, MVVM-ready con `IPopupService`.

Mapping popup type → caso d'uso:
| Caso d'uso | Tipo UXDivers |
|-----------|--------------|
| Conferma eliminazione (sessione, progetto) | `SimpleActionPopup` (titolo + testo + Annulla/Elimina) |
| Rinomina sessione (input testo) | `FormPopup` (campo testo + Salva/Annulla) |
| Action sheet More Menu (lista azioni) | `OptionSheetPopup` |
| Notifica di successo / errore breve | `Toast` |
| Errore con dettaglio (es. connessione fallita) | `IconTextPopup` |
| Bottom sheet picker (Project, Agent, Model) | `PopupPage` custom (estende `PopupPage`, layout verticale con handle bar) |

---

## Struttura di Navigazione Globale

```
AppShell (Shell Flyout)
│
├── [ROUTE: splash]        SplashPage          (non nel flyout)
├── [ROUTE: onboarding]    OnboardingPage       (non nel flyout)
│
├── [FLYOUT ITEM] Chat     ChatPage             ← schermata radice
│
├── [FLYOUT ITEM] Projects ProjectsPage
│   └── [ROUTE: project-detail/{id}]  ProjectDetailPage
│
└── [FLYOUT FOOTER] Settings  SettingsPage      (stub, spec separata)

Bottom Sheets / Popups (modali via UXDivers.Popups.Maui, non nel navigation stack):
├── ProjectSwitcherSheet     (PopupPage custom)
├── AgentPickerSheet         (PopupPage custom)
├── ModelPickerSheet         (PopupPage custom)
├── AddProjectSheet          (PopupPage custom)
├── RenameSessionPopup       (FormPopup)
├── DeleteConfirmPopup       (SimpleActionPopup)
└── MoreMenuSheet            (OptionSheetPopup)
```

---

## Pagine e Componenti

---

### 1. SplashPage

**Scopo:** Bootstrap dell'app. Determina il percorso iniziale.

**Componenti:**
- Logo centrato (icona app + nome "openMob")
- ActivityIndicator sotto il logo

**Logica di routing (eseguita in background):**

| Condizione | Destinazione |
|-----------|-------------|
| Prima apertura (nessun server configurato) | → OnboardingPage |
| Server configurato ma non raggiungibile | → ChatPage (con banner "Server offline") |
| Server configurato e raggiungibile, nessuna sessione precedente | → ChatPage (nuova sessione vuota) |
| Server configurato e raggiungibile, sessione precedente esistente | → ChatPage (ultima sessione) |

**Note:**
- Timeout bootstrap: 5 secondi. Se il server non risponde entro 5s → ChatPage con stato offline.
- La SplashPage non è raggiungibile tramite navigazione back.

---

### 2. OnboardingPage

**Scopo:** Configurazione iniziale guidata. Flusso lineare a 5 step con indicatore di progresso.

**Componenti globali (presenti in tutti gli step):**
- ProgressBar orizzontale in cima (step corrente / totale)
- Pulsante "Avanti" / "Inizia" in fondo (primario)
- Pulsante "Indietro" (secondario, nascosto al primo step)
- Pulsante "Salta" (link testuale, visibile solo sugli step opzionali)

---

#### Step 1 — Welcome
**Componenti:**
- Illustrazione / icona grande centrata
- Titolo: "Benvenuto in openMob"
- Sottotitolo: breve descrizione dell'app (2-3 righe)
- Pulsante "Inizia" (avanza allo step 2)

---

#### Step 2 — Connect Server
**Scopo:** Configurare l'URL e il token del server opencode self-hosted.

**Componenti:**
- Titolo: "Connetti il tuo server"
- Sottotitolo: istruzioni brevi (dove trovare URL e token)
- Campo: URL del server (Entry, placeholder "https://your-server.example.com", keyboard URL)
- Campo: Token di accesso (Entry, IsPassword=true, placeholder "sk-...")
- Pulsante "Testa connessione" (secondario) → mostra stato: ✓ Connesso / ✗ Errore
- Stato connessione: label con icona (verde/rosso)
- Pulsante "Avanti" abilitato solo dopo connessione verificata con successo

**Validazione:**
- URL: formato URL valido, non vuoto
- Token: non vuoto
- "Avanti" disabilitato finché il test non ha esito positivo

---

#### Step 3 — Provider Setup *(opzionale)*
**Scopo:** Configurare un provider AI (es. Anthropic, OpenAI). Saltabile.

**Componenti:**
- Titolo: "Configura un provider AI"
- Sottotitolo: "Puoi farlo anche in seguito dalle Impostazioni"
- Lista provider disponibili (CollectionView, 1 colonna): ogni item ha logo + nome + campo API key
- Solo un provider espandibile alla volta (accordion)
- Campo API key per il provider selezionato (Entry, IsPassword=true)
- Pulsante "Salta" (link testuale, top-right o sotto il pulsante Avanti)
- Pulsante "Avanti" sempre abilitato (step opzionale)

---

#### Step 4 — Permissions Intro
**Scopo:** Informare l'utente sulle permissioni che l'app potrebbe richiedere.

**Componenti:**
- Titolo: "Permissioni"
- Lista permissioni con icona + nome + descrizione breve:
  - Notifiche: "Per avvisarti quando il bot risponde"
  - (altre permissioni rilevanti)
- Pulsante "Continua" (avanza allo step 5, richiede le permissioni native al tap)

---

#### Step 5 — Completion
**Componenti:**
- Icona di successo (checkmark animato)
- Titolo: "Tutto pronto!"
- Sottotitolo: "Inizia a chattare con il tuo agente AI"
- Pulsante "Inizia a chattare" → naviga a ChatPage (nuova sessione vuota)

---

### 3. ChatPage *(schermata radice)*

**Scopo:** Schermata principale dell'app. Mostra la conversazione attiva con il bot.

> Struttura layout e componenti della chat (message list, input bar, bubbles, empty state, suggestion chips) già definiti in `spec-chat-ui-design-guidelines`. Questa spec definisce l'**header contestuale** e le **interazioni di navigazione** aggiuntive.

#### 3.1 Header Bar (esteso rispetto alle guidelines)

**Layout:** `[Hamburger] [ProjectName · SessionName] [NewChat] [MoreMenu]`

| Elemento | Posizione | Azione |
|---------|-----------|--------|
| Hamburger (☰) | Left | Apre Shell Flyout |
| ProjectName (tappabile) | Center-left | Apre ProjectSwitcherSheet (bottom sheet) |
| "·" separatore | Center | — |
| SessionName (tappabile, ellipsis) | Center-right | Nessuna azione (solo informativo, rinominabile dal flyout) |
| New Chat (✏️) | Right | Crea nuova sessione con impostazioni correnti, naviga a ChatPage vuota |
| More Menu (⋮) | Right | Apre action sheet: Rename Session / Change Agent / Change Model / Fork Session / Archive / Delete |

**Note:**
- Se nessun progetto è selezionato: ProjectName mostra "Nessun progetto" in ColorOnSurfaceTertiary.
- Se nessuna sessione attiva: SessionName nascosto.

#### 3.2 More Menu (OptionSheetPopup)
Implementato con `OptionSheetPopup` di UXDivers. Voci:
- **Rinomina sessione** → apre `FormPopup` con campo testo pre-compilato con nome corrente → salva nuovo nome
- **Cambia agente** → apre AgentPickerSheet (PopupPage custom)
- **Cambia modello** → apre ModelPickerSheet (PopupPage custom)
- **Fork sessione** → crea copia della sessione corrente fino all'ultimo messaggio, naviga alla nuova sessione
- **Archivia** → `SimpleActionPopup` di conferma → archivia sessione, naviga a nuova sessione vuota
- **Elimina** → `SimpleActionPopup` di conferma (azione distruttiva, pulsante rosso) → elimina sessione, naviga a nuova sessione vuota

#### 3.3 Banner di stato (condizionale, sotto l'header)
| Stato | Banner |
|-------|--------|
| Server offline | "Server non raggiungibile — modalità offline" (arancione) |
| Nessun provider configurato | "Nessun provider AI configurato — [Configura]" (giallo, link a Settings) |
| Errore tool execution | "Errore nell'esecuzione del tool: [nome tool]" (rosso, dismissibile) |
| Context overflow | "Contesto quasi pieno — considera di fare fork della sessione" (giallo) |

---

### 4. Shell Flyout (aggiornato)

**Scopo:** Navigazione principale + gestione sessioni del progetto corrente.

> Struttura base (header, footer, animazione, swipe-to-delete) già definita in `spec-chat-ui-design-guidelines`. Questa spec aggiunge la sezione Projects e ridefinisce il contenuto delle sessioni.

#### 4.1 Flyout Header
- Logo / nome app a sinistra
- Pulsante "Nuova chat" (✏️) a destra → crea nuova sessione, chiude flyout, naviga a ChatPage vuota

#### 4.2 Flyout Body — Sezione Sessioni
- Titolo sezione: nome del progetto corrente (es. "MyProject") in FontSizeCaption1, ColorOnSurfaceTertiary, uppercase
- Lista sessioni del progetto corrente (CollectionView, ordine decrescente per data)
- Ogni item: titolo sessione (ellipsis) + timestamp relativo
- Item selezionato: evidenziato con ColorPrimaryContainer
- Swipe-to-delete: elimina sessione con `SimpleActionPopup` di conferma (UXDivers)
- Se nessun progetto selezionato: placeholder "Seleziona un progetto per vedere le sessioni"

#### 4.3 Flyout Footer
- Voce "Projects" (con icona cartella) → naviga a ProjectsPage
- Voce "Settings" (con icona ingranaggio) → naviga a SettingsPage (stub)
- Versione app in FontSizeCaption2, ColorOnSurfaceTertiary

---

### 5. ProjectsPage

**Scopo:** Gestione dei progetti. Lista di tutti i progetti con possibilità di aggiungere, selezionare, modificare, eliminare.

**Componenti:**
- Header: titolo "Progetti" + pulsante "+" (top-right) → apre AddProjectSheet
- CollectionView: lista progetti
  - Ogni item: nome progetto + path/descrizione breve + badge "Attivo" se è il progetto corrente
  - Tap su item → naviga a ProjectDetailPage
  - Swipe-to-delete: elimina progetto con `SimpleActionPopup` di conferma (disabilitato se è il progetto corrente)
  - Long-press su item → `OptionSheetPopup`: Apri / Imposta come attivo / Rinomina / Elimina
- EmptyStateView: "Nessun progetto — aggiungi il tuo primo progetto" + pulsante "Aggiungi progetto"

---

### 6. ProjectDetailPage

**Scopo:** Dettaglio e configurazione di un singolo progetto.

**Navigazione:** `ProjectsPage → ProjectDetailPage` (push)

**Componenti:**
- Header: nome progetto (editabile inline, tap per rinominare) + pulsante "⋮" (more menu)
- More Menu (`OptionSheetPopup`): Imposta come attivo / Rinomina / Elimina
- Sezioni (ScrollView verticale):

  **Sezione: Informazioni**
  - Path del progetto (label, non editabile)
  - Descrizione (Entry, opzionale)
  - Pulsante "Imposta come progetto attivo" (primario, visibile solo se non è già attivo)

  **Sezione: Sessioni**
  - Lista compatta delle ultime 5 sessioni (CollectionView, non scrollabile inline)
  - Pulsante "Vedi tutte" → non necessario (le sessioni sono nel flyout)
  - Pulsante "Nuova sessione" → crea sessione in questo progetto, naviga a ChatPage

  **Sezione: Agente & Modello predefiniti**
  - Agente predefinito: label + valore + chevron → tap apre AgentPickerSheet
  - Modello predefinito: label + valore + chevron → tap apre ModelPickerSheet

  **Sezione: Impostazioni progetto** *(stub, spec separata)*
  - MCP Plugins: label + chevron (disabilitato, "Prossimamente")
  - Rules / Instructions: label + chevron (disabilitato, "Prossimamente")

---

### 7. AddProjectSheet (PopupPage custom — UXDivers)

**Scopo:** Aggiungere un nuovo progetto.

**Implementazione:** `PopupPage` custom (estende `UXDivers.Popups.Maui.PopupPage`), ancorata in basso, `CloseWhenBackgroundIsClicked="True"`.

**Componenti:**
- Handle bar in cima
- Titolo: "Nuovo progetto"
- Campo: Nome progetto (Entry, autofocus)
- Campo: Path del progetto (Entry, placeholder "/path/to/project", con pulsante "Sfoglia" se disponibile)
- Pulsante "Aggiungi" (primario, disabilitato se nome o path vuoti)
- Pulsante "Annulla" (secondario) → chiude popup via `IPopupService.Current.PopAsync()`

---

### 8. ProjectSwitcherSheet (PopupPage custom — UXDivers)

**Scopo:** Cambiare rapidamente il progetto attivo senza uscire dalla Chat.

**Trigger:** Tap su ProjectName nell'header della ChatPage.

**Implementazione:** `PopupPage` custom, ancorata in basso, `CloseWhenBackgroundIsClicked="True"`, `AppearingAnimation=SlideFromBottom`.

**Comportamento al cambio progetto:** selezionare un progetto diverso da quello attivo riprende l'**ultima sessione** di quel progetto (se esiste), altrimenti crea una nuova sessione vuota.

**Componenti:**
- Handle bar in cima
- Titolo: "Cambia progetto"
- CollectionView: lista progetti
  - Ogni item: nome + path breve + checkmark se attivo
  - Tap su item → imposta come progetto attivo, chiude popup, aggiorna header ChatPage
- Pulsante "Gestisci progetti" (link testuale in fondo) → chiude popup, naviga a ProjectsPage

---

### 9. AgentPickerSheet (PopupPage custom — UXDivers)

**Scopo:** Selezionare l'agente AI per la sessione corrente.

**Trigger:** More Menu ChatPage → "Cambia agente" / ProjectDetailPage → Agente predefinito.

**Implementazione:** `PopupPage` custom, ancorata in basso, `CloseWhenBackgroundIsClicked="True"`, `AppearingAnimation=SlideFromBottom`.

**Componenti:**
- Handle bar in cima
- Titolo: "Seleziona agente"
- CollectionView: lista agenti disponibili
  - Ogni item: nome agente + descrizione breve + checkmark se selezionato
  - Tap su item → seleziona agente, chiude popup
- EmptyStateView: "Nessun agente disponibile" (se lista vuota)

---

### 10. ModelPickerSheet (PopupPage custom — UXDivers)

**Scopo:** Selezionare il modello AI per la sessione corrente.

**Trigger:** More Menu ChatPage → "Cambia modello" / ProjectDetailPage → Modello predefinito.

**Implementazione:** `PopupPage` custom, ancorata in basso, `CloseWhenBackgroundIsClicked="True"`, `AppearingAnimation=SlideFromBottom`.

**Componenti:**
- Handle bar in cima
- Titolo: "Seleziona modello"
- Sezioni per provider (se più provider configurati): header sezione con nome provider
- CollectionView: lista modelli
  - Ogni item: nome modello + provider + indicatore contesto (es. "200k tokens") + checkmark se selezionato
  - Tap su item → seleziona modello, chiude popup
- EmptyStateView: "Nessun provider configurato — [Configura]" (link a Settings)

---

## Flussi di Navigazione

### Flusso 1 — Prima apertura
```
SplashPage (bootstrap)
  └─ nessun server configurato
       └─ OnboardingPage (Step 1 Welcome)
            └─ Step 2 Connect Server
                 └─ Step 3 Provider Setup [saltabile]
                      └─ Step 4 Permissions
                           └─ Step 5 Completion
                                └─ ChatPage (nuova sessione vuota)
```

### Flusso 2 — Apertura successiva (app già configurata)
```
SplashPage (bootstrap ~1-2s)
  ├─ sessione precedente esistente → ChatPage (ultima sessione)
  └─ nessuna sessione → ChatPage (nuova sessione vuota)
```

### Flusso 3 — Nuova sessione dalla Chat
```
ChatPage
  └─ tap "✏️ New Chat" (header) → ChatPage (nuova sessione, stesse impostazioni)
  └─ tap "✏️ Nuova chat" (flyout header) → ChatPage (nuova sessione, stesse impostazioni)
```

### Flusso 4 — Cambio sessione
```
ChatPage
  └─ tap hamburger → Flyout aperto
       └─ tap sessione nella lista → ChatPage (sessione selezionata)
```

### Flusso 5 — Gestione progetti
```
ChatPage
  └─ tap hamburger → Flyout aperto
       └─ tap "Projects" (footer) → ProjectsPage
            └─ tap progetto → ProjectDetailPage
            └─ tap "+" → AddProjectSheet
                 └─ conferma → ProjectsPage (lista aggiornata)
```

### Flusso 6 — Cambio progetto rapido dalla Chat
```
ChatPage
  └─ tap ProjectName (header) → ProjectSwitcherSheet (popup)
       └─ tap progetto (diverso dall'attivo)
            ├─ ultima sessione esiste → ChatPage (ultima sessione del nuovo progetto)
            └─ nessuna sessione → ChatPage (nuova sessione vuota nel nuovo progetto)
       └─ tap "Gestisci progetti" → ProjectsPage
```

### Flusso 7 — Cambio agente / modello
```
ChatPage
  └─ tap "⋮" (header) → Action Sheet
       └─ "Cambia agente" → AgentPickerSheet → ChatPage (agente aggiornato)
       └─ "Cambia modello" → ModelPickerSheet → ChatPage (modello aggiornato)
```

### Flusso 8 — Fork sessione
```
ChatPage (sessione A)
  └─ tap "⋮" → "Fork sessione"
       └─ ChatPage (nuova sessione B, copia di A fino al messaggio corrente)
```

---

## Requisiti Funzionali

### Bootstrap & Routing

- **[REQ-001]** All'avvio, SplashPage esegue il bootstrap e determina la destinazione iniziale entro 5 secondi.
- **[REQ-002]** Se nessun server è configurato, l'app naviga a OnboardingPage.
- **[REQ-003]** Se il server è configurato ma non raggiungibile entro 5s, l'app naviga a ChatPage con banner "Server offline".
- **[REQ-004]** Se il server è raggiungibile e esiste una sessione precedente, l'app naviga a ChatPage con l'ultima sessione caricata.
- **[REQ-005]** Se il server è raggiungibile ma non esiste sessione precedente, l'app naviga a ChatPage con nuova sessione vuota.
- **[REQ-006]** SplashPage non è raggiungibile tramite navigazione back.

### Onboarding

- **[REQ-007]** L'onboarding è composto da 5 step lineari con ProgressBar.
- **[REQ-008]** Il pulsante "Avanti" allo Step 2 è abilitato solo dopo un test di connessione riuscito.
- **[REQ-009]** Lo Step 3 (Provider Setup) è saltabile tramite pulsante "Salta".
- **[REQ-010]** Al completamento dell'onboarding, l'app naviga a ChatPage con nuova sessione vuota.
- **[REQ-011]** L'onboarding non è ripresentato alle aperture successive se il server è già configurato.

### Chat Header

- **[REQ-012]** L'header della ChatPage mostra: hamburger | ProjectName (tappabile) | "·" | SessionName | NewChat | MoreMenu.
- **[REQ-013]** Tap su ProjectName apre ProjectSwitcherSheet.
- **[REQ-014]** Tap su NewChat crea una nuova sessione con le impostazioni correnti (progetto, agente, modello).
- **[REQ-015]** Il MoreMenu espone: Rinomina sessione, Cambia agente, Cambia modello, Fork sessione, Archivia, Elimina.
- **[REQ-016]** I banner di stato (offline, no provider, errore tool, context overflow) sono mostrati sotto l'header in modo condizionale.

### Flyout

- **[REQ-017]** Il flyout mostra le sessioni del progetto corrente, ordinate per data decrescente.
- **[REQ-018]** Il titolo della sezione sessioni nel flyout è il nome del progetto corrente.
- **[REQ-019]** Se nessun progetto è selezionato, il flyout mostra un placeholder testuale al posto della lista sessioni.
- **[REQ-020]** Il footer del flyout contiene le voci "Projects" e "Settings".
- **[REQ-021]** Swipe-to-delete su una sessione nel flyout mostra un alert di conferma prima di eliminare.

### Projects

- **[REQ-022]** ProjectsPage mostra tutti i progetti con badge "Attivo" sul progetto corrente.
- **[REQ-023]** Tap su un progetto in ProjectsPage naviga a ProjectDetailPage.
- **[REQ-024]** ProjectDetailPage permette di impostare il progetto come attivo.
- **[REQ-025]** ProjectDetailPage mostra le ultime sessioni del progetto e un pulsante "Nuova sessione".
- **[REQ-026]** ProjectDetailPage permette di configurare agente e modello predefiniti tramite bottom sheet.
- **[REQ-027]** AddProjectSheet richiede nome e path; il pulsante "Aggiungi" è disabilitato se uno dei due è vuoto.

### Bottom Sheets / Popup

- **[REQ-028]** ProjectSwitcherSheet mostra tutti i progetti con checkmark sul progetto attivo.
- **[REQ-029]** Selezionare un progetto diverso dall'attivo in ProjectSwitcherSheet riprende l'ultima sessione di quel progetto; se non esiste, crea una nuova sessione vuota.
- **[REQ-030]** AgentPickerSheet mostra gli agenti disponibili con checkmark sull'agente corrente.
- **[REQ-031]** ModelPickerSheet mostra i modelli raggruppati per provider con checkmark sul modello corrente.
- **[REQ-032]** Se nessun provider è configurato, ModelPickerSheet mostra un EmptyStateView con link a Settings.

### UXDivers Popups

- **[REQ-033]** Tutti gli alert di conferma, dialog e action sheet usano la libreria `UXDivers.Popups.Maui`. Non si usa `DisplayAlert`, `DisplayActionSheet` o `DisplayPromptAsync` nativi di MAUI.
- **[REQ-034]** La libreria è inizializzata in `MauiProgram.cs` tramite `.UseUXDiversPopups()`.
- **[REQ-035]** I temi `DarkTheme` e `PopupStyles` di UXDivers sono aggiunti alle `MergedDictionaries` in `App.xaml`.
- **[REQ-036]** Tutti i popup sono invocati tramite `IPopupService` (iniettato via DI) — mai tramite `IPopupService.Current` statico nei ViewModel.
- **[REQ-037]** Le conferme di eliminazione (sessione, progetto) usano `SimpleActionPopup` con pulsante di conferma in stile distruttivo (rosso).
- **[REQ-038]** La rinomina sessione usa `FormPopup` con un campo testo pre-compilato con il nome corrente.
- **[REQ-039]** I bottom sheet picker (ProjectSwitcher, AgentPicker, ModelPicker, AddProject) sono implementati come `PopupPage` custom che estendono `UXDivers.Popups.Maui.PopupPage`, con animazione `SlideFromBottom` in entrata e `SlideToBottom` in uscita.
- **[REQ-040]** I toast di feedback (es. "Sessione rinominata", "Progetto aggiunto") usano il tipo `Toast` di UXDivers.
- **[REQ-041]** Gli errori con dettaglio (es. connessione server fallita nell'onboarding) usano `IconTextPopup`.

---

## Open Questions

| # | Domanda | Status | Decisione |
|---|---------|--------|-----------|
| 1 | Il cambio progetto da ProjectSwitcherSheet crea sempre una nuova sessione vuota, o riprende l'ultima sessione del nuovo progetto? | Risolto | Riprende l'ultima sessione del nuovo progetto; se non esiste, crea sessione vuota |
| 2 | La rinomina sessione avviene inline nel flyout o tramite un alert/bottom sheet? | Risolto | `FormPopup` (UXDivers) con campo testo pre-compilato |
| 3 | Il fork sessione è disponibile solo dall'ultimo messaggio o da qualsiasi punto della conversazione? | Risolto | Solo dall'ultimo messaggio (fork dell'intera sessione corrente) |
| 4 | L'onboarding è rivisitabile dalle Settings (es. "Riconfigura server")? | Risolto | Sì — Settings espone voce "Riconfigura server" che riapre lo Step 2 dell'onboarding |
| 5 | I bottom sheet picker (Project, Agent, Model) usano `OptionSheetPopup` o `PopupPage` custom? | Risolto | `PopupPage` custom: i picker hanno CollectionView con item complessi non supportati da `OptionSheetPopup` |

---

## Acceptance Criteria

- [AC-001] SplashPage instrada correttamente in tutti e 4 gli scenari di bootstrap *(REQ-001 – REQ-005)*
- [AC-002] Onboarding completo in 5 step con ProgressBar visibile *(REQ-007)*
- [AC-003] Step 2 blocca "Avanti" finché la connessione non è verificata *(REQ-008)*
- [AC-004] Step 3 è saltabile *(REQ-009)*
- [AC-005] Header ChatPage mostra ProjectName e SessionName corretti *(REQ-012)*
- [AC-006] Tap ProjectName apre ProjectSwitcherSheet *(REQ-013)*
- [AC-007] NewChat crea sessione con impostazioni correnti *(REQ-014)*
- [AC-008] MoreMenu espone tutte le azioni previste *(REQ-015)*
- [AC-009] Banner offline visibile quando server non raggiungibile *(REQ-016)*
- [AC-010] Flyout mostra sessioni del progetto corrente con titolo sezione corretto *(REQ-017, REQ-018)*
- [AC-011] ProjectsPage mostra badge "Attivo" sul progetto corrente *(REQ-022)*
- [AC-012] ProjectDetailPage permette di impostare progetto attivo e avviare nuova sessione *(REQ-024, REQ-025)*
- [AC-013] ModelPickerSheet mostra EmptyStateView se nessun provider configurato *(REQ-032)*
- [AC-014] Nessun `DisplayAlert` / `DisplayActionSheet` / `DisplayPromptAsync` nel codebase — solo UXDivers *(REQ-033)*
- [AC-015] `UseUXDiversPopups()` presente in `MauiProgram.cs`; `DarkTheme` e `PopupStyles` in `App.xaml` *(REQ-034, REQ-035)*
- [AC-016] Conferma eliminazione sessione mostra `SimpleActionPopup` con pulsante rosso *(REQ-037)*
- [AC-017] Rinomina sessione apre `FormPopup` con campo pre-compilato *(REQ-038)*
- [AC-018] Bottom sheet picker scorrono dal basso con animazione SlideFromBottom *(REQ-039)*
- [AC-019] Toast di feedback visibile dopo operazioni di successo (rinomina, aggiunta progetto) *(REQ-040)*

---

## Note per l'Analisi Tecnica

> Questa sezione è indirizzata all'agente che eseguirà l'analisi tecnica di implementazione.

- **UXDivers.Popups.Maui**: prima adozione nel progetto. Richiede setup in `MauiProgram.cs` (`.UseUXDiversPopups()`) e aggiunta di `DarkTheme` + `PopupStyles` in `App.xaml`. Attenzione: `App.xaml` è soggetto al crash `AppThemeBinding` documentato in `tech-chat-ui-design-guidelines` — verificare compatibilità con le `MergedDictionaries` di UXDivers prima di aggiungere.
- **IPopupService**: iniettare nei ViewModel tramite costruttore, non usare `IPopupService.Current` statico (viola il pattern DI del progetto).
- **PopupPage custom**: i bottom sheet picker devono estendere `UXDivers.Popups.Maui.PopupPage` (non `ContentPage`). Registrarli in DI come `Transient` in `MauiProgram.cs`.
- **Shell Flyout + Popup coesistenza**: verificare che l'apertura di un `PopupPage` sopra il flyout aperto non causi conflitti di z-order su Android.
- **Navigazione back Android**: `UseUXDiversPopups()` chiude il popup in cima allo stack al tap del tasto back Android — comportamento desiderato per tutti i popup di questa spec.
- **SplashPage routing**: implementare come `ShellContent` con `FlyoutItemIsVisible="False"` e navigazione programmatica in `OnAppearing` del ViewModel.
- **Onboarding step navigation**: usare un singolo `OnboardingViewModel` con `CurrentStep` observable e `CarouselView` o `ContentView` con swap, non pagine Shell separate (evita animazioni di navigazione indesiderate tra step).
- **File da creare (stima)**:
  - Core: `OnboardingViewModel`, `ProjectsViewModel`, `ProjectDetailViewModel`, `SplashViewModel`, popup ViewModel (RenameSession, DeleteConfirm, ProjectSwitcher, AgentPicker, ModelPicker, AddProject)
  - MAUI: `SplashPage`, `OnboardingPage`, `ProjectsPage`, `ProjectDetailPage`, popup custom (6x `PopupPage`)
  - Registrazioni DI in `MauiProgram.cs` e `CoreServiceExtensions.cs`

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-15

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/app-navigation-structure |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core, om-mobile-ui, om-tester, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Navigation Services | om-mobile-core | src/openMob.Core/Services/ |
| ViewModels (Splash, Onboarding, Projects, Popups) | om-mobile-core | src/openMob.Core/ViewModels/ |
| Data / Repositories | om-mobile-core | src/openMob.Core/Data/ (read-only, no new entities) |
| XAML Pages (Splash, Onboarding, Projects, ProjectDetail, Settings stub) | om-mobile-ui | src/openMob/Views/Pages/ |
| XAML Popups (4 custom PopupPages) | om-mobile-ui | src/openMob/Views/Popups/ |
| XAML Controls (Onboarding steps, Flyout content) | om-mobile-ui | src/openMob/Views/Controls/ |
| Shell Navigation | om-mobile-ui | src/openMob/AppShell.xaml |
| App Resources | om-mobile-ui | src/openMob/App.xaml |
| DI Registration | om-mobile-core + om-mobile-ui | src/openMob/MauiProgram.cs, src/openMob.Core/Infrastructure/DI/ |
| Unit Tests | om-tester | tests/openMob.Tests/ |
| Code Review | om-reviewer | all of the above |

### Files to Create

**In `src/openMob.Core/` (om-mobile-core):**

*Services & Interfaces:*
- `src/openMob.Core/Services/INavigationService.cs` — Abstraction over Shell.Current navigation for testability. Methods: `GoToAsync(string route)`, `GoToAsync(string route, IDictionary<string, object> parameters)`, `PopAsync()`, `SetRootAsync(string route)`.
- `src/openMob.Core/Services/IProjectService.cs` — Interface for project operations. Wraps `IOpencodeApiClient` project methods. Methods: `GetProjectsAsync()`, `GetCurrentProjectAsync()`, `SetActiveProjectAsync(string projectId)`.
- `src/openMob.Core/Services/ISessionService.cs` — Interface for session operations. Wraps `IOpencodeApiClient` session methods. Methods: `GetSessionsAsync()`, `GetSessionsByProjectAsync(string projectId)`, `GetSessionAsync(string id)`, `CreateSessionAsync(string? title)`, `UpdateSessionAsync(string id, string title)`, `DeleteSessionAsync(string id)`, `ForkSessionAsync(string id)`, `GetLastSessionForProjectAsync(string projectId)`.
- `src/openMob.Core/Services/IAgentService.cs` — Interface for agent operations. Methods: `GetAgentsAsync()`.
- `src/openMob.Core/Services/IProviderService.cs` — Interface for provider/model operations. Methods: `GetProvidersAsync()`, `GetModelsForProviderAsync(string providerId)`, `SetProviderAuthAsync(string providerId, string apiKey)`.
- `src/openMob.Core/Services/IPopupService.cs` — Abstraction over UXDivers `IPopupService` for testability in Core ViewModels. Methods: `ShowConfirmDeleteAsync(string title, string message)`, `ShowRenameAsync(string currentName)`, `ShowToastAsync(string message)`, `ShowErrorAsync(string title, string message)`, `ShowOptionSheetAsync(IReadOnlyList<string> options)`, `PushPopupAsync<T>() where T : class`.
- `src/openMob.Core/Services/ProjectService.cs` — Implementation wrapping `IOpencodeApiClient`.
- `src/openMob.Core/Services/SessionService.cs` — Implementation wrapping `IOpencodeApiClient`.
- `src/openMob.Core/Services/AgentService.cs` — Implementation wrapping `IOpencodeApiClient`.
- `src/openMob.Core/Services/ProviderService.cs` — Implementation wrapping `IOpencodeApiClient`.

*ViewModels:*
- `src/openMob.Core/ViewModels/SplashViewModel.cs` — Bootstrap logic: checks `IServerConnectionRepository.GetActiveAsync()`, checks `IOpencodeConnectionManager.IsServerReachableAsync()`, checks `ISessionService.GetSessionsAsync()`, determines route. Properties: `IsLoading`. Commands: `InitializeCommand` (auto-invoked on appearing).
- `src/openMob.Core/ViewModels/OnboardingViewModel.cs` — Single ViewModel for all 5 steps. Properties: `CurrentStep` (int, 1-5), `TotalSteps` (5), `Progress` (double, computed), `ServerUrl`, `ServerToken`, `IsConnectionTested`, `IsConnectionSuccessful`, `ConnectionStatusMessage`, `SelectedProviderId`, `ProviderApiKey`, `Providers` (ObservableCollection), `CanGoNext` (computed per step). Commands: `NextStepCommand`, `PreviousStepCommand`, `SkipStepCommand`, `TestConnectionCommand`, `CompleteOnboardingCommand`.
- `src/openMob.Core/ViewModels/ProjectsViewModel.cs` — Properties: `Projects` (ObservableCollection), `IsLoading`, `IsEmpty` (computed). Commands: `LoadProjectsCommand`, `SelectProjectCommand(string id)`, `DeleteProjectCommand(string id)`, `SetActiveProjectCommand(string id)`, `ShowAddProjectCommand`.
- `src/openMob.Core/ViewModels/ProjectDetailViewModel.cs` — Properties: `Project`, `ProjectName`, `ProjectPath`, `ProjectDescription`, `IsActiveProject`, `RecentSessions` (ObservableCollection, max 5), `DefaultAgentName`, `DefaultModelName`. Commands: `LoadProjectCommand(string id)`, `SetActiveCommand`, `NewSessionCommand`, `ChangeAgentCommand`, `ChangeModelCommand`, `DeleteProjectCommand`.
- `src/openMob.Core/ViewModels/AddProjectViewModel.cs` — Properties: `ProjectName`, `ProjectPath`, `CanAdd` (computed). Commands: `AddProjectCommand`, `CancelCommand`.
- `src/openMob.Core/ViewModels/ProjectSwitcherViewModel.cs` — Properties: `Projects` (ObservableCollection), `ActiveProjectId`. Commands: `LoadProjectsCommand`, `SelectProjectCommand(string id)`, `ManageProjectsCommand`.
- `src/openMob.Core/ViewModels/AgentPickerViewModel.cs` — Properties: `Agents` (ObservableCollection), `SelectedAgentName`, `IsEmpty`. Commands: `LoadAgentsCommand`, `SelectAgentCommand(string name)`.
- `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` — Properties: `ProviderGroups` (grouped ObservableCollection), `SelectedModelId`, `IsEmpty`, `HasProviders`. Commands: `LoadModelsCommand`, `SelectModelCommand(string modelId)`, `ConfigureProvidersCommand`.

*Models:*
- `src/openMob.Core/Models/ProjectItem.cs` — Display model for project list items: `Id`, `Name`, `Path`, `IsActive`.
- `src/openMob.Core/Models/SessionItem.cs` — Display model for session list items: `Id`, `Title`, `RelativeTimestamp`, `IsSelected`.
- `src/openMob.Core/Models/AgentItem.cs` — Display model for agent picker: `Name`, `Description`, `IsSelected`.
- `src/openMob.Core/Models/ModelItem.cs` — Display model for model picker: `Id`, `Name`, `ProviderName`, `ContextSize`, `IsSelected`.
- `src/openMob.Core/Models/ProviderModelGroup.cs` — Grouped model for ModelPickerSheet: `ProviderName`, `Models` (list of ModelItem).
- `src/openMob.Core/Models/StatusBannerInfo.cs` — Model for conditional status banners: `Type` (enum: Offline, NoProvider, ToolError, ContextOverflow), `Message`, `ActionLabel`, `IsDismissible`.

**In `src/openMob/` (om-mobile-ui):**

*Pages:*
- `src/openMob/Views/Pages/SplashPage.xaml` + `.xaml.cs` — Logo + ActivityIndicator, binds to SplashViewModel.
- `src/openMob/Views/Pages/OnboardingPage.xaml` + `.xaml.cs` — Single page with ContentView swap per step, ProgressBar, navigation buttons.
- `src/openMob/Views/Pages/ProjectsPage.xaml` + `.xaml.cs` — CollectionView of projects with swipe-to-delete, EmptyStateView.
- `src/openMob/Views/Pages/ProjectDetailPage.xaml` + `.xaml.cs` — ScrollView with sections.
- `src/openMob/Views/Pages/SettingsPage.xaml` + `.xaml.cs` — Stub page with "Coming soon" placeholder.

*Popups (new directory `src/openMob/Views/Popups/`):*
- `src/openMob/Views/Popups/ProjectSwitcherSheet.xaml` + `.xaml.cs` — Custom PopupPage, SlideFromBottom.
- `src/openMob/Views/Popups/AgentPickerSheet.xaml` + `.xaml.cs` — Custom PopupPage, SlideFromBottom.
- `src/openMob/Views/Popups/ModelPickerSheet.xaml` + `.xaml.cs` — Custom PopupPage, SlideFromBottom.
- `src/openMob/Views/Popups/AddProjectSheet.xaml` + `.xaml.cs` — Custom PopupPage, SlideFromBottom.

*Controls (onboarding step views):*
- `src/openMob/Views/Controls/OnboardingWelcomeView.xaml` + `.xaml.cs` — Step 1 content.
- `src/openMob/Views/Controls/OnboardingConnectServerView.xaml` + `.xaml.cs` — Step 2 content.
- `src/openMob/Views/Controls/OnboardingProviderSetupView.xaml` + `.xaml.cs` — Step 3 content.
- `src/openMob/Views/Controls/OnboardingPermissionsView.xaml` + `.xaml.cs` — Step 4 content.
- `src/openMob/Views/Controls/OnboardingCompletionView.xaml` + `.xaml.cs` — Step 5 content.
- `src/openMob/Views/Controls/StatusBannerView.xaml` + `.xaml.cs` — Reusable conditional banner for ChatPage header.
- `src/openMob/Views/Controls/FlyoutContentView.xaml` + `.xaml.cs` — Custom flyout body with session list + project section header.
- `src/openMob/Views/Controls/FlyoutHeaderView.xaml` + `.xaml.cs` — Flyout header with logo + new chat button.
- `src/openMob/Views/Controls/FlyoutFooterView.xaml` + `.xaml.cs` — Flyout footer with Projects, Settings, version.

*Platform services:*
- `src/openMob/Services/MauiNavigationService.cs` — Implementation of `INavigationService` wrapping `Shell.Current`.
- `src/openMob/Services/MauiPopupService.cs` — Implementation of `IPopupService` wrapping UXDivers `IPopupService.Current` and popup type instantiation.

**In `tests/openMob.Tests/` (om-tester):**
- `tests/openMob.Tests/ViewModels/SplashViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/OnboardingViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ProjectsViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ProjectDetailViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/AddProjectViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ProjectSwitcherViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/AgentPickerViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ModelPickerViewModelTests.cs`
- `tests/openMob.Tests/Services/ProjectServiceTests.cs`
- `tests/openMob.Tests/Services/SessionServiceTests.cs`
- `tests/openMob.Tests/Services/AgentServiceTests.cs`
- `tests/openMob.Tests/Services/ProviderServiceTests.cs`

### Files to Modify

- `src/openMob/AppShell.xaml` + `AppShell.xaml.cs` — Complete rewrite: Shell Flyout with `FlyoutBehavior="Flyout"`, custom `Shell.FlyoutContent`, `Shell.FlyoutHeader`, `Shell.FlyoutFooter`. Register routes for `splash`, `onboarding`, `chat`, `projects`, `project-detail`, `settings`. Remove current `MainPage` ShellContent.
- `src/openMob/App.xaml` — Add UXDivers `DarkTheme` and `PopupStyles` to MergedDictionaries. Add `xmlns:uxd` namespace.
- `src/openMob/MauiProgram.cs` — Add `.UseUXDiversPopups()`, register all new ViewModels as Transient, register all new Pages as Transient, register all Popup pages as Transient, register `INavigationService` → `MauiNavigationService` as Singleton, register `IPopupService` → `MauiPopupService` as Singleton.
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — Register `IProjectService`, `ISessionService`, `IAgentService`, `IProviderService` and their implementations.
- `src/openMob/openMob.csproj` — Add `<PackageReference Include="UXDivers.Popups.Maui" Version="*" />`.
- `src/openMob/Views/Pages/MainPage.xaml` + `.xaml.cs` — Delete (replaced by ChatPage as root).

### Technical Dependencies

- **Existing infrastructure (completed):**
  - `IServerConnectionRepository` — used by SplashViewModel to check if any server is configured
  - `IOpencodeConnectionManager` — used by SplashViewModel to check server reachability
  - `IOpencodeApiClient` — used by all services (ProjectService, SessionService, AgentService, ProviderService) to call server API
  - `IServerCredentialStore` — used by OnboardingViewModel Step 2 to save server credentials
  - `ServerConnection` entity + repository — used to persist server configuration during onboarding

- **Dependency on `feature/chat-ui-design-guidelines` branch:**
  - The ChatPage layout (message list, input bar, bubbles, empty state) is defined in that spec. This feature adds the **header bar** and **navigation integration** on top of it.
  - **Decision:** This feature can proceed independently. The ChatPage created here will include the extended header bar and status banners. The message list / input bar / bubbles from the chat-ui-design-guidelines spec will be integrated later when that feature merges. For now, ChatPage body will show a placeholder or empty state.

- **opencode server API endpoints involved:**
  - `GET /global/health` — SplashViewModel bootstrap
  - `GET /project` — ProjectService, ProjectSwitcherViewModel
  - `GET /project/current` — ProjectService
  - `GET /session` — SessionService
  - `POST /session` — SessionService.CreateSessionAsync
  - `PUT /session/{id}` — SessionService.UpdateSessionAsync (rename)
  - `DELETE /session/{id}` — SessionService.DeleteSessionAsync
  - `POST /session/{id}/fork` — SessionService.ForkSessionAsync
  - `GET /agent` — AgentService
  - `GET /provider` — ProviderService
  - `POST /provider/{id}/auth` — ProviderService (onboarding Step 3)
  - `GET /config/providers` — ProviderService (model list)

- **New NuGet package required:**
  - `UXDivers.Popups.Maui` (version `*` — latest, currently 0.9.3, supports net10.0)

### Technical Decisions

1. **INavigationService abstraction**: The project currently has no `INavigationService`. This feature introduces it as a Core interface with a MAUI implementation wrapping `Shell.Current`. All ViewModels use this interface — never `Shell.Current` directly. This enables unit testing of navigation logic.

2. **IPopupService abstraction**: REQ-036 mandates DI injection, not `IPopupService.Current` static access. We create a Core `IPopupService` interface (different namespace from UXDivers) that wraps the UXDivers popup types. The MAUI implementation (`MauiPopupService`) uses `UXDivers.Popups.Maui.IPopupService.Current` internally. ViewModels depend only on the Core interface.

3. **Onboarding as single page with ContentView swap**: Per the spec's technical notes, the onboarding uses a single `OnboardingPage` with a `ContentPresenter` or `ContentView` that swaps child views based on `CurrentStep`. This avoids Shell navigation animations between steps and keeps the ProgressBar persistent. Each step is a separate `ContentView` in `Views/Controls/`.

4. **SplashPage as non-flyout ShellContent**: Implemented as a `ShellContent` with `FlyoutItemIsVisible="False"` and `Shell.NavBarIsVisible="False"`. The `SplashViewModel.InitializeCommand` runs on appearing and navigates away using `Shell.Current.GoToAsync("//chat")` (absolute route) to prevent back navigation.

5. **UXDivers DarkTheme + AppThemeBinding compatibility**: The spec notes warn about the `AppThemeBinding` crash documented in `tech-chat-ui-design-guidelines`. UXDivers `DarkTheme` and `PopupStyles` are standard `ResourceDictionary` types — they should be safe to add to `MergedDictionaries` alongside existing dictionaries. The crash only affects `AppThemeBinding` used as standalone `ResourceDictionary` values, not as property values within controls. **Decision:** Add UXDivers dictionaries after the existing `Colors.xaml` and `Styles.xaml` entries. Test on Android to verify no crash.

6. **Service layer wrapping IOpencodeApiClient**: Rather than having ViewModels call `IOpencodeApiClient` directly, we introduce thin service interfaces (`IProjectService`, `ISessionService`, etc.) that provide a cleaner API surface and handle `OpencodeResult<T>` unwrapping, error mapping, and caching. This follows the existing pattern where `IServerConnectionRepository` wraps EF Core operations.

7. **Session filtering by project**: The opencode API `GET /session` returns all sessions. `SessionService.GetSessionsByProjectAsync(projectId)` filters client-side by `SessionDto.ProjectId`. If the API adds server-side filtering later, only the service implementation changes.

8. **Flyout as custom Shell.FlyoutContent**: MAUI Shell supports `Shell.FlyoutContent` for fully custom flyout body. We use a `FlyoutContentView` ContentView that binds to a `FlyoutViewModel` (or reuses `SessionListViewModel` from the chat-ui-design-guidelines spec). The flyout header and footer use `Shell.FlyoutHeader` and `Shell.FlyoutFooter` templates.

### Technical Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| UXDivers.Popups.Maui is a relatively new library (v0.9.x) — potential stability issues | Medium | Library supports net10.0, has active development. Pin to latest stable. Test all popup types on both platforms. |
| Shell Flyout + UXDivers PopupPage z-order conflict on Android | Medium | Test opening a PopupPage while flyout is open. If conflict, close flyout before showing popup. |
| `AppThemeBinding` crash when adding UXDivers MergedDictionaries | Medium | Add UXDivers dictionaries after existing ones. Test on Android immediately after integration. Fallback: load UXDivers resources in code-behind if XAML crashes. |
| Onboarding Step 2 server connection test requires real network call | Low | Use `IOpencodeConnectionManager.IsServerReachableAsync()` which already has 5s timeout. Mock in tests. |
| Shell navigation `//chat` absolute route may not work with Flyout items | Medium | Test absolute route navigation from SplashPage. Fallback: use `Shell.Current.GoToAsync("//ChatPage")` with the route name matching the ShellContent Route. |
| Large number of new files (40+) increases merge conflict risk with `feature/chat-ui-design-guidelines` | Low | This feature creates new files in new directories (`Views/Popups/`, new ViewModels). Overlap is limited to `AppShell.xaml`, `MauiProgram.cs`, `CoreServiceExtensions.cs`, and `App.xaml` — all of which are additive changes. |

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[Git Flow]** Create branch `feature/app-navigation-structure` from `develop`
2. **[om-mobile-core]** Implement all interfaces, services, models, and ViewModels:
   - `INavigationService`, `IPopupService` (Core abstractions)
   - `IProjectService`, `ISessionService`, `IAgentService`, `IProviderService` + implementations
   - All display models (`ProjectItem`, `SessionItem`, `AgentItem`, `ModelItem`, `ProviderModelGroup`, `StatusBannerInfo`)
   - All ViewModels (`SplashViewModel`, `OnboardingViewModel`, `ProjectsViewModel`, `ProjectDetailViewModel`, `AddProjectViewModel`, `ProjectSwitcherViewModel`, `AgentPickerViewModel`, `ModelPickerViewModel`)
   - DI registration in `CoreServiceExtensions.cs`
3. ⟳ **[om-mobile-ui]** Once om-mobile-core defines the ViewModel binding surface:
   - Add `UXDivers.Popups.Maui` NuGet package to `openMob.csproj`
   - Configure `MauiProgram.cs` (`.UseUXDiversPopups()`, register pages/popups/platform services)
   - Configure `App.xaml` (add UXDivers `DarkTheme` + `PopupStyles`)
   - Rewrite `AppShell.xaml` for Flyout navigation with all routes
   - Implement all pages: `SplashPage`, `OnboardingPage`, `ProjectsPage`, `ProjectDetailPage`, `SettingsPage` (stub)
   - Implement all popups: `ProjectSwitcherSheet`, `AgentPickerSheet`, `ModelPickerSheet`, `AddProjectSheet`
   - Implement all controls: 5 onboarding step views, `StatusBannerView`, `FlyoutContentView`, `FlyoutHeaderView`, `FlyoutFooterView`
   - Implement platform services: `MauiNavigationService`, `MauiPopupService`
   - Delete `MainPage.xaml` + `.xaml.cs`
4. ⟳ **[om-tester]** Once om-mobile-core completes: write unit tests for all Services and ViewModels
5. **[om-reviewer]** Full review against spec — all agents must complete before review starts
6. **[Fix loop if needed]** Address Critical and Major findings
7. **[Git Flow]** Finish branch and merge into `develop`

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-041]` requirements implemented
- [ ] All `[AC-001]` through `[AC-019]` acceptance criteria satisfied
- [ ] Unit tests written for all new Services and ViewModels
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] `dotnet build openMob.sln` — zero errors, zero warnings
- [ ] `dotnet test tests/openMob.Tests/openMob.Tests.csproj` — all tests pass
- [ ] No `DisplayAlert` / `DisplayActionSheet` / `DisplayPromptAsync` in codebase
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
- [ ] Knowledge base indexed
