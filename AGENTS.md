# AGENTS.md — openMob

Guidance for AI coding agents operating in this repository.

---

## Project Overview

**openMob** is a cross-platform iOS/Android mobile app built with **.NET MAUI** that lets users interact with one or more [opencode](https://opencode.ai) servers from their mobile device. The repository is in early development — no source code exists yet. All work begins from a functional spec in `specs/todo/`.

---

## Multi-Agent Workflow

This project uses a structured multi-agent pipeline via **opencode** (config in `.opencode/`).

| Agent | Role | Writes to |
|---|---|---|
| `om-planner` | Product analyst — writes functional specs | `specs/todo/` |
| `om-orchestrator` | Technical director — coordinates all agents, drives Git Flow lifecycle | `specs/` only |
| `om-mobile-core` | Senior .NET MAUI engineer — Services, ViewModels, EF Core, HTTP, DI | `src/` |
| `om-mobile-ui` | UI/UX engineer — XAML, styles, components, theming | `src/Views/`, `Resources/` |
| `om-tester` | Unit test specialist | `tests/` |
| `om-reviewer` | Quality gatekeeper — read-only review | (no writes) |

**Entry point:** place a spec in `specs/todo/` → invoke `om-orchestrator`.  
**No spec = no work.** Never begin implementation without a written spec.

---

## Repository Structure

```
specs/
├── todo/          ← new specs waiting for pickup
├── in-progress/   ← active work (spec + Technical Analysis appended inline)
└── done/          ← completed specs with metadata updated

src/               ← application source (to be created)
├── Models/        ← EF Core entity POCOs
├── ViewModels/    ← CommunityToolkit.Mvvm ObservableObject subclasses
├── Views/         ← XAML pages + minimal code-behind
│   └── Controls/  ← reusable ContentView components
├── Services/      ← business logic, repositories, HTTP client
├── Data/          ← AppDbContext + EF Core Migrations
├── Infrastructure/
│   ├── Http/      ← OpenCodeApiClient (typed IHttpClientFactory)
│   ├── Monitoring/← Sentry helpers
│   └── DI/        ← service registration extension methods
├── Converters/    ← IValueConverter implementations
└── MauiProgram.cs ← DI wiring, Sentry init, migration apply

tests/
└── openMob.Tests/ ← xUnit unit tests (no MAUI runtime)
    ├── ViewModels/
    ├── Services/
    ├── Converters/
    └── Helpers/TestDataBuilder.cs
```

---

## Build & Run Commands

```bash
# Build entire solution
dotnet build

# Build for a specific platform (once scaffolded)
dotnet build -f net9.0-ios
dotnet build -f net9.0-android

# Run on Android emulator
dotnet run -f net9.0-android

# Run on iOS simulator
dotnet run -f net9.0-ios

# EF Core migrations (run from src/ project)
dotnet ef migrations add <MigrationName> --project src/openMob.csproj
dotnet ef database update
```

---

## Test Commands

```bash
# Run all unit tests
dotnet test tests/openMob.Tests/openMob.Tests.csproj

# Run a single test class
dotnet test tests/openMob.Tests/openMob.Tests.csproj --filter "FullyQualifiedName~SessionListViewModelTests"

# Run a single test method
dotnet test tests/openMob.Tests/openMob.Tests.csproj --filter "FullyQualifiedName~SessionListViewModelTests.LoadSessionsCommand_WhenServiceReturnsData_PopulatesSessionsCollection"

# Run tests with detailed output
dotnet test tests/openMob.Tests/openMob.Tests.csproj --logger "console;verbosity=detailed"
```

### Test Stack (non-negotiable)

| Concern | Library |
|---|---|
| Runner | xUnit 2.x |
| Mocking | NSubstitute 5.x |
| Assertions | FluentAssertions 6.x |

---

## Code Style Guidelines

### C# Conventions

- **Target framework:** .NET 9, C# 13, nullable reference types enabled (`<Nullable>enable</Nullable>`)
- **File-scoped namespaces:** `namespace openMob.ViewModels;`
- **`sealed` by default** on leaf classes (ViewModels, Services, Converters)
- **`async/await` everywhere:** never use `.Result` or `.Wait()`; always propagate `CancellationToken`
- **`ConfigureAwait(false)`** in service/library code; omit in ViewModels (UI thread needed)
- **No `static` mutable state** in ViewModels or Services
- **No `async void`** except MAUI lifecycle event handlers that cannot be avoided

### Naming Conventions

| Item | Convention | Example |
|---|---|---|
| Classes, interfaces | PascalCase | `SessionService`, `ISessionService` |
| Private fields | `_camelCase` | `private string _title` |
| Properties, methods | PascalCase | `LoadDataAsync()`, `IsLoading` |
| Constants | PascalCase | `MaxRetryCount` |
| XAML `x:Key` resources | PascalCase semantic | `AccentColor`, `SpacingLg` |
| Test methods | `Method_Condition_Expected` | `LoadSessionsCommand_WhenServiceThrows_SetsIsErrorTrue` |

### XML Documentation

Every `public` and `internal` member **must** have XML doc comments:

```csharp
/// <summary>Creates a new session on the opencode server.</summary>
/// <param name="title">Optional display title.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>The created <see cref="Session"/>.</returns>
/// <exception cref="OpenCodeApiException">Thrown on API failure.</exception>
public async Task<Session> CreateSessionAsync(string? title = null, CancellationToken ct = default)
```

### Imports / Usings

- Use global usings in `GlobalUsings.cs` for framework namespaces (`System`, `System.Collections.Generic`, `Microsoft.Maui.Controls`, etc.)
- Feature-specific usings stay in the file; keep them sorted alphabetically
- No unused usings

---

## MVVM Rules (CommunityToolkit.Mvvm)

- All ViewModels inherit `ObservableObject` (or `ObservableRecipient` when messaging is needed)
- Use `[ObservableProperty]` source generator — **never** write manual `OnPropertyChanged`
- Use `[RelayCommand]` / `[AsyncRelayCommand]` — **never** expose raw `ICommand` manually
- Implement `IQueryAttributable` for Shell navigation parameter handling
- Zero business logic in code-behind — only `InitializeComponent()` and MAUI lifecycle overrides
- All external dependencies injected via constructor interfaces — no `ServiceLocator`, no `App.Current.Services`

```csharp
// Correct ViewModel pattern
[ObservableProperty]
private string _title = string.Empty;

[RelayCommand]
private async Task LoadDataAsync(CancellationToken ct)
{
    IsLoading = true;
    try { /* call service via interface */ }
    catch (Exception ex) { IsError = true; }
    finally { IsLoading = false; }
}
```

---

## XAML Rules

- **Always** declare `x:DataType` for compiled bindings on every `ContentPage`, `ContentView`, `DataTemplate`
- Use `CollectionView` — **never** `ListView`
- Use `VerticalStackLayout` / `HorizontalStackLayout` — **never** deprecated `StackLayout`
- Never nest `ScrollView` inside `ScrollView`
- Never use `BoxView` as a spacer — use `Margin`/`Padding`
- All colors, sizes, and spacing must reference `StaticResource` tokens — never hardcode values
- `Mode=TwoWay` only on form inputs (`Entry`, `Editor`, `Slider`, `Switch`, `Picker`)
- Set `FallbackValue` and `TargetNullValue` on bindings that may receive null

---

## Architecture Rules

- **MVVM separation is strict:** Views own display, ViewModels own state + commands, Services own logic + I/O
- **Persistence:** One `AppDbContext`; always use EF Core Migrations; never call `EnsureCreated()` in production
- **HTTP:** All opencode API calls go through `IOpenCodeApiClient` (typed `IHttpClientFactory` client)
- **Navigation:** Shell navigation with `INavigationService` abstraction — never call `Shell.Current` from ViewModels
- **Platform conditionals:** Use `#if ANDROID` / `#if IOS` only when MAUI abstractions are insufficient; always prefer MAUI APIs
- **File paths:** Always use `FileSystem.AppDataDirectory` — never hardcode platform paths

---

## Secrets — Absolute Rules

This is a **public repository**. A secret committed to git is a compromised secret.

- **Never hardcode:** API keys, DSNs, passwords, tokens, or connection strings with credentials
- **Development:** use `dotnet user-secrets`
- **Runtime on device:** use MAUI `SecureStorage`
- If a hardcoded secret is found anywhere in the codebase, **stop immediately and warn the user** before any other action

---

## Testing Rules

- Tests are pure unit tests — **no real DB, no real HTTP, no MAUI platform APIs**
- All external dependencies are mocked via NSubstitute against interfaces
- If a class has no interface, **stop and report** — do not write tests until `I<ClassName>` is extracted
- Every test follows **Arrange / Act / Assert** with blank-line separation and `// Arrange` comments
- One conceptual assertion per test; no `if`/`switch`/loops inside test bodies
- Use `[Theory]` + `[InlineData]` for parameterized cases (converters, multi-variant logic)
- Always use `Arg.Any<CancellationToken>()` in NSubstitute setups — never match on `CancellationToken.None`
- Never mix `Assert.*` (xUnit) with `Should()` (FluentAssertions) in the same file

---

## Git Flow

Branch model: **Git Flow** (main + develop + feature/bugfix/hotfix/release/support prefixes).  
Commit format: **Conventional Commits** (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`).

```
main         ← stable, tagged releases only
develop      ← integration branch
feature/<slug>   ← new features, branch from develop
bugfix/<slug>    ← non-urgent fixes, branch from develop
hotfix/<version> ← urgent fixes, branch from main
release/<version>← release stabilisation, branch from develop
```

**Never push to `main` or `develop` directly.** All work goes through feature branches.  
**Always confirm with the user** before any `git push`, `git flow finish`, or branch deletion.

---

## opencode Server API

Base URL (default): `http://127.0.0.1:4096` — configured via `appsettings.json`, never hardcoded.

Key endpoints: `GET /global/health`, `GET /session`, `POST /session`, `POST /session/:id/message`,
`POST /session/:id/prompt_async`, `GET /session/:id/message`, `GET /event` (SSE), `GET /agent`, `GET /config`.

SSE events from `GET /event` must be consumed as `IAsyncEnumerable<ServerEvent>` from the service layer.
