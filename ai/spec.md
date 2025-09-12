# Harmony Debugger Specification

Defines stable scope and architecture for the Avalonia-based Harmony debugger. Complements the actively changing `ai/roadmap.md` (delivery order) and `harmony-summary.md` (CRDT model reference).

## 1. Purpose
Provide a read-oriented desktop tool to inspect Harmony commits, step through incoming sync commits, view per-commit entity diffs, and verify snapshot correctness—without modifying Harmony core APIs.

## 2. In-Scope (Current Direction)
- Open a Harmony SQLite database (file picker / drag & drop).
- Display commits with lazy loading of change lists.
- Stepwise application of pending (remote) commits: single, small batch, or all.
- Attribute errors precisely to a commit + change index.
- Show which entities a commit affects and their before/after state.
- Regenerate all snapshots and compare to existing ones (drift detection).
- Present a retained “last snapshot drift report”.

## 3. Future Ideas (Not Active Now)
- Branch visualization / DAG UI.
- Branch simulation / speculative change patching.
- Profiling / performance flamegraphs.
- Attribute-based `CrdtConfig` discovery.
- In-app change editing / patch authoring.

## 4. High-Level Architecture
Component layers inside the `ui/` project:
- ViewModels (Avalonia + MVVM) orchestrate queries and stepping actions.
- Services wrapping Harmony APIs (e.g., CommitReplayService, SnapshotCompareService) – thin facades, no business logic duplication.
- Harmony library (referenced projects in `lexbox/backend/harmony/`) provides `DataModel`, entities, change types, and snapshot mechanisms.
- DbPathContext enables switching the active SQLite file at runtime.

## 5. Error Reporting Model
- Central collection (in-memory) of replay errors (commit id, change index, change type, entity id, exception message).
- UI surfaces latest error and a list history (cleared only on user action).

## 6. Minimal Services (Stable Contracts)
| Service | Responsibility | Notes |
|---------|----------------|-------|
| CommitReplayService | Queue & step through commits; expose current index & last error | Uses standard Harmony apply path |
| EntityDiffService | Capture & diff entity states across a commit application | Serialization format stable (deterministic ordering) |
| SnapshotCompareService | Regenerate snapshots & compare with prior set | No partial regeneration in scope |
| ErrorLogService | Record and enumerate replay errors | Simple list abstraction |

## 7. Relationship to Roadmap
Spec: stable scope & structure. Roadmap: delivery order for in-scope items.

---
This spec intentionally omits implementation mechanics; those belong in code and transient planning notes.
