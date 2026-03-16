# Minimalist Design System & UI Redesign — ChatGPT-Inspired

## Metadata
| Field       | Value                                    |
|-------------|------------------------------------------|
| Date        | 2026-03-16                               |
| Status      | **Completed**                            |
| Version     | 1.0                                      |
| Completed   | 2026-03-16                               |
| Branch      | feature/minimalist-ui-redesign (merged)  |
| Merged into | develop                                  |

---

## Executive Summary

openMob adotta un nuovo design system ispirato allo stile minimalista di ChatGPT: font Inter, icone Material Symbols Outlined, palette verde accent con pieno supporto light/dark. La ChatPage viene ridisegnata con topbar (selettore modello) e input bar multilinea moderna. Tutte le schermate esistenti vengono aggiornate al nuovo sistema visivo senza modifiche alla logica di business o ai ViewModel.

---

## Scope

### In Scope
- Sostituzione font: da OpenSans → **Inter** (Regular, Medium, SemiBold, Bold)
- Sostituzione sistema icone: da Unicode glyph → **Material Symbols Outlined** (TTF font)
- Aggiornamento `Colors.xaml`: nuovo accent verde, palette light/dark rivisitata
- Aggiornamento `Styles.xaml`: tutti gli stili impliciti ed espliciti aggiornati
- Registrazione font in `MauiProgram.cs`
- **ChatPage** — redesign completo (topbar, area messaggi, input bar multilinea)
- **FlyoutHeaderView / FlyoutContentView / FlyoutFooterView** — stile aggiornato
- **SplashPage** — stile aggiornato
- **OnboardingPage** e tutte le sue sub-view (5 step) — stile aggiornato
- **ProjectsPage / ProjectDetailPage** — stile aggiornato
- **SettingsPage** — stile aggiornato
- **Popup sheets**: ModelPickerSheet, AgentPickerSheet, ProjectSwitcherSheet, AddProjectSheet — stile aggiornato
- **StatusBannerView** — stile aggiornato
- Light/Dark mode completo su tutte le schermate via `AppThemeBinding`
- `ModelPickerSheet`: placeholder statico con 3 modelli Claude (Haiku, Sonnet, Opus)

### Out of Scope
- Nessuna modifica a ViewModel, Service, Repository, o logica di business
- Nessuna modifica alla struttura di navigazione (Shell, routing)
- Nessuna modifica ai file `.cs` eccetto `MauiProgram.cs` (solo registrazione font)
- Funzionalità audio/microfono: icona presente come placeholder visivo, non collegata
- Funzionalità del pulsante `+` nell'input bar: placeholder visivo, non collegato
- Icona secondaria destra nella topbar ChatPage: placeholder visivo, azione TBD
- Integrazione API per lista modelli reali (spec futura separata)

---

## Functional Requirements

> Requirements are numbered per traceability.

### Design System — Font

1. **[REQ-001]** Il font **Inter** viene adottato come font principale dell'app in quattro varianti: Regular (400), Medium (500), SemiBold (600), Bold (700). I file TTF vengono aggiunti in `src/openMob/Resources/Fonts/`. OpenSans viene rimosso.

2. **[REQ-002]** I quattro varianti Inter vengono registrati in `MauiProgram.cs` con alias: `InterRegular`, `InterMedium`, `InterSemiBold`, `InterBold`.

3. **[REQ-003]** `Styles.xaml` aggiorna tutti gli stili impliciti ed espliciti per usare `InterRegular` come font di default e le varianti appropriate per heading e label enfatizzate.

### Design System — Icone

4. **[REQ-004]** Il font **Material Symbols Outlined** (variante statica, weight 400) viene aggiunto come TTF in `src/openMob/Resources/Fonts/` e registrato in `MauiProgram.cs` con alias `MaterialSymbols`.

5. **[REQ-005]** Viene creata una classe statica `MaterialIcons` in `src/openMob/Helpers/` (o equivalente) che espone le costanti glyph Unicode usate nell'app (es. `Menu`, `Add`, `Send`, `Mic`, `Edit`, `Settings`, `ChevronRight`, `Close`, `Check`, `Folder`, `Chat`, `MoreVert`). Questa classe vive nel progetto MAUI (non in Core) poiché è puramente presentazionale.

6. **[REQ-006]** Tutti i glyph Unicode esistenti nell'app (hamburger `&#x2630;`, matita `&#x270F;`, ecc.) vengono sostituiti con i corrispondenti glyph Material Symbols tramite `FontImageSource` con `FontFamily="MaterialSymbols"`.

