# Model Picker — Search & Favorites

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-18                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

La `ModelPickerSheet` mostra attualmente tutti i modelli disponibili raggruppati per provider, senza possibilità di filtrare né di evidenziare i modelli di uso frequente. Questa feature aggiunge una barra di ricerca testuale in cima al picker e un sistema di preferiti globali persistiti in SQLite. I modelli preferiti vengono mostrati in cima al loro gruppo provider (con stellina filled), e la ricerca produce una lista piatta filtrata per nome indipendente dai preferiti.

---

## Scope

### In Scope
- Barra di ricerca testuale in cima alla `ModelPickerSheet`, bindata a `SearchText` su `ModelPickerViewModel`
- Vista raggruppata (SearchText vuoto): provider ordinati alfabeticamente by name; dentro ogni gruppo, preferiti prima (by name) poi non-preferiti (by name)
- Vista ricerca (SearchText non vuoto): lista piatta filtrata per nome modello (case-insensitive, contains), ordinata by name, senza raggruppamento e senza evidenziazione preferiti
- Stellina inline su ogni item della lista raggruppata: filled se preferito, outlined se non preferito
- Toggle preferito in-place: tap sulla stellina aggiorna SQLite e riordina il gruppo senza chiudere il picker
- Nuova entità SQLite `FavoriteModel` con metadati completi (modelId, providerName, displayName, contextSize, addedAt)
- Nuovo servizio `IFavoriteModelService` / `FavoriteModelService`
- Migrazione EF Core per la nuova tabella
- Caricamento preferiti da SQLite al momento dell'apertura del picker

### Out of Scope
- Preferiti per-progetto (i preferiti sono globali, condivisi tra tutti i progetti)
- Ordinamento manuale dei preferiti (drag & drop)
- Sincronizzazione preferiti tra dispositivi
- Modifica della logica di selezione del modello (il callback `OnModelSelected` rimane invariato)
- Stellina nella vista ricerca (lista piatta — nessuna evidenziazione preferiti)
- Qualsiasi modifica ad `AgentPickerSheet` o `AgentPickerViewModel`
- Modifica a `ChatViewModel`, `ProjectDetailViewModel`, `ProjectPreference`

---

## Functional Requirements

> Requirements are numbered for traceability.

1. **[REQ-001]** Creare la nuova entità EF Core `FavoriteModel` con i campi: `ModelId` (string, PK, formato `"providerId/modelId"`), `ProviderName` (string, not null), `DisplayName` (string, not null), `ContextSize` (string, not null), `AddedAt` (DateTimeOffset, UTC, set on insert). Creare la relativa migrazione EF Core con `dotnet ef migrations add AddFavoriteModels`.

2. **[REQ-002]** Creare `IFavoriteModelService` con i seguenti metodi:
   - `GetAllAsync(CancellationToken ct) → Task<IReadOnlyList<FavoriteModel>>`
   - `IsFavoriteAsync(string modelId, CancellationToken ct) → Task<bool>`
   - `ToggleAsync(string modelId, FavoriteModelMetadata metadata, CancellationToken ct) → Task<bool>` — restituisce `true` se il modello è stato aggiunto, `false` se rimosso.
   Creare l'implementazione `FavoriteModelService` che usa `AppDbContext` direttamente. Registrare come Transient in `CoreServiceExtensions.AddOpenMobCore()`.

3. **[REQ-003]** `ModelPickerViewModel` espone la proprietà osservabile `SearchText` (string, default stringa vuota). Ogni modifica a `SearchText` aggiorna la vista attiva: se vuoto → vista raggruppata `FilteredGroups`; se non vuoto → vista ricerca piatta `SearchResults`.

4. **[REQ-004]** `ModelPickerViewModel` espone `FilteredGroups` (`ObservableCollection<ProviderModelGroup>`), usata quando `SearchText` è vuoto. I gruppi sono ordinati alfabeticamente per `ProviderName`. All'interno di ogni gruppo, i modelli sono ordinati: prima i preferiti (`IsFavorite == true`) in ordine alfabetico per `DisplayName`, poi i non-preferiti in ordine alfabetico per `DisplayName`.

5. **[REQ-005]** `ModelPickerViewModel` espone `SearchResults` (`ObservableCollection<ModelItem>`), usata quando `SearchText` non è vuoto. La lista è filtrata per `DisplayName` (case-insensitive, contains) e ordinata alfabeticamente per `DisplayName`. Non viene applicato alcun raggruppamento né alcuna evidenziazione dei preferiti.

6. **[REQ-006]** Il modello `ModelItem` (o equivalente) espone la proprietà osservabile `IsFavorite` (bool). Questa proprietà viene inizializzata al caricamento del picker confrontando il `ModelId` con la lista restituita da `IFavoriteModelService.GetAllAsync`.

