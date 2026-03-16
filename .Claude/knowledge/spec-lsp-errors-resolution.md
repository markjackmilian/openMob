# Spec — LSP Errors Resolution

**Slug:** lsp-errors-resolution
**Date:** 2026-03-16
**Status:** Completed
**Branch:** bugfix/lsp-errors-resolution (merged into develop)

---

## Executive Summary

Il progetto `openMob.Core` presentava errori LSP (Language Server Protocol) visibili nell'IDE ma non bloccanti per la build CLI. Le cause radice erano quattro: gap TFM `net10.0` nei package NuGet (falsi positivi a cascata), API EF Core obsoleta (`ExecuteSqlInterpolatedAsync`), namespace mancante in `GlobalUsings.cs`, e `using` ridondanti. Tutte risolte con modifiche chirurgiche a 4 file.

---

## Requirements & Outcome

| REQ | Descrizione | Stato |
|-----|-------------|-------|
| REQ-001 | Sostituire `ExecuteSqlInterpolatedAsync` con `ExecuteSqlAsync` in `ServerConnectionRepository.cs` | ✅ Completato |
| REQ-002 | Aggiungere `global using openMob.Core.Infrastructure.Monitoring;` in `GlobalUsings.cs` | ✅ Completato |
| REQ-003 | Rimuovere `using System.Net.Http;` da `OpencodeConnectionManager.cs` | ✅ Completato (deviazione: `using System.Text;` mantenuto — necessario per `Encoding.UTF8`) |
| REQ-004 | Aggiungere commento TFM gap in `openMob.Core.csproj` | ✅ Completato |
| REQ-005 | Build 0 errori, tutti i test verdi | ✅ 348/348 test verdi |

---

## Acceptance Criteria — Final

- [x] AC-001: `ExecuteSqlAsync` usato, nessuna diagnostica `CS0618`/`EF1001`
- [x] AC-002: `GlobalUsings.cs` contiene `global using openMob.Core.Infrastructure.Monitoring;`
- [x] AC-003: `using System.Net.Http;` rimosso da `OpencodeConnectionManager.cs`
- [x] AC-004: `dotnet build openMob.sln` → codice 0, zero errori
- [x] AC-005: `dotnet test` → 348/348 verdi
- [x] AC-006: commento TFM presente in `openMob.Core.csproj`

---

## Files Modified

| File | Modifica |
|------|----------|
| `src/openMob.Core/Data/Repositories/ServerConnectionRepository.cs` | riga 173: `ExecuteSqlInterpolatedAsync` → `ExecuteSqlAsync` |
| `src/openMob.Core/GlobalUsings.cs` | aggiunta `global using openMob.Core.Infrastructure.Monitoring;` |
| `src/openMob.Core/Infrastructure/Http/OpencodeConnectionManager.cs` | rimosso `using System.Net.Http;` (ridondante con implicit usings) |
| `src/openMob.Core/openMob.Core.csproj` | aggiunto commento documentativo gap TFM |

---

## Review Outcome

**Verdict: ⚠️ Approved with remarks** (0 Critical, 0 Major, 2 Minor)

**Minor findings (follow-up non-bloccante):**
- [m-001] 14 file hanno `using openMob.Core.Infrastructure.Monitoring;` esplicito ora ridondante con il global using. Cleanup consigliato in commit separato.
- [m-002] `OpencodeApiClient.cs` ha `using System.Net.Http;` ridondante. Cleanup consigliato in commit separato.

---

## Key Learnings

1. `System.Text` **non** è incluso negli implicit usings di `Microsoft.NET.Sdk` per class library `net10.0`. Solo `System.Net.Http` lo è. Verificare sempre con `dotnet build` prima di rimuovere `using` directives.
2. I falsi positivi LSP da gap TFM sono attesi e documentati — non indicano errori reali. Usare C# Dev Kit (non OmniSharp classic) per mitigare.
3. Aggiungere un namespace a `GlobalUsings.cs` rende ridondanti tutti i `using` espliciti dello stesso namespace nei file del progetto — pianificare un cleanup pass.