### Design System — Colori

7. **[REQ-007]** Il colore accent primario diventa **verde**: `#10A37F` in light mode, `#1DB88E` in dark mode. I token `ColorPrimary`, `ColorPrimaryContainer`, `ColorOnPrimary`, `ColorOnPrimaryContainer` vengono aggiornati di conseguenza.

8. **[REQ-008]** La palette background light mode: `ColorBackground` = `#FFFFFF` (bianco puro), `ColorBackgroundSecondary` = `#F7F7F8` (grigio molto chiaro). Dark mode: `ColorBackground` = `#0D0D0D`, `ColorBackgroundSecondary` = `#1A1A1A`.

9. **[REQ-009]** La palette surface light mode: `ColorSurface` = `#FFFFFF`, `ColorSurfaceSecondary` = `#F0F0F0`. Dark mode: `ColorSurface` = `#1A1A1A`, `ColorSurfaceSecondary` = `#2A2A2A`.

10. **[REQ-010]** I colori testo: `ColorOnBackground` = `#0D0D0D` / `#FFFFFF`, `ColorOnBackgroundSecondary` = `#6E6E80` / `#8E8EA0`, `ColorOnBackgroundTertiary` = `#ACACBE` / `#565869`.

11. **[REQ-011]** I token `ColorOutline` e `ColorSeparator` vengono aggiornati: light = `#E5E5E5`, dark = `#2A2A2A` — linee sottili e quasi invisibili, coerenti con lo stile minimalista.

### ChatPage — Topbar

12. **[REQ-012]** La topbar della ChatPage è una barra custom (non la NavigationBar nativa di Shell) con layout orizzontale a tre zone:
    - **Sinistra**: pulsante hamburger (icona `Menu` Material Symbols) che apre il flyout
    - **Centro**: label tappabile con testo `"openMob [NomeModello] ›"` — `NomeModello` è bindato a `ChatViewModel.SelectedModelName` (placeholder: `"Sonnet"`). Il tap esegue il comando esistente per aprire `ModelPickerSheet`.
    - **Destra**: due pulsanti icona — (1) nuova chat (icona `Edit`), (2) azione secondaria TBD (icona `MoreVert` come placeholder)

13. **[REQ-013]** La topbar ha sfondo `ColorBackground`, altezza minima 56dp, separatore inferiore sottile `ColorSeparator`. Nessuna ombra/elevation.

14. **[REQ-014]** Il testo centrale della topbar usa `InterSemiBold`, `FontSizeHeadline` (17pt), colore `ColorOnBackground`. Il suffisso `"›"` è un glyph `ChevronRight` Material Symbols inline, colore `ColorOnBackgroundSecondary`.

### ChatPage — Area Messaggi

15. **[REQ-015]** L'area messaggi ha sfondo `ColorBackground`. Nessun pattern, nessuna texture.

16. **[REQ-016]** Le bolle dei messaggi **utente** sono allineate a destra, sfondo `ColorPrimary` (verde accent), testo `ColorOnPrimary` (bianco), border radius `RadiusXl` (24) con angolo inferiore destro `RadiusSm` (8) — stile "coda" minimalista.

17. **[REQ-017]** Le bolle dei messaggi **assistant** sono allineate a sinistra, sfondo `ColorSurfaceSecondary`, testo `ColorOnSurface`, border radius `RadiusXl` (24) con angolo inferiore sinistro `RadiusSm` (8).

18. **[REQ-018]** Padding interno bolle: `SpacingMd` (12) verticale, `SpacingLg` (16) orizzontale. Margine tra bolle consecutive: `SpacingSm` (8). Margine laterale dal bordo schermo: `SpacingLg` (16). Larghezza massima bolla: 80% della larghezza schermo.

19. **[REQ-019]** Il font nelle bolle è `InterRegular`, `FontSizeBody` (17pt).

### ChatPage — Input Bar

20. **[REQ-020]** L'input bar è una barra fissa in basso, sopra la safe area, con sfondo `ColorBackground` e separatore superiore sottile `ColorSeparator`.

21. **[REQ-021]** Layout orizzontale dell'input bar:
    - **Sinistra**: pulsante `+` circolare (diametro 36dp), bordo `ColorOutline` 1.5pt, icona `Add` Material Symbols colore `ColorOnBackgroundSecondary`. Placeholder visivo, non collegato.
    - **Centro**: campo testo multilinea (vedi REQ-022 e REQ-023), flex-grow.
    - **Destra**: pulsante invia circolare (diametro 36dp), sfondo verde `ColorPrimary` quando attivo, sfondo `ColorSurfaceSecondary` quando vuoto, icona `ArrowUpward` Material Symbols colore `ColorOnPrimary` (attivo) / `ColorOnBackgroundTertiary` (vuoto).