7. **[REQ-007]** `ModelPickerViewModel` espone `ToggleFavoriteCommand` (parametro: `ModelItem`). All'esecuzione: chiama `IFavoriteModelService.ToggleAsync` con i metadati dell'item, aggiorna `IsFavorite` sull'item in-memory, riordina il `ProviderModelGroup` interessato in `FilteredGroups` (preferiti prima, by name). Il picker rimane aperto. Il comando è eseguibile anche durante la ricerca (ma la stellina non è visibile nella vista ricerca — vedere Out of Scope).

8. **[REQ-008]** `ModelPickerSheet.xaml` mostra una `SearchBar` (o `Entry` con stile search) in cima al contenuto, sopra la lista, bindata a `SearchText` di `ModelPickerViewModel`. La barra è sempre visibile indipendentemente dallo stato del picker (loading, empty, populated).

9. **[REQ-009]** `ModelPickerSheet.xaml` mostra due layout alternativi in base a `SearchText`:
   - **Vista raggruppata** (`SearchText` vuoto): `CollectionView` con `IsGrouped="True"`, `ItemsSource` bindato a `FilteredGroups`. Ogni item modello mostra una stellina (filled/outlined) bindata a `IsFavorite`; tap sulla stellina esegue `ToggleFavoriteCommand`.
   - **Vista ricerca** (`SearchText` non vuoto): `CollectionView` non raggruppata, `ItemsSource` bindato a `SearchResults`. Nessuna stellina visibile.

10. **[REQ-010]** Al caricamento del picker (`LoadModelsCommand`), dopo aver recuperato i modelli dal server via `IProviderService.GetProvidersAsync`, `ModelPickerViewModel` chiama `IFavoriteModelService.GetAllAsync` e imposta `IsFavorite` su ogni `ModelItem` prima di popolare `FilteredGroups`.

11. **[REQ-011]** `FavoriteModelMetadata` è un `sealed record` con i campi: `ProviderName` (string), `DisplayName` (string), `ContextSize` (string). Viene costruito da `ModelPickerViewModel` al momento del toggle a partire dai dati già presenti nell'item, senza ulteriori chiamate di rete.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `ModelPickerViewModel` | Estensione significativa: aggiunta `SearchText`, `FilteredGroups`, `SearchResults`, `ToggleFavoriteCommand`, dipendenza su `IFavoriteModelService` | `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` |
| `ModelItem` (model class) | Aggiunta proprietà osservabile `IsFavorite` (bool) | Parte di `ModelPickerViewModel.cs` o file separato in `src/openMob.Core/Models/` |
| `ModelPickerSheet.xaml` | Aggiunta `SearchBar` in cima; stellina inline su ogni item nella vista raggruppata; switch tra vista raggruppata e lista piatta | `src/openMob/Views/Popups/ModelPickerSheet.xaml` |
| `ModelPickerSheet.xaml.cs` | Eventuale wiring minimo per la SearchBar (solo se necessario — preferire binding puri) | `src/openMob/Views/Popups/ModelPickerSheet.xaml.cs` |
| `FavoriteModel` (nuova entità) | Nuova tabella SQLite con migrazione EF Core | `src/openMob.Core/Data/Entities/FavoriteModel.cs` |
| `AppDbContext` | Aggiunta `DbSet<FavoriteModel>` e configurazione in `OnModelCreating` | `src/openMob.Core/Data/AppDbContext.cs` |
| `IFavoriteModelService` / `FavoriteModelService` (nuovi) | Nuovo servizio CRUD per `FavoriteModel` | `src/openMob.Core/Services/IFavoriteModelService.cs`, `src/openMob.Core/Services/FavoriteModelService.cs` |
| `FavoriteModelMetadata` (nuovo record) | DTO per il passaggio dei metadati al toggle | `src/openMob.Core/Services/IFavoriteModelService.cs` (inline) o file separato |
| `CoreServiceExtensions` | Registrazione DI di `IFavoriteModelService` come Transient | `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` |

