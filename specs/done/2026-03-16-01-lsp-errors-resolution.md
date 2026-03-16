# LSP Errors Resolution — Correzione Errori di Compilazione e Diagnostica IDE

## Metadata
| Field       | Value                              |
|-------------|------------------------------------|
| Date        | 2026-03-16                         |
| Status      | **Completed**                      |
| Version     | 1.0                                |
| Completed   | 2026-03-16                         |
| Branch      | bugfix/lsp-errors-resolution (merged) |
| Merged into | develop                            |

---

## Executive Summary

Il progetto `openMob.Core` presenta una serie di errori LSP (Language Server Protocol) che compaiono nell'IDE ma non bloccano la build CLI (`dotnet build` esce con codice 0). Le cause radice sono quattro: un gap di compatibilità TFM tra i package NuGet e `net10.0` (che causa falsi positivi a cascata nel language server), un'API EF Core obsoleta usata in produzione, una voce mancante in `GlobalUsings.cs`, e `using` ridondanti. Questa spec risolve tutti i problemi in modo definitivo, ripristinando la correttezza della diagnostica IDE e prevenendo mascheramenti futuri di errori reali.

---

## Scope

### In Scope
- Sostituzione di `ExecuteSqlInterpolatedAsync` (obsoleto EF Core 7+) con `ExecuteSqlAsync` in `ServerConnectionRepository.cs`
- Aggiunta di `global using openMob.Core.Infrastructure.Monitoring;` in `GlobalUsings.cs`
- Rimozione dei `using` ridondanti in `OpencodeConnectionManager.cs`
- Documentazione del gap TFM `net10.0` per i package `Ulid`, `CommunityToolkit.Mvvm`, `Microsoft.EntityFrameworkCore.*`, `Zeroconf` e strategia di mitigazione LSP
- Verifica che `dotnet build openMob.sln` e `dotnet test` continuino a passare dopo le modifiche

### Out of Scope
- Sostituzione del package `Ulid` con un'alternativa (il build funziona correttamente via TFM fallback; il problema è solo LSP)
- Modifica delle migrazioni EF Core esistenti (la struttura è corretta)
- Aggiornamento dei package NuGet a versioni con supporto `net10.0` nativo (non ancora disponibili)
- Configurazione dell'IDE o del language server (fuori dal controllo del codebase)
- Rimozione di `TreatWarningsAsErrors=false` (decisione di progetto da rivalutare separatamente)

---

## Functional Requirements

> Requirements are numbered for traceability.

### REQ-001 — Sostituzione API EF Core Obsoleta

1. **[REQ-001]** In `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs`, sostituire la chiamata a `ExecuteSqlInterpolatedAsync` (obsoleta da EF Core 7, rimossa in EF Core 10) con `ExecuteSqlAsync`:

   **Prima (obsoleto):**
   ```csharp
   await _context.Database
       .ExecuteSqlInterpolatedAsync(
           $"UPDATE ServerConnections SET IsActive = 0 WHERE Id != {id}",
           cancellationToken)
       .ConfigureAwait(false);
   ```

   **Dopo (corretto):**
   ```csharp
   await _context.Database
       .ExecuteSqlAsync(
           $"UPDATE ServerConnections SET IsActive = 0 WHERE Id != {id}",
           cancellationToken)
       .ConfigureAwait(false);
   ```

   `ExecuteSqlAsync` accetta `FormattableString` e parametrizza automaticamente il valore interpolato, prevenendo SQL injection esattamente come il metodo obsoleto. La firma è identica; è un rename diretto.

### REQ-002 — GlobalUsings.cs — Aggiunta namespace mancante

2. **[REQ-002]** In `src/openMob.Core/GlobalUsings.cs`, aggiungere la riga:
   ```csharp
   global using openMob.Core.Infrastructure.Monitoring;
   ```
   Questo elimina la necessità di `using` espliciti per `SentryHelper` in tutti i ViewModel e Service che lo usano, e risolve il falso positivo LSP in `OnboardingViewModel.cs` e negli altri file che usano `SentryHelper` senza `using` esplicito.

### REQ-003 — Rimozione `using` ridondanti

3. **[REQ-003]** In `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs`, rimuovere le seguenti righe ridondanti (già fornite da `ImplicitUsings` per `net10.0`):
   ```csharp
   using System.Net.Http;
   using System.Text;
   ```
   Verificare che nessun altro `using` nel file sia ridondante rispetto ai global usings del progetto.

### REQ-004 — Documentazione gap TFM e strategia di mitigazione

4. **[REQ-004]** Aggiungere un commento esplicativo in `src/openMob.Core/openMob.Core.csproj` sopra i package con gap TFM, per documentare il comportamento atteso e prevenire confusione futura:

   ```xml
   <!-- 
     NOTA TFM: I seguenti package non hanno ancora un TFM net10.0 nativo.
     Il build risolve correttamente via fallback a net8.0 (comportamento standard .NET).
     Gli errori LSP "tipo non trovato" su Ulid, CommunityToolkit.Mvvm, EF Core sono
     falsi positivi del language server — non indicano errori reali di compilazione.
     Verificare con: dotnet build openMob.sln
     Aggiornare questo commento quando i package rilasciano supporto net10.0 nativo.
   -->
   ```