22. **[REQ-022]** Il campo testo è un `Editor` MAUI (multilinea) con le seguenti caratteristiche:
    - Placeholder: `"Fai una domanda"`, colore `ColorOnBackgroundTertiary`
    - Sfondo: `ColorSurfaceSecondary` con border radius `RadiusFull` (999) — forma pill
    - Padding interno: `SpacingSm` (8) verticale, `SpacingMd` (12) orizzontale
    - Font: `InterRegular`, `FontSizeBody` (17pt)
    - Altezza minima: 44dp (una riga); altezza massima: 120dp (circa 5 righe)
    - L'`Editor` cresce verticalmente al crescere del testo fino al massimo di 120dp, poi diventa scrollabile internamente
    - Nessun bordo/outline visibile — il campo è integrato nella pill

23. **[REQ-023]** L'icona microfono (`Mic` Material Symbols, colore `ColorOnBackgroundSecondary`) è posizionata **all'interno** del campo pill, allineata a destra verticalmente centrata. È un placeholder visivo non collegato. Quando l'utente inizia a digitare, l'icona microfono scompare (o si nasconde) per non interferire con il testo.

24. **[REQ-024]** Il pulsante invia è abilitato (verde) quando `Editor.Text` è non-nullo e non-vuoto, disabilitato (grigio) quando il campo è vuoto. Questa logica è puramente XAML tramite `DataTrigger` o `Trigger` sull'`IsEnabled` del pulsante, bindato alla proprietà esistente nel ViewModel.

25. **[REQ-025]** Il padding orizzontale dell'input bar è `SpacingSm` (8) tra gli elementi. Il padding verticale esterno (sopra/sotto la barra) è `SpacingSm` (8).

### Flyout (Sidebar)

26. **[REQ-026]** `FlyoutHeaderView`: sfondo `ColorBackground`, logo/nome app `"openMob"` in `InterBold` `FontSizeTitle2`, icona nuova chat (`Edit`) a destra. Stile pulito, nessuna immagine profilo.

27. **[REQ-027]** `FlyoutContentView`: lista sessioni con item minimalisti — titolo sessione in `InterRegular` `FontSizeBody`, data relativa in `InterRegular` `FontSizeCaption1` colore `ColorOnBackgroundSecondary`. Separatori sottili `ColorSeparator`. Sfondo `ColorBackground`.

28. **[REQ-028]** `FlyoutFooterView`: link "Projects" e "Settings" con icone Material Symbols (`Folder`, `Settings`), font `InterMedium` `FontSizeCallout`, colore `ColorOnBackgroundSecondary`. Label versione in `FontSizeCaption2`.

### Altre Schermate

29. **[REQ-029]** **SplashPage**: sfondo `ColorBackground`, logo centrato, `ActivityIndicator` con colore `ColorPrimary`. Font Inter.

30. **[REQ-030]** **OnboardingPage** (5 step): stile aggiornato con font Inter, colori nuovi, pulsanti `PrimaryButton` verde. Icone step aggiornate a Material Symbols.

31. **[REQ-031]** **ProjectsPage / ProjectDetailPage**: card con `CardBorder` aggiornato, font Inter, icone Material Symbols, colori aggiornati.

32. **[REQ-032]** **SettingsPage**: lista impostazioni con stile minimalista, separatori sottili, font Inter.

33. **[REQ-033]** **Popup sheets** (ModelPickerSheet, AgentPickerSheet, ProjectSwitcherSheet, AddProjectSheet): handle bar in cima, sfondo `ColorSurface`, font Inter, icone Material Symbols.

34. **[REQ-034]** **ModelPickerSheet** mostra un elenco placeholder con 3 voci statiche: `"Claude Haiku"`, `"Claude Sonnet"` (selezionato di default, con checkmark verde), `"Claude Opus"`. Nessuna chiamata API in questa fase.

35. **[REQ-035]** **StatusBannerView**: aggiornato con font Inter, icone Material Symbols, colori `ColorWarning` / `ColorError` esistenti.

### Light / Dark Mode

36. **[REQ-036]** Ogni colore usato in XAML fa riferimento a un token semantico `AppThemeBinding` da `Colors.xaml`. Nessun colore hardcoded in nessun file XAML.

