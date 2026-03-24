# How-To: EF Core Migrations and Compiled Model

**Date:** 2026-03-24
**Applies to:** openMob — `src/openMob.Core` (EF Core 9.x + SQLite)

---

## Background

openMob uses EF Core 9 with SQLite for local persistence. The iOS Release build uses the .NET linker in **"Link all assemblies"** mode, which strips the reflection that EF Core normally uses to build the database model at runtime.

Without a compiled model, the app crashes on iOS Release at the first DB access with:

```
System.InvalidOperationException: Model building is not supported when
publishing with NativeAOT. Use a compiled model.
  at IModel DbContextServices.CreateModel(bool designTime)()
  ...
  at async Task ServerManagementViewModel.LoadAsync(CancellationToken ct)()
```

This was discovered via Sentry on 2026-03-24 after the first TestFlight release.

**The fix:** generate a compiled model with `dotnet ef dbcontext optimize`. This produces pre-compiled C# code in `Data/CompiledModels/` that replaces all runtime reflection. The model is registered explicitly in `AppDbContext.OnConfiguring`.

---

## The Golden Rule

> **Every time you add, remove, or modify a migration, you must regenerate the compiled model.**

Forgetting this step causes a silent regression: the app works in Debug but crashes on iOS Release.

---

## Commands Reference

### Add a migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

### Regenerate the compiled model (run after EVERY migration change)

```bash
dotnet ef dbcontext optimize \
  --project src/openMob.Core/openMob.Core.csproj \
  --output-dir Data/CompiledModels \
  --namespace openMob.Core.Data.CompiledModels
```

### Remove the last migration

```bash
dotnet ef migrations remove \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

---

## Full Workflow

```
1. Edit entity / OnModelCreating
2. dotnet ef migrations add <Name> ...
3. Review generated Up() / Down()
4. dotnet ef dbcontext optimize ...       ← NEVER skip this
5. dotnet build src/openMob.Core/...      ← verify zero errors
6. git add Migrations/ CompiledModels/ && git commit
```

---

## Key Files

| File | Purpose |
|------|---------|
| `src/openMob.Core/Data/AppDbContext.cs` | Registers `AppDbContextModel.Instance` in `OnConfiguring` |
| `src/openMob.Core/Data/AppDbContextFactory.cs` | Design-time stub — lets EF tooling run without the MAUI project |
| `src/openMob.Core/Data/Migrations/` | Auto-generated migration files |
| `src/openMob.Core/Data/CompiledModels/` | Pre-compiled model — must be regenerated after every migration |

---

## Why `--framework net10.0-android`?

`openMob.csproj` targets both `net10.0-android` and `net10.0-ios`. EF tooling requires a single target framework for the startup project. `net10.0-android` is used because it resolves correctly on all development machines (including Windows, where iOS SDK is not available). The compiled model is platform-agnostic — the same files work for both iOS and Android.

---

## Design-Time Factory

`AppDbContextFactory` exists because `AppDbContext` requires `IAppDataPathProvider`, which is only registered in the MAUI project. The factory provides a stub implementation pointing to `Path.GetTempPath()` so EF tooling can instantiate the context without the full MAUI DI container.

```csharp
// AppDbContextFactory.cs — internal, never used at runtime
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AppDbContext(new DesignTimePathProvider(), options);
    }
}
```

---

## Sentry Integration

Migration failures at startup are captured to Sentry with full context:

```csharp
// MauiProgram.cs
catch (Exception ex)
{
    SentryHelper.CaptureException(ex, new Dictionary<string, object>
    {
        ["context"] = "MauiProgram.EFCoreMigration",
        ["exceptionType"] = ex.GetType().FullName ?? "Unknown",
        ["message"] = ex.Message,
    });
}
```

If a migration fails in production, the event will appear in Sentry under the `MauiProgram.EFCoreMigration` context.
