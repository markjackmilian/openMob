# Server Offline Detection — Startup & Runtime Navigation to Server Management

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Attualmente, quando il server configurato è irraggiungibile all'avvio, l'app naviga silenziosamente a `ChatPage` con un banner arancione. Questo comportamento non è sufficientemente esplicito: l'utente non capisce immediatamente cosa fare. Questa spec ridefinisce il flusso di startup e il comportamento runtime per dare evidenza chiara dello stato offline e guidare l'utente verso `ServerManagementPage`, dove può verificare e correggere la configurazione del server.

---

## Scope

### In Scope
- Modifica del flusso di `SplashViewModel`: aggiunta di messaggi di stato visivi durante il tentativo di connessione e in caso di errore, seguita da navigazione automatica a `ServerManagementPage`.
- Gestione differenziata di tutti gli scenari di errore al startup: timeout, errore di rete, errore 5xx, errore 401 (credenziali non valide).
- Il caso "nessun server configurato" rimane invariato: navigazione a `OnboardingPage`.
- Aggiunta di `StatusMessage` (`string`) come nuova `ObservableProperty` su `SplashViewModel`, visualizzata sulla `SplashPage` sotto lo spinner.
- Modifica del banner `StatusBannerType.ServerOffline` in `ChatViewModel` a runtime: aggiunta di `ActionLabel` e `ActionCommand` per navigare a `ServerManagementPage`.
- Fix del binding mancante di `ActionCommand` su `StatusBannerView` in `ChatPage.xaml`.
- Aggiunta di `INavigationService` come dipendenza di `ChatViewModel`.