37. **[REQ-037]** Il cambio tema (light ↔ dark) avviene in tempo reale senza riavvio dell'app, sfruttando il meccanismo nativo `AppThemeBinding` di MAUI.

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Notes |
|-----------|--------|-------|
| `src/openMob/Resources/Fonts/` | Aggiunta font TTF | Inter (4 varianti) + Material Symbols Outlined |
| `src/openMob/MauiProgram.cs` | Modifica | Registrazione nuovi font, rimozione OpenSans |
| `src/openMob/Resources/Styles/Colors.xaml` | Modifica completa | Nuova palette verde accent, background aggiornati |
| `src/openMob/Resources/Styles/Styles.xaml` | Modifica completa | Font Inter, stili aggiornati |
| `src/openMob/Views/Pages/ChatPage.xaml` | Redesign completo | Topbar custom, input bar multilinea, bolle |
| `src/openMob/Views/Pages/ChatPage.xaml.cs` | Modifica minima | Eventuale gestione altezza Editor dinamica |
| `src/openMob/Views/Controls/FlyoutHeaderView.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Controls/FlyoutContentView.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Controls/FlyoutFooterView.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Controls/StatusBannerView.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Pages/SplashPage.xaml` | Modifica | Font + colori aggiornati |
| `src/openMob/Views/Pages/OnboardingPage.xaml` + sub-view | Modifica | Font + icone + colori aggiornati |
| `src/openMob/Views/Pages/ProjectsPage.xaml` | Modifica | Font + icone + colori aggiornati |
| `src/openMob/Views/Pages/ProjectDetailPage.xaml` | Modifica | Font + icone + colori aggiornati |
| `src/openMob/Views/Pages/SettingsPage.xaml` | Modifica | Font + icone + colori aggiornati |
| `src/openMob/Views/Popups/ModelPickerSheet.xaml` | Modifica + placeholder | Font + icone + 3 modelli statici |
| `src/openMob/Views/Popups/AgentPickerSheet.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Popups/ProjectSwitcherSheet.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Views/Popups/AddProjectSheet.xaml` | Modifica | Font + icone aggiornati |
| `src/openMob/Helpers/MaterialIcons.cs` | Nuovo file | Costanti glyph Material Symbols |

### Dependencies
- Font **Inter**: scaricabile gratuitamente da Google Fonts (OFL license). File TTF da aggiungere manualmente in `Resources/Fonts/`.
- Font **Material Symbols Outlined**: scaricabile da Google Fonts (OFL license). Usare la variante statica weight-400 per semplicità. File TTF da aggiungere manualmente in `Resources/Fonts/`.
- Nessuna nuova dipendenza NuGet richiesta.

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | Pulsante invia: verde quando testo presente, grigio quando vuoto | Resolved | Grigio quando campo vuoto, verde quando testo presente |
| 2 | Icona secondaria destra nella topbar ChatPage | Open | TBD — placeholder `MoreVert` per ora |
| 3 | Pulsante `+` nell'input bar | Resolved | Placeholder visivo, non collegato in questa fase |
| 4 | Icona microfono: scompare o si nasconde quando si digita? | Resolved | Si nasconde quando `Editor.Text` non è vuoto (via `DataTrigger`) |
| 5 | Altezza massima Editor multilinea | Resolved | 120dp (~5 righe), poi scroll interno |
| 6 | Variante Material Symbols (Outlined / Rounded / Sharp) | Resolved | Outlined, weight 400 statico |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [ ] **[AC-001]** Dato che l'app è avviata su iOS o Android, il font Inter è visibile in tutte le schermate in light e dark mode. *(REQ-001, REQ-002, REQ-003)*
- [ ] **[AC-002]** Tutte le icone dell'app usano Material Symbols Outlined e sono visivamente corrette (nessun glyph mancante o quadratino). *(REQ-004, REQ-005, REQ-006)*
- [ ] **[AC-003]** Il colore accent in tutta l'app è verde (`#10A37F` light / `#1DB88E` dark); nessun blu Apple HIG residuo nei componenti primari. *(REQ-007)*
- [ ] **[AC-004]** Nessun colore hardcoded in nessun file XAML; tutti i colori referenziano token `AppThemeBinding`. *(REQ-036)*
- [ ] **[AC-005]** Il cambio tema light/dark aggiorna l'intera UI in tempo reale senza riavvio. *(REQ-037)*
- [ ] **[AC-006]** La ChatPage topbar mostra `"openMob Sonnet ›"` (placeholder); il tap sul titolo apre il `ModelPickerSheet`. *(REQ-012, REQ-013, REQ-014)*
- [ ] **[AC-007]** Il `ModelPickerSheet` mostra tre voci statiche: Haiku, Sonnet (con checkmark verde), Opus. *(REQ-034)*
- [ ] **[AC-008]** L'input bar della ChatPage mostra: pulsante `+` circolare | campo pill multilinea | icona mic | pulsante invia circolare. *(REQ-021)*
- [ ] **[AC-009]** Il campo testo è un `Editor` multilinea: cresce da 1 riga (44dp) fino a 5 righe (120dp), poi diventa scrollabile internamente. *(REQ-022)*
- [ ] **[AC-010]** L'icona microfono è visibile quando il campo è vuoto e scompare quando l'utente inizia a digitare. *(REQ-023)*
- [ ] **[AC-011]** Il pulsante invia è verde e abilitato quando il campo contiene testo; è grigio e disabilitato quando il campo è vuoto. *(REQ-024)*
- [ ] **[AC-012]** Le bolle utente sono verdi a destra; le bolle assistant sono grigio-surface a sinistra; larghezza massima 80% schermo. *(REQ-016, REQ-017, REQ-018)*
- [ ] **[AC-013]** Tutte le schermate (Splash, Onboarding, Projects, ProjectDetail, Settings, Popups, Flyout) compilano senza errori XAML e rispettano il nuovo design system. *(REQ-029–REQ-035)*
- [ ] **[AC-014]** La build `dotnet build openMob.sln` termina con codice 0 e zero warning dopo il redesign. *(tutti i REQ)*

