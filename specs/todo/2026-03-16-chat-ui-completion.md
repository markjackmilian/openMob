# Chat UI Completion — Correzioni Strutturali, Componenti Mancanti e Wiring

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-16                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

Completa l'implementazione UI della chat risolvendo tutti i gap strutturali identificati rispetto alla spec `2026-03-14-chat-ui-design-guidelines.md`: sposta i converter nel progetto corretto (`openMob.Core`), aggiunge i design token mancanti, crea i componenti XAML assenti (`MessageBubbleView`, `InputBarView`, `EmptyStateView`, `SuggestionChipView`), collega il pulsante Send al `SendMessageCommand`, corregge il pulsante "New Chat" nel flyout, e attiva la `CollectionView` messaggi in `ChatPage.xaml`. Questa spec è puramente UI/XAML e dipende dal contratto di binding definito in Spec B.

---

## Scope

### In Scope
- Spostamento di `BoolToVisibilityConverter` e `DateTimeToRelativeStringConverter` da `src/openMob/Converters/` a `src/openMob.Core/Converters/`
- Creazione di `MessageStatusToIconConverter` in `src/openMob.Core/Converters/`
- Aggiunta dei 10 design token chat-specifici in `Colors.xaml` (REQ-027 della spec guidelines)
- Aggiunta degli stili espliciti per i componenti chat in `Styles.xaml`
- Creazione di `MessageBubbleView.xaml` + code-behind
- Creazione di `InputBarView.xaml` + code-behind
- Creazione di `EmptyStateView.xaml` + code-behind
- Creazione di `SuggestionChipView.xaml` + code-behind
- Attivazione della `CollectionView` messaggi in `ChatPage.xaml` (rimozione del commento, binding a `Messages`)
- Collegamento del pulsante Send a `SendMessageCommand`
- Collegamento del pulsante "New Chat" in `FlyoutHeaderView` a `FlyoutViewModel.NewChatCommand`
- Aggiornamento registrazione converter in `App.xaml` o `MauiProgram.cs`
- Rimozione dei converter duplicati dal progetto MAUI dopo lo spostamento

### Out of Scope
- Logica di business (Spec B)
- Unit test (Spec D)
- Animazioni avanzate oltre quelle già specificate in `2026-03-14-chat-ui-design-guidelines.md`
- Visualizzazione rich content (code blocks, immagini) — spec futura
- Typing indicator animato — spec futura (deferred in guidelines spec)
- Long-press context menu sui messaggi — spec futura

---

## Functional Requirements

> Requirements are numbered for traceability.

### Converter — Spostamento e Creazione

1. **[REQ-001]** Spostare `BoolToVisibilityConverter` da `src/openMob/Converters/BoolToVisibilityConverter.cs` a `src/openMob.Core/Converters/BoolToVisibilityConverter.cs`. Il namespace deve diventare `openMob.Core.Converters`. Rimuovere il file originale dal progetto MAUI.

2. **[REQ-002]** Spostare `DateTimeToRelativeStringConverter` da `src/openMob/Converters/DateTimeToRelativeStringConverter.cs` a `src/openMob.Core/Converters/DateTimeToRelativeStringConverter.cs`. Il namespace deve diventare `openMob.Core.Converters`. Rimuovere il file originale dal progetto MAUI.

3. **[REQ-003]** Creare `MessageStatusToIconConverter` in `src/openMob.Core/Converters/MessageStatusToIconConverter.cs`. Implementa `IValueConverter`. Converte `MessageDeliveryStatus` → stringa glyph/resource name:
   - `Sending` → `"clock_icon"` (o glyph Unicode equivalente da Material Icons / SF Symbols)
   - `Sent` → `"check_icon"`
   - `Error` → `"error_icon"`
   - Qualsiasi altro valore → `string.Empty`