### Dependencies
- `IProviderService.GetProvidersAsync` — già implementato, nessuna modifica; i metadati del modello (DisplayName, ContextSize) sono già disponibili nella risposta
- `AppDbContext` + EF Core 9.x + SQLite — già configurati; nuova migrazione richiesta
- `ModelPickerViewModel` — già esteso dalla Spec 03 (dynamic-model-selection) con `ProviderGroups` e `OnModelSelected`; questa spec aggiunge ulteriori proprietà e comandi
- CommunityToolkit.Mvvm — già usato; nessun nuovo pacchetto NuGet richiesto
- Spec 03 (`dynamic-provider-model-selection`) — deve essere completata o in stato avanzato prima di questa, poiché questa spec estende `ModelPickerViewModel` e `ModelPickerSheet.xaml` già modificati da quella

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | La stellina deve essere visibile anche nella vista ricerca? | **Resolved** | No — nella vista ricerca (lista piatta) la stellina non è visibile. I preferiti appaiono come tutti gli altri risultati. |
| 2 | Il toggle preferito durante la ricerca attiva deve aggiornare anche `FilteredGroups` in background? | **Resolved** | `ToggleFavoriteCommand` aggiorna sempre SQLite e `IsFavorite` sull'item. `FilteredGroups` viene ricalcolato al prossimo switch alla vista raggruppata (quando `SearchText` torna vuoto). Non è necessario un aggiornamento in background durante la ricerca. |
| 3 | I preferiti sono globali o per-progetto? | **Resolved** | Globali — un unico set di preferiti condiviso tra tutti i progetti e tutte le sessioni. |
| 4 | I metadati del modello (DisplayName, ContextSize) devono essere salvati in SQLite o recuperati dal server al momento della visualizzazione? | **Resolved** | Salvati in SQLite insieme al preferito. Questo evita dipendenze dal server per la visualizzazione e garantisce che i preferiti siano riconoscibili anche se il server non è raggiungibile. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che il picker è aperto e `SearchText` è vuoto, quando l'utente visualizza la lista, allora vede i provider ordinati alfabeticamente, con i modelli preferiti in cima a ogni gruppo (ordinati by name) seguiti dai non-preferiti (ordinati by name). *(REQ-004, REQ-009)*

- [ ] **[AC-002]** Dato che il picker è aperto, quando l'utente digita un testo nella SearchBar, allora la lista passa alla vista piatta filtrata per nome modello (case-insensitive, contains), senza gruppi provider e senza stelline. *(REQ-003, REQ-005, REQ-009)*

- [ ] **[AC-003]** Dato che la SearchBar viene svuotata dopo una ricerca, quando `SearchText` torna vuoto, allora la lista torna alla vista raggruppata con l'ordinamento preferiti-first. *(REQ-003, REQ-004, REQ-009)*

- [ ] **[AC-004]** Dato che l'utente tocca la stellina outlined su un modello non-preferito nella vista raggruppata, allora: la stellina diventa filled, il modello viene salvato in SQLite con `ProviderName`, `DisplayName`, `ContextSize` e `AddedAt` (UTC), il modello si sposta in cima al suo gruppo, e il picker rimane aperto. *(REQ-006, REQ-007, REQ-011)*

- [ ] **[AC-005]** Dato che l'utente tocca la stellina filled su un modello già preferito nella vista raggruppata, allora: la stellina torna outlined, il record viene rimosso da SQLite, il modello torna nella sezione non-preferiti del suo gruppo (ordinato by name), e il picker rimane aperto. *(REQ-006, REQ-007)*

- [ ] **[AC-006]** Dato che l'app viene riavviata e il picker viene riaperto, allora i modelli precedentemente aggiunti ai preferiti mostrano la stellina filled e sono posizionati in cima al loro gruppo provider. *(REQ-001, REQ-010)*

- [ ] **[AC-007]** Dato che `IFavoriteModelService.ToggleAsync` viene chiamato con un `modelId` non presente in SQLite, allora il record viene inserito e il metodo restituisce `true`. Dato che viene chiamato con un `modelId` già presente, allora il record viene rimosso e il metodo restituisce `false`. *(REQ-002)*

- [ ] **[AC-008]** Dato che il picker è in stato di caricamento (`IsLoading == true`), allora la SearchBar è visibile ma la lista non è ancora mostrata. *(REQ-008)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Key areas to investigate

- **`ModelPickerViewModel` — stato attuale post-Spec-03:** Verificare che la Spec 03 (`dynamic-provider-model-selection`) sia già applicata prima di procedere. `ModelPickerViewModel` deve già esporre `ProviderGroups` (grouped) e `OnModelSelected`. Questa spec aggiunge `SearchText`, `FilteredGroups` (che sostituisce o affianca `ProviderGroups`), `SearchResults`, e `ToggleFavoriteCommand`.

- **`ModelItem` — mutabilità di `IsFavorite`:** `ModelItem` deve implementare `INotifyPropertyChanged` (ereditare da `ObservableObject`) per permettere il binding reattivo della stellina. Se attualmente è un `record` immutabile, va convertito in `sealed class : ObservableObject` — stesso pattern di `ChatMessage` stabilito nella Spec Chat UI Design Guidelines.

- **Riordinamento in-place del gruppo:** Quando `ToggleFavoriteCommand` viene eseguito, il gruppo `ProviderModelGroup` interessato in `FilteredGroups` deve essere riordinato. Valutare se usare `ObservableCollection<ModelItem>` per il gruppo (con `Move` / `Remove` + `Insert`) oppure ricostruire l'intera `ObservableCollection<ProviderModelGroup>`. La prima opzione è più performante e produce animazioni più fluide nella UI.