---

## Notes for Technical Analysis

> This section is addressed to the agent that will perform the technical implementation analysis.

### Font — Inter
- Scaricare da https://fonts.google.com/specimen/Inter i file: `Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf`
- Aggiungere in `src/openMob/Resources/Fonts/` con `MauiFont` build action (automatica in MAUI per file in `Resources/Fonts/`)
- Registrare in `MauiProgram.cs`: `fonts.AddFont("Inter-Regular.ttf", "InterRegular")` ecc.
- Rimuovere le registrazioni OpenSans e i file TTF OpenSans
- Aggiornare `Styles.xaml`: tutti gli stili impliciti che usano `FontFamily` devono passare a `InterRegular`; heading a `InterSemiBold`

### Font — Material Symbols Outlined
- Scaricare da https://fonts.google.com/icons la variante **Outlined, weight 400, grade 0, optical size 24** come file TTF statico
- File: `MaterialSymbols-Outlined.ttf` → `src/openMob/Resources/Fonts/`
- Registrare: `fonts.AddFont("MaterialSymbols-Outlined.ttf", "MaterialSymbols")`
- Creare `src/openMob/Helpers/MaterialIcons.cs` con costanti `public const string NomeIcona = "\uXXXX"` per ogni glyph usato
- Usare in XAML: `<Label FontFamily="MaterialSymbols" Text="{x:Static helpers:MaterialIcons.Send}" />`
- Oppure come `FontImageSource` per `ImageButton.Source`

### ChatPage — Editor Multilinea
- Usare `Editor` (non `Entry`) per il campo testo
- L'auto-crescita verticale si ottiene con `AutoSize="TextChanges"` su `Editor` in MAUI
- Vincolare l'altezza massima a 120dp tramite `MaximumHeightRequest="120"` — MAUI rispetta questo limite e abilita lo scroll interno dell'Editor
- Il layout dell'input bar deve usare una `Grid` o `HorizontalStackLayout` con `HorizontalOptions="FillAndExpand"` sull'Editor
- L'icona microfono inline nella pill richiede un `Grid` con overlay: `Editor` in colonna espansa + `Label` (MaterialSymbols Mic) sovrapposta a destra con `HorizontalOptions="End"` e padding adeguato. La visibilità del mic è controllata da un `DataTrigger` su `Editor.Text`

### ChatPage — Topbar Custom
- La NavigationBar nativa di Shell deve essere nascosta per ChatPage: `Shell.NavBarIsVisible="False"` già presente o da aggiungere
- La topbar custom è una `Grid` con 3 colonne (Auto | * | Auto) dentro un `Border` o `Grid` con `HeightRequest="56"`
- Il titolo centrale è un `HorizontalStackLayout` con `Label` ("openMob ") + `Label` (modello, bindato) + `Label` (glyph ChevronRight) — il tutto wrappato in un `TapGestureRecognizer` che esegue il comando `OpenModelPickerCommand`