### Out of Scope
- Modifiche alla `ServerManagementPage` stessa (layout, funzionalità, indicatori di stato inline).
- Retry automatico della connessione (già gestito dall'exponential backoff esistente in `IOpencodeConnectionManager`).
- Modifiche al flusso di Onboarding.
- Background monitoring del server o notifiche push.
- Modifiche ad altre pagine che mostrano errori di connettività (es. `ProjectsPage`, `ServerDetailPage`).

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Al startup, se `IServerConnectionRepository.GetActiveAsync()` restituisce `null`, il comportamento rimane invariato: `SplashViewModel` naviga a `//onboarding`.

2. **[REQ-002]** Al startup, se un server è configurato, `SplashViewModel` imposta `StatusMessage` a un testo che indica il tentativo di connessione in corso (es. *"Connessione al server in corso…"*) prima di chiamare `IsServerReachableAsync`.

3. **[REQ-003]** Al startup, se il server non risponde entro 5 secondi (`OperationCanceledException` con timeout), `SplashViewModel` imposta `StatusMessage` a un messaggio che indica che il server non è raggiungibile (es. *"Server non raggiungibile"*).

4. **[REQ-004]** Al startup, se la chiamata a `IsServerReachableAsync` restituisce un `OpencodeResult` con `OpencodeApiError.ErrorKind == Unauthorized` (401), `SplashViewModel` imposta `StatusMessage` a un messaggio che indica credenziali non valide (es. *"Credenziali non valide — verifica la configurazione"*).

5. **[REQ-005]** Al startup, se la chiamata a `IsServerReachableAsync` restituisce un errore di rete (`NetworkUnreachable`) o errore server (`ServerError` / 5xx), `SplashViewModel` imposta `StatusMessage` a un messaggio generico di errore di connessione (es. *"Errore di connessione al server"*).

6. **[REQ-006]** Dopo aver impostato il messaggio di errore (REQ-003, REQ-004, REQ-005), `SplashViewModel` attende un breve intervallo (2 secondi) per consentire all'utente di leggere il messaggio, poi naviga automaticamente a `ServerManagementPage` usando un route assoluto che non consente back-navigation alla `SplashPage`.

7. **[REQ-007]** La navigazione da `SplashPage` a `ServerManagementPage` avviene tramite `INavigationService.GoToAsync("//server-management")` (route assoluto), in modo che la `SplashPage` non sia raggiungibile tramite back navigation.

8. **[REQ-008]** A runtime, quando `IOpencodeConnectionManager.StatusChanged` emette `ServerConnectionStatus.Disconnected` o `ServerConnectionStatus.Error`, il `StatusBannerInfo` costruito da `ChatViewModel.UpdateStatusBanner()` per il tipo `ServerOffline` include `ActionLabel: "Gestisci server"` e un `ActionCommand` che chiama `INavigationService.GoToAsync("server-management")` (route relativo, per preservare la back-navigation verso `ChatPage`).

9. **[REQ-009]** `ChatViewModel` riceve `INavigationService` come nuova dipendenza nel costruttore.

10. **[REQ-010]** In `ChatPage.xaml`, il binding `ActionCommand` su `StatusBannerView` viene aggiunto, puntando alla property `StatusBannerActionCommand` esposta da `ChatViewModel` (o equivalente pattern stabilito dall'implementazione).

11. **[REQ-011]** Quando l'utente naviga a `ServerManagementPage` tramite il banner di `ChatPage`, è possibile tornare a `ChatPage` tramite back navigation (`PopAsync`).

12. **[REQ-012]** `SplashViewModel` espone la property `StatusMessage` (`string`) come `[ObservableProperty]`, inizializzata a `string.Empty`. La `SplashPage` mostra questa label sotto lo spinner; la label è visibile solo quando `StatusMessage` non è vuoto.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `SplashViewModel` | **Modifica** — nuovo routing per scenari offline; nuova property `StatusMessage`; attesa di 2s prima della navigazione | Attualmente naviga a `//chat` in tutti i casi di errore |
| `SplashPage.xaml` | **Modifica** — aggiunta label per `StatusMessage` sotto lo spinner | Label visibile solo quando `StatusMessage != string.Empty` |
| `ChatViewModel` | **Modifica** — aggiunta dipendenza `INavigationService`; banner `ServerOffline` arricchito con `ActionLabel` e `ActionCommand` | `INavigationService` attualmente non iniettato in `ChatViewModel` |
| `ChatPage.xaml` | **Fix** — aggiunta del binding `ActionCommand` su `StatusBannerView` | Gap identificato: `ActionCommand` non è attualmente bindato nel XAML |
| `AppShell.xaml` | **Verifica** — confermare che `server-management` sia registrato come route assoluto Shell | Necessario per supportare `//server-management` dalla Splash |
| `SplashViewModelTests` | **Aggiornamento** — test esistenti per routing offline devono aspettarsi navigazione a `server-management` invece di `chat`; nuovi test per `StatusMessage` e attesa 2s | |
| `ChatViewModelTests` | **Aggiunta** — nuovi test per `ActionCommand` del banner `ServerOffline` | |

### Dependencies
- `IOpencodeConnectionManager.IsServerReachableAsync()` — già esistente; restituisce `OpencodeResult<bool>` con `OpencodeApiError` in caso di fallimento.
- `INavigationService.GoToAsync(string route)` — già esistente; da iniettare in `ChatViewModel`.
- `ServerManagementPage` / route `server-management` — già esistente (spec `server-management-ui`); verificare registrazione Shell come route assoluto.
- `StatusBannerView.ActionCommand` bindable property — già esistente sul controllo; il binding in `ChatPage.xaml` è il gap da correggere.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Testo esatto dei messaggi di errore per ciascuno scenario (timeout, 401, 5xx, network error) | Open | Testi proposti nei REQ-003/004/005; da confermare con il team |
| 2 | La route `server-management` è già registrata come route Shell assoluto (`//server-management`)? | Open | Da verificare in `AppShell.xaml` durante l'analisi tecnica |
| 3 | L'`ActionCommand` del banner deve essere una `RelayCommand` separata su `ChatViewModel` o può essere derivato da `StatusBannerInfo`? | Open | Da decidere durante l'analisi tecnica; `StatusBannerInfo` è un record immutabile, quindi probabilmente serve una property separata su `ChatViewModel` |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato un server configurato e offline (timeout), quando l'app si avvia, allora la `SplashPage` mostra prima "Connessione al server in corso…" e poi "Server non raggiungibile", e dopo 2 secondi naviga automaticamente a `ServerManagementPage`. *(REQ-002, REQ-003, REQ-006, REQ-007)*

- [ ] **[AC-002]** Dato un server configurato con credenziali errate (401), quando l'app si avvia, allora la `SplashPage` mostra "Credenziali non valide — verifica la configurazione" e dopo 2 secondi naviga a `ServerManagementPage`. *(REQ-004, REQ-006, REQ-007)*

- [ ] **[AC-003]** Dato un server configurato che restituisce un errore 5xx o di rete, quando l'app si avvia, allora la `SplashPage` mostra "Errore di connessione al server" e dopo 2 secondi naviga a `ServerManagementPage`. *(REQ-005, REQ-006, REQ-007)*

- [ ] **[AC-004]** Dato nessun server configurato, quando l'app si avvia, allora il comportamento è invariato: navigazione a `OnboardingPage` senza mostrare messaggi di errore. *(REQ-001)*

- [ ] **[AC-005]** Dato che l'app naviga a `ServerManagementPage` dalla `SplashPage`, allora non è possibile tornare alla `SplashPage` tramite back navigation. *(REQ-007)*

- [ ] **[AC-006]** Dato il server offline a runtime su `ChatPage`, quando `StatusChanged` emette `Error` o `Disconnected`, allora il banner mostra il pulsante "Gestisci server". *(REQ-008)*

- [ ] **[AC-007]** Dato il banner "Gestisci server" visibile su `ChatPage`, quando l'utente lo preme, allora naviga a `ServerManagementPage` e può tornare a `ChatPage` con back navigation. *(REQ-008, REQ-011)*

- [ ] **[AC-008]** Dato che `SplashViewModel` è in attesa della risposta del server, allora `StatusMessage` è visibile sulla `SplashPage` sotto lo spinner. *(REQ-002, REQ-012)*

- [ ] **[AC-009]** I test esistenti di `SplashViewModelTests` per lo scenario offline passano con il nuovo comportamento atteso (navigazione a `server-management`). *(REQ-006, REQ-007)*

- [ ] **[AC-010]** Nuovi test in `ChatViewModelTests` verificano che `UpdateStatusBanner()` produca un `StatusBannerInfo` con `ActionLabel = "Gestisci server"` quando lo stato è `Disconnected` o `Error`. *(REQ-008)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

**Key areas to investigate:**

1. **Route `//server-management` in AppShell**: Verificare in `AppShell.xaml` se `ServerManagementPage` è registrata come `ShellContent` con route assoluto. Se è registrata solo come route relativo (via `Routing.RegisterRoute`), la navigazione `//server-management` dalla Splash potrebbe non funzionare. Potrebbe essere necessario aggiungere uno `ShellContent` con `FlyoutItemIsVisible="False"` o usare un pattern alternativo.

2. **Attesa 2 secondi in `SplashViewModel`**: Implementare con `await Task.Delay(TimeSpan.FromSeconds(2), ct)` per rispettare la cancellazione. Se `ct` viene cancellato durante l'attesa (es. app in background), la navigazione non deve avvenire.

3. **`ActionCommand` in `ChatViewModel`**: `StatusBannerInfo` è un `sealed record` immutabile — non può contenere un `ICommand`. Il pattern consigliato è esporre una `[RelayCommand]` separata su `ChatViewModel` (es. `NavigateToServerManagementCommand`) e bindarla in `ChatPage.xaml` direttamente su `StatusBannerView.ActionCommand`. Alternativa: aggiungere un `ICommand?` a `StatusBannerInfo`, ma questo introduce una dipendenza su `System.Windows.Input` nel record — valutare se accettabile.

4. **`INavigationService` in `ChatViewModel`**: Aggiungere come ultimo parametro del costruttore per minimizzare l'impatto sui test esistenti. Aggiornare la registrazione DI in `MauiProgram.cs` se necessario (ma `ChatViewModel` è già `Transient` e `INavigationService` è `Singleton` — nessun problema di lifetime).

5. **Test di `SplashViewModel` con `Task.Delay`**: I test esistenti usano probabilmente `CancellationToken` per controllare il flusso. Il `Task.Delay(2s)` deve essere mockabile o bypassabile nei test. Valutare l'introduzione di `ITimeProvider` (o `TimeProvider` di .NET 8+) per rendere il delay testabile senza rallentare la suite.

6. **Messaggi di errore localizzati**: I testi proposti sono in italiano. Verificare se il progetto usa un sistema di localizzazione (`.resx`) o stringhe hardcoded. Allinearsi al pattern esistente.

**Suggested implementation approach:**
- Modificare `SplashViewModel.InitializeAsync`: dopo il blocco `catch` per `OperationCanceledException` e gli altri errori, impostare `StatusMessage`, attendere 2s, poi navigare a `//server-management`.
- Modificare `ChatViewModel.UpdateStatusBanner`: nel ramo `ServerOffline`, impostare `ActionLabel: "Gestisci server"`. Aggiungere `[RelayCommand] private async Task NavigateToServerManagementAsync()` che chiama `_navigationService.GoToAsync("server-management")`.
- In `ChatPage.xaml`: aggiungere `ActionCommand="{Binding NavigateToServerManagementCommand}"` su `StatusBannerView`.

**Constraints to respect:**
- `openMob.Core` ha zero dipendenze MAUI — `INavigationService` è già un'interfaccia Core, quindi l'iniezione in `ChatViewModel` è conforme all'architettura.
- Nessuna navigazione diretta tramite `Shell.Current` nei ViewModel.
- Nessun `async void` — il comando di navigazione deve essere `[AsyncRelayCommand]`.
- Back navigation dalla `ServerManagementPage` raggiunta dalla Splash non deve essere possibile (route assoluto `//server-management`).

**Related files or modules:**
- `src/openMob.Core/ViewModels/SplashViewModel.cs`
- `src/openMob/Views/SplashPage.xaml`
- `src/openMob.Core/ViewModels/ChatViewModel.cs`
- `src/openMob/Views/ChatPage.xaml`
- `src/openMob/Views/Controls/StatusBannerView.xaml`
- `src/openMob.Core/Models/StatusBannerInfo.cs`
- `src/openMob.Core/Models/StatusBannerType.cs`
- `src/openMob/AppShell.xaml`
- `tests/openMob.Tests/ViewModels/SplashViewModelTests.cs`
- `tests/openMob.Tests/ViewModels/ChatViewModelTests.cs`

**As established in past specs:**
- `IOpencodeConnectionManager.IsServerReachableAsync()` è il punto di ingresso per il controllo di raggiungibilità (spec `opencode-api-client`).
- `ServerConnectionStatus` enum: `Disconnected`, `Connecting`, `Connected`, `Error` (spec `opencode-api-client`).
- `OpencodeApiError.ErrorKind` include `Timeout`, `NetworkUnreachable`, `Unauthorized`, `ServerError` (spec `opencode-api-client`).
- Il pattern `LoadError` + `ActivityIndicator` overlay è lo standard per gestire errori di caricamento nei ViewModel (spec `server-delete-navigation-bugfix`).
- `INavigationService` è Singleton, registrato in `MauiProgram.cs`; non in `CoreServiceExtensions` (spec `app-navigation-structure`).