- **Switch vista raggruppata / lista piatta:** Valutare se usare due `CollectionView` sovrapposti con visibilità alternata (più semplice) oppure un singolo `CollectionView` con `IsGrouped` dinamico (più complesso, potenzialmente instabile in MAUI). La soluzione con due CollectionView distinti è preferibile per chiarezza e stabilità.

- **`SearchText` debounce:** Considerare l'aggiunta di un debounce (es. 200ms) sulla proprietà `SearchText` per evitare ricalcoli eccessivi durante la digitazione rapida. Implementabile con `CancellationTokenSource` nel setter o con un `Task.Delay` nel metodo di filtraggio.

- **`FavoriteModel` entity — PK strategy:** `ModelId` (string, formato `"providerId/modelId"`) come PK è semanticamente corretto — è un identificatore esterno univoco, non ha un ciclo di vita indipendente. Segue il pattern satellite stabilito da `ProjectPreference` (Spec 03, OQ-2 resolved).

- **`FavoriteModelService` — `ConfigureAwait(false)`:** Obbligatorio su tutti i metodi `async` del servizio, come da standard del progetto per il Core library.

- **EF Core migration:** `dotnet ef migrations add AddFavoriteModels --project src/openMob.Core/openMob.Core.csproj --startup-project src/openMob/openMob.csproj`. La migrazione è additiva (nuova tabella) — nessun rischio di data loss su installazioni esistenti.

### Suggested implementation approach

1. Creare `FavoriteModel` entity + `AppDbContext` DbSet + migrazione EF Core
2. Creare `FavoriteModelMetadata` record + `IFavoriteModelService` + `FavoriteModelService` + registrazione DI
3. Estendere `ModelItem` con `IsFavorite` (ObservableObject) — o convertire da record a sealed class
4. Estendere `ModelPickerViewModel`: aggiungere `IFavoriteModelService` come dipendenza, aggiungere `SearchText`, `FilteredGroups`, `SearchResults`, `ToggleFavoriteCommand`; aggiornare `LoadModelsCommand` per caricare i preferiti e popolare `FilteredGroups` con ordinamento corretto
5. Aggiornare `ModelPickerSheet.xaml`: aggiungere `SearchBar` in cima; aggiungere secondo `CollectionView` per la vista ricerca; aggiungere stellina inline agli item della vista raggruppata
6. Scrivere unit test per `FavoriteModelService` (toggle add/remove), `ModelPickerViewModel` (ordinamento, search, toggle in-memory)

### Constraints to respect

- `openMob.Core` deve avere zero dipendenze MAUI — `IFavoriteModelService` e `FavoriteModelService` sono pure .NET
- `ModelItem` deve ereditare da `ObservableObject` (CommunityToolkit.Mvvm) per il binding reattivo di `IsFavorite`
- Nessuna logica di business nel code-behind XAML di `ModelPickerSheet`
- `async void` solo per handler MAUI lifecycle
- `ConfigureAwait(false)` in tutti i metodi di `FavoriteModelService`
- La migrazione EF Core deve essere generata con `dotnet ef migrations add` — mai scritta a mano
- Usare `ExecuteSqlAsync` (non `ExecuteSqlInterpolatedAsync`, rimosso in EF Core 10) per eventuali query raw SQL
- Nessun nuovo pacchetto NuGet — tutte le dipendenze necessarie sono già presenti

### Related files or modules

- `src/openMob.Core/ViewModels/ModelPickerViewModel.cs` — estensione principale (search, favorites, ordinamento)
- `src/openMob.Core/Data/Entities/FavoriteModel.cs` — nuova entità
- `src/openMob.Core/Data/AppDbContext.cs` — aggiungere `DbSet<FavoriteModel>`
- `src/openMob.Core/Services/IFavoriteModelService.cs` — nuova interfaccia + `FavoriteModelMetadata` record
- `src/openMob.Core/Services/FavoriteModelService.cs` — nuova implementazione
- `src/openMob.Core/Infrastructure/DI/CoreServiceExtensions.cs` — registrazione DI
- `src/openMob/Views/Popups/ModelPickerSheet.xaml` — SearchBar + stellina + switch vista
- `tests/openMob.Tests/Services/FavoriteModelServiceTests.cs` — nuovi test
- `tests/openMob.Tests/ViewModels/ModelPickerViewModelTests.cs` — estensione test esistenti (o nuovo file)
- Spec correlata: `specs/in-progress/2026-03-16-03-dynamic-provider-model-selection.md` — prerequisito; questa spec estende il lavoro di quella