### Colori — Aggiornamento Colors.xaml
- Come stabilito nel progetto (tech-project-scaffolding), i token `AppThemeBinding` sono definiti direttamente come `<AppThemeBinding x:Key="..." Light="..." Dark="..." />` nel ResourceDictionary — NON come `<Color>` child elements
- Aggiornare solo i token semantici (non i raw palette tokens che possono restare invariati o essere estesi con nuovi verdi)
- Aggiungere token raw per il verde: `Green400="#10A37F"`, `Green500="#0D8F6F"`, `GreenDark400="#1DB88E"` ecc.

### Stili Impliciti — Attenzione
- Come stabilito in tech-project-scaffolding: `BasedOn="{StaticResource {x:Type ...}}"` NON è supportato in MAUI XAML compiler — non usarlo
- Gli stili espliciti che estendono impliciti devono ridichiarare i setter necessari senza `BasedOn`

### Pulsante Invia — Stato Abilitato/Disabilitato
- Il `VisualStateManager` in MAUI gestisce lo stile del pulsante in stato `Disabled` — usare VSM per cambiare `BackgroundColor` tra verde (enabled) e grigio (disabled)
- Alternativa: `DataTrigger` su `IsEnabled` che cambia `BackgroundColor` — più semplice e leggibile

### Schermate da Aggiornare — Priorità Suggerita
1. `Colors.xaml` + `Styles.xaml` (base del sistema)
2. `MauiProgram.cs` + font files (prerequisito per tutto)
3. `MaterialIcons.cs` (prerequisito per icone)
4. `ChatPage.xaml` (priorità massima per la UX)
5. `FlyoutHeaderView/ContentView/FooterView.xaml`
6. Tutte le altre schermate in ordine di visibilità utente

### Rischi
- `Editor` con `AutoSize` e `MaximumHeightRequest` combinati: testare su entrambe le piattaforme — il comportamento può differire tra iOS e Android
- Material Symbols TTF statico: verificare che il file scaricato contenga tutti i glyph necessari (la variante statica weight-400 è la più completa)
- Rimozione OpenSans: verificare che nessun file XAML referenzi `OpenSansRegular` o `OpenSansSemibold` direttamente (cercare con grep prima di rimuovere i TTF)

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-16

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Feature |
| Git Flow branch | feature/minimalist-ui-redesign |
| Branches from | develop |
| Estimated complexity | High |
| Estimated agents involved | om-mobile-core (minimal — MauiProgram.cs + MaterialIcons.cs only), om-mobile-ui (primary), om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Font registration (MauiProgram.cs) | om-mobile-core | `src/openMob/MauiProgram.cs` — font registration only |
| Icon constants (MaterialIcons.cs) | om-mobile-core | `src/openMob/Helpers/MaterialIcons.cs` — new file |
| Design tokens (Colors.xaml) | om-mobile-ui | `src/openMob/Resources/Styles/Colors.xaml` |
| Styles (Styles.xaml) | om-mobile-ui | `src/openMob/Resources/Styles/Styles.xaml` |
| ChatPage redesign | om-mobile-ui | `src/openMob/Views/Pages/ChatPage.xaml`, `ChatPage.xaml.cs` |
| Flyout views | om-mobile-ui | `src/openMob/Views/Controls/Flyout*.xaml` |
| All other pages | om-mobile-ui | `src/openMob/Views/Pages/*.xaml` |
| Popup sheets | om-mobile-ui | `src/openMob/Views/Popups/*.xaml` |
| StatusBannerView | om-mobile-ui | `src/openMob/Views/Controls/StatusBannerView.xaml` |
| Code Review | om-reviewer | all of the above |

### Files to Create

- `src/openMob/Helpers/MaterialIcons.cs` — static class with Material Symbols glyph constants (REQ-005)
- `src/openMob/Resources/Fonts/Inter-Regular.ttf` — Inter Regular 400 (REQ-001) *(manual download required)*
- `src/openMob/Resources/Fonts/Inter-Medium.ttf` — Inter Medium 500 (REQ-001) *(manual download required)*
- `src/openMob/Resources/Fonts/Inter-SemiBold.ttf` — Inter SemiBold 600 (REQ-001) *(manual download required)*
- `src/openMob/Resources/Fonts/Inter-Bold.ttf` — Inter Bold 700 (REQ-001) *(manual download required)*
- `src/openMob/Resources/Fonts/MaterialSymbols-Outlined.ttf` — Material Symbols icon font (REQ-004) *(manual download required)*