4. **[REQ-004]** Tutti i converter in `openMob.Core.Converters` devono essere classi `public sealed` che implementano `IValueConverter` da `System.Windows.Input` (o `Microsoft.Maui.Controls` — verificare quale è disponibile senza dipendenze MAUI in Core). **Nota**: se `IValueConverter` richiede una dipendenza MAUI, definire un'interfaccia custom `ICoreValueConverter` in Core e un adapter nel progetto MAUI.

5. **[REQ-005]** Aggiornare `App.xaml` (o `MauiProgram.cs`) per registrare i converter dalla nuova posizione in `openMob.Core`. Rimuovere le registrazioni precedenti che puntavano al namespace MAUI.

### Design Token — Colors.xaml

6. **[REQ-006]** Aggiungere i seguenti 10 token in `src/openMob/Resources/Styles/Colors.xaml` con `AppThemeBinding` corretto (valori raw palette, non riferimenti ad altri token):

   | Token | Tipo | Light | Dark |
   |---|---|---|---|
   | `ColorBubbleSent` | Color | stesso valore raw di `ColorPrimary` light | stesso valore raw di `ColorPrimary` dark |
   | `ColorBubbleReceived` | Color | stesso valore raw di `ColorSurface` light | stesso valore raw di `ColorSurface` dark |
   | `ColorOnBubbleSent` | Color | stesso valore raw di `ColorOnPrimary` light | stesso valore raw di `ColorOnPrimary` dark |
   | `ColorOnBubbleReceived` | Color | stesso valore raw di `ColorOnSurface` light | stesso valore raw di `ColorOnSurface` dark |
   | `ColorInputBarBackground` | Color | stesso valore raw di `ColorSurface` light | stesso valore raw di `ColorSurface` dark |
   | `ColorInputFieldBackground` | Color | stesso valore raw di `ColorSurfaceSecondary` light | stesso valore raw di `ColorSurfaceSecondary` dark |
   | `SizeBubbleMaxWidth` | x:Double | 0.80 | 0.80 |
   | `SizeAvatarSmall` | x:Double | 28 | 28 |
   | `SizeFlyoutWidth` | x:Double | 0.80 | 0.80 |
   | `SizeFlyoutMaxWidth` | x:Double | 320 | 320 |

   I token `x:Double` sono theme-independent e definiti una sola volta (non con `AppThemeBinding`).

### Stili — Styles.xaml

7. **[REQ-007]** Aggiungere i seguenti stili espliciti in `src/openMob/Resources/Styles/Styles.xaml`:
   - `MessageBubbleSentBorder` — `Border` style: `BackgroundColor = {ColorBubbleSent}`, `StrokeShape = RoundRectangle` con corner radius `16,16,4,16` (top-left, top-right, bottom-left, bottom-right)
   - `MessageBubbleReceivedBorder` — `Border` style: `BackgroundColor = {ColorBubbleReceived}`, `StrokeShape = RoundRectangle` con corner radius `16,16,16,4`
   - `InputBarEditor` — `Editor` style: `BackgroundColor = {ColorInputFieldBackground}`, `FontSize = {FontSizeBody}`, `PlaceholderColor = {ColorOnSurfaceTertiary}`
   - `SuggestionChipBorder` — `Border` style: `BackgroundColor = {ColorSurface}`, `Stroke = {ColorOutline}`, `StrokeThickness = 1`, `StrokeShape = RoundRectangle 12`

### MessageBubbleView

8. **[REQ-008]** Creare `src/openMob/Views/Controls/MessageBubbleView.xaml` come `ContentView` con i seguenti `BindableProperty`:
   - `TextContent` (string) — testo del messaggio
   - `IsFromUser` (bool) — determina allineamento e colori
   - `Timestamp` (DateTimeOffset) — mostrato formattato sotto il testo
   - `DeliveryStatus` (MessageDeliveryStatus) — icona stato (solo per messaggi utente)
   - `IsFirstInGroup` (bool) — riduce il padding superiore se `false`
   - `IsLastInGroup` (bool) — mostra/nasconde avatar se `false`
   - `IsStreaming` (bool) — mostra indicatore di streaming (pulsing dots o cursore lampeggiante)

