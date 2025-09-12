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

## Things to remember
- Use the build task available to you. Don't ask me to run a cli build command.