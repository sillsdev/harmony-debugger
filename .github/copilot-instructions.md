# AI Coding Agent Instructions (Harmony Debugger)

Goal: Desktop UI to inspect and debug Harmony (CRDT) databases. Keep startup lightweight (no real DB required) while always loading CRDT type metadata. Provide fast, read‑only exploration plus on-demand change loading.

## Architecture Overview
- Solution Roots:
  - `ui/` Avalonia desktop app (focus here).
  - `lexbox/backend/harmony/` Harmony CRDT library (imported via project refs) providing CRDT model (`Commit`, `ChangeEntity<IChange>`, object & change types, config).
  - `test-data/` sample SQLite DBs (e.g. `sena-3.sqlite`).
- Startup (`Program.cs` + `CrdtLoader`): builds a DI container, loads CRDT config using an in-memory SQLite placeholder so type metadata is always available before selecting a physical DB.
- `DbPathContext`: mutable holder for the active SQLite path used by EF Core factory.
- `HarmonyConfigService`: surfaces pretty names and summaries of CRDT object/change types; fallback `NullHarmonyConfigService` when config absent (rare now).
- MVVM: ViewModels derive from `ViewModelBase`; commands via CommunityToolkit.Mvvm (`RelayCommand`, manual `AsyncRelayCommand` when needed).

## Docs for you
- [Span](../ai/spec.md): stable spec defining scope and architecture.
- [Roadmap](../ai/roadmap.md): ordered list of features to build
- [Harmony summary](ai/harmony-summary.md): reference for Harmony CRDT model and key APIs.
- []

## Things to remember
- Always invoke the existing VS Code build task ("build") yourself when a build is needed. Do NOT ask the user to run any CLI build command manually.
- Never suggest `dotnet build` or similar; just run the task directly.
- Stop mentioning or trying to fix the build warnings regarding the versions of (SQLitePCLRaw.core, Microsoft.Extensions.Configuration).
- To RUN the app, start the appropriate VS Code launch configuration (e.g. "HarmonyDebugger" or variants). Do not ask the user to run `dotnet run`; trigger the debugger/launch config instead.