9. **[REQ-009]** Layout `MessageBubbleView`:
   - Messaggi utente (`IsFromUser = true`): allineati a destra, bubble con stile `MessageBubbleSentBorder`, testo `ColorOnBubbleSent`, max width 80% via `HorizontalOptions = End` + `MaximumWidthRequest`
   - Messaggi AI (`IsFromUser = false`): allineati a sinistra, bubble con stile `MessageBubbleReceivedBorder`, testo `ColorOnBubbleReceived`, avatar `SizeAvatarSmall` a sinistra (visibile solo se `IsLastInGroup = true`)
   - Timestamp sotto il testo: `FontSizeCaption2`, formattato via `DateTimeToRelativeStringConverter`
   - Icona delivery status accanto al timestamp (solo se `IsFromUser = true`): via `MessageStatusToIconConverter`
   - Indicatore streaming: tre puntini animati (opacity animation) visibili solo se `IsStreaming = true` e `IsFromUser = false`

### InputBarView

10. **[REQ-010]** Creare `src/openMob/Views/Controls/InputBarView.xaml` come `ContentView` con i seguenti `BindableProperty`:
    - `Text` (string, TwoWay) — testo corrente, bindato a `ChatViewModel.InputText`
    - `SendCommand` (ICommand) — bindato a `ChatViewModel.SendMessageCommand`
    - `IsEnabled` (bool) — disabilita l'intera barra durante `IsBusy`
    - `Placeholder` (string) — testo placeholder, default `"Message..."`

11. **[REQ-011]** Layout `InputBarView`:
    - Background: `ColorInputBarBackground`, top separator 1px `ColorSeparator`
    - Da sinistra a destra: pulsante Attach (+, 44x44pt, non funzionale — placeholder), `Editor` con `AutoSize="TextChanges"` e `MaximumHeightRequest` per max 5 righe, pulsante Send (cerchio 36pt, `ColorPrimary`, icona freccia su)
    - Il pulsante Send è visibile solo quando `Text` non è vuoto (via `BoolToVisibilityConverter` su `IsVisible`)
    - Quando `Text` è vuoto, al posto del Send appare un'icona microfono (non funzionale — placeholder)
    - Il pulsante Send ha `Command = {SendCommand}`

### EmptyStateView

12. **[REQ-012]** Creare `src/openMob/Views/Controls/EmptyStateView.xaml` come `ContentView` con i seguenti `BindableProperty`:
    - `Title` (string) — default `"How can I help you?"`
    - `Subtitle` (string) — default `string.Empty`
    - `IsVisible` (bool) — bindato a `ChatViewModel.IsEmpty`

13. **[REQ-013]** Layout `EmptyStateView`: icona app 64pt (`ColorOnBackgroundTertiary`), titolo `FontSizeTitle2` centrato, sottotitolo `FontSizeSubheadline` centrato. Posizionato al 40% dall'alto nell'area messaggi.

### SuggestionChipView

14. **[REQ-014]** Creare `src/openMob/Views/Controls/SuggestionChipView.xaml` come `ContentView` con i seguenti `BindableProperty`:
    - `Title` (string)
    - `Subtitle` (string)
    - `TapCommand` (ICommand) — bindato a `ChatViewModel.SelectSuggestionChipCommand`
    - `CommandParameter` (object) — il `SuggestionChip` model

15. **[REQ-015]** Layout `SuggestionChipView`: stile `SuggestionChipBorder`, min width 200pt, max width 280pt. Testo su due righe: `Title` in `FontSizeCallout` bold, `Subtitle` in `FontSizeCallout` regular `ColorOnSurfaceSecondary`. Wrapped in `TapGestureRecognizer`.