### REQ-005 — Verifica build e test post-modifica

5. **[REQ-005]** Dopo tutte le modifiche, verificare che:
   - `dotnet build openMob.sln` esce con codice 0 e zero errori
   - `dotnet build openMob.sln` non introduce nuovi warning rispetto allo stato precedente
   - `dotnet test tests/openMob.Tests/openMob.Tests.csproj` passa con tutti i test verdi

---

## Functional Impacts

### Affected Components / Systems
| Component | Impact | Tipo modifica |
|-----------|--------|---------------|
| `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` | Modifica riga 173 | Sostituzione metodo obsoleto → `ExecuteSqlAsync` |
| `src/openMob.Core/GlobalUsings.cs` | Aggiunta 1 riga | `global using openMob.Core.Infrastructure.Monitoring;` |
| `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` | Rimozione 1 riga | `using System.Net.Http;` (rimosso); `using System.Text;` mantenuto — necessario per `Encoding.UTF8` |
| `src/openMob.Core/openMob.Core.csproj` | Aggiunta commento | Documentazione gap TFM |

### Dependencies
- `Microsoft.EntityFrameworkCore.Relational` 9.x — `ExecuteSqlAsync` è disponibile da EF Core 7.0+; nessun aggiornamento di package necessario
- Nessuna dipendenza esterna aggiuntiva

---

## Analisi Cause Radice

### Causa 1 — Gap TFM `net10.0` (falsi positivi LSP a cascata)

**Package coinvolti:**

| Package | Versione | TFM massimo disponibile | Impatto LSP |
|---|---|---|---|
| `Ulid` | 1.4.1 | `net8.0` | `CS0246` su `ServerConnection.cs` line 14 + cascade |
| `CommunityToolkit.Mvvm` | 8.4.0 | `net8.0` | `CS0246` su tutti i ViewModel |
| `Microsoft.EntityFrameworkCore.*` | 9.x | `net8.0` | `CS0246` su file EF Core |
| `Zeroconf` | 3.7.16 | `net8.0` | `CS0246` su file discovery |

**Comportamento corretto:** `dotnet build` risolve questi package via TFM fallback (`net10.0` → `net8.0`) per design del sistema di asset NuGet. Il build è corretto.

