# Technical Analysis — LSP Errors Resolution

**Feature slug:** lsp-errors-resolution
**Completed:** 2026-03-16
**Branch:** bugfix/lsp-errors-resolution (merged into develop)
**Complexity:** Low

---

## Change Classification

| Field | Value |
|-------|-------|
| Change type | Bug Fix |
| Git Flow branch | bugfix/lsp-errors-resolution |
| Branches from | develop |
| Complexity | Low |
| Agents involved | om-mobile-core, om-reviewer |

---

## Root Cause Analysis

### Causa 1 — Gap TFM `net10.0` (falsi positivi LSP a cascata)

I seguenti package NuGet non hanno ancora un TFM `net10.0` nativo:

| Package | Versione | TFM max | Impatto LSP |
|---------|----------|---------|-------------|
| `Ulid` | 1.4.1 | `net8.0` | `CS0246` cascade |
| `CommunityToolkit.Mvvm` | 8.4.0 | `net8.0` | `CS0246` su tutti i ViewModel |
| `Microsoft.EntityFrameworkCore.*` | 9.x | `net8.0` | `CS0246` su file EF Core |
| `Zeroconf` | 3.7.16 | `net8.0` | `CS0246` su file discovery |

**`dotnet build` funziona correttamente** via TFM fallback (`net10.0` → `net8.0`). Gli errori LSP sono falsi positivi del language server.

**Mitigazione:** usare **C# Dev Kit** (non OmniSharp classic). Eseguire `dotnet restore` e riavviare il language server dopo ogni modifica ai package.

### Causa 2 — API EF Core Obsoleta

`ExecuteSqlInterpolatedAsync` è `[Obsolete]` da EF Core 7.0. Sostituito con `ExecuteSqlAsync` (firma identica per `FormattableString`).

### Causa 3 — Namespace mancante in GlobalUsings.cs

`SentryHelper` (in `openMob.Core.Infrastructure.Monitoring`) usato senza `using` esplicito. Il compilatore lo risolve (stesso assembly), il language server no. Soluzione: aggiunto a `GlobalUsings.cs`.

### Causa 4 — `using` ridondanti con ImplicitUsings

`net10.0` + `<ImplicitUsings>enable</ImplicitUsings>` fornisce automaticamente `System.Net.Http`. Il `using` esplicito in `OpencodeConnectionManager.cs` era ridondante.

---

## Key Technical Facts

### ImplicitUsings per `Microsoft.NET.Sdk` (`net10.0` class library)

Gli implicit usings **inclusi** sono:
- `System`
- `System.Collections.Generic`
- `System.IO`
- `System.Linq`
- `System.Net.Http`
- `System.Threading`
- `System.Threading.Tasks`

**`System.Text` NON è incluso.** Rimuovere `using System.Text;` da un file che usa `Encoding.UTF8` causa `CS0103`. Verificare sempre con `dotnet build` prima di rimuovere `using` directives.

### ExecuteSqlAsync vs ExecuteSqlInterpolatedAsync

Entrambi accettano `FormattableString` e parametrizzano automaticamente i valori interpolati (nessun rischio SQL injection). `ExecuteSqlAsync` è il successore diretto disponibile da EF Core 7.0+. Disponibile transitivamente via `Microsoft.EntityFrameworkCore.Sqlite` 9.x — nessun package aggiuntivo necessario.

### Effetto collaterale di aggiungere un namespace a GlobalUsings.cs

Aggiungere `global using <namespace>;` a `GlobalUsings.cs` rende ridondanti tutti i `using <namespace>;` espliciti nei file del progetto. Il compilatore li ignora silenziosamente (`CS8019` è `hidden`), ma il language server li segnala come `IDE0005`. Pianificare un cleanup pass dopo ogni aggiunta a `GlobalUsings.cs`.

**File con `using openMob.Core.Infrastructure.Monitoring;` ora ridondante (da pulire in follow-up):**
- `ViewModels/ServerManagementViewModel.cs`
- `ViewModels/ServerDetailViewModel.cs`
- `ViewModels/OnboardingViewModel.cs`
- `ViewModels/SplashViewModel.cs`
- `ViewModels/ProjectSwitcherViewModel.cs`
- `ViewModels/ProjectsViewModel.cs`
- `ViewModels/ProjectDetailViewModel.cs`
- `ViewModels/FlyoutViewModel.cs`
- `ViewModels/ModelPickerViewModel.cs`
- `ViewModels/ChatViewModel.cs`
- `ViewModels/AgentPickerViewModel.cs`
- `Services/SessionService.cs`
- `Services/ProviderService.cs`
- `Services/ProjectService.cs`
- `Services/AgentService.cs`

---

## Files Modified

| File | Modifica | REQ |
|------|----------|-----|
| `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` | `ExecuteSqlInterpolatedAsync` → `ExecuteSqlAsync` riga 173 | REQ-001 |
| `src/openMob.Core/GlobalUsings.cs` | `global using openMob.Core.Infrastructure.Monitoring;` aggiunto | REQ-002 |
| `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` | `using System.Net.Http;` rimosso | REQ-003 |
| `src/openMob.Core/openMob.Core.csproj` | Commento TFM gap aggiunto sopra PackageReference block | REQ-004 |

---

## Build & Test Results

- `dotnet build openMob.sln`: **0 errori**, 3 warning pre-esistenti non correlati (in MAUI project)
- `dotnet test`: **348/348 verdi**