### ChatPage.xaml — Attivazione CollectionView e Wiring

16. **[REQ-016]** In `ChatPage.xaml`, sostituire il blocco messaggi commentato con una `CollectionView` funzionante:
    - `ItemsSource = {Binding Messages}`
    - `ItemsUpdatingScrollMode = KeepLastItemInView`
    - `DataTemplate` che usa `MessageBubbleView` con binding a tutte le proprietà del `ChatMessage`
    - Visibile solo quando `IsEmpty = false`

17. **[REQ-017]** In `ChatPage.xaml`, sostituire il placeholder statico empty-state con `EmptyStateView`:
    - `IsVisible = {Binding IsEmpty}`
    - `Title = "How can I help you?"`

18. **[REQ-018]** In `ChatPage.xaml`, aggiungere la `CollectionView` per i suggestion chips:
    - `ItemsSource = {Binding SuggestionChips}`
    - `ItemsLayout = HorizontalLinearItemsLayout`
    - `DataTemplate` che usa `SuggestionChipView`
    - Visibile solo quando `IsEmpty = true`
    - Posizionata sopra `InputBarView`

19. **[REQ-019]** In `ChatPage.xaml`, sostituire il codice-behind del pulsante Send con il binding al comando:
    - Rimuovere la logica di cambio colore da `OnMessageEditorTextChanged` nel code-behind
    - Collegare `InputBarView.SendCommand = {Binding SendMessageCommand}`
    - Collegare `InputBarView.Text = {Binding InputText, Mode=TwoWay}`

20. **[REQ-020]** In `ChatPage.xaml`, aggiungere un banner di errore (o usare `StatusBannerView` esistente) bindato a `ChatViewModel.HasError` e `ChatViewModel.ErrorMessage`, con pulsante dismiss bindato a `DismissErrorCommand`.

### FlyoutHeaderView — Fix New Chat

