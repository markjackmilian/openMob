# Skill: ef-migrations

## What I do

I provide the exact shell commands and workflow for managing EF Core migrations in the openMob project. I cover adding new migrations, applying them locally, and — critically — regenerating the compiled model that is **mandatory** after every migration change for iOS Release builds.

## When to use me

Load this skill whenever you need to:
- Add a new EF Core migration
- Remove or revert a migration
- Regenerate the compiled model after any schema change
- Troubleshoot EF Core linker errors on iOS Release

---

## ⚠️ Critical Rule — Compiled Model Must Always Be Regenerated

**After every migration add, remove, or schema change, you MUST regenerate the compiled model.**

The iOS Release build uses the aggressive linker ("Link all assemblies") which strips the reflection used by EF Core to build the model at runtime. Without a compiled model, the app crashes on iOS Release with:

```
System.InvalidOperationException: Model building is not supported when publishing with NativeAOT. Use a compiled model.
```

The compiled model is pre-generated C# code in `src/openMob.Core/Data/CompiledModels/` that replaces runtime reflection entirely. It is registered explicitly in `AppDbContext.OnConfiguring` via `optionsBuilder.UseModel(AppDbContextModel.Instance)`.

**This is not optional. Skipping this step will cause a silent regression on iOS Release.**

---

## Project Structure

```
src/openMob.Core/
├── Data/
│   ├── AppDbContext.cs                  ← DbContext (registers compiled model)
│   ├── AppDbContextFactory.cs           ← Design-time factory for EF tooling
│   ├── Entities/                        ← EF Core entity classes
│   ├── Migrations/                      ← Migration files (auto-generated)
│   └── CompiledModels/                  ← Pre-compiled model (MUST regenerate after migrations)
│       ├── AppDbContextAssemblyAttributes.cs
│       ├── AppDbContextModel.cs
│       ├── AppDbContextModelBuilder.cs
│       ├── AppStateEntityType.cs
│       ├── ProjectPreferenceEntityType.cs
│       └── ServerConnectionEntityType.cs
src/openMob/                             ← MAUI startup project (needed for migrations)
```

---

## Commands

### Add a new migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

**Naming convention:** `PascalCase` describing the schema change.
Examples: `AddUserTable`, `AddIndexOnSessionCreatedAt`, `RenameHostToBaseUrl`

> ⚠️ After this command, you **must** run the compiled model regeneration step below.

### Remove the last migration (if not yet applied to any DB)

```bash
dotnet ef migrations remove \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

> ⚠️ After this command, you **must** run the compiled model regeneration step below.

### Apply migrations locally (development only)

```bash
dotnet ef database update \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

> Note: In production the app applies migrations automatically at startup via `db.Database.Migrate()` in `MauiProgram.cs`.

### ✅ Regenerate the compiled model — MANDATORY after every migration

```bash
dotnet ef dbcontext optimize \
  --project src/openMob.Core/openMob.Core.csproj \
  --output-dir Data/CompiledModels \
  --namespace openMob.Core.Data.CompiledModels
```

This overwrites all files in `src/openMob.Core/Data/CompiledModels/`. Commit all changed files.

### List all migrations

```bash
dotnet ef migrations list \
  --project src/openMob.Core/openMob.Core.csproj \
  --startup-project src/openMob/openMob.csproj \
  --framework net10.0-android
```

---

## Full Workflow — Adding a Migration End to End

Follow these steps in strict order every time a new migration is needed:

```
1. Modify entity class(es) in src/openMob.Core/Data/Entities/
   └── or modify OnModelCreating in AppDbContext.cs

2. Add the migration:
   dotnet ef migrations add <MigrationName> \
     --project src/openMob.Core/openMob.Core.csproj \
     --startup-project src/openMob/openMob.csproj \
     --framework net10.0-android

3. Review the generated migration file in src/openMob.Core/Data/Migrations/
   └── Verify Up() and Down() are correct

4. Regenerate the compiled model (MANDATORY):
   dotnet ef dbcontext optimize \
     --project src/openMob.Core/openMob.Core.csproj \
     --output-dir Data/CompiledModels \
     --namespace openMob.Core.Data.CompiledModels

5. Build to verify zero errors:
   dotnet build src/openMob.Core/openMob.Core.csproj

6. Commit all changes together:
   git add src/openMob.Core/Data/Entities/
   git add src/openMob.Core/Data/Migrations/
   git add src/openMob.Core/Data/CompiledModels/
   git add src/openMob.Core/Data/AppDbContext.cs   # if OnModelCreating changed
   git commit -m "feat(db): add <MigrationName> migration and regenerate compiled model"
```

---

## Checklist — Before Committing Any Migration

- [ ] Migration file reviewed — `Up()` and `Down()` are correct and reversible
- [ ] Compiled model regenerated (`dotnet ef dbcontext optimize ...`)
- [ ] `dotnet build src/openMob.Core/openMob.Core.csproj` — zero errors
- [ ] Migration files **and** CompiledModels files staged together in the same commit
- [ ] Commit message references the migration name

---

## Troubleshooting

### `InvalidOperationException: Model building is not supported when publishing with NativeAOT`

**Cause:** The compiled model is missing or out of date.
**Fix:** Run the `dotnet ef dbcontext optimize` command above and commit the regenerated files.

### `Unable to create a 'DbContext' of type 'AppDbContext'` during tooling

**Cause:** EF tooling cannot resolve `IAppDataPathProvider` from DI.
**Fix:** `AppDbContextFactory.cs` in `src/openMob.Core/Data/` provides a design-time stub. If it is missing, recreate it — see the file for the template.

### `Assets file doesn't have a target for 'net10.0-android'` on Core project

**Cause:** The `--framework` flag was passed to the Core project instead of the startup project.
**Fix:** Always pass `--framework net10.0-android` — EF tooling applies it to the startup project (`openMob.csproj`), not to the Core library.

### Migration added but compiled model not updated

**Symptom:** App works in Debug but crashes on iOS Release with the NativeAOT error.
**Fix:** Run `dotnet ef dbcontext optimize` and commit the updated `CompiledModels/` files.

---

## Notes

- `AppDbContextFactory.cs` is `internal` and `sealed` — it is only used by EF tooling, never at runtime.
- The compiled model is registered in `AppDbContext.OnConfiguring` via `optionsBuilder.UseModel(AppDbContextModel.Instance)`. This explicit registration takes precedence over the auto-discovery attribute in `AppDbContextAssemblyAttributes.cs` and is more reliable under aggressive linking.
- EF Core version: **9.x** (pinned in `openMob.Core.csproj`). If upgraded to EF Core 10+, regenerate the compiled model immediately after the upgrade.

Base directory for this skill: file:///C:/Projects/openMob/.opencode/skills/ef-migrations
