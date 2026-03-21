# Tabler Icons — Font File Integration & Codepoint Migration

## Metadata
| Field   | Value                        |
|---------|------------------------------|
| Date    | 2026-03-21                   |
| Status  | Draft                        |
| Version | 1.0                          |

---

## Executive Summary

La feature `ui-overhaul-design-system` ha completato tutta l'architettura necessaria per Tabler Icons: `IconKeys.cs` centralizzato, stili `IconLabel`/`IconLabelLg`, font alias `TablerIcons` registrato in `MauiProgram.cs`, e tutti i 68 riferimenti icone migrati in XAML. Tuttavia i codepoint Unicode in `IconKeys.cs` sono ancora quelli di **MaterialSymbols** (il vecchio font), non di Tabler Icons. Questa spec completa la migrazione: sostituisce il file font fisico e aggiorna tutti i 36 codepoint con i valori corretti di Tabler Icons.

---

## Contesto Tecnico

### Stato attuale (dopo merge di `ui-overhaul-design-system`)

- `src/openMob/Resources/Fonts/MaterialSymbols-Outlined.ttf` — font fisico ancora presente
- `src/openMob/MauiProgram.cs` — font registrato con alias `"TablerIcons"` ma punta a `MaterialSymbols-Outlined.ttf`
- `src/openMob/openMob.csproj` — `<MauiFont>` punta a `MaterialSymbols-Outlined.ttf`
- `src/openMob/Helpers/IconKeys.cs` — 36 costanti con codepoint MaterialSymbols (es. `"\ue5d2"`)
- Tutti i file XAML usano già `{x:Static helpers:IconKeys.XxxName}` — **nessun XAML da modificare**

### Obiettivo

- Sostituire `MaterialSymbols-Outlined.ttf` con `TablerIcons.ttf`
- Aggiornare i 36 codepoint in `IconKeys.cs` con i valori corretti di Tabler Icons
- Aggiornare `openMob.csproj` e `MauiProgram.cs`

---

## Scope

### In Scope
- Aggiunta di `TablerIcons.ttf` in `src/openMob/Resources/Fonts/`
- Rimozione di `MaterialSymbols-Outlined.ttf`
- Aggiornamento `<MauiFont>` in `openMob.csproj`
- Aggiornamento registrazione font in `MauiProgram.cs`
- Aggiornamento dei 36 codepoint in `src/openMob/Helpers/IconKeys.cs`
- Rimozione del commento `"Currently uses MaterialSymbols codepoints"` da `IconKeys.cs`
- Verifica visiva che tutte le icone renderino correttamente

### Out of Scope
- Modifiche a qualsiasi file XAML (già migrati nella feature precedente)
- Modifiche a ViewModel, servizi, test
- Aggiunta di nuove icone non già presenti in `IconKeys.cs`

---

## Functional Requirements

1. **[REQ-001]** Il file `TablerIcons.ttf` (Tabler Icons webfont, MIT licence) deve essere presente in `src/openMob/Resources/Fonts/` e registrato come `<MauiFont>` in `openMob.csproj`.

2. **[REQ-002]** Il file `MaterialSymbols-Outlined.ttf` deve essere rimosso da `src/openMob/Resources/Fonts/` e da `openMob.csproj`.