21. **[REQ-021]** In `src/openMob/Views/Controls/FlyoutHeaderView.xaml.cs`, sostituire lo stub `OnNewChatTapped` con il binding al comando `FlyoutViewModel.NewChatCommand`:
    - Rimuovere il code-behind `OnNewChatTapped` con `await Task.CompletedTask`
    - Aggiungere `Command = {Binding NewChatCommand}` al pulsante "New Chat" nel XAML

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob.Core/Converters/BoolToVisibilityConverter.cs` | Nuovo file (spostato) | Da `src/openMob/Converters/` |
| `src/openMob.Core/Converters/DateTimeToRelativeStringConverter.cs` | Nuovo file (spostato) | Da `src/openMob/Converters/` |
| `src/openMob.Core/Converters/MessageStatusToIconConverter.cs` | Nuovo file | Creato da zero |
| `src/openMob/Converters/BoolToVisibilityConverter.cs` | Eliminato | Sostituito da Core |
| `src/openMob/Converters/DateTimeToRelativeStringConverter.cs` | Eliminato | Sostituito da Core |
| `src/openMob/Resources/Styles/Colors.xaml` | Modifica | +10 token chat |
| `src/openMob/Resources/Styles/Styles.xaml` | Modifica | +4 stili espliciti |
| `src/openMob/Views/Controls/MessageBubbleView.xaml` | Nuovo file | |
| `src/openMob/Views/Controls/InputBarView.xaml` | Nuovo file | |
| `src/openMob/Views/Controls/EmptyStateView.xaml` | Nuovo file | |
| `src/openMob/Views/Controls/SuggestionChipView.xaml` | Nuovo file | |
| `src/openMob/Views/Pages/ChatPage.xaml` | Modifica maggiore | Attivazione CollectionView, wiring comandi |
| `src/openMob/Views/Controls/FlyoutHeaderView.xaml` + `.cs` | Modifica | Fix New Chat command binding |
| `src/openMob/App.xaml` | Modifica | Aggiornamento registrazione converter |
| `src/openMob/MauiProgram.cs` | Modifica minore | Eventuale registrazione nuovi converter |

### Dependencies
- **Spec B (`chat-conversation-loop`)** — `ChatViewModel` deve esporre `Messages`, `InputText`, `SendMessageCommand`, `IsEmpty`, `SuggestionChips`, `ErrorMessage`, `DismissErrorCommand` prima che questa spec possa fare il wiring XAML
- `ChatMessage` model (Spec B) — `MessageBubbleView` fa binding alle sue proprietà
- `MessageDeliveryStatus` enum (Spec B) — usato da `MessageStatusToIconConverter`
- `SuggestionChip` model (Spec B) — usato da `SuggestionChipView`
- `FlyoutViewModel.NewChatCommand` — già definito, solo il binding XAML mancava

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `IValueConverter` in `openMob.Core` — dipendenza MAUI? | Open | Verificare se `Microsoft.Maui.Controls.IValueConverter` può essere referenziato da un progetto `net10.0` puro. Se no, usare un adapter pattern: interfaccia custom in Core + wrapper MAUI nel progetto UI. |
| 2 | Icone per `MessageStatusToIconConverter` — usare Unicode glyphs, FontAwesome, o Material Icons già inclusi? | Open | Verificare quale font icon è già referenziato nel progetto. Usare quello esistente per coerenza. |
| 3 | L'indicatore di streaming (pulsing dots) — implementare come animazione XAML pura o con Lottie? | Resolved | Animazione XAML pura (opacity animation su tre `Ellipse`) per non aggiungere dipendenze. Lottie è disponibile ma non necessario per questo caso. |
| 4 | `MaximumWidthRequest` per le bubble — MAUI supporta percentuale della larghezza schermo? | Resolved | No, `MaximumWidthRequest` è in pt assoluti. Calcolare in code-behind di `ChatPage` usando `DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density * 0.80` e impostarlo come `BindableProperty` su `MessageBubbleView`, oppure usare un `Behavior`. |
| 5 | I converter spostati in Core devono essere re-registrati in `App.xaml` come `StaticResource`? | Resolved | Sì. Aggiornare il namespace XML in `App.xaml` per puntare a `openMob.Core.Converters` invece di `openMob.Converters`. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato il progetto compilato, quando si ispeziona `openMob.Tests`, allora `BoolToVisibilityConverter` e `DateTimeToRelativeStringConverter` sono referenziabili e testabili senza dipendenze MAUI. *(REQ-001, REQ-002, REQ-004)*
- [ ] **[AC-002]** Dato `MessageDeliveryStatus.Sending`, quando `MessageStatusToIconConverter.Convert` viene chiamato, allora restituisce la stringa glyph per l'icona clock. *(REQ-003)*
- [ ] **[AC-003]** Dato `Colors.xaml` ispezionato, quando si cercano i token chat, allora tutti e 10 i token definiti in REQ-006 sono presenti con `AppThemeBinding` corretto. *(REQ-006)*
- [ ] **[AC-004]** Dato un `ChatMessage` con `IsFromUser = true`, quando `MessageBubbleView` lo renderizza, allora la bubble è allineata a destra con background `ColorBubbleSent` e mostra l'icona delivery status. *(REQ-008, REQ-009)*
- [ ] **[AC-005]** Dato un `ChatMessage` con `IsFromUser = false` e `IsStreaming = true`, quando `MessageBubbleView` lo renderizza, allora l'indicatore di streaming (pulsing dots) è visibile. *(REQ-008, REQ-009)*
- [ ] **[AC-006]** Dato `InputBarView` con `Text` vuoto, quando si ispeziona la UI, allora il pulsante Send è nascosto e l'icona microfono è visibile. *(REQ-010, REQ-011)*
- [ ] **[AC-007]** Dato `InputBarView` con testo presente, quando si tappa il pulsante Send, allora `SendCommand` viene eseguito. *(REQ-010, REQ-011)*
- [ ] **[AC-008]** Dato `ChatViewModel.IsEmpty = true`, quando `ChatPage` è visualizzata, allora `EmptyStateView` è visibile, la `CollectionView` messaggi è nascosta, e i suggestion chips sono visibili. *(REQ-016, REQ-017, REQ-018)*
- [ ] **[AC-009]** Dato `ChatViewModel.IsEmpty = false`, quando `ChatPage` è visualizzata, allora la `CollectionView` messaggi è visibile con le bubble corrette e `EmptyStateView` è nascosta. *(REQ-016, REQ-017)*
- [ ] **[AC-010]** Dato il flyout aperto, quando l'utente tappa "New Chat", allora `FlyoutViewModel.NewChatCommand` viene eseguito (non più uno stub). *(REQ-021)*
- [ ] **[AC-011]** Dato `ChatViewModel.HasError = true`, quando `ChatPage` è visualizzata, allora il banner di errore è visibile con `ErrorMessage`; quando l'utente tappa dismiss, `DismissErrorCommand` viene eseguito e il banner scompare. *(REQ-020)*
- [ ] **[AC-012]** Dato il device in dark mode, quando `ChatPage` è visualizzata, allora tutte le bubble, l'input bar e i chip usano i token dark senza colori hardcoded. *(da REQ-006, REQ-007)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

- **`IValueConverter` e layer separation**: `Microsoft.Maui.Controls.IValueConverter` è definito nel package `Microsoft.Maui.Controls` che NON è referenziabile da un progetto `net10.0` puro (solo da `net10.0-ios`, `net10.0-android`). La soluzione raccomandata è: definire i converter come classi pure in `openMob.Core` che implementano `object Convert(object value, ...)` senza implementare formalmente `IValueConverter`, e creare thin wrapper nel progetto MAUI che implementano `IValueConverter` e delegano al Core. Alternativa più semplice: aggiungere `<TargetFrameworks>net10.0;net10.0-ios;net10.0-android</TargetFrameworks>` a `openMob.Core.csproj` — ma questo viola la regola "zero MAUI dependencies". Scegliere l'approccio adapter.
- **`AppThemeBinding` con valori raw**: come stabilito nella Technical Analysis di `2026-03-14-chat-ui-design-guidelines.md`, i token bubble devono usare valori raw palette (es. `#007AFF`) non riferimenti ad altri token, perché MAUI non supporta `DynamicResource` dentro `AppThemeBinding`.
- **`MessageBubbleView` max width**: usare `BindingContext`-aware behavior oppure calcolare in `ChatPage.xaml.cs` la larghezza massima e passarla come `BindableProperty`. Evitare calcoli in converter (violazione SRP).
- **Animazione streaming**: tre `Ellipse` in `HorizontalStackLayout`, ciascuna con `Animation` in loop su `Opacity` (0→1→0) con delay sfasato (0ms, 200ms, 400ms). Avviare/fermare l'animazione nel code-behind di `MessageBubbleView` tramite `PropertyChanged` su `IsStreaming`.
- **Rimozione converter dal progetto MAUI**: dopo lo spostamento, verificare che nessun altro file nel progetto MAUI referenzi il vecchio namespace `openMob.Converters`. Aggiornare tutti i namespace XML in XAML.
- **`FlyoutHeaderView` binding context**: verificare che `FlyoutHeaderView` abbia accesso a `FlyoutViewModel` come `BindingContext`. Attualmente il binding context potrebbe non essere propagato correttamente dal `AppShell`. Potrebbe essere necessario usare `x:Reference` o `RelativeSource` per raggiungere il ViewModel.
- **As established in `specs/in-progress/2026-03-14-chat-ui-design-guidelines.md`**: usare `CollectionView` (non `ListView`), `Border` (non `Frame`), `VerticalStackLayout` (non `StackLayout`). Nessun colore hardcoded.