**Comportamento LSP:** Alcuni language server (OmniSharp classic, C# Dev Kit in modalità project-system) non applicano il TFM fallback durante la risoluzione IntelliSense, causando `CS0246` falsi. Il problema scompare quando i package rilasciano TFM `net10.0` nativo.

**Mitigazione immediata (senza modifiche al codice):** Assicurarsi che il language server usato sia **C# Dev Kit** (non OmniSharp classic). C# Dev Kit usa il Roslyn SDK project-system che gestisce correttamente il TFM fallback. Eseguire `dotnet restore` e riavviare il language server dopo ogni modifica ai package.

**Mitigazione a lungo termine:** Monitorare i release dei package sopra elencati per il supporto `net10.0` nativo. Aggiornare le versioni quando disponibili.

### Causa 2 — API EF Core Obsoleta (`ExecuteSqlInterpolatedAsync`)

`DatabaseFacade.ExecuteSqlInterpolatedAsync(FormattableString, CancellationToken)` è marcata `[Obsolete]` da EF Core 7.0 in favore di `ExecuteSqlAsync`. Il build non fallisce perché `TreatWarningsAsErrors=false`, ma il language server segnala correttamente l'uso di API obsoleta come errore/warning IDE. Questa è una segnalazione **legittima** che richiede correzione nel codice.

### Causa 3 — `SentryHelper` senza `using` esplicito

`SentryHelper` è in `openMob.Core.Infrastructure.Monitoring`. Il compilatore C# risolve tipi nello stesso assembly senza `using` esplicito (non c'è ambiguità), quindi il build passa. Il language server, che valuta i file in contesti parziali, non sempre applica questa regola e segnala `CS0246`. La soluzione corretta è aggiungere il namespace a `GlobalUsings.cs` per coerenza e chiarezza.

### Causa 4 — `using` ridondanti con `ImplicitUsings`

`net10.0` con `<ImplicitUsings>enable</ImplicitUsings>` genera automaticamente `global using System.Net.Http;` e altri namespace di sistema. I `using` espliciti in `OpencodeConnectionManager.cs` sono duplicati. Il compilatore li ignora silenziosamente (`CS8019` è `hidden` per default), ma il language server li segnala come `IDE0005` (remove unnecessary using).

---

## Open Questions & Clarifications

| # | Question | Status | Answer / Decision |
|---|----------|--------|-------------------|
| 1 | `ExecuteSqlAsync` vs `ExecuteSqlInterpolatedAsync` — la semantica di parametrizzazione è identica? | Resolved | Sì. Entrambi accettano `FormattableString` e parametrizzano automaticamente i valori interpolati. `ExecuteSqlAsync` è il successore diretto. |
| 2 | Aggiungere `global using openMob.Core.Infrastructure.Monitoring;` potrebbe causare conflitti di nome con altri namespace? | Resolved | No. `SentryHelper` è un nome univoco nel progetto. Verificare con `dotnet build` dopo la modifica. |
| 3 | I falsi positivi LSP da gap TFM impattano anche il progetto `openMob.Tests`? | Open | Probabilmente sì per i package condivisi (EF Core, CommunityToolkit.Mvvm). Da verificare. Se confermato, la stessa strategia di mitigazione (C# Dev Kit + `dotnet restore`) si applica. |
| 4 | `TreatWarningsAsErrors=false` — dovrebbe essere rimosso o impostato a `true` per aumentare la qualità? | Open | Decisione di progetto separata. Rimuoverlo o impostarlo a `true` esporrebbe tutti i warning come errori di build, inclusi quelli dei package NuGet. Valutare in una spec dedicata alla qualità del build. |

---

## Acceptance Criteria

> Each criterion maps to one or more functional requirements.

- [x] **[AC-001]** Dato `ServerConnectionRepository.cs`, quando si ispeziona la riga dell'aggiornamento SQL, allora usa `ExecuteSqlAsync` e non compaiono diagnostiche `CS0618` o `EF1001` nel language server. *(REQ-001)*
- [x] **[AC-002]** Dato `GlobalUsings.cs`, quando si ispeziona il file, allora contiene `global using openMob.Core.Infrastructure.Monitoring;` e `OnboardingViewModel.cs` non mostra più `CS0246` per `SentryHelper`. *(REQ-002)*
- [x] **[AC-003]** Dato `OpencodeConnectionManager.cs`, quando si ispeziona il file, allora non contiene `using System.Net.Http;` ridondante. *(REQ-003)* — **Nota implementazione:** `using System.Text;` è stato mantenuto intenzionalmente perché `System.Text` non è incluso negli implicit usings di `Microsoft.NET.Sdk` per class library `net10.0`; rimuoverlo causerebbe `CS0103` su `Encoding.UTF8`. Deviazione approvata da om-reviewer.
- [x] **[AC-004]** Dato `dotnet build openMob.sln`, quando eseguito dopo tutte le modifiche, allora esce con codice 0 e zero errori. *(REQ-005)*
- [x] **[AC-005]** Dato `dotnet test tests/openMob.Tests/openMob.Tests.csproj`, quando eseguito dopo tutte le modifiche, allora tutti i test esistenti passano. *(REQ-005)* — 348/348 verdi.
- [x] **[AC-006]** Dato `openMob.Core.csproj`, quando si ispeziona il file, allora contiene il commento documentativo sul gap TFM sopra i package interessati. *(REQ-004)*

---

## Technical Analysis

> Added by: om-orchestrator | Date: 2026-03-16

### Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/lsp-errors-resolution |
| Branches from | develop |
| Estimated complexity | Low |
| Estimated agents involved | om-mobile-core, om-reviewer |

### Layers Involved

| Layer | Agent | Scope |
|-------|-------|-------|
| Business logic / Services | om-mobile-core | `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` |
| Infrastructure | om-mobile-core | `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` |
| Project config | om-mobile-core | `src/openMob.Core/GlobalUsings.cs`, `src/openMob.Core/openMob.Core.csproj` |
| Code Review | om-reviewer | all of the above |

### Files Modified

- `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` — riga 173: `ExecuteSqlInterpolatedAsync` → `ExecuteSqlAsync` ✅
- `src/openMob.Core/GlobalUsings.cs` — aggiunta `global using openMob.Core.Infrastructure.Monitoring;` ✅
- `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` — rimozione `using System.Net.Http;` ✅
- `src/openMob.Core/openMob.Core.csproj` — aggiunta commento documentativo gap TFM ✅

### Review Outcome

| Severity | Count |
|---|---|
| 🔴 Critical | 0 |
| 🟠 Major | 0 |
| 🟡 Minor | 2 (non-blocking, advisory) |

**Verdict: ⚠️ Approved with remarks**

**Minor findings (follow-up, non-blocking):**
- [m-001] 14 file hanno `using openMob.Core.Infrastructure.Monitoring;` esplicito ora ridondante con il global using aggiunto. Cleanup consigliato in commit separato.
- [m-002] `OpencodeApiClient.cs` ha `using System.Net.Http;` ridondante. Cleanup consigliato in commit separato.

### Definition of Done — Final Status

- [x] REQ-001: `ExecuteSqlAsync` usato in `ServerConnectionRepository.cs` riga 173
- [x] REQ-002: `global using openMob.Core.Infrastructure.Monitoring;` presente in `GlobalUsings.cs`
- [x] REQ-003: `using System.Net.Http;` rimosso da `OpencodeConnectionManager.cs` (deviazione approvata su `System.Text`)
- [x] REQ-004: commento TFM presente in `openMob.Core.csproj`
- [x] REQ-005: `dotnet build openMob.sln` → codice 0, zero errori; `dotnet test` → 348/348 verdi
- [x] `om-reviewer` verdict: ⚠️ Approved with remarks
- [x] Git Flow branch finished e deleted
- [x] Spec moved to `specs/done/` con status Completed