3. **[REQ-003]** In `MauiProgram.cs`, la registrazione del font deve puntare a `TablerIcons.ttf` con alias `"TablerIcons"` (l'alias è già corretto — solo il file sorgente cambia).

4. **[REQ-004]** Tutti i 36 codepoint in `src/openMob/Helpers/IconKeys.cs` devono essere aggiornati con i valori Unicode corretti di Tabler Icons webfont.

5. **[REQ-005]** Il commento `<remarks>` in `IconKeys.cs` che dice *"Currently uses MaterialSymbols codepoints"* deve essere rimosso o aggiornato per riflettere l'uso di Tabler Icons.

6. **[REQ-006]** Il progetto deve buildare con zero errori e zero warning dopo la migrazione.

7. **[REQ-007]** Nessun file XAML, ViewModel, servizio o test deve essere modificato.

---

## Mapping dei Codepoint

Di seguito la tabella completa dei 36 simboli da migrare. La colonna **Tabler Icon Name** indica il nome dell'icona su https://tabler.io/icons. Il codepoint Tabler va cercato nel file `tabler-icons.css` (incluso nel pacchetto webfont) nella forma `.ti-xxx::before { content: "\eaXX"; }`.

| Costante `IconKeys` | Descrizione semantica | Tabler Icon Name | Codepoint Tabler |
|---------------------|----------------------|------------------|-----------------|
| `Menu` | Hamburger menu (3 linee) | `menu-2` | da verificare |
| `Add` | Aggiungi / plus | `plus` | da verificare |
| `Send` | Invia messaggio | `send-2` | da verificare |
| `ArrowUp` | Freccia su (send button) | `arrow-up` | da verificare |
| `AutoAwesome` | Sparkle / auto-accept | `sparkles` | da verificare |
| `Mic` | Microfono | `microphone` | da verificare |
| `Edit` | Modifica / matita | `pencil` | da verificare |
| `Settings` | Impostazioni / ingranaggio | `settings` | da verificare |
| `ChevronRight` | Chevron destra (disclosure) | `chevron-right` | da verificare |
| `Code` | Codice (command palette) | `code` | da verificare |
| `X` | Chiudi / X | `x` | da verificare |
| `Copy` | Copia / clipboard | `copy` | da verificare |
| `Check` | Spunta / checkmark | `check` | da verificare |
| `Folder` | Cartella | `folder` | da verificare |
| `Chat` | Chat / bolla messaggio | `message` | da verificare |
| `DotsVertical` | Tre puntini verticali | `dots-vertical` | da verificare |
| `Trash` | Elimina / cestino | `trash` | da verificare |
| `Bell` | Notifiche / campanella | `bell` | da verificare |
| `Globe` | Globo / pubblico | `world` | da verificare |
| `Brain` | Cervello / thinking | `brain` | da verificare |
| `AlertTriangle` | Avviso / triangolo | `alert-triangle` | da verificare |
| `AlertCircle` | Errore / cerchio alert | `alert-circle` | da verificare |
| `InfoCircle` | Info / cerchio i | `info-circle` | da verificare |
| `Search` | Cerca | `search` | da verificare |
| `ArrowLeft` | Freccia sinistra / back | `arrow-left` | da verificare |
| `ChevronDown` | Chevron giù / espandi | `chevron-down` | da verificare |
| `Robot` | Robot / AI agent | `robot` | da verificare |
| `PlayerStop` | Stop / ferma | `player-stop` | da verificare |
| `Key` | Chiave / API key | `key` | da verificare |
| `Link` | Link / URL | `link` | da verificare |
| `CircleCheck` | Cerchio con spunta (successo) | `circle-check` | da verificare |
| `Circle` | Cerchio vuoto (radio off) | `circle` | da verificare |
| `CircleDot` | Cerchio con punto (radio on) | `circle-dot` | da verificare |
| `Clock` | Orologio / schedule | `clock` | da verificare |
| `Terminal` | Terminale | `terminal-2` | da verificare |
| `Adjustments` | Regolazioni / tune | `adjustments-horizontal` | da verificare |

> **Nota**: i nomi Tabler nella colonna "Tabler Icon Name" sono suggerimenti basati sulla semantica. L'agente deve verificare che l'icona esista su https://tabler.io/icons e scegliere quella visivamente più appropriata se il nome esatto non esiste.

---

## Come trovare i codepoint

### Metodo 1 — File CSS nel pacchetto webfont (preferito)

Il pacchetto `@tabler/icons-webfont` contiene il file `tabler-icons.css`. Ogni icona ha una regola:
```css
.ti-arrow-left::before { content: "\ea64"; }
```
Il valore `content` è il codepoint Unicode da usare in `IconKeys.cs` come `"\uea64"`.

Il file CSS è disponibile anche via CDN:
```
https://cdn.jsdelivr.net/npm/@tabler/icons-webfont@latest/tabler-icons.css
```

### Metodo 2 — Sito ufficiale

Su https://tabler.io/icons, cerca l'icona, clicca su di essa e seleziona la tab "Webfont" — mostra il codepoint e la classe CSS.

---

## Acceptance Criteria

- [ ] **[AC-001]** `dotnet build openMob.sln` — zero errori, zero warning.
- [ ] **[AC-002]** `MaterialSymbols-Outlined.ttf` non è presente in `Resources/Fonts/` né in `openMob.csproj`.
- [ ] **[AC-003]** `TablerIcons.ttf` è presente in `Resources/Fonts/` e registrato in `openMob.csproj`.
- [ ] **[AC-004]** Tutti i 36 codepoint in `IconKeys.cs` sono valori Unicode Tabler Icons (range `\uea01`–`\uefff` circa).
- [ ] **[AC-005]** L'app lanciata su simulatore iOS o emulatore Android mostra tutte le icone correttamente — nessun quadratino vuoto, nessun glifo errato.
- [ ] **[AC-006]** Nessun file XAML, ViewModel, servizio o test è stato modificato.
- [ ] **[AC-007]** `dotnet test` — tutti i test passano invariati.

---

## Note per l'Analisi Tecnica

- **Branch di partenza**: questa spec si innesta su `develop` dopo il merge di `feature/ui-overhaul-design-system`. Creare un nuovo branch `feature/tabler-icons-codepoint-migration`.
- **Nessuna modifica XAML**: tutti i file XAML usano già `{x:Static helpers:IconKeys.XxxName}`. Aggiornando solo i codepoint in `IconKeys.cs`, tutte le icone si aggiornano automaticamente.
- **Font alias invariato**: l'alias `"TablerIcons"` in `MauiProgram.cs` è già corretto. Cambia solo il file sorgente da `MaterialSymbols-Outlined.ttf` a `TablerIcons.ttf`.
- **Codepoint range Tabler**: i codepoint del webfont Tabler Icons sono nel range Unicode Private Use Area, tipicamente `\uea01`–`\uefff`. Se un codepoint in `IconKeys.cs` è fuori da questo range dopo la migrazione, è probabilmente sbagliato.
- **Verifica visiva obbligatoria**: dopo la build, lanciare l'app e navigare su almeno: ChatPage (icone input bar), FlyoutContentView (icone drawer), SettingsPage (icone sezioni), un popup (close button X). Verificare che ogni icona mostri il glifo atteso.