### Files to Modify

- `src/openMob/MauiProgram.cs` — replace OpenSans font registrations with Inter (4 variants) + MaterialSymbols (REQ-002, REQ-004)
- `src/openMob/Resources/Styles/Colors.xaml` — new green accent palette, updated backgrounds, surfaces, text colors, outlines (REQ-007 through REQ-011)
- `src/openMob/Resources/Styles/Styles.xaml` — add FontFamily setters to all implicit/explicit styles (REQ-003)
- `src/openMob/Views/Pages/ChatPage.xaml` — complete redesign: topbar with model selector, message area with bubbles, multiline input bar (REQ-012 through REQ-025)
- `src/openMob/Views/Pages/ChatPage.xaml.cs` — minimal changes for Editor height management and hamburger handler (already exists)
- `src/openMob/Views/Controls/FlyoutHeaderView.xaml` — Inter font, Material Symbols Edit icon (REQ-026)
- `src/openMob/Views/Controls/FlyoutContentView.xaml` — Inter font, updated styling (REQ-027)
- `src/openMob/Views/Controls/FlyoutFooterView.xaml` — Inter font, Material Symbols Folder/Settings icons (REQ-028)
- `src/openMob/Views/Pages/SplashPage.xaml` — Inter font, updated colors (REQ-029)
- `src/openMob/Views/Pages/OnboardingPage.xaml` — Inter font, Material Symbols icons, green buttons (REQ-030)
- `src/openMob/Views/Controls/OnboardingWelcomeView.xaml` — Inter font, Material Symbols (REQ-030)
- `src/openMob/Views/Controls/OnboardingConnectServerView.xaml` — Inter font, Material Symbols (REQ-030)
- `src/openMob/Views/Controls/OnboardingProviderSetupView.xaml` — Inter font, Material Symbols (REQ-030)
- `src/openMob/Views/Controls/OnboardingPermissionsView.xaml` — Inter font, Material Symbols (REQ-030)
- `src/openMob/Views/Controls/OnboardingCompletionView.xaml` — Inter font, Material Symbols (REQ-030)
- `src/openMob/Views/Pages/ProjectsPage.xaml` — Inter font, Material Symbols icons (REQ-031)
- `src/openMob/Views/Pages/ProjectDetailPage.xaml` — Inter font, Material Symbols icons (REQ-031)
- `src/openMob/Views/Pages/SettingsPage.xaml` — redesign from stub to minimalist settings list (REQ-032)
- `src/openMob/Views/Popups/ModelPickerSheet.xaml` — Inter font, Material Symbols, 3 static Claude models (REQ-033, REQ-034)
- `src/openMob/Views/Popups/AgentPickerSheet.xaml` — Inter font, Material Symbols (REQ-033)
- `src/openMob/Views/Popups/ProjectSwitcherSheet.xaml` — Inter font, Material Symbols (REQ-033)
- `src/openMob/Views/Popups/AddProjectSheet.xaml` — Inter font, Material Symbols (REQ-033)
- `src/openMob/Views/Controls/StatusBannerView.xaml` — Inter font, Material Symbols (REQ-035)

### Files to Delete

- `src/openMob/Resources/Fonts/OpenSans-Regular.ttf` — replaced by Inter (REQ-001)
- `src/openMob/Resources/Fonts/OpenSans-Semibold.ttf` — replaced by Inter (REQ-001)

### Technical Dependencies

- **Font files must be downloaded manually** by the user before agents can begin work. The TTF files cannot be generated by agents:
  - Inter (4 weights): https://fonts.google.com/specimen/Inter
  - Material Symbols Outlined (static, weight 400): https://fonts.google.com/icons
