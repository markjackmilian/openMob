# ADR: ImplicitUsings Coverage for net10.0 Class Library Targets

## Date
2026-03-16

## Status
Accepted

## Context

Durante la risoluzione degli errori LSP (bugfix/lsp-errors-resolution), è emerso che `System.Text` non è incluso negli implicit usings generati da `Microsoft.NET.Sdk` per progetti class library targeting `net10.0`. Il tentativo di rimuovere `using System.Text;` da `OpencodeConnectionManager.cs` ha causato `CS0103: The name 'Encoding' does not exist` — un errore di build reale, non un falso positivo LSP.

## Decision

**Non rimuovere `using System.Text;` da file che usano `Encoding`, `StringBuilder`, o altri tipi di `System.Text`**, anche quando si esegue un cleanup pass di `using` ridondanti in progetti `openMob.Core` (`net10.0` class library).

## Rationale

Gli implicit usings di `Microsoft.NET.Sdk` per `net10.0` class library coprono:
- `System`
- `System.Collections.Generic`
- `System.IO`
- `System.Linq`
- `System.Net.Http`
- `System.Threading`
- `System.Threading.Tasks`

`System.Text` **non è incluso**. È invece incluso negli implicit usings di `Microsoft.NET.Sdk.Web` (ASP.NET Core) ma non nel SDK base per class library.

## Alternatives Considered

- **Aggiungere `global using System.Text;` a `GlobalUsings.cs`**: possibile, ma introduce `System.Text` globalmente in tutto il progetto. Preferibile mantenere i `using` espliciti nei file che ne hanno bisogno, per chiarezza.
- **Rimuovere `using System.Text;` e aggiungere `global using`**: equivalente alla prima alternativa, con il rischio di rendere ridondanti altri `using` espliciti.

## Consequences

### Positive
- Nessun `CS0103` inatteso durante cleanup pass di `using` directives
- Comportamento prevedibile: i `using` espliciti per `System.Text` sono sempre necessari in `openMob.Core`

### Negative / Trade-offs
- I file che usano `Encoding` o `StringBuilder` mantengono un `using System.Text;` esplicito che potrebbe sembrare ridondante a prima vista

## Related Features
lsp-errors-resolution

## Related Agents
om-mobile-core (cleanup pass di using directives)

---

## Quick Reference — ImplicitUsings per SDK

| SDK | `System.Text` incluso? |
|-----|----------------------|
| `Microsoft.NET.Sdk` (class library) | ❌ No |
| `Microsoft.NET.Sdk.Web` (ASP.NET Core) | ✅ Sì |
| `Microsoft.NET.Sdk.Razor` | ✅ Sì |
| `Microsoft.NET.Sdk.Worker` | ❌ No |

Fonte: https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview#implicit-using-directives