- **No new NuGet packages required** — all functionality uses existing MAUI APIs
- **No ViewModel changes** — the spec explicitly excludes business logic changes. The ChatPage topbar will bind to existing `ChatViewModel` properties. The `SelectedModelName` property referenced in REQ-012 does not exist yet in `ChatViewModel` — the UI will use a hardcoded placeholder `"Sonnet"` for now (consistent with spec's "placeholder" note)
- **No API endpoints involved** — ModelPickerSheet uses static data (REQ-034)
- **Existing in-progress spec** `2026-03-14-chat-ui-design-guidelines.md` defines the chat layout guidelines. This redesign spec supersedes the visual aspects while maintaining the structural patterns defined there.

### Technical Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| `Editor` with `AutoSize="TextChanges"` + `MaximumHeightRequest` may behave differently on iOS vs Android | Medium | Test on both platforms; fallback to code-behind height clamping if needed |
| Material Symbols TTF static variant may not contain all required glyphs | Low | Verify glyph codes before implementation; the static weight-400 variant is the most complete |
| Removing OpenSans while no `FontFamily` is set in XAML | Low | Verified: no XAML file references `OpenSansRegular` or `OpenSansSemibold` — clean swap. Adding `FontFamily` setters in Styles.xaml ensures Inter is used everywhere |
| `Colors.xaml` token format: spec note says use `AppThemeBinding` directly — but current file uses `<Color>` pairs with `Light`/`Dark` suffix convention | Medium | Maintain the existing `<Color x:Key="ColorXxxLight/Dark">` pattern already established in the codebase. Do NOT change the token architecture — only update the hex values |
| ChatPage message bubbles reference a message collection that doesn't exist in ChatViewModel yet | Low | Use a placeholder empty state with static sample bubbles for visual verification. The actual message binding will come in a future spec |
| `RoundRectangle` with asymmetric corner radii (REQ-016, REQ-017) requires `CornerRadius="24,24,8,24"` syntax | Low | Supported in MAUI `RoundRectangle` — verify syntax |

### Execution Order

> Steps that can run in parallel are marked with ⟳. Steps that must be sequential are numbered.

1. **[User action]** Download font TTF files (Inter 4 weights + Material Symbols Outlined) and place them in `src/openMob/Resources/Fonts/`. Delete `OpenSans-Regular.ttf` and `OpenSans-Semibold.ttf`.
2. **[om-mobile-core]** Update `MauiProgram.cs` font registrations + create `Helpers/MaterialIcons.cs` (REQ-002, REQ-004, REQ-005)
3. **[om-mobile-ui]** Update `Colors.xaml` with new green accent palette and updated semantic tokens (REQ-007 through REQ-011)
4. **[om-mobile-ui]** Update `Styles.xaml` with `FontFamily` setters for Inter across all implicit/explicit styles (REQ-003)
5. ⟳ **[om-mobile-ui]** Redesign `ChatPage.xaml` — topbar, message area, input bar (REQ-012 through REQ-025) — can start once steps 2-4 are complete
6. ⟳ **[om-mobile-ui]** Update Flyout views (REQ-026, REQ-027, REQ-028) — can run in parallel with step 5
7. ⟳ **[om-mobile-ui]** Update all other pages: SplashPage, OnboardingPage + 5 sub-views, ProjectsPage, ProjectDetailPage, SettingsPage (REQ-029 through REQ-032) — can run in parallel with steps 5-6
8. ⟳ **[om-mobile-ui]** Update all popup sheets: ModelPickerSheet (with 3 static models), AgentPickerSheet, ProjectSwitcherSheet, AddProjectSheet, StatusBannerView (REQ-033 through REQ-035) — can run in parallel with steps 5-7
9. **[om-reviewer]** Full review against spec — all REQ and AC items
10. **[Fix loop if needed]** Address Critical and Major findings
11. **[Git Flow]** Finish branch and merge

### Special Notes

- **No om-tester involvement**: This feature is purely UI/XAML with no new business logic, ViewModels, or Services. The only `.cs` changes are `MauiProgram.cs` (font registration — trivial, not testable) and `MaterialIcons.cs` (static constants — trivial, not testable). Existing tests should continue to pass unchanged.
- **om-mobile-core involvement is minimal**: Only `MauiProgram.cs` font registration and `MaterialIcons.cs` creation. Both are in the MAUI project (not Core), but om-mobile-core handles `MauiProgram.cs` changes per convention.
- **The ChatPage message bubbles (REQ-015 through REQ-019)** define the visual template for messages, but `ChatViewModel` has no message collection yet. The UI agent should create the bubble DataTemplates inside a `CollectionView` bound to a future `Messages` property, with a visible empty state. For visual verification, 2-3 hardcoded sample bubbles can be included as static XAML (commented out or in a design-time data context).

### Definition of Done

- [ ] All `[REQ-001]` through `[REQ-037]` requirements implemented
- [ ] All `[AC-001]` through `[AC-014]` acceptance criteria satisfied
- [ ] No unit tests needed (pure UI feature — no new business logic)
- [ ] `om-reviewer` verdict: ✅ Approved or ⚠️ Approved with remarks
- [ ] `dotnet build openMob.sln` exits with code 0 and zero warnings
- [ ] Git Flow branch finished and deleted
- [ ] Spec moved to `specs/done/` with Completed status
